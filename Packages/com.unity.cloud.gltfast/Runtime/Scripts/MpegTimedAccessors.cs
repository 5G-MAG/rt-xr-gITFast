// Delivery point for timed data that the media pipeline makes available through
// MPEG_accessor_timed buffers (ISO/IEC 23090-14:2024(E), 5.2.2 and 5.2.3), and shared helpers
// of the scene level MPEG extensions. A consumer registers the accessors it needs with Want;
// the player (see TimedMetadataBridge) publishes one sample per buffer frame. When no media
// supplies a sample, the consumers keep the constant values of the scene description.

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace GLTFast {

    using Schema;

    /// <summary>
    /// One frame of an accessor: the bytes of the accessor data of that frame, after the
    /// timed accessor information header, and the element count of that frame.
    /// </summary>
    public sealed class MpegTimedSample {
        public double time;
        public long sequence;
        public byte[] data;
        /// <summary>Number of elements (accessor count) of this frame.</summary>
        public int count;

        /// <summary>Reads the data as 32 bit floats in machine byte order (the buffer API delivers FLOAT as is).</summary>
        public float[] ToFloats() {
            if (data == null) {
                return new float[0];
            }
            var result = new float[data.Length / 4];
            if (result.Length > 0) {
                System.Buffer.BlockCopy(data, 0, result, 0, result.Length * 4);
            }
            return result;
        }
    }

    /// <summary>
    /// Static queue of timed samples per accessor index.
    /// </summary>
    public static class MpegTimedAccessors {

        /// <summary>
        /// Number of samples kept per accessor. No clause gives a bound; an application with bursts of samples between two
        /// frames can raise it. When the queue is full the oldest sample is dropped and a warning is logged once.
        /// </summary>
        public static int MaxQueuedSamples = 64;
        static readonly object sync = new object();
        static readonly HashSet<int> wanted = new HashSet<int>();
        static readonly Dictionary<int, List<MpegTimedSample>> queues = new Dictionary<int, List<MpegTimedSample>>();
        static readonly Dictionary<int, long> sequences = new Dictionary<int, long>();

        /// <summary>Registers an accessor whose samples the caller wants to receive.</summary>
        public static void Want(int accessorIndex) {
            if (accessorIndex < 0) return;
            lock (sync) {
                wanted.Add(accessorIndex);
            }
        }

        /// <summary>Accessor indices that consumers have registered.</summary>
        public static int[] GetWanted() {
            lock (sync) {
                var result = new int[wanted.Count];
                wanted.CopyTo(result);
                return result;
            }
        }

        /// <summary>Publishes one frame of an accessor (called by the media side).</summary>
        public static void Publish(int accessorIndex, double time, byte[] data, int count) {
            lock (sync) {
                List<MpegTimedSample> q;
                if (!queues.TryGetValue(accessorIndex, out q)) {
                    q = new List<MpegTimedSample>();
                    queues[accessorIndex] = q;
                    sequences[accessorIndex] = 0;
                }
                long seq = sequences[accessorIndex] + 1;
                sequences[accessorIndex] = seq;
                q.Add(new MpegTimedSample { time = time, sequence = seq, data = data, count = count });
                if (q.Count > Math.Max(1, MaxQueuedSamples)) {
                    q.RemoveAt(0);
                    MpegWarn.Once("timed/queue/" + accessorIndex, "timed accessor " + accessorIndex + ": more than " + MaxQueuedSamples + " samples queued, the oldest is dropped (MpegTimedAccessors.MaxQueuedSamples)");
                }
            }
        }

        /// <summary>Newest sample of an accessor, if any.</summary>
        public static bool TryGetLatest(int accessorIndex, out MpegTimedSample sample) {
            lock (sync) {
                List<MpegTimedSample> q;
                if (queues.TryGetValue(accessorIndex, out q) && q.Count > 0) {
                    sample = q[q.Count - 1];
                    return true;
                }
            }
            sample = null;
            return false;
        }

        /// <summary>
        /// Next sample with a sequence number above <paramref name="cursor"/>, in order. The cursor
        /// is advanced; every consumer owns its cursor, so a sample is handled once per consumer.
        /// </summary>
        public static bool TryDequeue(int accessorIndex, ref long cursor, out MpegTimedSample sample) {
            lock (sync) {
                List<MpegTimedSample> q;
                if (queues.TryGetValue(accessorIndex, out q)) {
                    for (int i = 0; i < q.Count; i++) {
                        if (q[i].sequence > cursor) {
                            sample = q[i];
                            cursor = sample.sequence;
                            return true;
                        }
                    }
                }
            }
            sample = null;
            return false;
        }

        /// <summary>Forgets all registrations and samples (new scene).</summary>
        public static void Clear() {
            lock (sync) {
                wanted.Clear();
                queues.Clear();
                sequences.Clear();
            }
        }
    }

    /// <summary>
    /// Logs a message once per key and scene.
    /// </summary>
    public static class MpegWarn {
        static readonly HashSet<string> seen = new HashSet<string>();

        public static void Once(string key, string message) {
            lock (seen) {
                if (!seen.Add(key)) return;
            }
            Debug.LogWarning(message);
        }

        public static void Reset() {
            lock (seen) {
                seen.Clear();
            }
        }
    }

    /// <summary>
    /// Keyframe times of the first sampler of each glTF animation, recorded while the animation
    /// clips are built (before the accessor data is released). Used to map start_frame and
    /// end_frame of an animation sample (7.6.3.3) to a time.
    /// </summary>
    public static class MpegAnimationKeyframes {
        static readonly Dictionary<int, float[]> times = new Dictionary<int, float[]>();

        public static void Record(int animationIndex, NativeArray<float> keyframeTimes) {
            if (times.ContainsKey(animationIndex)) return;
            times[animationIndex] = keyframeTimes.ToArray();
        }

        public static bool TryGetTime(int animationIndex, uint frame, out float time) {
            float[] t;
            if (times.TryGetValue(animationIndex, out t) && t.Length > 0) {
                time = t[(int)Math.Min((long)frame, (long)(t.Length - 1))];
                return true;
            }
            time = 0f;
            return false;
        }

        public static void Clear() {
            times.Clear();
        }
    }

    /// <summary>
    /// Entry points called by the importer while a document is loaded.
    /// </summary>
    public static class MpegLoadState {
        /// <summary>Called once per document, before the scene is built.</summary>
        public static void BeginLoad(string json) {
            MpegAnimationKeyframes.Clear();
            MpegSceneUpdates.SetOriginalDocument(json);
        }
    }

    /// <summary>
    /// glTF to Unity conversions identical to the ones the importer applies to nodes.
    /// </summary>
    public static class MpegConvert {
        public static Vector3 Position(float x, float y, float z) {
            return new Vector3(-x, y, z);
        }

        public static Quaternion Rotation(float x, float y, float z, float w) {
            return new Quaternion(x, -y, -z, w);
        }

        /// <summary>Column-major glTF 4x4 matrix at offset to a Unity matrix (same mapping as Node.GetTransform).</summary>
        public static Matrix4x4 Matrix(float[] a, int o) {
            Matrix4x4 m = new Matrix4x4();
            m.m00 = a[o + 0];
            m.m10 = -a[o + 1];
            m.m20 = -a[o + 2];
            m.m30 = a[o + 3];
            m.m01 = -a[o + 4];
            m.m11 = a[o + 5];
            m.m21 = a[o + 6];
            m.m31 = a[o + 7];
            m.m02 = -a[o + 8];
            m.m12 = a[o + 9];
            m.m22 = a[o + 10];
            m.m32 = a[o + 11];
            m.m03 = -a[o + 12];
            m.m13 = a[o + 13];
            m.m23 = a[o + 14];
            m.m33 = a[o + 15];
            return m;
        }

        /// <summary>True when the accessor exists and has the given component type and element type.</summary>
        public static bool CheckAccessor(Root root, int index, GltfComponentType componentType, GltfAccessorAttributeType type, string what) {
            if (root == null || root.accessors == null || index < 0 || index >= root.accessors.Length) {
                MpegWarn.Once(what + "/index", what + ": accessor " + index + " does not exist");
                return false;
            }
            var acc = root.accessors[index];
            if (acc.componentType != componentType || acc.GetAttributeType() != type) {
                MpegWarn.Once(what + "/type", what + ": accessor " + index + " must be " + componentType + " " + type + ", it is " + acc.componentType + " " + acc.GetAttributeType());
                return false;
            }
            if (acc.extensions == null || acc.extensions.MPEG_accessor_timed == null) {
                MpegWarn.Once(what + "/timed", what + ": accessor " + index + " has no MPEG_accessor_timed extension");
                return false;
            }
            return true;
        }
    }
}
