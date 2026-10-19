// MPEG_lights_texture_based (ISO/IEC 23090-14:2024(E), 8.4.2.1 and 8.4.3): an image-based light
// whose specular images (a video texture) and irradiance coefficients, intensity and rotation
// (timed accessors) vary over time. Unity has one global environment: the equirectangular
// specular image becomes the skybox material that drives reflections and ambient light, the
// intensity scales both, and the rotation (yaw only) turns the image.
//
// Not implemented, with the reason:
// - projection CUBEMAP and six specular images: the face order is "TBD" (Table 56) and the packing
//   refers to Table 10 of ISO/IEC 23090-7, which was not available.
// - irradiance coefficients: the sample is 27 floats, three per coefficient in RGB order (Table 56);
//   their normalisation is defined in EXT_lights_image_based, which was not available. They can be
//   applied unchanged as a Unity spherical harmonics probe with ApplyIrradianceCoefficients.
// - the full rotation quaternion (only its yaw is applied) and the position (the light is global).

using UnityEngine;
using UnityEngine.Rendering;

namespace GLTFast {

    using Schema;

    public class MpegTextureBasedLightController : MonoBehaviour {

        /// <summary>Apply the irradiance sample as the ambient probe without conversion. Off by default, see the file header.</summary>
        public static bool ApplyIrradianceCoefficients = false;

        const float EnvironmentUpdateInterval = 0.5f;

        MpegTextureBasedLight m_Light;
        UnityEngine.Material m_Skybox;
        bool m_Active;
        bool m_HasIntensity;
        bool m_HasRotation;
        bool m_HasIrradiance;
        float m_Intensity = 1f;     // Table 56: without intensityAccessor the intensity is 1.0
        float m_NextUpdate;

        // previous environment, restored when the scene goes away
        UnityEngine.Material m_PrevSkybox;
        AmbientMode m_PrevAmbientMode;
        DefaultReflectionMode m_PrevReflectionMode;
        float m_PrevAmbientIntensity;
        float m_PrevReflectionIntensity;
        SphericalHarmonicsL2 m_PrevProbe;
        bool m_Saved;

        public string Nature { get; private set; }

        public void Init(MpegSceneContext context, MpegTextureBasedLight light) {
            m_Light = light;
            Nature = string.IsNullOrEmpty(light.nature) ? "PHYSICAL" : light.nature;
            var root = context.Root;

            string projection = string.IsNullOrEmpty(light.projection) ? "EQUIRECTANGULAR" : light.projection.ToUpperInvariant();
            int images = light.specularImages == null ? 0 : light.specularImages.Length;
            if (images != 1 && images != 6) {
                MpegWarn.Once("lights_texture/images", "MPEG_lights_texture_based: specularImages must have 1 or 6 items (Table 56), the light is ignored");
                return;
            }
            if (images == 6 && projection != "CUBEMAP") {
                MpegWarn.Once("lights_texture/six", "MPEG_lights_texture_based: six specular images require the CUBEMAP projection (Table 56)");
            }
            if (images == 6 || projection == "CUBEMAP") {
                MpegWarn.Once("lights_texture/cubemap", "MPEG_lights_texture_based: cube map specular images are not implemented (face order not defined in the text)");
                return;
            }
            if (projection != "EQUIRECTANGULAR") {
                MpegWarn.Once("lights_texture/projection", "MPEG_lights_texture_based: projection '" + light.projection + "' is not known, the light is ignored");
                return;
            }

            var texture = context.Import.GetTexture(light.specularImages[0]);
            if (texture == null) {
                MpegWarn.Once("lights_texture/texture", "MPEG_lights_texture_based: specular image texture " + light.specularImages[0] + " is not available");
                return;
            }
            // Skybox/Panoramic is only found when the build includes it; Assets/Resources/MpegEquirectSkybox.shader
            // is part of every build and has the same properties
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) shader = Shader.Find("MPEG/EquirectSkybox");
            if (shader == null) {
                MpegWarn.Once("lights_texture/shader", "MPEG_lights_texture_based: neither Skybox/Panoramic nor MPEG/EquirectSkybox is part of this build, no environment is set");
                return;
            }

            m_HasIntensity = light.intensityAccessor >= 0 && CheckFloat(root, light.intensityAccessor, GltfAccessorAttributeType.SCALAR, "MPEG_lights_texture_based.intensityAccessor");
            m_HasRotation = light.rotationAccessor >= 0 && CheckFloat(root, light.rotationAccessor, GltfAccessorAttributeType.VEC4, "MPEG_lights_texture_based.rotationAccessor");
            m_HasIrradiance = light.irradianceAccessor >= 0 && CheckFloat(root, light.irradianceAccessor, GltfAccessorAttributeType.Undefined, "MPEG_lights_texture_based.irradianceAccessor");
            if (light.irradianceAccessor < 0) {
                MpegWarn.Once("lights_texture/irradiance", "MPEG_lights_texture_based: irradianceAccessor is mandatory (Table 56)");
            }
            if (m_HasIntensity) MpegTimedAccessors.Want(light.intensityAccessor);
            if (m_HasRotation) MpegTimedAccessors.Want(light.rotationAccessor);
            if (m_HasIrradiance) MpegTimedAccessors.Want(light.irradianceAccessor);

