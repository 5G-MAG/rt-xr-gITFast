/*
* Copyright (c) 2023 InterDigital
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// ACTION_SET_HAPTIC (ISO/IEC 23090-14:2024, Tables 44 to 50). The action is parsed and validated.
    /// The haptic payload is not decoded; each rendering is a default vibration and the log says so.
    /// </summary>
    public class ActionSetHaptic : MonoBehaviour, IMpegInteractivityAction, IMpegInteractivityStoppable
    {
        public float Delay => m_Delay;
        private float m_Delay;
        private Schema.HapticActionNode[] m_Nodes = new Schema.HapticActionNode[0];
        private bool m_Rendering;
        private bool m_RenderingLoop;
        private static readonly HashSet<string> s_Logged = new HashSet<string>();

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Schema.Action action)
        {
            m_Delay = action.delay;
            m_Nodes = action.hapticActionNodes != null ? action.hapticActionNodes : new Schema.HapticActionNode[0];
            if (m_Nodes.Length == 0)
            {
                Debug.LogWarning("ACTION_SET_HAPTIC without hapticActionNodes: nothing to render.");
            }
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                Validate(m_Nodes[i]);
            }
        }

        public void Invoke()
        {
            if (Delay > 0.0f)
            {
                StartCoroutine(StartWithDelay(Delay));
            }
            else
            {
                Execute();
            }
        }

        public void Stop()
        {
            if (m_Rendering)
            {
                m_Rendering = false;
                HapticOutput.Cancel();
            }
        }

        private static int HapticAccessorCount(int hapticObject)
        {
            var haptic = VirtualSceneGraph.root?.extensions?.MPEG_haptic;
            if (haptic == null || haptic.hapticObjects == null || hapticObject < 0 || hapticObject >= haptic.hapticObjects.Length) return -1;
            var accessors = haptic.hapticObjects[hapticObject].accessors;
            return accessors != null ? accessors.Length : 0;
        }

        private void Validate(Schema.HapticActionNode node)
        {
            if (node == null) return;
            if (node.hapticObject >= 0 && HapticAccessorCount(node.hapticObject) < 0)
            {
                Debug.LogWarning("ACTION_SET_HAPTIC: haptic object " + node.hapticObject + " does not exist in MPEG_haptic.hapticObjects.");
            }
            var medias = node.Medias;
            if (node.MaterialModalities != null && node.MaterialModalities.Length > 0)
            {
                ValidateMaterial(node);
            }
            if (medias == null || medias.Length == 0)
            {
                if (node.MaterialModalities == null || node.MaterialModalities.Length == 0)
                {
                    Debug.LogWarning("ACTION_SET_HAPTIC node " + node.node + ": no haptic media and no material modality.");
                }
                return;
            }
            for (int i = 0; i < medias.Length; i++)
            {
                int count = node.hapticObject >= 0 ? HapticAccessorCount(node.hapticObject) : -1;
                if (count >= 0 && (medias[i].MediaIndex < 0 || medias[i].MediaIndex >= count))
                {
                    Debug.LogWarning("ACTION_SET_HAPTIC node " + node.node + ": mediaIndex " + medias[i].MediaIndex + " is outside the accessors of haptic object " + node.hapticObject + ".");
                }
            }
        }

        // G.3.4: a haptic material is attached to a mesh of the node (Table 78) and listed in the
        // MPEG_haptic_material root extension (Table 76).
        private static void ValidateMaterial(Schema.HapticActionNode node)
        {
            var root = VirtualSceneGraph.root;
            if (root == null || root.nodes == null || node.node < 0 || node.node >= root.nodes.Length) return;
            int meshIndex = root.nodes[node.node].mesh;
            if (root.meshes == null || meshIndex < 0 || meshIndex >= root.meshes.Length) return;
            var meshExt = root.meshes[meshIndex].extensions != null ? root.meshes[meshIndex].extensions.MPEG_haptic_material : null;
            var materials = root.extensions?.MPEG_haptic_material?.materials;
            if (meshExt == null || meshExt.hapticMaterialIndex < 0 || materials == null || meshExt.hapticMaterialIndex >= materials.Length)
            {
                Debug.LogWarning("ACTION_SET_HAPTIC node " + node.node + ": materialHapticModality is set but its mesh has no valid MPEG_haptic_material.");
            }
        }

        private void LogOnce(string message)
        {
            if (s_Logged.Add(message)) Debug.LogWarning(message);
        }

        private void Execute()
        {
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                if (m_Nodes[i] != null) ExecuteNode(m_Nodes[i]);
            }
        }

        private void ExecuteNode(Schema.HapticActionNode node)
        {
            if (node.actionLocation != 0xFFFFFFFFL)
            {
                LogOnce("ACTION_SET_HAPTIC: actionLocation 0x" + node.actionLocation.ToString("X8") + " is not applicable, a handheld vibrator has no body location.");
            }
            if (node.useCollider)
            {
                LogOnce("ACTION_SET_HAPTIC: useCollider is not applied, the collision position is not mapped to a body location.");
            }

            if (node.washout)
            {
                // Washout: reset the associated devices; for a vibrator that is stopping the output.
                m_Rendering = false;
                HapticOutput.Cancel();
            }

            var medias = node.Medias;
            if (medias == null || medias.Length == 0)
            {
                if (node.MaterialModalities != null && node.MaterialModalities.Length > 0)
                {
                    LogOnce("ACTION_SET_HAPTIC node " + node.node + ": haptic material textures (MPEG_haptic_material) are not sampled; playing a default vibration.");
                    Render(false);
                }
                return;
            }

            for (int i = 0; i < medias.Length; i++)
            {
                switch (medias[i].Control)
                {
                    case Schema.HapticControl.HAPTIC_PLAY:
                        LogOnce("ACTION_SET_HAPTIC: the haptic payload of haptic object " + node.hapticObject + " (ISO/IEC 23090-31) is not decoded; playing a default vibration.");
                        Render(medias[i].loop);
                        break;
                    case Schema.HapticControl.HAPTIC_PAUSE:
                        if (m_Rendering) HapticOutput.Pause(m_RenderingLoop);
                        m_Rendering = false;
                        break;
                    case Schema.HapticControl.HAPTIC_RESUME:
                        if (HapticOutput.PausedLoop) Render(true);
                        break;
                    case Schema.HapticControl.HAPTIC_STOP:
                        m_Rendering = false;
                        HapticOutput.Cancel();
                        break;
                }
            }
        }

        private void Render(bool loop)
        {
            m_Rendering = true;
            m_RenderingLoop = loop;
            HapticOutput.PlayDefault(loop);
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}
