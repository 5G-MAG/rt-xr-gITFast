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
        private Matrix4x4 m_TargetMatrix;
        private GameObject[] m_Targets;
        public float Delay => throw new NotImplementedException();

        public void Init(Schema.Action action)
        {
            m_TargetMatrix = action.transform;
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

        private void Execute()
        {
            for(int i = 0; i < m_Targets.Length; i++)
            {
                Vector3 _targetPos = m_TargetMatrix.GetPosition();
                Quaternion _targetRot = m_TargetMatrix.rotation;
                m_Targets[i].transform.position = _targetPos;
                m_Targets[i].transform.rotation = _targetRot;
            }
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}