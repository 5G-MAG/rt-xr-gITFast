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
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Set transform to a node or a set of nodes
    /// </summary>
    public class ActionTransform : MonoBehaviour, IMpegInteractivityAction
    {
        private Matrix4x4 m_TargetMatrix = Matrix4x4.identity;
        private bool m_Valid;
        private GameObject[] m_Targets;
        public float Delay => m_Delay;
        private float m_Delay;

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Schema.Action action)
        {
            m_Delay = action.delay;
            // Table 37 gives no element order for the 4x4 matrix. The content in use (gravity.gltf) lists it
            // row by row with the translation at elements 3, 7 and 11, so that order is read here.
            // The X axis is negated like the node transforms of the loader (NodeExtension.GetTransform).
            m_Valid = action.transform != null && action.transform.Length == 16;
            if (!m_Valid)
            {
                Debug.LogWarning("ACTION_TRANSFORM: transform must hold 16 numbers, the action has no effect.");
            }
            else
            {
                var t = action.transform;
                for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        m_TargetMatrix[r, c] = t[r * 4 + c] * ((r == 0) != (c == 0) ? -1f : 1f);
                    }
                }
            }

            m_Targets = new GameObject[action.nodes.Length];

            for(int i = 0; i < action.nodes.Length; i++)
            {
                GameObject _obj = VirtualSceneGraph.GetGameObjectFromIndex(action.nodes[i]);
                m_Targets[i] = _obj;
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

        // Sets the transform of the nodes in the space of their parent, keeping translation, rotation and scale.
        private void Execute()
        {
            if (!m_Valid) return;
            m_TargetMatrix.Decompose(out Vector3 _pos, out Quaternion _rot, out Vector3 _scale);
            for(int i = 0; i < m_Targets.Length; i++)
            {
                if (m_Targets[i] == null) continue;
                Transform _t = m_Targets[i].transform;
                _t.localPosition = _pos;
                _t.localRotation = _rot;
                _t.localScale = _scale;
            }
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}