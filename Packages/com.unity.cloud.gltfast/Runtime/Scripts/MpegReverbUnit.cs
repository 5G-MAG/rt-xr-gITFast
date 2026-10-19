// MPEG_audio_spatial reverb units (ISO/IEC 23090-14:2024(E), 5.4.1.1 and 5.4.1.3, Tables 15 to 17).
// A reverb unit is a late-reverberation processor configured by one reverb object: pre-delay (frequency independent),
// RT60 and DSR for each listed frequency. Sources feed it through reverbFeed and reverbFeedGain; the feeds of all sources are mixed.
// The clause defines the parameters, not the algorithm. This unit splits the fed signal into one band per listed frequency
// (band edges at the geometric means of neighbouring frequencies) and runs one feedback delay network per band:
//   - RT60 of the band: 60 dB (a factor 1000 in amplitude) of decay in RT60 seconds, for the band's own listed frequency,
//   - DSR of the band: the diffuse energy in the band divided by the emitted energy in the band equals the listed DSR
//     (5.4.1.3: "dividing the total diffuse reverb energy by the total emitted energy"), calibrated on the simulated impulse response,
//   - the pre-delay is applied before the networks; the onset of the diffuse part is the pre-delay, but not earlier than the
//     shortest delay line of the network (about 21 ms).
// The feed is taken before the distance gain: DSR "is therefore directly linked to the PCM signal's reference level" and does not
// depend on the listener position. The direct sound is not changed ("the DSR values do not influence the amplitude of the direct sound").
// The two output channels are decorrelated taps of the same network; the DSR is met by each output channel (one point of the room).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast {

    public class MpegReverbUnit : MonoBehaviour {

        static readonly Dictionary<int, MpegReverbUnit> s_Units = new Dictionary<int, MpegReverbUnit>();

        public static MpegReverbUnit Get(int id) {
            lock (s_Units) {
                MpegReverbUnit u;
                return s_Units.TryGetValue(id, out u) ? u : null;
            }
        }

        /// <summary>Largest output sample of the last block (test aid).</summary>
        public float Peak { get { return m_Peak; } }
        public int Id { get; private set; }

        const int Lines = 4;

        // Second-order Butterworth section (direct form 1). Two of them in series make a Linkwitz-Riley filter of order 4.
        struct Biquad {
            public float B0, B1, B2, A1, A2, X1, X2, Y1, Y2;
            public static Biquad Make(bool high, float freq, int fs) {
                double w0 = 2.0 * Math.PI * Math.Min(freq, 0.45 * fs) / fs, cosw = Math.Cos(w0), alpha = Math.Sin(w0) / (2.0 * Math.Sqrt(0.5));
                double a0 = 1.0 + alpha, b0, b1, b2;
                if (high) { b0 = (1.0 + cosw) / 2.0; b1 = -(1.0 + cosw); b2 = b0; }
                else { b0 = (1.0 - cosw) / 2.0; b1 = 1.0 - cosw; b2 = b0; }
                var q = new Biquad();
                q.B0 = (float)(b0 / a0); q.B1 = (float)(b1 / a0); q.B2 = (float)(b2 / a0);
                q.A1 = (float)(-2.0 * cosw / a0); q.A2 = (float)((1.0 - alpha) / a0);
                return q;
            }
            public float Process(float x) {
                float y = B0 * x + B1 * X1 + B2 * X2 - A1 * Y1 - A2 * Y2;
                X2 = X1; X1 = x; Y2 = Y1; Y1 = y;
                return y;
            }
            public void Reset() { X1 = X2 = Y1 = Y2 = 0f; }
        }

        // One band: a Linkwitz-Riley band-pass (4th order at both edges) and a feedback delay network
        class Band {
            public Biquad Hp1, Hp2, Lp1, Lp2;
            public bool HasHp, HasLp;
            public float[][] Delay = new float[Lines][];
            public int[] Pos = new int[Lines];
            public float[] Gain = new float[Lines];
            public float WetGain;

            public float Filter(float x) {
                float y = x;
                if (HasLp) { y = Lp2.Process(Lp1.Process(y)); }
                if (HasHp) { y = Hp2.Process(Hp1.Process(y)); }
                return y;
            }

            public void Reset() {
                Hp1.Reset(); Hp2.Reset(); Lp1.Reset(); Lp2.Reset();
                for (int i = 0; i < Lines; i++) { Array.Clear(Delay[i], 0, Delay[i].Length); Pos[i] = 0; }
            }

            public void Tick(float x, out float left, out float right) {
                float o0 = Delay[0][Pos[0]], o1 = Delay[1][Pos[1]], o2 = Delay[2][Pos[2]], o3 = Delay[3][Pos[3]];
                float f0 = o0 * Gain[0], f1 = o1 * Gain[1], f2 = o2 * Gain[2], f3 = o3 * Gain[3];
                // orthogonal (Hadamard) feedback matrix
                float a = 0.5f * (f0 + f1 + f2 + f3), b = 0.5f * (f0 - f1 + f2 - f3), c = 0.5f * (f0 + f1 - f2 - f3), d = 0.5f * (f0 - f1 - f2 + f3);
                Delay[0][Pos[0]] = x + a; Delay[1][Pos[1]] = x + b; Delay[2][Pos[2]] = x + c; Delay[3][Pos[3]] = x + d;
                for (int i = 0; i < Lines; i++) Pos[i] = (Pos[i] + 1) % Delay[i].Length;
                left = 0.5f * (o0 + o1 - o2 - o3);
                right = 0.5f * (o0 - o1 + o2 - o3);
            }
        }

        Band[] m_Bands;
        float[] m_PreDelay;
        int m_PrePos;
        float m_Peak;
        // feed of all sources: sources add into the write buffer, the unit swaps and processes it (one block of latency)
        readonly object m_Lock = new object();
        float[] m_Write = new float[4096], m_Read = new float[4096];
        int m_WriteLength;

        public static MpegReverbUnit Create(GameObject parent, Schema.MpegAudioSpatialReverb reverb) {
            var go = new GameObject("MPEG_audio_spatial reverb " + reverb.id);
            go.transform.SetParent(parent.transform, false);
            var unit = go.AddComponent<MpegReverbUnit>();
            unit.Configure(reverb);
            var src = go.AddComponent<AudioSource>();
            // a silent looping clip keeps the audio filter running; the unit is not positioned (the diffuse field is the same everywhere)
            src.clip = AudioClip.Create("reverb", 1024, 1, AudioSettings.outputSampleRate, false);
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
            lock (s_Units) { s_Units[reverb.id] = unit; }
            return unit;
        }

        void Configure(Schema.MpegAudioSpatialReverb reverb) {
            Id = reverb.id;
            int fs = AudioSettings.outputSampleRate;
            var props = new List<Schema.MpegSpatialAudioReverbProperties>(reverb.properties ?? new Schema.MpegSpatialAudioReverbProperties[0]);
            props.Sort((a, b) => a.frequency.CompareTo(b.frequency));
            m_Bands = new Band[props.Count];
            // base delay lengths of about 21 to 41 ms, each band scaled by its own factor so that the networks of different bands are not
            // coherent (the lengths are made odd, which keeps them mutually prime for the factors used)
            float[] baseSeconds = { 0.0213f, 0.0277f, 0.0319f, 0.0397f };
            for (int k = 0; k < props.Count; k++) {
                var p = props[k];
                var band = new Band();
                float rt60 = Mathf.Max(0.01f, p.RT60);
                for (int i = 0; i < Lines; i++) {
                    int length = ((int)(fs * baseSeconds[i] * (1f + 0.083f * k))) | 1;
                    band.Delay[i] = new float[length];
                    // 60 dB in RT60 seconds: the loop attenuates by 1000 per RT60, so by 10^(-3 d / (RT60 fs)) per round trip of the line
                    band.Gain[i] = (float)Math.Pow(10.0, -3.0 * length / (rt60 * fs));
                }
                // band edges: geometric means of the neighbouring listed frequencies (none below the first and above the last)
                if (k > 0) {
                    float lo = Mathf.Sqrt(Mathf.Max(1f, props[k - 1].frequency) * Mathf.Max(1f, p.frequency));
                    band.Hp1 = Biquad.Make(true, lo, fs); band.Hp2 = band.Hp1; band.HasHp = true;
                }
                if (k < props.Count - 1) {
                    float hi = Mathf.Sqrt(Mathf.Max(1f, p.frequency) * Mathf.Max(1f, props[k + 1].frequency));
                    band.Lp1 = Biquad.Make(false, hi, fs); band.Lp2 = band.Lp1; band.HasLp = true;
                }
                m_Bands[k] = band;
            }
            int minLine = (int)(fs * baseSeconds[0]) | 1;
            int pre = Mathf.Max(0, Mathf.RoundToInt(reverb.predelay * fs) - minLine);
            m_PreDelay = new float[pre + 1];
            Calibrate(props, fs);
        }

        // Level: the DSR of a listed frequency is the diffuse energy divided by the emitted energy (5.4.1.3), here taken in the band of that frequency.
        // The impulse response of every band network is simulated once; the wet gains are then adjusted until the energy of the summed response seen
        // through the analysis filter of each band equals DSR times the energy of an impulse seen through the same filter (the bands overlap a
        // little, so the gains are iterated). The simulation is limited to 8 s: an implementation limit, the clause gives none.
        void Calibrate(List<Schema.MpegSpatialAudioReverbProperties> props, int fs) {
            int n = m_Bands.Length;
            if (n == 0) return;
            float maxRt = 0f;
            foreach (var p in props) maxRt = Mathf.Max(maxRt, p.RT60);
            int total = Mathf.Min((int)(maxRt * 1.5f * fs) + 1, fs * 8);
            var yl = new float[n][]; var yr = new float[n][];
            for (int k = 0; k < n; k++) {
                yl[k] = new float[total]; yr[k] = new float[total];
                var band = m_Bands[k];
                band.Reset();
                for (int t = 0; t < total; t++) {
                    float x = band.Filter(t == 0 ? 1f : 0f);
                    band.Tick(x, out yl[k][t], out yr[k][t]);
                }
                band.Reset();
            }
            // emitted energy of an impulse through the analysis filter of each band
            var emitted = new double[n];
            for (int j = 0; j < n; j++) {
                var probe = CloneFilter(m_Bands[j]);
                double e = 0.0;
                for (int t = 0; t < total; t++) { float v = probe.Filter(t == 0 ? 1f : 0f); e += v * (double)v; }
                emitted[j] = e;
            }
            var gain = new double[n];
            for (int j = 0; j < n; j++) gain[j] = 1.0;
            for (int iter = 0; iter < 5; iter++) {
                var sl = new float[total]; var sr = new float[total];
                for (int k = 0; k < n; k++) for (int t = 0; t < total; t++) { sl[t] += (float)(gain[k] * yl[k][t]); sr[t] += (float)(gain[k] * yr[k][t]); }
                for (int j = 0; j < n; j++) {
                    var fl = CloneFilter(m_Bands[j]); var fr = CloneFilter(m_Bands[j]);
                    double e = 0.0;
                    for (int t = 0; t < total; t++) { float a = fl.Filter(sl[t]), b = fr.Filter(sr[t]); e += 0.5 * (a * (double)a + b * (double)b); }
                    double target = Math.Pow(10.0, props[j].DSR / 10.0) * emitted[j];
                    if (e > 1e-18 && target > 0.0) gain[j] *= Math.Pow(target / e, 0.5);
                }
            }
            for (int k = 0; k < n; k++) m_Bands[k].WetGain = (float)gain[k];
        }

        // a band filter with its own state, for the analysis
        static Band CloneFilter(Band source) {
            var c = new Band();
            c.Hp1 = source.Hp1; c.Hp2 = source.Hp2; c.Lp1 = source.Lp1; c.Lp2 = source.Lp2; c.HasHp = source.HasHp; c.HasLp = source.HasLp;
            c.Hp1.Reset(); c.Hp2.Reset(); c.Lp1.Reset(); c.Lp2.Reset();
            for (int i = 0; i < Lines; i++) c.Delay[i] = new float[1];
            return c;
        }

        /// <summary>Called on the audio thread by a source: adds one block of its mono signal, scaled by the feed gain.</summary>
        public void Feed(float[] data, int channels, float gain) {
            lock (m_Lock) {
                int frames = Math.Min(data.Length / channels, m_Write.Length);
                for (int i = 0; i < frames; i++) m_Write[i] += data[i * channels] * gain;
                if (frames > m_WriteLength) m_WriteLength = frames;
            }
        }

        void OnAudioFilterRead(float[] data, int channels) {
            int frames = data.Length / channels;
            int available;
            lock (m_Lock) {
                var t = m_Write; m_Write = m_Read; m_Read = t;
                available = m_WriteLength;
                m_WriteLength = 0;
                Array.Clear(m_Write, 0, m_Write.Length);
            }
            float peak = 0f;
            for (int i = 0; i < frames; i++) {
                float x = i < available ? m_Read[i] : 0f;
                if (m_PreDelay.Length > 1) {
                    float delayed = m_PreDelay[m_PrePos];
                    m_PreDelay[m_PrePos] = x;
                    m_PrePos = (m_PrePos + 1) % m_PreDelay.Length;
                    x = delayed;
                }
                float l = 0f, r = 0f;
                for (int k = 0; k < m_Bands.Length; k++) {
                    var band = m_Bands[k];
                    float bl, br;
                    band.Tick(band.Filter(x), out bl, out br);
                    l += bl * band.WetGain; r += br * band.WetGain;
                }
                if (channels == 1) data[i] = 0.5f * (l + r);
                else { data[i * channels] = l; data[i * channels + 1] = r; for (int c = 2; c < channels; c++) data[i * channels + c] = 0f; }
                peak = Mathf.Max(peak, Mathf.Abs(l), Mathf.Abs(r));
            }
            m_Peak = peak;
        }

        void OnDestroy() {
            lock (s_Units) {
                MpegReverbUnit u;
                if (s_Units.TryGetValue(Id, out u) && u == this) s_Units.Remove(Id);
            }
        }
    }
}
