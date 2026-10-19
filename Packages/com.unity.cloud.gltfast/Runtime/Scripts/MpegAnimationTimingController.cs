// MPEG_animation_timing (ISO/IEC 23090-14:2024(E), 5.5.2 and 7.6): the samples of the timed
// accessor control the state of the glTF animations. The sample layout is the glTFAnimationSample
// of 7.6.3.2 (big-endian, as stored in the ISOBMFF sample). Samples are handled in order, once.

#if UNITY_ANIMATION
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast {

    using Schema;

    public class MpegAnimationTimingController : MonoBehaviour {

        /// <summary>One animation event of a sample (7.6.3.2).</summary>
        internal sealed class TimingEvent {
            public uint index;
            public float speed;
            public byte state;
            public uint startFrame;
            public uint endFrame;
            public byte order;
            public List<byte> weights = new List<byte>();
            public List<uint> channels = new List<uint>();
        }

        /// <summary>Playback state kept per glTF animation.</summary>
        sealed class Track {
            public float speed = 1f;
            public bool paused;
            public bool loop;
            public bool relative;
            public float loopStart;
            public float loopEnd;
            public int loops;
            public Vector3[] deltaPosition;
            public Quaternion[] deltaRotation;
            public Vector3[] deltaScale;
            public Vector3[] basePosition;
        }

        MpegSceneContext m_Context;
        int m_Accessor;
        float m_DiagAt;
        UnityEngine.Animation m_Animation;
        AnimationClip[] m_Clips;
        long m_Cursor;
        Track[] m_Tracks;
        bool m_FormatWarned;

        public void Init(MpegSceneContext context, int accessor, UnityEngine.Animation animation, AnimationClip[] clips) {
            m_Context = context;
            m_Accessor = accessor;
            m_Animation = animation;
            m_Clips = clips;
            m_Tracks = new Track[clips.Length];
            for (int i = 0; i < m_Tracks.Length; i++) m_Tracks[i] = new Track();
            MpegTimedAccessors.Want(accessor);
            Debug.Log("XRTEST animation timing controller ready: accessor " + accessor + ", " + clips.Length + " clip(s)");
        }

        void Update() {
            MpegTimedSample sample;
            while (MpegTimedAccessors.TryDequeue(m_Accessor, ref m_Cursor, out sample)) {
                var events = new List<TimingEvent>();
                bool applyToAll;
                int length = sample.data == null ? 0 : (sample.count > 0 ? Math.Min(sample.count, sample.data.Length) : sample.data.Length);
                if (!TryParse(sample.data, length, events, out applyToAll)) {
                    MpegWarn.Once("animation_timing/format", "MPEG_animation_timing: a sample does not follow the glTFAnimationSample syntax (7.6.3.2), it is ignored");
                    continue;
                }
                if (applyToAll && events.Count != 1) {
                    MpegWarn.Once("animation_timing/apply_to_all", "MPEG_animation_timing: apply_to_all requires num_events equal to 1 (7.6.3.3), only the first event is used");
                    if (events.Count > 1) events.RemoveRange(1, events.Count - 1);
                }
                // Lower order_id values are applied first (7.6.3.3); events with equal values keep their order in the sample
                for (int k = 1; k < events.Count; k++) {
                    var current = events[k];
                    int at = k;
                    while (at > 0 && events[at - 1].order > current.order) {
                        events[at] = events[at - 1];
                        at--;
                    }
                    events[at] = current;
                }
                foreach (var e in events) {
                    if (applyToAll) {
                        for (int i = 0; i < m_Clips.Length; i++) Apply(e, i);
                    } else {
                        Apply(e, (int)e.index);
                    }
                }
            }
            Tick();
        }

        static uint ReadU32(byte[] d, ref int p) {
            uint v = ((uint)d[p] << 24) | ((uint)d[p + 1] << 16) | ((uint)d[p + 2] << 8) | d[p + 3];
            p += 4;
            return v;
        }

        /// <summary>Parses the sample syntax of 7.6.3.2. Returns false when it is truncated.</summary>
        internal static bool TryParse(byte[] d, int length, List<TimingEvent> events, out bool applyToAll) {
            applyToAll = false;
            if (d == null || length < 3) return false;
            applyToAll = (d[0] & 0x80) != 0;
            int numEvents = (d[1] << 8) | d[2];
            int p = 3;
            for (int i = 0; i < numEvents; i++) {
                if (p + 9 > length) return false;
                var e = new TimingEvent();
                e.index = ReadU32(d, ref p);
                // speed is a signed 15.16 fixed-point value
                e.speed = (int)ReadU32(d, ref p) / 65536f;
                e.state = d[p++];
                if (e.state == 6) {
                    if (p + 8 > length) return false;
                    e.startFrame = ReadU32(d, ref p);
                    e.endFrame = ReadU32(d, ref p);
                }
                if (p + 5 > length) return false;
                e.order = d[p++];
                uint numChannels = ReadU32(d, ref p);
                if (numChannels > (uint)((length - p) / 5)) return false;
                for (uint j = 0; j < numChannels; j++) {
                    e.weights.Add(d[p++]);
                    e.channels.Add(ReadU32(d, ref p));
                }
                events.Add(e);
            }
            return true;
        }

        void Apply(TimingEvent e, int animationIndex) {
            if (animationIndex < 0 || animationIndex >= m_Clips.Length) {
                MpegWarn.Once("animation_timing/index", "MPEG_animation_timing: animation index " + animationIndex + " does not exist");
                return;
            }
            var clip = m_Clips[animationIndex];
            if (clip == null) return;
            var st = m_Animation[clip.name];
            if (st == null) return;
            Debug.Log("XRTEST animation timing apply at=" + Time.timeSinceLevelLoad.ToString("0.00") + " enabled=" + st.enabled + " playing=" + m_Animation.isPlaying + " animEnabled=" + m_Animation.enabled + " go=" + m_Animation.gameObject.name + " index=" + animationIndex + " state=" + e.state + " speed=" + e.speed + " time=" + st.time.ToString("0.00") + " clip=" + clip.name);
            var t = m_Tracks[animationIndex];

            st.layer = e.order;
            if (e.weights.Count > 0) {
                // Per-channel weights cannot be applied by the legacy Animation component: the mean weight is applied to the whole animation.
                // weight is read as an unsigned value in units of 1/255 (the syntax says int(8), the semantics say units of 1/255).
                float sum = 0f;
                bool uniform = true;
                for (int i = 0; i < e.weights.Count; i++) {
                    sum += e.weights[i];
                    if (e.weights[i] != e.weights[0]) uniform = false;
                }
                if (!uniform) {
                    MpegWarn.Once("animation_timing/weights", "MPEG_animation_timing: channel weights that differ per channel are applied as their mean to the whole animation");
                }
                st.weight = Mathf.Clamp01(sum / e.weights.Count / 255f);
            } else {
                // no channel weights: the animation applies with its full weight
                st.weight = 1f;
            }

            switch (e.state) {
                case 0: // play
                    t.loop = false;
                    t.relative = false;
                    t.speed = e.speed;
                    st.wrapMode = UnityEngine.WrapMode.ClampForever;
                    if (!st.enabled || (e.speed > 0 && st.time >= st.length) || (e.speed < 0 && st.time <= 0f)) {
                        st.time = e.speed < 0 ? st.length : 0f;
                        st.enabled = true;
                    }
                    t.paused = false;
                    st.speed = e.speed;
                    break;
                case 1: // stop_at_initial
                    SetPose(st, 0f);
                    ResetTrack(t);
                    break;
                case 2: // stop_at_final
                    SetPose(st, st.length);
                    ResetTrack(t);
                    break;
                case 3: // pause
                    t.paused = true;
                    st.speed = 0f;
                    break;
                case 4: // restart
                    t.speed = e.speed;
                    t.paused = false;
                    t.loops = 0;
                    st.speed = e.speed;
                    st.time = e.speed < 0 ? st.length : 0f;
                    st.enabled = true;
                    break;
                case 5: // update
                    t.speed = e.speed;
                    if (!t.paused) st.speed = e.speed;
                    break;
                case 6: // loop between start_frame and end_frame
                    StartLoop(e, animationIndex, st, t, false);
                    break;
                case 7: // loop_relative
                    StartLoop(e, animationIndex, st, t, true);
                    break;
                default:
                    MpegWarn.Once("animation_timing/state" + e.state, "MPEG_animation_timing: reserved state value " + e.state + " ignored");
                    break;
            }
        }

        static void ResetTrack(Track t) {
            t.loop = false;
            t.relative = false;
            t.paused = false;
            t.loops = 0;
        }

        void SetPose(AnimationState st, float time) {
            float oldSpeed = st.speed;
            st.enabled = true;
            st.speed = 0f;
            st.time = time;
            m_Animation.Sample();
            st.enabled = false;
            st.speed = oldSpeed;
        }

        void StartLoop(TimingEvent e, int animationIndex, AnimationState st, Track t, bool relative) {
            t.loop = true;
            t.relative = relative;
            t.paused = false;
            t.speed = e.speed;
            t.loops = 0;
            if (relative) {
                // loop_relative has no start_frame and end_frame in the syntax: the whole animation is looped.
                t.loopStart = 0f;
                t.loopEnd = st.length;
            } else {
                float start, end;
                if (MpegAnimationKeyframes.TryGetTime(animationIndex, e.startFrame, out start) &&
                    MpegAnimationKeyframes.TryGetTime(animationIndex, e.endFrame, out end)) {
                    t.loopStart = start;
                    t.loopEnd = end;
                } else {
                    MpegWarn.Once("animation_timing/frames", "MPEG_animation_timing: key frame times are not known, the whole animation is looped");
                    t.loopStart = 0f;
                    t.loopEnd = st.length;
                }
            }
            if (t.loopEnd <= t.loopStart) {
                t.loopStart = 0f;
                t.loopEnd = st.length;
            }
            st.wrapMode = UnityEngine.WrapMode.ClampForever;
            st.speed = e.speed;
            if (st.time < t.loopStart || st.time > t.loopEnd || !st.enabled) {
                st.time = e.speed < 0 ? t.loopEnd : t.loopStart;
            }
            st.enabled = true;
            if (relative) ComputeRelativeDeltas(animationIndex, st, t);
        }

        List<int> TargetNodes(int animationIndex) {
            var result = new List<int>();
            var anims = m_Context.Root.animations;
            if (anims == null || animationIndex >= anims.Length || anims[animationIndex].channels == null) return result;
            foreach (var ch in anims[animationIndex].channels) {
                if (ch != null && ch.target != null && ch.target.node >= 0 && !result.Contains(ch.target.node)) {
                    result.Add(ch.target.node);
                }
            }
            return result;
        }

        /// <summary>Pose change over one loop, applied cumulatively so each loop starts where the previous one ended (Table 26, loop_relative).</summary>
        void ComputeRelativeDeltas(int animationIndex, AnimationState st, Track t) {
            var nodes = TargetNodes(animationIndex);
            var objs = new List<Transform>();
            foreach (int n in nodes) {
                GameObject go;
                if (m_Context.NodeObjects.TryGetValue(n, out go) && go != null) objs.Add(go.transform);
            }
            int count = objs.Count;
            t.deltaPosition = new Vector3[count];
            t.deltaRotation = new Quaternion[count];
            t.deltaScale = new Vector3[count];
            t.basePosition = null;
            if (count == 0) return;

            float oldTime = st.time;
            float oldSpeed = st.speed;
            float oldWeight = st.weight;
            st.speed = 0f;
            st.weight = 1f;
            st.enabled = true;
            var p0 = new Vector3[count];
            var r0 = new Quaternion[count];
            var s0 = new Vector3[count];
            st.time = t.loopStart;
            m_Animation.Sample();
            for (int i = 0; i < count; i++) {
                p0[i] = objs[i].localPosition;
                r0[i] = objs[i].localRotation;
                s0[i] = objs[i].localScale;
            }
            st.time = t.loopEnd;
            m_Animation.Sample();
            for (int i = 0; i < count; i++) {
                t.deltaPosition[i] = objs[i].localPosition - p0[i];
                t.deltaRotation[i] = objs[i].localRotation * Quaternion.Inverse(r0[i]);
                var s1 = objs[i].localScale;
                t.deltaScale[i] = new Vector3(
                    Mathf.Approximately(s0[i].x, 0f) ? 1f : s1.x / s0[i].x,
                    Mathf.Approximately(s0[i].y, 0f) ? 1f : s1.y / s0[i].y,
                    Mathf.Approximately(s0[i].z, 0f) ? 1f : s1.z / s0[i].z);
            }
            st.time = oldTime;
            st.speed = oldSpeed;
            st.weight = oldWeight;
            m_RelativeTargets[animationIndex] = objs;
        }

        readonly Dictionary<int, List<Transform>> m_RelativeTargets = new Dictionary<int, List<Transform>>();

        // Loops of loop and loop_relative states: wrap the time inside the loop range.
        void Tick() {
            for (int i = 0; i < m_Tracks.Length; i++) {
                var t = m_Tracks[i];
                if (!t.loop) continue;
                var clip = m_Clips[i];
                if (clip == null) continue;
                var st = m_Animation[clip.name];
                if (st == null || !st.enabled) continue;
                float range = t.loopEnd - t.loopStart;
                if (range <= 0f) continue;
                if (t.speed > 0f && st.time >= t.loopEnd) {
                    st.time = t.loopStart + Mathf.Repeat(st.time - t.loopEnd, range);
                    t.loops++;
                } else if (t.speed < 0f && st.time <= t.loopStart) {
                    st.time = t.loopEnd - Mathf.Repeat(t.loopStart - st.time, range);
                    t.loops--;
                }
            }
        }

        // The animation system has evaluated the clips before LateUpdate: add the accumulated loop offsets.
        void LateUpdate() {
            if (Time.unscaledTime > m_DiagAt) {
                m_DiagAt = Time.unscaledTime + 1f;
                foreach (var c in m_Clips) {
                    if (c == null) continue;
                    var st = m_Animation[c.name];
                    var sb = new System.Text.StringBuilder();
                    foreach (Transform ch in m_Animation.transform) sb.Append(ch.name + "=" + ch.localPosition.ToString("0.00") + " ");
                    Debug.Log("XRTEST animation diag at=" + Time.timeSinceLevelLoad.ToString("0.0") + " time=" + st.time.ToString("0.00") + " enabled=" + st.enabled + " speed=" + st.speed + " weight=" + st.weight + " playing=" + m_Animation.IsPlaying(c.name) + " " + sb);
                }
            }
            for (int i = 0; i < m_Tracks.Length; i++) {
                var t = m_Tracks[i];
                List<Transform> objs;
                if (!t.loop || !t.relative || t.loops == 0 || t.deltaPosition == null || !m_RelativeTargets.TryGetValue(i, out objs)) continue;
                for (int k = 0; k < objs.Count && k < t.deltaPosition.Length; k++) {
                    if (objs[k] == null) continue;
                    objs[k].localPosition += t.loops * t.deltaPosition[k];
                    objs[k].localRotation = Quaternion.SlerpUnclamped(Quaternion.identity, t.deltaRotation[k], t.loops) * objs[k].localRotation;
                    var s = objs[k].localScale;
                    objs[k].localScale = new Vector3(
                        s.x * Mathf.Pow(Mathf.Max(t.deltaScale[k].x, 1e-6f), t.loops),
                        s.y * Mathf.Pow(Mathf.Max(t.deltaScale[k].y, 1e-6f), t.loops),
                        s.z * Mathf.Pow(Mathf.Max(t.deltaScale[k].z, 1e-6f), t.loops));
                }
            }
        }

        void OnDestroy() {
            m_RelativeTargets.Clear();
        }
    }
}
#endif
