// MPEG_lights_punctual (ISO/IEC 23090-14:2024(E), 8.4.2.2 and 8.4.3): "The Presentation Engine
// places the punctual lights using the same processing model as the KHR_lights_punctual elements
// and retrieves timed dependent information through the color, intensity and range accessors."
// The Unity Light is created by the importer from the KHR light; this controller replaces the
// constant color, intensity and range with the timed values. Without a value for an attribute the
// constant of the KHR light stays (Table 58).

using UnityEngine;

namespace GLTFast {

    using Schema;

    public class MpegPunctualLightController : MonoBehaviour {

        /// <summary>PHYSICAL, VIRTUAL, PHYSICAL_VIRTUAL or UNSPECIFIED (Table 58). The player renders virtual
        /// content only, so every nature lights that content; the value is kept for applications.</summary>
        public string Nature { get; private set; }

        MpegLightsPunctual m_Ext;
        Light m_Light;
        float m_IntensityScale = 1f;
        bool m_Color;
        bool m_Intensity;
        bool m_Range;

        public void Init(MpegSceneContext context, MpegLightsPunctual ext, LightPunctual source, Light light) {
            m_Ext = ext;
            m_Light = light;
            Nature = ext.nature;
            var root = context.Root;

            // Relation between the glTF intensity and the Unity intensity as the importer applied it
            if (source != null && source.intensity > 0f && light.intensity > 0f) {
                m_IntensityScale = light.intensity / source.intensity;
            } else {
                m_IntensityScale = source != null ? ImporterScale(source) : (RenderPipelineUtils.RenderPipeline == RenderPipeline.BuiltIn ? 1f / Mathf.PI : 1f);
            }

            m_Color = ext.color_accessor >= 0 &&
                MpegConvert.CheckAccessor(root, ext.color_accessor, GltfComponentType.Float, GltfAccessorAttributeType.VEC3, "MPEG_lights_punctual.color_accessor");
            m_Intensity = ext.intensity_accessor >= 0 &&
                MpegConvert.CheckAccessor(root, ext.intensity_accessor, GltfComponentType.Float, GltfAccessorAttributeType.SCALAR, "MPEG_lights_punctual.intensity_accessor");
            m_Range = ext.range_accessor >= 0 &&
                MpegConvert.CheckAccessor(root, ext.range_accessor, GltfComponentType.Float, GltfAccessorAttributeType.SCALAR, "MPEG_lights_punctual.range_accessor");
            if (m_Color) MpegTimedAccessors.Want(ext.color_accessor);
            if (m_Intensity) MpegTimedAccessors.Want(ext.intensity_accessor);
            if (m_Range) MpegTimedAccessors.Want(ext.range_accessor);
        }

        /// <summary>Intensity conversion factor of the importer settings (InstantiationSettings.lightIntensityFactor), recorded when the light is instantiated.</summary>
        public static float ImporterIntensityFactor = 1f;

        // The importer's own conversion from glTF intensity to Unity intensity, found by converting a unit
        // intensity with the same function; falls back to 1 when that does not give a usable value.
        float ImporterScale(LightPunctual source) {
            float original = source.intensity;
            var probe = new GameObject("MPEG_light_scale");
            probe.hideFlags = HideFlags.HideAndDontSave;
            float scale = 1f;
            try {
                var light = probe.AddComponent<Light>();
                source.intensity = 1f;
                source.ToUnityLight(light, ImporterIntensityFactor);
                if (light.intensity > 0f) scale = light.intensity;
            } finally {
                source.intensity = original;
                Destroy(probe);
            }
            return scale;
        }

        void Update() {
            if (m_Light == null) return;
            MpegTimedSample s;
            if (m_Color && MpegTimedAccessors.TryGetLatest(m_Ext.color_accessor, out s)) {
                var f = s.ToFloats();
                if (f.Length >= 3) {
                    // the importer converts the linear glTF color with .gamma
                    m_Light.color = new Color(f[0], f[1], f[2], 1f).gamma;
                }
            }
            if (m_Intensity && MpegTimedAccessors.TryGetLatest(m_Ext.intensity_accessor, out s)) {
                var f = s.ToFloats();
                if (f.Length >= 1) {
                    m_Light.intensity = Mathf.Max(0f, f[0]) * m_IntensityScale;
                }
            }
            if (m_Range && MpegTimedAccessors.TryGetLatest(m_Ext.range_accessor, out s)) {
                var f = s.ToFloats();
                if (f.Length >= 1) {
                    // same convention as the importer: no range means a very large range
                    m_Light.range = f[0] > 0f ? f[0] : 100000f;
                }
            }
        }
    }
}
