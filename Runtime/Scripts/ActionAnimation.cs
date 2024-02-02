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
using GLTFast.Schema;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Handle the Animation state of an object
    /// </summary>
    public class ActionAnimation : MonoBehaviour, IMpegInteractivityAction
    {
        public float Delay => m_Delay;
        private float m_Delay;

        private AnimationControl m_AnimationControl;
        private Animation m_TargetAnimation;
        private Animation m_PauseFrame;
        private AnimationClip m_Clip;

        public void Init(Schema.Action action)
        {
            m_Delay = action.delay;
            m_AnimationControl = action.animationControl;
            m_Clip = VirtualSceneGraph.GetAnimationClipFromIndex(action.animation);
            m_TargetAnimation = VirtualSceneGraph.GetAnimationFromClipIndex(action.animation);
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
            switch (m_AnimationControl)
            {
                case AnimationControl.ANIMATION_PLAY:
                    m_TargetAnimation.clip = m_Clip;
                    m_TargetAnimation.Play();
                    break;

                case AnimationControl.ANIMATION_PAUSE:
                    throw new System.NotImplementedException("ANIMATION_PAUSE not implemented");

                case AnimationControl.ANIMATION_RESUME:
                    m_TargetAnimation.Play();
                    break;

                case AnimationControl.ANIMATION_STOP:
                    m_TargetAnimation.Stop();
                    break;
            }
        }

        
        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}