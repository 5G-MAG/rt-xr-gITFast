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

using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    // Helper class to debug behaviors in editor
    public class BehaviorController : MonoBehaviour
    {
        private List<IMpegInteractivityBehavior> m_Behaviors;

        public void Init()
        {
            m_Behaviors = new List<IMpegInteractivityBehavior>();
        }

        public void AddBehavior(IMpegInteractivityBehavior bhv)
        {
            m_Behaviors.Add(bhv);
        }

        private void FixedUpdate()
        {
            for(int i = 0; i < m_Behaviors.Count; i++)
            {
                bool result = m_Behaviors[i].AreTriggersActived();
                if(!result)
                {
                    continue;
                }
                m_Behaviors[i].ActivateActions();
            }
        }

        internal void Dispose()
        {
            for(int i = 0; i < m_Behaviors.Count; i++)
            {
                m_Behaviors[i].Dispose();
            }
            m_Behaviors.Clear();
            Destroy(gameObject);
        }
    }
}