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

    // Applied by the presentation engine: the output frames are taken from the input at `speed` input frames per output
    // frame, by linear interpolation, so the signal plays faster or slower and its pitch follows. Samples are interleaved.
    // `speed` is the MPEG_audio_spatial playbackSpeed (ISO/IEC 23090-14, Table 14, between 0.5 and 2.0), times the ratio of
    // the sample rate of the signal to the rate the audio is played at. There is no low-pass filter before a decimation.
    public class PlaybackSpeedResampler
    {
        private readonly int channels;
        private readonly double speed;
        private float[] pending = new float[0];   // input frames not consumed yet
        private int pendingFrames = 0;
        private double position = 0.0;            // position of the next output frame in pending, in input frames

        public PlaybackSpeedResampler(int channelCount, double inputFramesPerOutputFrame)
        {
            channels = channelCount;
            speed = inputFramesPerOutputFrame;
        }

        // The input frames to push before outFrames output frames can be produced: the frames that the interpolation reads, and
        // the whole frames that the output consumes, which exceed the first for a speed above 2.
        public int FramesNeeded(int outFrames)
        {
            int need = (int)System.Math.Ceiling(position + outFrames * speed) + 1;
            return System.Math.Max(0, need - pendingFrames);
        }

        public void Push(float[] data, int frames)
        {
            int samples = frames * channels;
            if (pending.Length < (pendingFrames * channels) + samples)
            {
                System.Array.Resize(ref pending, (pendingFrames * channels) + samples);
            }
            System.Array.Copy(data, 0, pending, pendingFrames * channels, samples);
            pendingFrames += frames;
        }

        public void Resample(float[] dst, int outFrames)
        {
            for (int k = 0; k < outFrames; k++)
            {
                double p = position + k * speed;
                int i = (int)System.Math.Floor(p);
                float f = (float)(p - i);
                for (int c = 0; c < channels; c++)
                {
                    dst[k * channels + c] = pending[i * channels + c] * (1f - f) + pending[(i + 1) * channels + c] * f;
                }
            }
            position += outFrames * speed;
            int consumed = (int)System.Math.Floor(position);
            System.Array.Copy(pending, consumed * channels, pending, 0, (pendingFrames - consumed) * channels);
            pendingFrames -= consumed;
            position -= consumed;
        }
    }

    // Interleaved samples between the thread that drains the MAF buffer (the only writer) and the audio callback (the only
    // reader). Whole frames only: a write keeps a multiple of the channel count.
    public class AudioSampleFifo
    {
        private readonly float[] ring;
        private readonly int channels;
        private long written;   // samples written so far, advanced by the writer
        private long read;      // samples read so far, advanced by the reader

        public AudioSampleFifo(int capacitySamples, int channelCount)
        {
            channels = System.Math.Max(1, channelCount);
            ring = new float[System.Math.Max(channels, capacitySamples - capacitySamples % channels)];
        }

        public int Capacity { get { return ring.Length; } }
        public int Count { get { return (int)(System.Threading.Volatile.Read(ref written) - System.Threading.Volatile.Read(ref read)); } }

        // writer: copies as many whole frames of src as fit, returns the samples copied
        public int Write(float[] src, int offset, int count)
        {
            long w = written;
            int n = System.Math.Min(count, ring.Length - (int)(w - System.Threading.Volatile.Read(ref read)));
            n -= n % channels;
            if (n <= 0) return 0;
            int pos = (int)(w % ring.Length);
            int first = System.Math.Min(n, ring.Length - pos);
            System.Array.Copy(src, offset, ring, pos, first);
            if (n > first) System.Array.Copy(src, offset + first, ring, 0, n - first);
            System.Threading.Volatile.Write(ref written, w + n);
            return n;
        }

        // reader: copies up to count samples to dst, returns the samples copied
        public int Read(float[] dst, int offset, int count)
        {
            long r = read;
            int n = System.Math.Min(count, (int)(System.Threading.Volatile.Read(ref written) - r));
            if (n <= 0) return 0;
            int pos = (int)(r % ring.Length);
            int first = System.Math.Min(n, ring.Length - pos);
            System.Array.Copy(ring, pos, dst, offset, first);
            if (n > first) System.Array.Copy(ring, 0, dst, offset + first, n - first);
            System.Threading.Volatile.Write(ref read, r + n);
            return n;
        }

        // drops what is buffered; only while the writer is stopped
        public void Clear()
        {
            System.Threading.Volatile.Write(ref read, System.Threading.Volatile.Read(ref written));
        }
    }

    public class MafAudioReader
    {
        /*
         * Class to bridge Unity's C# audio callbacks and MAF BufferHandler.
         * A thread drains the MAF buffer into a FIFO as soon as the pipeline writes a frame, so the pipeline can decode
         * ahead of playback even when the circular buffer holds few frames; the audio callback reads the FIFO only and
         * never waits for the pipeline.
         * The current implementation allows a single SpatialAudioSource to read from the buffer.
         * If the AudioSource's channel count doesn't match, and the source audio buffers has a single channel, that channel is duplicated.
         */

        // FIFO sizes, implementation choices: the samples the drain thread may buffer ahead, what playback waits for at the start
        // (prebuffer) and after the FIFO ran empty (rebuffer). Waiting plays silence and holds the media clock.
        public static float FifoSeconds = 1.0f;
        public static float PrebufferSeconds = 0.2f;
        public static float RebufferSeconds = 0.1f;

        // Audio sources using this buffer
        public List<SpatialAudioSource> AudioSources { get; private set; }

        // elapsed time
        public bool Active { get; set; }
        // frames (one sample per channel) read from the buffer: media time, before playbackSpeed
        public long SampleCount { get; set; }
        public virtual double CurrentTime { get { return SampleCount/(double)SampleRate; } }

        // MAF buffer
        public readonly MediaBufferHandler BufferHandler;
        public readonly BufferInfoArray BufferInfo;
        // the pipeline writes a timed accessor information header (immutable layout) when the buffer layout has one
        private readonly bool useAccessorHeaders;
        private BufferInfoHeaderArray bufferHeaders = null;
        // playbackSpeed other than 1.0: the resampler and the buffer it reads the media samples into
        private PlaybackSpeedResampler resampler = null;
        private float[] speedInput = null;
        // buffer frames taken per frame handed to Unity (playbackSpeed and the rate conversion)
        protected double Step { get; private set; } = 1.0;

        public readonly int ChannelCount;
        // sample rate of the samples in the buffer (what the media pipeline was asked for in BufferInfo.outputMedia.sampleRate)
        public readonly int SampleRate;

        // MAF buffer to FIFO (drain thread)
        private readonly AudioSampleFifo fifo;
        private System.Threading.Thread drainThread;
        private volatile bool draining;
        private float[] pending = new float[0];   // samples of the last frame read that are not in the FIFO yet
        private int pendingCount;
        private int pendingOffset;
        const int UNITY_AUDIO_SAMPLE_SIZE = 4;

        // FIFO to Unity (audio callback)
        private volatile bool rebuffering = true;
        private readonly int prebufferSamples;
        private readonly int rebufferSamples;
        private bool started;

        // diagnostics, written by the audio callback, read on the main thread
        private int underruns;
        private int callbacks;
        private int minCallbackFrames = int.MaxValue;
        private int maxCallbackFrames;
        private int reportedUnderruns;
        private float nextUnderrunReport;
        private bool statsReported;

        public MafAudioReader(MediaBufferHandler handler, BufferInfoArray bufferInfo, int sampleRate)
        {
            BufferHandler = handler;
            BufferInfo = bufferInfo;
            ChannelCount = BufferInfo.Count;
            SampleRate = sampleRate;
            useAccessorHeaders = BufferInfo.Count > 0 && MediaBufferHandler.HasTimedAccessorHeader(BufferInfo);

            if (useAccessorHeaders)
            {
                bufferHeaders = BufferInfoHeader.CreateAccessorsHeaders(handler.BufferInfo, true);
            }

            int samplesPerSecond = System.Math.Max(1, ChannelCount) * SampleRate;
            fifo = new AudioSampleFifo((int)(FifoSeconds * samplesPerSecond), ChannelCount);
            prebufferSamples = System.Math.Min(fifo.Capacity / 2, (int)(PrebufferSeconds * samplesPerSecond));
            rebufferSamples = System.Math.Min(fifo.Capacity / 2, (int)(RebufferSeconds * samplesPerSecond));

            AudioSources = new List<SpatialAudioSource>();
        }

        // The rate at which the audio callback is asked for samples. The media pipeline supplies the signal at SampleRate, the
        // target sample rate of the source (Table 14). Unity converts the frequency of an AudioClip to the output rate by itself;
        // OnAudioFilterRead is called at the output rate of the audio engine.
        protected virtual int CallbackSampleRate { get { return SampleRate; } }

        // the CallbackSampleRate the resampler was set up for, when the first audio source was added (0 before)
        public int SetupCallbackSampleRate { get; private set; }

        public virtual void AddAudioSource(SpatialAudioSource aSrc)
        {
            AudioSources.Add(aSrc);
            if (SetupCallbackSampleRate == 0)
            {
                SetupCallbackSampleRate = CallbackSampleRate;
            }
            // input frames taken per output frame: the playback speed, and the rate conversion when the callback rate differs
            double step = aSrc.PlaybackSpeed * (double)SampleRate / SetupCallbackSampleRate;
            if (step != 1.0 && resampler == null)
            {
                resampler = new PlaybackSpeedResampler(ChannelCount, step);
                Step = step;
            }
            UnityEngine.Debug.Log("MafAudioReader " + GetType().Name + ": buffer " + BufferHandler.bufferId + ", " + ChannelCount + " channel(s) at " + SampleRate
                + " Hz, callback at " + SetupCallbackSampleRate + " Hz, step " + Step.ToString("0.####") + ", MAF frames " + BufferHandler.Capacity
                + ", FIFO " + (fifo.Capacity / (double)(System.Math.Max(1, ChannelCount) * SampleRate)).ToString("0.00") + " s, DSP buffer " + AudioSettings.GetConfiguration().dspBufferSize);
        }

        // samples in the FIFO that playback will still use (what is left below the rebuffer level when the FIFO ran empty is not)
        public bool HasPendingAudio { get { return !rebuffering && fifo.Count > 0; } }

        public void Play()
        {
            bool fresh = drainThread == null;
            if (fresh)
            {
                fifo.Clear();
                rebuffering = true;
                started = false;
                OnPlaybackReset();
                StartDrain();
            }
            Active = true;
            if (fresh)
            {
                OnPlaybackStart();
            }
            foreach (var aSrc in AudioSources)
            {
                aSrc.Play();
            }
        }

        public void Stop()
        {
            Active = false;
            StopDrain();
            foreach (var aSrc in AudioSources)
            {
                aSrc.Stop();
            }
        }

        // stops the drain thread without touching the audio sources (they may be destroyed already)
        public void Shutdown()
        {
            Active = false;
            StopDrain();
        }

        protected virtual void OnPlaybackReset() { }

        // reader active, the audio sources are about to play (main thread)
        protected virtual void OnPlaybackStart() { }

        // diagnostics: the values the media clock is computed from (main thread)
        public virtual string ClockState
        {
            get { return "clock " + CurrentTime.ToString("0.000") + " s, media frames " + SampleCount + ", FIFO " + fifo.Count / System.Math.Max(1, ChannelCount) + " frames, underruns " + underruns; }
        }

        // main thread, once per frame (MediaPlayer.Update)
        public virtual void Update()
        {
            if (!statsReported && callbacks >= 50)
            {
                statsReported = true;
                UnityEngine.Debug.Log("MafAudioReader " + GetType().Name + ": buffer " + BufferHandler.bufferId + ", callbacks ask for "
                    + minCallbackFrames + " to " + maxCallbackFrames + " frames");
            }
            int u = underruns;
            if (u != reportedUnderruns && Time.unscaledTime >= nextUnderrunReport)
            {
                UnityEngine.Debug.LogWarning("MafAudioReader: buffer " + BufferHandler.bufferId + ", the audio FIFO ran empty " + (u - reportedUnderruns)
                    + " time(s): the media pipeline does not keep up, playback waits for " + RebufferSeconds + " s of audio each time");
                reportedUnderruns = u;
                nextUnderrunReport = Time.unscaledTime + 2f;
            }
        }

        void StartDrain()
        {
            pendingCount = 0;
            pendingOffset = 0;
            draining = true;
            drainThread = new System.Threading.Thread(DrainLoop) { IsBackground = true, Name = "MafAudioReader" + BufferHandler.bufferId };
            drainThread.Start();
        }

        void StopDrain()
        {
            draining = false;
            if (drainThread != null)
            {
                drainThread.Join(500);
                drainThread = null;
            }
        }

        void DrainLoop()
        {
            try
            {
                while (draining)
                {
                    if (pendingOffset < pendingCount)
                    {
                        pendingOffset += fifo.Write(pending, pendingOffset, pendingCount - pendingOffset);
                        if (pendingOffset < pendingCount)
                        {
                            System.Threading.Thread.Sleep(5);   // FIFO full
                            continue;
                        }
                    }
                    if (!ReadFrame())
                    {
                        System.Threading.Thread.Sleep(2);       // nothing written by the pipeline yet
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        // copies the next frame of the MAF buffer to `pending`; false when there is none
        bool ReadFrame()
        {
            if (ChannelCount == 0)
            {
                return false;
            }
            Frame frame = BufferHandler.ReadFrame();
            if (frame == null)
            {
                return false;
            }
            int n;
            if (useAccessorHeaders)
            {
                BufferInfoHeader.ReadAccessorsHeaders(frame.data, bufferHeaders);
                n = ChannelCount * (int)bufferHeaders[0].count;
            }
            else
            {
                n = (int)frame.length / UNITY_AUDIO_SAMPLE_SIZE;
                n -= n % ChannelCount;
            }
            if (n <= 0)
            {
                return true;
            }
            var samples = BufferHandler.GetNativeArray(frame, bufferHeaders).Reinterpret<float>(sizeof(byte));
            n = System.Math.Min(n, samples.Length);
            if (pending.Length < n)
            {
                pending = new float[n];
            }
            NativeArray<float>.Copy(samples, 0, pending, 0, n);
            pendingCount = n;
            pendingOffset = 0;
            return true;
        }

        public void AudioReaderCallback(float[] dst)
        {
            int frames = ChannelCount > 0 ? dst.Length / ChannelCount : dst.Length;
            if (frames < minCallbackFrames) minCallbackFrames = frames;
            if (frames > maxCallbackFrames) maxCallbackFrames = frames;
            callbacks++;

            if (resampler == null || ChannelCount == 0)
            {
                FillSamples(dst, dst.Length);
                return;
            }
            // playbackSpeed other than 1.0: read the samples of the media at that many times the rate they are played at
            int outFrames = dst.Length / ChannelCount;
            int needed = resampler.FramesNeeded(outFrames);
            if (needed > 0)
            {
                if (speedInput == null || speedInput.Length < needed * ChannelCount)
                {
                    speedInput = new float[needed * ChannelCount];
                }
                FillSamples(speedInput, needed * ChannelCount);
                resampler.Push(speedInput, needed);
            }
            resampler.Resample(dst, outFrames);
        }

        // writes `length` samples to dst: from the FIFO, silence for what it cannot supply
        private void FillSamples(float[] dst, int length)
        {
            int got = 0;
            if (Active && ChannelCount > 0)
            {
                if (rebuffering && fifo.Count >= (started ? rebufferSamples : prebufferSamples))
                {
                    rebuffering = false;
                    started = true;
                }
                if (!rebuffering)
                {
                    got = fifo.Read(dst, 0, length);
                    if (got < length)
                    {
                        underruns++;
                        rebuffering = true;
                    }
                    SampleCount += got / ChannelCount;
                }
            }
            if (got < length)
            {
                System.Array.Clear(dst, got, length - got);
            }
        }

    } // MafAudioReader

    public class AudioFilterReader : MafAudioReader
    {
        private float[] mono = new float[0];   // reused, the audio callback does not allocate

        public AudioFilterReader(MediaBufferHandler handler, BufferInfoArray bufferInfo, int sampleRate)
            : base(handler, bufferInfo, sampleRate) { }

        protected override int CallbackSampleRate
        {
            get
            {
                int outputRate = UnityEngine.AudioSettings.outputSampleRate;
                return outputRate > 0 ? outputRate : SampleRate;
            }
        }

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
                int frames = data.Length / channels;
                if (mono.Length != frames)
                {
                    mono = new float[frames];
                }
                AudioReaderCallback(mono);
                int j = 0;
                for (int i = 0; i < frames; i++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        data[j] = mono[i];
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

    // Streams the samples through an AudioClip at the sample rate of the buffer: Unity resamples the clip to its output rate.
    // Unity reads a streamed clip ahead of what it plays, in chunks, so the media clock is taken from the playback position
    // of the source: each chunk read records where it ends in clip frames and in media frames, and the clock is the media
    // position of the chunk being played, interpolated in it.
    public class AudioClipReader : MafAudioReader
    {

        static public int internaCliplId = 0;
        public AudioClip Clip { get; private set; }
        // the clip loops every ClipLength frames: one second, so that a loop is not missed between two frames of the application
        public readonly int ClipLength;

        // chunks read by Unity: clip frames and media frames at the end of each (written by Unity's callback, read on the main thread)
        const int ChunkHistory = 256;
        private readonly long[] chunkEnd = new long[ChunkHistory];
        private readonly long[] chunkMediaEnd = new long[ChunkHistory];
        private int chunkCount;
        private int chunkNext;
        private long delivered;     // clip frames read by Unity since the clip was created, silence included
        private readonly object chunkLock = new object();

        private long played;        // clip frames played, from AudioSource.timeSamples unwrapped (main thread)
        private int lastTimeSamples;

        public AudioClipReader(MediaBufferHandler handler, BufferInfoArray bufferInfo, int sampleRate)
            : base(handler, bufferInfo, sampleRate)
        {
            ClipLength = System.Math.Max(SampleRate, AudioSettings.GetConfiguration().dspBufferSize * 2);
        }

        void OnClipRead(float[] data)
        {
            AudioReaderCallback(data);
            long media = SampleCount;
            lock (chunkLock)
            {
                delivered += ChannelCount > 0 ? data.Length / ChannelCount : data.Length;
                chunkEnd[chunkNext] = delivered;
                chunkMediaEnd[chunkNext] = media;
                chunkNext = (chunkNext + 1) % ChunkHistory;
                if (chunkCount < ChunkHistory) chunkCount++;
            }
        }

        override public void AddAudioSource(SpatialAudioSource aSrc)
        {
            if (AudioSources.Count > 0)
            {
                UnityEngine.Debug.LogWarning("Multiple audio sources are sharing the same AudioClip.");
            }
            base.AddAudioSource(aSrc);
            if (Clip != null)
            {
                aSrc.SetAudioClip(Clip);
            }
        }

        protected override void OnPlaybackReset()
        {
            lock (chunkLock)
            {
                delivered = 0;
                chunkCount = 0;
                chunkNext = 0;
            }
            played = 0;
            lastTimeSamples = 0;
        }

        // A new clip for each start: AudioClip.Create already reads the first chunk, which must be counted from the start of playback.
        protected override void OnPlaybackStart()
        {
            var old = Clip;
            Clip = AudioClip.Create($"mafAudioClip{internaCliplId}", ClipLength, ChannelCount, SampleRate, true, OnClipRead);
            internaCliplId++;
            foreach (var aSrc in AudioSources)
            {
                aSrc.SetAudioClip(Clip);
            }
            if (old != null)
            {
                UnityEngine.Object.Destroy(old);
            }
        }

        public override void Update()
        {
            base.Update();
            if (AudioSources.Count == 0 || Clip == null)
            {
                return;
            }
            int ts = AudioSources[0].TimeSamples;
            played += ts >= lastTimeSamples ? ts - lastTimeSamples : ClipLength - lastTimeSamples + ts;
            lastTimeSamples = ts;
            // loops missed while Update did not run (media paused, a long frame): Unity does not read a clip length ahead
            long read;
            lock (chunkLock) { read = delivered; }
            while (read - played > ClipLength)
            {
                played += ClipLength;
            }
        }

        // media frames at clip frame `position`, from the chunks read; false when the history does not reach it
        bool MediaAt(long position, out double media)
        {
            lock (chunkLock)
            {
                media = 0;
                if (chunkCount == 0)
                {
                    return false;
                }
                int oldest = (chunkNext - chunkCount + ChunkHistory) % ChunkHistory;
                long startClip = 0, startMedia = 0;
                for (int k = 0; k < chunkCount; k++)
                {
                    int i = (oldest + k) % ChunkHistory;
                    if (position <= chunkEnd[i])
                    {
                        if (k == 0 && chunkCount == ChunkHistory)
                        {
                            // the oldest chunk kept: its start is unknown, take it as played whole
                            media = chunkMediaEnd[i];
                            return true;
                        }
                        long length = chunkEnd[i] - startClip;
                        double f = length > 0 ? (position - startClip) / (double)length : 1.0;
                        media = startMedia + f * (chunkMediaEnd[i] - startMedia);
                        return true;
                    }
                    startClip = chunkEnd[i];
                    startMedia = chunkMediaEnd[i];
                }
                // played past what was read: the last media frame read
                media = startMedia;
                return true;
            }
        }

        public override string ClockState
        {
            get
            {
                long read;
                lock (chunkLock) { read = delivered; }
                return base.ClockState + ", delivered " + read + ", played " + played + ", timeSamples " + lastTimeSamples + ", ahead " + (read - played);
            }
        }

        public override double CurrentTime
        {
            get
            {
                double media;
                return MediaAt(played, out media) ? media / SampleRate : 0.0;
            }
        }
    }

}
