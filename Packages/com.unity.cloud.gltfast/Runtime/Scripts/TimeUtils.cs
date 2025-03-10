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
    /// Launch timers within Unity coroutines to execute delayed actions
    /// </summary>
    public class TimeUtils : MonoBehaviour
    {
        public static TimeUtils instance => m_Instance;
        private static TimeUtils m_Instance;

        private void Awake()
        {
            if(m_Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        public void StartTimer(float _delay, System.Action _action)
        {
            StartCoroutine(IStartTimer(_delay, _action));
        }

        private IEnumerator IStartTimer(float _delay, System.Action _action)
        {
            yield return new WaitForSeconds(_delay);
            _action();
        }
    }
}