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

using System.Collections;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Lock any object or set of objects at a given position/rotation
    /// </summary>
    public class ActionBlock : MonoBehaviour, IMpegInteractivityAction
    {
        public float Delay => m_Delay;
        private float m_Delay;
        private bool m_IsBlock = false;

        private Vector3[] m_BlockedPosition;
        private Quaternion[] m_BlockedRotation;
        private Vector3[] m_BlockedScale;
        private GameObject[] m_LockedObjects;

        public void Init(Schema.Action action)
        {
            m_LockedObjects = VirtualSceneGraph.GetGameObjectsFromIndexes(action.nodes);
            m_BlockedPosition = new Vector3[action.nodes.Length];
            m_BlockedRotation = new Quaternion[action.nodes.Length];
            m_BlockedScale = new Vector3[action.nodes.Length];
            m_Delay = action.delay;
        }

        private void Update()
        {
            if (!m_IsBlock)
            {
                return;
            }

            for (int i = 0; i < m_LockedObjects.Length; i++)
            {
                m_LockedObjects[i].transform.position = m_BlockedPosition[i];
                m_LockedObjects[i].transform.rotation = m_BlockedRotation[i];
                m_LockedObjects[i].transform.localScale = m_BlockedScale[i];
            }
        }

        public void Unlock()
        {
            m_IsBlock = false;
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
            m_IsBlock = true;

            // Cache values 
            for (int i = 0; i < m_LockedObjects.Length; i++)
            {
                m_BlockedPosition[i] = m_LockedObjects[i].transform.position;
                m_BlockedRotation[i] = m_LockedObjects[i].transform.rotation;
                m_BlockedScale[i] = m_LockedObjects[i].transform.localScale;
            }
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}