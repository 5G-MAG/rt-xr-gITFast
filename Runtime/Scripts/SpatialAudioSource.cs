/*
 * Copyright (c) 2023 MotionSpell
 * Licensed under the License terms and conditions for use, reproduction,
 * and distribution of 5GMAG software (the “License”).
 * You may not use this file except in compliance with the License.
 * You may obtain a copy of the License at https://www.5g-mag.com/license .
 * Unless required by applicable law or agreed to in writing, software distributed under the License is
 * distributed on an “AS IS” BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and limitations under the License.
 */

using System;
using UnityEngine;
using GLTFast.Schema;

namespace GLTFast
{
    [RequireComponent(typeof(UnityEngine.AudioSource))]
    public class SpatialAudioSource : MonoBehaviour
    {

        public delegate void AudioFilterCallback(float[] data, int channels);
        public AudioFilterCallback audioReaderCallback_;

        public int BufferId { get; private set; }
        public int SampleRate { get; private set; }


        private AudioClip clip_ = null;
        
        private string attenuationType = "no_attenuation";
        private float referenceDistance;
        private float[] attenuationParams;

        UnityEngine.AudioSource aSrc;

        void Start()
        {
            aSrc = GetComponent<UnityEngine.AudioSource>();
            aSrc.spatialize = true;
            aSrc.spatializePostEffects = true; // required if audio buffer read through Monoibehavior.OnAudioFilterRead instead of AudioClip.OnPCMReaderdCallback
            aSrc.loop = true; // required for AudioClip.OnPCMReaderdCallback to run indefinitly
            
            aSrc.dopplerLevel = 0; // doppler results in annoying audio artifacts if improperly configured, 0 disables it.

            aSrc.spatialBlend = attenuationType == "noAttenuation" ? 0 : 1;

            if (attenuationType == "linearDistance")
            {
                aSrc.spatialBlend = 1;
                aSrc.rolloffMode = AudioRolloffMode.Linear;
                aSrc.minDistance = referenceDistance;
                if (attenuationParams != null)
                {
                    aSrc.maxDistance = attenuationParams[0];
                }
                // rooloffFactor is not supported in Unity's builtin RollOff modes
            }
            else if (attenuationType == "exponentialDistance")
            {
                aSrc.spatialBlend = 1;
                aSrc.rolloffMode = AudioRolloffMode.Logarithmic;
                aSrc.minDistance = referenceDistance;
                if (attenuationParams != null)
                {
                    aSrc.maxDistance = attenuationParams[0];
                }
                // rooloffFactor is not supported in Unity's builtin RollOff modes
            }
            else
            {
                Debug.LogWarning("Unsupported audio attenuation mode " + attenuationType);
                Debug.LogWarning("Using noAttenuation instead.");
            }

            if (clip_ != null)
            {
                aSrc.clip = clip_;
            }
        }

        public void Play()
        {
            aSrc.Play();
        }

        public void Stop()
        {
            aSrc.Stop();
        }


        public void SetAudioClip(AudioClip clip)
        {
            clip_ = clip;
            if (aSrc != null)
            {
                aSrc.clip = clip;
            }
        }

        public void SetAudioFilterCallback(AudioFilterCallback callback)
        {
            audioReaderCallback_ = callback;
        }

        public void Configure(MpegAudioSpatialSource srcDef, int bufferId)
        {
            BufferId = bufferId;
            if (srcDef.targetSampleRate <= 0)
            {
                throw new Exception("Invalid targetSampleRate on MpegAudioSpatialSource");
            }
            SampleRate = srcDef.targetSampleRate;
            if (srcDef.type != null && String.Equals(srcDef.type, "HOA", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("HOA audio source currently not implemented");
            }
            attenuationType = srcDef.attenuation ?? "linearDistance";
            referenceDistance = srcDef.referenceDistance;
            attenuationParams = srcDef.attenuationParameters ?? new float[]{ 100 };
        }

#if !ENABLE_AUDIO_CLIP_READER
        public void OnAudioFilterRead(float[] data, int channels)
        {
            if (audioReaderCallback_ != null)
            {
                audioReaderCallback_(data, channels);
            }
        }
#endif

    } // SpatialAudioSource


}