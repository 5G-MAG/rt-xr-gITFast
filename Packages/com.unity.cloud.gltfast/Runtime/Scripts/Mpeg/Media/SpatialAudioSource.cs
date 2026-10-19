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

        // Gain applied to the samples on the audio thread: the Annex D distance gain, evaluated once per frame
        // on the main thread, and the pregain of the source.
        private volatile float distanceGain = 1f;
        /// <summary>The distance gain of Annex D applied to the source now (test aid).</summary>
        public float CurrentDistanceGain { get { return distanceGain; } }
        private float pregainLinear = 1f;
        private int[] reverbFeed;
        private float[] reverbFeedGainLinear;
        private AudioListener listener;
        private float nextListenerSearch;

        /// <summary>False when the source type is not supported by this player (it then stays silent).</summary>
        public bool IsSupported { get; private set; } = true;


        // ISO/IEC 23090-14:2024 Annex D: distance gain of the clamped attenuation models,
        // with md the maximum distance, rf the roll factor and rd the reference distance.
        static float DistanceGain(string model, float d, float md, float rf, float rd)
        {
            float clamped = Mathf.Min(md, Mathf.Max(d, rd));
            switch (model)
            {
                case "inverseDistance": return rd / (rd + rf * (clamped - rd));
                case "linearDistance": return md > rd ? 1f - rf * (clamped - rd) / (md - rd) : 1f;
                case "exponentialDistance": return rd > 0f ? Mathf.Pow(clamped / rd, -rf) : 1f;
            }
            return 1f;   // noAttenuation, or a function that cannot be evaluated
        }

        // true when Annex D can be evaluated: a known function and attenuationParameters (maximum distance first)
        bool attenuates;
        float maxDistance;
        float rollFactor;
        float refDistance;

        void ApplyAttenuation()
        {
            bool known = attenuationType == "inverseDistance" || attenuationType == "linearDistance" || attenuationType == "exponentialDistance";
            if (!known && attenuationType != "noAttenuation")
            {
                Debug.LogWarning("Unsupported audio attenuation mode " + attenuationType + ": the signal is not attenuated.");
            }
            bool hasParams = attenuationParams != null && attenuationParams.Length > 0;
            if (known && !hasParams)
            {
                Debug.LogWarning("Audio attenuation " + attenuationType + " has no attenuationParameters: the signal is not attenuated.");
            }
            attenuates = known && hasParams;

            // attenuationParameters: [maximum distance, roll factor]; the roll factor defaults to 1 when absent
            maxDistance = hasParams ? attenuationParams[0] : float.PositiveInfinity;
            rollFactor = hasParams && attenuationParams.Length > 1 ? attenuationParams[1] : 1f;
            refDistance = Mathf.Max(referenceDistance, 0.0001f);

            // Unity's distance rolloff stays flat (gain 1); the Annex D distance gain is applied to the samples in OnAudioFilterRead
            aSrc.rolloffMode = AudioRolloffMode.Custom;
            aSrc.minDistance = refDistance;
            aSrc.maxDistance = Mathf.Max(refDistance * 2f, 500f);
            aSrc.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
        }

        void Update()
        {
            if (!attenuates)
            {
                distanceGain = 1f;
                return;
            }
            if ((listener == null || !listener.isActiveAndEnabled) && Time.unscaledTime >= nextListenerSearch)
            {
                listener = FindFirstObjectByType<AudioListener>();
                nextListenerSearch = Time.unscaledTime + 1f;
            }
            if (listener == null)
            {
                distanceGain = 1f;
                return;
            }
            float d = Vector3.Distance(transform.position, listener.transform.position);
            float g = DistanceGain(attenuationType, d, maxDistance, rollFactor, refDistance);
            // Annex D: a function that cannot be evaluated does not attenuate. A negative value (linearDistance
            // with a roll factor above 1) is applied as computed: the clause states no limit on the gain.
            distanceGain = float.IsNaN(g) || float.IsInfinity(g) ? 1f : g;
        }

        public static bool UseSpatializerPlugin = false;

        void Start()
        {
            aSrc = GetComponent<UnityEngine.AudioSource>();
            // The project names a spatializer plugin (Steam Audio) that is not part of this build: asking for it makes every source
            // log "failed to initialize audio spatializer". Unity's own 3D panning and rolloff apply without it. Set to true when the plugin is installed.
            aSrc.spatialize = UseSpatializerPlugin;
            aSrc.spatializePostEffects = UseSpatializerPlugin; // required if audio buffer read through Monoibehavior.OnAudioFilterRead instead of AudioClip.OnPCMReaderdCallback
            aSrc.loop = true; // required for AudioClip.OnPCMReaderdCallback to run indefinitly
            
            aSrc.dopplerLevel = 0; // doppler results in annoying audio artifacts if improperly configured, 0 disables it.

            aSrc.spatialBlend = 1;
            ApplyAttenuation();

            if (clip_ != null)
            {
                aSrc.clip = clip_;
            }
        }

        public void Play()
        {
            if (!IsSupported) return;
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
            // Table 14: "The value shall be between 0.5 and 2.0." The media pipeline applies the speed.
            if (!(srcDef.playbackSpeed >= 0.5f && srcDef.playbackSpeed <= 2.0f))
            {
                throw new Exception("Invalid playbackSpeed " + srcDef.playbackSpeed + " on MpegAudioSpatialSource: the value shall be between 0.5 and 2.0");
            }
            SampleRate = srcDef.targetSampleRate;
            if (srcDef.type != null && String.Equals(srcDef.type, "HOA", StringComparison.OrdinalIgnoreCase))
            {
                // 5.4.1.1 requires 3DoF rendering of HOA; the clause gives no decoding to the output, so this player does not render it
                Debug.LogWarning("HOA audio sources are not supported by this player: the source stays silent.");
                IsSupported = false;
            }
            attenuationType = srcDef.attenuation ?? "linearDistance";
            referenceDistance = srcDef.referenceDistance;
            attenuationParams = srcDef.attenuationParameters;
            // pregain is a level adjustment in dB
            pregainLinear = Mathf.Pow(10f, srcDef.pregain / 20f);
            // Table 15: reverbFeed lists the reverb units, reverbFeedGain [dB] the gain of each feed: the arrays shall have the same number of elements
            if (srcDef.reverbFeed != null && srcDef.reverbFeed.Length > 0)
            {
                bool hasGain = srcDef.reverbFeedGain != null && srcDef.reverbFeedGain.Length > 0;
                if (hasGain && srcDef.reverbFeedGain.Length != srcDef.reverbFeed.Length)
                {
                    Debug.LogError("MPEG_audio_spatial: reverbFeedGain has " + srcDef.reverbFeedGain.Length + " elements and reverbFeed " + srcDef.reverbFeed.Length + ": they shall have the same number of elements, the feed is ignored");
                }
                else
                {
                    reverbFeed = srcDef.reverbFeed;
                    reverbFeedGainLinear = new float[reverbFeed.Length];
                    for (int k = 0; k < reverbFeed.Length; k++) reverbFeedGainLinear[k] = hasGain ? Mathf.Pow(10f, srcDef.reverbFeedGain[k] / 20f) : 1f;
                }
            }
        }

        public void OnAudioFilterRead(float[] data, int channels)
        {
            if (audioReaderCallback_ != null)
            {
                audioReaderCallback_(data, channels);
            }
            // reverb feeds take the signal after the pregain and before the distance gain (5.4.1.3, DSR is linked to the reference level)
            if (reverbFeed != null)
            {
                for (int k = 0; k < reverbFeed.Length; k++)
                {
                    var unit = MpegReverbUnit.Get(reverbFeed[k]);
                    if (unit != null) unit.Feed(data, channels, pregainLinear * reverbFeedGainLinear[k]);
                }
            }
            float gain = distanceGain * pregainLinear;
            if (gain != 1f)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] *= gain;
                }
            }
        }

    } // SpatialAudioSource


}