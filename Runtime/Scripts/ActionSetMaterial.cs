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

        public void Init(Schema.Action action)
        {
            m_Objects = VirtualSceneGraph.GetGameObjectsFromIndexes(action.nodes);
            m_TargetMaterial = VirtualSceneGraph.GetMaterialFromIndex(action.material);
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
            for(int i = 0; i < m_Objects.Length; i++)
            {
                m_Objects[i].GetComponent<Renderer>().material = m_TargetMaterial;
            }
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}