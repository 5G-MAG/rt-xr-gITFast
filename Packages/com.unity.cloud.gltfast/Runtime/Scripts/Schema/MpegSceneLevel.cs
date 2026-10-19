// Schema classes for scene level and camera level MPEG extensions of
// ISO/IEC 23090-14:2024(E): MPEG_animation_timing (5.5.2), MPEG_scene_dynamic (5.2.4),
// MPEG_viewport_recommended (5.5.1). A member that is absent from the JSON keeps its
// default (-1 or null), because JsonUtility may create default instances of nested objects.

using System.Collections.Generic;

namespace GLTFast.Schema {

    /// <summary>
    /// MPEG_animation_timing, extension of a scene object (Table 20).
    /// </summary>
    [System.Serializable]
    public class MpegAnimationTiming {
        /// <summary>Index of the timed accessor (BYTE, SCALAR) carrying glTFAnimationSample data.</summary>
        public int accessor = -1;
    }

    /// <summary>
    /// MPEG_scene_dynamic, extension of a scene object (Table 10).
    /// </summary>
    [System.Serializable]
    public class MpegSceneDynamic {
        /// <summary>Index of the media entry of MPEG_media that carries the scene updates.</summary>
        public int media = -1;
        /// <summary>Index of one track of that media entry; -1 when not provided (all tracks).</summary>
        public int track = -1;
    }

    /// <summary>
    /// One recommended viewport (the members listed in Table 19).
    /// </summary>
    [System.Serializable]
    public class MpegViewport {
        public string name;
        /// <summary>Timed accessor, FLOAT VEC3.</summary>
        public int translation = -1;
        /// <summary>Timed accessor, FLOAT VEC4, unit quaternion (x, y, z, w).</summary>
        public int rotation = -1;
        /// <summary>"perspective" (default) or "orthographic".</summary>
        public string type;
        /// <summary>Timed accessor, FLOAT VEC4.</summary>
        public int parameters = -1;

        public bool HasData {
            get { return translation >= 0 || rotation >= 0 || parameters >= 0; }
        }
    }

    /// <summary>
    /// MPEG_viewport_recommended. Table 19 of the PDF lists the viewport members directly in
    /// the extension; the registry schema wraps them in a "viewports" array. Both shapes are read.
    /// </summary>
    [System.Serializable]
    public class MpegViewportRecommended {
        public MpegViewport[] viewports;

        // Flat form (Table 19)
        public string name;
        public int translation = -1;
        public int rotation = -1;
        public string type;
        public int parameters = -1;

        /// <summary>All viewports described by this object, flat form first.</summary>
        public List<MpegViewport> GetViewports() {
            var list = new List<MpegViewport>();
            var flat = new MpegViewport {
                name = name,
                translation = translation,
                rotation = rotation,
                type = type,
                parameters = parameters
            };
            if (flat.HasData) {
                list.Add(flat);
            }
            if (viewports != null) {
                foreach (var v in viewports) {
                    if (v != null && v.HasData) {
                        list.Add(v);
                    }
                }
            }
            return list;
        }
    }

    /// <summary>
    /// Camera extensions. The registry README places MPEG_viewport_recommended on a camera,
    /// the PDF (5.5.1.1) places it on a scene; both are read.
    /// </summary>
    [System.Serializable]
    public class CameraExtensions {
        public MpegViewportRecommended MPEG_viewport_recommended;
    }
}
