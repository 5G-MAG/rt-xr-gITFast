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
    /// Assign a material to one or multiple gameObject
    /// </summary>
    public class ActionSetMaterial : MonoBehaviour, IMpegInteractivityAction
    {
        public float Delay => m_Delay;

        private GameObject[] m_Objects;
        private Material m_TargetMaterial;
        private float m_Delay;

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Schema.Action action)
        {
            m_Objects = VirtualSceneGraph.GetGameObjectsFromIndexes(action.nodes);
            try
            {
                m_TargetMaterial = VirtualSceneGraph.GetMaterialFromIndex(action.material);
            }
            catch (System.Exception)
            {
                Debug.LogWarning("ACTION_SET_MATERIAL: material " + action.material + " does not exist, the action is ignored.");
                m_TargetMaterial = null;
            }
            m_Delay = action.delay;
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

        private void Execute()
        {
            if (m_TargetMaterial == null) return;
            for(int i = 0; i < m_Objects.Length; i++)
            {
                if (m_Objects[i] == null) continue;
                Apply(m_Objects[i].GetComponent<Renderer>());
                // Further primitives of the node mesh are children that are not glTF nodes.
                Transform t = m_Objects[i].transform;
                for (int c = 0; c < t.childCount; c++)
                {
                    GameObject child = t.GetChild(c).gameObject;
                    if (!VirtualSceneGraph.IsNodeGameObject(child)) Apply(child.GetComponent<Renderer>());
                }
            }
        }

        private void Apply(Renderer target)
        {
            if (target == null) return;
            Material[] current = target.sharedMaterials;
            if (current == null || current.Length <= 1)
            {
                target.sharedMaterial = m_TargetMaterial;
                return;
            }
            for (int i = 0; i < current.Length; i++) current[i] = m_TargetMaterial;
            target.sharedMaterials = current;
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}