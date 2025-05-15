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

using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;


namespace GLTFast
{

    using maf;

    public class MafAudioReader
    {
        /*
         * Class to bridge Unity's C# audio callbacks and MAF BufferHandler.
         * The current implementation allows a single SpatialAudioSource to read from the buffer.
         * If the AudioSource's channel count doesn't match, and the source audio buffers has a single channel, that channel is duplicated.
         */
        
        // Audio sources using this buffer
        public List<SpatialAudioSource> AudioSources { get; private set; }

        // elapsed time
        public bool Active { get; set; }
        public long SampleCount { get; set; }
        public double CurrentTime { get { return SampleCount/(double)SampleRate; } }

        // MAF buffer
        public readonly MediaBufferHandler BufferHandler;
        public readonly BufferInfoArray BufferInfo;
        private bool useAccessorHeaders = true;
        private BufferInfoHeaderArray bufferHeaders = null;

        public readonly int ChannelCount;
        public readonly int SampleRate;

        // rebuffering from MAF to Unity
        private Frame frame = null;
        private int srcOffset = 0;
        private NativeArray<float> src;

        // reading/copying samples shouldn't take longer than the requested buffer duration
        int UNITY_AUDIO_SAMPLE_SIZE = 4;
        
        Stopwatch sw;

        public MafAudioReader(MediaBufferHandler handler, BufferInfoArray bufferInfo, int sampleRate)
        {
            BufferHandler = handler;
            BufferInfo = bufferInfo;
            ChannelCount = BufferInfo.Count; 
            SampleRate = sampleRate;

            if (useAccessorHeaders)
            {
                bufferHeaders = BufferInfoHeader.CreateAccessorsHeaders(handler.BufferInfo, true);
            }

            AudioSources = new List<SpatialAudioSource>();
        }

        public virtual void AddAudioSource(SpatialAudioSource aSrc)
        {
            AudioSources.Add(aSrc);
        }

        public void Play()
        {
            foreach (var aSrc in AudioSources)
            {
                aSrc.Play();
            }
            Active = true;
        }

        public void Stop()
        {
            Active = false;
            foreach (var aSrc in AudioSources)
            {
                aSrc.Stop();
            }
        }

        public void AudioReaderCallback(float[] dst)
        {
            // reads the latest frame from BufferHandler, and write the content to audio buffer dst.
            if (!Active)
            {
                return;
            }
            if (BufferInfo.Count == 0)
            {
                return;
            }

            int dstOffset = 0;
            while (dstOffset < dst.Length)
            {
                if (frame == null)
                {
                    double maxDelay = (double)dst.Length / (double)SampleRate;
                    sw = Stopwatch.StartNew();
                    do
                    {
                        frame = BufferHandler.ReadFrame();
                        if (sw.Elapsed.TotalSeconds >= maxDelay)
                        {
                            // TODO: seek proper sample position in the source media to keep audio in sync
                            UnityEngine.Debug.LogWarning("Unity audio DSP is starving");
                            break;
                        }
                    } while (frame == null);
                    
                    sw.Stop();
                    if(frame == null)
                    {
                        return;
                    }

                    int packedSampleCount;
                    if (useAccessorHeaders)
                    {
                        BufferInfoHeader.ReadAccessorsHeaders(frame.data, bufferHeaders);
                        packedSampleCount = ChannelCount * (int)bufferHeaders[0].count;
                    }
                    else
                    {
                        packedSampleCount = (int)frame.length / (ChannelCount * UNITY_AUDIO_SAMPLE_SIZE);
                    }

                    if (packedSampleCount == 0)
                    {
                        srcOffset = src.Length; // drop the current frame
                        break;
                    }

                    var tmp = BufferHandler.GetNativeArray(frame, bufferHeaders);
                    src = tmp.Reinterpret<float>(sizeof(byte)).GetSubArray(0, packedSampleCount);
                }

                if (src.Length == 0)
                {
                    frame = null;
                    srcOffset = 0;
                    return;
                }

                int chunkSize = Mathf.Min(src.Length - srcOffset, dst.Length - dstOffset);
                NativeArray<float>.Copy(src, srcOffset, dst, dstOffset, chunkSize);
                dstOffset += chunkSize;
                srcOffset += chunkSize;
                if (srcOffset == src.Length)
                {
                    frame = null;
                    srcOffset = 0;
                }
            
            }

            SampleCount += dst.Length;
        }

    } // MafAudioReader

    public class AudioFilterReader : MafAudioReader
    {
        public AudioFilterReader(MediaBufferHandler handler, BufferInfoArray bufferInfo, int sampleRate)
            : base(handler, bufferInfo, sampleRate) { }

        override public void AddAudioSource(SpatialAudioSource aSrc)
        {
            if (AudioSources.Count > 0)
            {
                throw new System.Exception("Current implementation doesn't support multiple spatial audio sources reading.");
            }
            base.AddAudioSource(aSrc);
            aSrc.SetAudioFilterCallback(OnAudioFilterReadCallback);
        }

        public void OnAudioFilterReadCallback(float[] data, int channels)
        {
            if (channels == ChannelCount)
            {
                AudioReaderCallback(data);
            }
            else if (ChannelCount == 1)
            {
                int mono = data.Length / channels;
                float[] tmp = new float[mono];
                AudioReaderCallback(tmp);
                int j = 0;
                for (int i = 0; i < tmp.Length; i++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        data[j] = tmp[i];
                        j++;
                    }
                }
            }
            else
            {
                throw new System.Exception("Can't convert from " + ChannelCount + " audio reader channels to " + channels + " channels requested by AudioSource.OnAudioFilterRead");
            }
        }
    }

    public class AudioClipReader : MafAudioReader
    {

        static public int internaCliplId = 0;
        public readonly AudioClip Clip;
        public readonly int ClipLength;
        

        public AudioClipReader(MediaBufferHandler handler, BufferInfoArray bufferInfo, int sampleRate)
            : base(handler, bufferInfo, sampleRate)
        {
            // Unsure if/how ClipLength impacts memory alloc/performances.
            // After reading 'ClipLength' samples, the clip will 'loop' and 'SetAudioSamplePosition' is called by unity to reset position at 0.
            ClipLength = AudioSettings.GetConfiguration().dspBufferSize * 2;
            Clip = AudioClip.Create($"mafAudioClip{internaCliplId}", ClipLength, ChannelCount, SampleRate, true, AudioReaderCallback); //, SetAudioSamplePosition);
            internaCliplId++;
        }

        new public void AddAudioSource(SpatialAudioSource aSrc)
        {
            if (AudioSources.Count > 0)
            {
                UnityEngine.Debug.LogWarning("Multiple audio sources are sharing the same AudioClip.");
            }
            base.AddAudioSource(aSrc);
            aSrc.SetAudioClip(Clip);
        }

        void SetAudioSamplePosition(int newPosition)
        {
            SampleCount = newPosition;
        }
    }

}
