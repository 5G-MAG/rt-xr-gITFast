// MPEG_viewport_recommended (ISO/IEC 23090-14:2024(E), 5.5.1): timed translation, rotation and
// camera parameters that a renderer should apply to the camera object. The data comes from timed
// accessors (the carriage is not specified by the document, 5.5.1.3 NOTE).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast {

    using Schema;

    /// <summary>
    /// Public view of one recommended viewport.
    /// </summary>
    public sealed class RecommendedViewportInfo {
        public string Name;
        /// <summary>"perspective" or "orthographic".</summary>
        public string Type;
        public UnityEngine.Camera Camera;
        public Transform Node;
    }

    /// <summary>
    /// Recommended viewports of the loaded scene. An application with its own camera selection
    /// can take PreferredCamera and let the controllers mirror the recommended view onto its camera.
    /// </summary>
    public static class MpegViewportRecommendedRuntime {
        static readonly List<RecommendedViewportInfo> viewports = new List<RecommendedViewportInfo>();

        public static IReadOnlyList<RecommendedViewportInfo> Viewports { get { return viewports; } }

        /// <summary>Camera object of the first recommended viewport, or null when the scene has none.</summary>
        public static UnityEngine.Camera PreferredCamera {
            get { return viewports.Count > 0 ? viewports[0].Camera : null; }
        }

        /// <summary>Camera that follows the first recommended viewport while <see cref="FollowRecommended"/> is true.</summary>
        public static UnityEngine.Camera MirrorTarget { get; private set; }

        /// <summary>The client may render the viewport (5.5.1.1); set to false to stop mirroring.</summary>
        public static bool FollowRecommended = true;

        public static void SetMirrorTarget(UnityEngine.Camera target) {
            MirrorTarget = target;
        }

        internal static void Register(RecommendedViewportInfo info) {
            viewports.Add(info);
        }

        public static void Reset() {
            viewports.Clear();
            MirrorTarget = null;
        }
    }

    public class MpegViewportRecommendedController : MonoBehaviour {

        MpegViewport m_Viewport;
        Transform m_Node;
        UnityEngine.Camera m_Camera;
        RecommendedViewportInfo m_Info;
        bool m_Perspective = true;
        bool m_HasTranslation;
        bool m_HasRotation;
        bool m_HasParameters;
        // true once timed data was applied: before that the viewer keeps control of its own camera
        bool m_Received;

        /// <param name="cameraType">"perspective" or "orthographic": the type of the glTF camera object that is driven.
        /// Table 19 ties the meaning of parameters to the type of that camera object; the type member of the extension
        /// is used only when the camera object does not give one.</param>
        public void Init(MpegSceneContext context, MpegViewport viewport, Transform node, UnityEngine.Camera camera, string cameraType = null) {
            m_Viewport = viewport;
            m_Node = node;
            m_Camera = camera;
            var root = context.Root;

            string type = !string.IsNullOrEmpty(cameraType) ? cameraType : (string.IsNullOrEmpty(viewport.type) ? "perspective" : viewport.type);
            if (!string.IsNullOrEmpty(cameraType) && !string.IsNullOrEmpty(viewport.type) &&
                !string.Equals(cameraType, viewport.type, StringComparison.OrdinalIgnoreCase)) {
                MpegWarn.Once("viewport/typemismatch", "MPEG_viewport_recommended: the type of the extension ('" + viewport.type + "') differs from the type of the camera object ('" + cameraType + "'), the camera object type is used");
            }
            if (string.Equals(type, "orthographic", StringComparison.OrdinalIgnoreCase)) {
                m_Perspective = false;
                type = "orthographic";
            } else if (!string.Equals(type, "perspective", StringComparison.OrdinalIgnoreCase)) {
                MpegWarn.Once("viewport/type", "MPEG_viewport_recommended: camera type '" + type + "' is not known, perspective is used");
                type = "perspective";
            } else {
                type = "perspective";
            }

            m_HasTranslation = viewport.translation >= 0 &&
                MpegConvert.CheckAccessor(root, viewport.translation, GltfComponentType.Float, GltfAccessorAttributeType.VEC3, "MPEG_viewport_recommended.translation");
            m_HasRotation = viewport.rotation >= 0 &&
                MpegConvert.CheckAccessor(root, viewport.rotation, GltfComponentType.Float, GltfAccessorAttributeType.VEC4, "MPEG_viewport_recommended.rotation");
            m_HasParameters = viewport.parameters >= 0 &&
                MpegConvert.CheckAccessor(root, viewport.parameters, GltfComponentType.Float, GltfAccessorAttributeType.VEC4, "MPEG_viewport_recommended.parameters");
            if (m_HasTranslation) MpegTimedAccessors.Want(viewport.translation);
            if (m_HasRotation) MpegTimedAccessors.Want(viewport.rotation);
            if (m_HasParameters) MpegTimedAccessors.Want(viewport.parameters);

            m_Info = new RecommendedViewportInfo { Name = viewport.name, Type = type, Camera = camera, Node = node };
            MpegViewportRecommendedRuntime.Register(m_Info);
        }

        void Update() {
            if (m_Node == null || m_Camera == null) return;
            MpegTimedSample s;
            float[] f;
            if (m_HasTranslation && MpegTimedAccessors.TryGetLatest(m_Viewport.translation, out s)) {
                f = s.ToFloats();
                if (f.Length >= 3) {
                    m_Node.localPosition = MpegConvert.Position(f[0], f[1], f[2]);
                    m_Received = true;
                }
            }
            if (m_HasRotation && MpegTimedAccessors.TryGetLatest(m_Viewport.rotation, out s)) {
                f = s.ToFloats();
                if (f.Length >= 4) {
                    var q = new Quaternion(f[0], f[1], f[2], f[3]);
                    float norm = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                    if (norm > 1e-6f) {
                        m_Node.localRotation = MpegConvert.Rotation(q.x / norm, q.y / norm, q.z / norm, q.w / norm);
                        m_Received = true;
                    }
                }
            }
            if (m_HasParameters && MpegTimedAccessors.TryGetLatest(m_Viewport.parameters, out s)) {
                f = s.ToFloats();
                if (f.Length >= 4) {
                    ApplyParameters(f);
                    m_Received = true;
                }
            }
        }

        // Perspective: (aspectRatio, yfov, zfar, znear). Orthographic: (xmag, ymag, zfar, znear).
        // The screen decides the rendered aspect ratio, so aspectRatio and xmag are not forced on the camera.
        void ApplyParameters(float[] f) {
            m_Camera.ResetProjectionMatrix();
            if (m_Perspective) {
                m_Camera.orthographic = false;
                if (f[1] > 0f) m_Camera.fieldOfView = f[1] * Mathf.Rad2Deg;
            } else {
                m_Camera.orthographic = true;
                if (Mathf.Abs(f[1]) > 0f) m_Camera.orthographicSize = Mathf.Abs(f[1]);
            }
            float far = f[2], near = f[3];
            if (near > 0f && far > near) {
                var ls = m_Node.lossyScale;
                float scale = (ls.x + ls.y + ls.z) / 3f;
                if (scale <= 0f) scale = 1f;
                m_Camera.nearClipPlane = near * scale;
                m_Camera.farClipPlane = far * scale;
            } else {
                MpegWarn.Once("viewport/clip", "MPEG_viewport_recommended: parameters need 0 < znear < zfar, the clip planes are left unchanged");
            }
        }

        void LateUpdate() {
            var mirror = MpegViewportRecommendedRuntime.MirrorTarget;
            if (!m_Received || !MpegViewportRecommendedRuntime.FollowRecommended || mirror == null || m_Camera == null) return;
            if (MpegViewportRecommendedRuntime.PreferredCamera != m_Camera) return;
            mirror.transform.SetPositionAndRotation(m_Camera.transform.position, m_Camera.transform.rotation);
            mirror.orthographic = m_Camera.orthographic;
            mirror.fieldOfView = m_Camera.fieldOfView;
            mirror.orthographicSize = m_Camera.orthographicSize;
            mirror.nearClipPlane = m_Camera.nearClipPlane;
            mirror.farClipPlane = m_Camera.farClipPlane;
        }

        void OnDestroy() {
            // the registry is rebuilt for every scene
        }
    }
}
