using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Haptic output of this player: the vibrator of the handheld device (Android). The haptic media
    /// format (HMPG, ISO/IEC 23090-31) is not decoded, so every rendering is a default pattern chosen
    /// by this player, not a signal derived from the haptic data.
    /// </summary>
    public static class HapticOutput
    {
        /// <summary>Duration of the default pulse in milliseconds (a player choice, not from the specification).</summary>
        public const long DefaultPulseMs = 200;
        /// <summary>Pause between two pulses of a looping rendering in milliseconds (a player choice).</summary>
        public const long DefaultLoopGapMs = 300;

        /// <summary>True while a looping rendering was stopped by HAPTIC_PAUSE and can be resumed.</summary>
        public static bool PausedLoop { get; private set; }

        public static void PlayDefault(bool loop)
        {
            PausedLoop = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    AndroidJavaObject effect;
                    if (loop)
                    {
                        // timings: wait, vibrate, wait; index 0 is the repeat start
                        long[] timings = new long[] { 0L, DefaultPulseMs, DefaultLoopGapMs };
                        effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, 0);
                    }
                    else
                    {
                        effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", DefaultPulseMs, -1);
                    }
                    using (effect)
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Haptic: vibration failed (" + e.Message + "), using Handheld.Vibrate");
                Handheld.Vibrate();
            }
#else
            Debug.Log("Haptic: default vibration (" + (loop ? "looping" : "one pulse") + ") is only produced on Android devices.");
#endif
        }

        /// <summary>Stops the vibration (also used as the washout of the vibrator).</summary>
        public static void Cancel()
        {
            PausedLoop = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    vibrator.Call("cancel");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Haptic: cancel failed (" + e.Message + ")");
            }
#endif
        }

        /// <summary>HAPTIC_PAUSE: a vibrator cannot hold a position, so the output is stopped.</summary>
        public static void Pause(bool wasLooping)
        {
            Cancel();
            PausedLoop = wasLooping;
        }
    }

    /// <summary>
    /// Annex G.3.4: haptic media with autoplay true, or with a startTime, is rendered without an action.
    /// </summary>
    internal class HapticMediaStarter : MonoBehaviour
    {
        public static void Apply(Schema.Root root)
        {
            if (root == null) return;
            var haptic = root.extensions?.MPEG_haptic;
            var media = root.extensions?.MPEG_media?.media;
            if (haptic == null || haptic.hapticObjects == null || media == null) return;

            // media indices referenced by the haptic objects (accessor, buffer view, circular buffer)
            var mediaIndices = new HashSet<int>();
            for (int o = 0; o < haptic.hapticObjects.Length; o++)
            {
                var accessors = haptic.hapticObjects[o].accessors;
                if (accessors == null) continue;
                for (int a = 0; a < accessors.Length; a++)
                {
                    int m = MediaOfAccessor(root, accessors[a]);
                    if (m >= 0) mediaIndices.Add(m);
                }
            }

            GameObject host = null;
            foreach (int m in mediaIndices)
            {
                if (m >= media.Length) continue;
                var entry = media[m];
                // 4.3: when autoplay is true startTime is ignored. startTime is negative when absent
                // (MpegMedia.startTime), so 0 starts the rendering at once.
                if (!entry.autoplay && entry.startTime < 0f) continue;
                if (host == null)
                {
                    host = new GameObject("HapticMediaStarter");
                    Transform parent = VirtualSceneGraph.GetSceneTransform();
                    if (parent != null) host.transform.SetParent(parent, false);
                }
                var starter = host.AddComponent<HapticMediaStarter>();
                starter.StartCoroutine(starter.Run(entry.autoplay ? 0f : entry.startTime, entry.loop, m));
            }
        }

        private static int MediaOfAccessor(Schema.Root root, int accessorIndex)
        {
            if (root.accessors == null || accessorIndex < 0 || accessorIndex >= root.accessors.Length) return -1;
            int view = root.accessors[accessorIndex].bufferView;
            if (root.bufferViews == null || view < 0 || view >= root.bufferViews.Length) return -1;
            int buffer = root.bufferViews[view].buffer;
            if (root.buffers == null || buffer < 0 || buffer >= root.buffers.Length) return -1;
            var circular = root.buffers[buffer].extensions?.MPEG_buffer_circular;
            return circular != null ? circular.media : -1;
        }

        private IEnumerator Run(float delay, bool loop, int mediaIndex)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Debug.LogWarning("Haptic media " + mediaIndex + " starts (autoplay or startTime): the payload (ISO/IEC 23090-31) is not decoded; playing a default vibration.");
            HapticOutput.PlayDefault(loop);
        }

        private void OnDestroy()
        {
            HapticOutput.Cancel();
        }
    }
}
