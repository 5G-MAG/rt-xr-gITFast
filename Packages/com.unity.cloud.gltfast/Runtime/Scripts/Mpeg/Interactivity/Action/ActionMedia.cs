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
    /// Entry point the application sets to control its media players: media index in the
    /// MPEG_media array and the control (ISO/IEC 23090-14:2024, Table 40).
    /// </summary>
    public static class MpegMediaControl
    {
        public static Action<int, Schema.MediaControl> Handler;
    }

    /// <summary>
    /// Handle the execution of media
    /// </summary>
    public class ActionMedia : MonoBehaviour, IMpegInteractivityAction, IMpegInteractivityStoppable
    {
        public float Delay => m_Delay;
        private float m_Delay;
        private int m_Media;
        private Schema.MediaControl m_Control;
        private bool m_Played;

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Schema.Action action)
        {
            m_Delay = action.delay;
            m_Media = action.media;
            m_Control = action.mediaControl;
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

        /// <summary>Stops a media started by this action when its behavior is interrupted.</summary>
        public void Stop()
        {
            if (m_Played)
            {
                m_Played = false;
                Send(Schema.MediaControl.MEDIA_STOP);
            }
        }

        private void Execute()
        {
            if (m_Control == Schema.MediaControl.MEDIA_PLAY) m_Played = true;
            Send(m_Control);
        }

        private void Send(Schema.MediaControl control)
        {
            if (MpegMediaControl.Handler != null)
            {
                MpegMediaControl.Handler(m_Media, control);
                return;
            }
#if MAF_MEDIA_PIPELINES
            // no application handler: the media player is controlled directly
            MediaPlayer mp = VirtualSceneGraph.GetMediaPlayerFromMediaIndex(m_Media);
            if (mp == null)
            {
                Debug.LogWarning("ACTION_MEDIA " + control + " on media " + m_Media + ": no media player is attached.");
                return;
            }
            switch (control)
            {
                case Schema.MediaControl.MEDIA_PLAY: mp.Play(); break;
                case Schema.MediaControl.MEDIA_PAUSE: mp.Pause(); break;
                case Schema.MediaControl.MEDIA_RESUME: mp.Play(); break;
                case Schema.MediaControl.MEDIA_STOP: mp.Stop(); break;
            }
#else
            Debug.LogWarning("ACTION_MEDIA " + control + " on media " + m_Media + ": no media player is attached.");
#endif
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}