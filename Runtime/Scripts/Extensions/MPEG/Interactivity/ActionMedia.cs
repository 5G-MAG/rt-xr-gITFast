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
    /// Handle the execution of media
    /// </summary>
    public class ActionMedia : MonoBehaviour, IMpegInteractivityAction
    {
        public float Delay => m_Delay;
        private float m_Delay;
        
        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Schema.Action action)
        {
            m_Delay = action.delay;
            Debug.Log("TODO: Init ActionMedia");
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
            // VirtualSceneGraph.root.extensions.MPEG_media.media[0].alternatives[0].uri;
            Debug.LogError("TODO: Execute ActionMedia");
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}