            m_Skybox = new UnityEngine.Material(shader);
            m_Skybox.SetTexture("_MainTex", texture);
            SaveEnvironment();
            RenderSettings.skybox = m_Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            ApplyIntensity();
            m_Active = true;
            DynamicGI.UpdateEnvironment();
        }

        static bool CheckFloat(Root root, int index, GltfAccessorAttributeType type, string what) {
            if (root.accessors == null || index >= root.accessors.Length) {
                MpegWarn.Once(what + "/index", what + ": accessor " + index + " does not exist");
                return false;
            }
            var acc = root.accessors[index];
            if (acc.componentType != GltfComponentType.Float || (type != GltfAccessorAttributeType.Undefined && acc.GetAttributeType() != type)) {
                MpegWarn.Once(what + "/type", what + ": accessor " + index + " must be FLOAT" + (type == GltfAccessorAttributeType.Undefined ? "" : " " + type));
                return false;
            }
            if (acc.extensions == null || acc.extensions.MPEG_accessor_timed == null) {
                MpegWarn.Once(what + "/timed", what + ": accessor " + index + " has no MPEG_accessor_timed extension");
                return false;
            }
            return true;
        }

        void SaveEnvironment() {
            m_PrevSkybox = RenderSettings.skybox;
            m_PrevAmbientMode = RenderSettings.ambientMode;
            m_PrevReflectionMode = RenderSettings.defaultReflectionMode;
            m_PrevAmbientIntensity = RenderSettings.ambientIntensity;
            m_PrevReflectionIntensity = RenderSettings.reflectionIntensity;
            m_PrevProbe = RenderSettings.ambientProbe;
            m_Saved = true;
        }

        void ApplyIntensity() {
            if (m_Skybox != null && m_Skybox.HasProperty("_Exposure")) {
                m_Skybox.SetFloat("_Exposure", m_Intensity);
            }
            RenderSettings.ambientIntensity = m_Intensity;
            RenderSettings.reflectionIntensity = m_Intensity;
        }

        void Update() {
            if (!m_Active) return;
            bool changed = false;
            MpegTimedSample s;
            if (m_HasIntensity && MpegTimedAccessors.TryGetLatest(m_Light.intensityAccessor, out s)) {
                var f = s.ToFloats();
                if (f.Length >= 1 && !Mathf.Approximately(f[0], m_Intensity)) {
                    m_Intensity = Mathf.Max(0f, f[0]);
                    ApplyIntensity();
                    changed = true;
                }
            }
            if (m_HasRotation && MpegTimedAccessors.TryGetLatest(m_Light.rotationAccessor, out s)) {
                var f = s.ToFloats();
                if (f.Length >= 4 && m_Skybox.HasProperty("_Rotation")) {
                    var q = MpegConvert.Rotation(f[0], f[1], f[2], f[3]);
                    m_Skybox.SetFloat("_Rotation", q.eulerAngles.y);
                    changed = true;
                }
            }
            if (m_HasIrradiance && ApplyIrradianceCoefficients && MpegTimedAccessors.TryGetLatest(m_Light.irradianceAccessor, out s)) {
                var f = s.ToFloats();
                if (f.Length >= 27) {
                    var probe = new SphericalHarmonicsL2();
                    for (int c = 0; c < 9; c++) {
                        for (int ch = 0; ch < 3; ch++) {
                            probe[ch, c] = f[c * 3 + ch];
                        }
                    }
                    RenderSettings.ambientMode = AmbientMode.Custom;
                    RenderSettings.ambientProbe = probe;
                    changed = true;
                }
            } else if (m_HasIrradiance && !ApplyIrradianceCoefficients) {
                MpegWarn.Once("lights_texture/irradiance_off", "MPEG_lights_texture_based: irradiance coefficients are read but not applied (conversion to the Unity probe is not verified)");
            }
            // The specular image changes every video frame: refresh the environment lighting at a low rate.
            if (changed || Time.unscaledTime >= m_NextUpdate) {
                m_NextUpdate = Time.unscaledTime + EnvironmentUpdateInterval;
                DynamicGI.UpdateEnvironment();
            }
        }

        void OnDestroy() {
            if (m_Saved) {
                RenderSettings.skybox = m_PrevSkybox;
                RenderSettings.ambientMode = m_PrevAmbientMode;
                RenderSettings.defaultReflectionMode = m_PrevReflectionMode;
                RenderSettings.ambientIntensity = m_PrevAmbientIntensity;
                RenderSettings.reflectionIntensity = m_PrevReflectionIntensity;
                RenderSettings.ambientProbe = m_PrevProbe;
            }
            if (m_Skybox != null) Destroy(m_Skybox);
        }
    }
}
