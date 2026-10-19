// Instantiates the runtime behaviour of the MPEG extensions that have no counterpart in the base
// glTF importer: MPEG_animation_timing, MPEG_scene_dynamic, MPEG_viewport_recommended,
// MPEG_mesh_linking, MPEG_lights_punctual, MPEG_lights_texture_based, MPEG_node_avatar and
// MPEG_primitive_V3C (ISO/IEC 23090-14:2024(E)). Called once after a scene was instantiated.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast {

    using Schema;

    /// <summary>
    /// What a controller needs to know about the instantiated scene.
    /// </summary>
    public sealed class MpegSceneContext {
        public GltfImport Import;
        public Root Root;
        public int SceneIndex;
        /// <summary>Parent for controller objects; destroyed together with the scene.</summary>
        public Transform Parent;
        /// <summary>Instantiated node objects by glTF node index, for the nodes reachable from the scene.</summary>
        public Dictionary<int, GameObject> NodeObjects = new Dictionary<int, GameObject>();
        public List<int> NodeIndices = new List<int>();

        public GameObject NewObject(string name) {
            var go = new GameObject(name);
            if (Parent != null) {
                go.transform.SetParent(Parent, false);
            }
            return go;
        }
    }

    public static class MpegSceneExtensionsRuntime {

        /// <summary>
        /// Creates the controllers for the MPEG extensions present in the document.
        /// Never throws: a failure is logged and the scene continues without the extension.
        /// </summary>
        public static void OnSceneInstantiated(GltfImport import, Root root, int sceneIndex) {
            try {
                MpegTimedAccessors.Clear();
                MpegWarn.Reset();
                MpegViewportRecommendedRuntime.Reset();
                if (root == null || root.scenes == null || sceneIndex < 0 || sceneIndex >= root.scenes.Length) {
                    return;
                }
                var context = new MpegSceneContext {
                    Import = import,
                    Root = root,
                    SceneIndex = sceneIndex,
                    Parent = VirtualSceneGraph.GetSceneTransform()
                };
                CollectNodes(context);

                var scene = root.scenes[sceneIndex];
                var ext = scene.extensions;
                Run("MPEG_animation_timing", () => SetUpAnimationTiming(context, ext));
                Run("MPEG_scene_dynamic", () => SetUpSceneDynamic(context, ext));
                Run("MPEG_viewport_recommended", () => SetUpViewports(context, ext));
                Run("MPEG_mesh_linking", () => SetUpMeshLinking(context));
                Run("MPEG_lights_punctual", () => SetUpPunctualLights(context));
                Run("MPEG_lights_texture_based", () => SetUpTextureBasedLights(context, ext));
                Run("MPEG_audio_spatial reverbs", () => SetUpReverbs(context));
                Run("MPEG_node_avatar", () => SetUpAvatars(context));
                Run("MPEG_primitive_V3C", () => CheckV3C(root));
                Run("required fallbacks", () => WarnRequiredFallbacks(root));
            } catch (Exception e) {
                Debug.LogWarning("MPEG scene extensions: " + e.Message);
            }
        }

        static void Run(string name, System.Action action) {
            try {
                action();
            } catch (Exception e) {
                Debug.LogWarning(name + ": " + e.Message);
            }
        }

        static void CollectNodes(MpegSceneContext context) {
            var root = context.Root;
            var scene = root.scenes[context.SceneIndex];
            if (scene.nodes == null || root.nodes == null) return;
            var stack = new Stack<int>();
            foreach (var n in scene.nodes) stack.Push((int)n);
            while (stack.Count > 0) {
                int index = stack.Pop();
                if (index < 0 || index >= root.nodes.Length || context.NodeObjects.ContainsKey(index)) continue;
                GameObject go = null;
                try {
                    go = VirtualSceneGraph.GetGameObjectFromIndex(index);
                } catch (Exception) {
                    go = null;
                }
                if (go == null) continue;
                context.NodeObjects[index] = go;
                context.NodeIndices.Add(index);
                var children = root.nodes[index].children;
                if (children != null) {
                    foreach (var c in children) stack.Push((int)c);
                }
            }
        }

        // 5.5.2
        static void SetUpAnimationTiming(MpegSceneContext context, SceneExtensions ext) {
#if UNITY_ANIMATION
            if (ext == null || ext.MPEG_animation_timing == null || ext.MPEG_animation_timing.accessor < 0) return;
            int accessor = ext.MPEG_animation_timing.accessor;
            if (!MpegConvert.CheckAccessor(context.Root, accessor, GltfComponentType.Byte, GltfAccessorAttributeType.SCALAR, "MPEG_animation_timing")) return;
            var animation = context.Parent != null ? context.Parent.GetComponent<UnityEngine.Animation>() : null;
            var clips = context.Import.GetAnimationClips();
            if (animation == null || clips == null) {
                MpegWarn.Once("animation_timing/legacy", "MPEG_animation_timing: the scene has no legacy Animation component, the extension is ignored");
                return;
            }
            context.NewObject("MPEG_animation_timing").AddComponent<MpegAnimationTimingController>().Init(context, accessor, animation, clips);
#endif
        }

        // 5.4.1.1 and 5.4.1.3: the reverb units of the glTF file level extension
        static void SetUpReverbs(MpegSceneContext context) {
            var reverbs = context.Root.extensions?.MPEG_audio_spatial?.reverbs;
            if (reverbs == null) return;
            foreach (var reverb in reverbs) {
                if (reverb == null) continue;
                MpegReverbUnit.Create(context.NewObject("reverb " + reverb.id), reverb);
            }
        }

        // 5.2.4
        static void SetUpSceneDynamic(MpegSceneContext context, SceneExtensions ext) {
            if (ext == null || ext.MPEG_scene_dynamic == null || ext.MPEG_scene_dynamic.media < 0) return;
            context.NewObject("MPEG_scene_dynamic").AddComponent<MpegSceneDynamicController>().Init(context, ext.MPEG_scene_dynamic);
        }

        // 5.5.1: on the scene object (PDF) and on camera objects (registry README, Annex F)
        static void SetUpViewports(MpegSceneContext context, SceneExtensions ext) {
            var root = context.Root;
            // Camera nodes of the scene by camera index
            var cameraNodes = new Dictionary<int, int>();
            int firstCameraNode = -1;
            foreach (int n in context.NodeIndices) {
                int cam = root.nodes[n].camera;
                if (cam >= 0) {
                    if (!cameraNodes.ContainsKey(cam)) cameraNodes[cam] = n;
                    if (firstCameraNode < 0) firstCameraNode = n;
                }
            }
            if (ext != null && ext.MPEG_viewport_recommended != null) {
                foreach (var vp in ext.MPEG_viewport_recommended.GetViewports()) {
                    AddViewport(context, vp, firstCameraNode);
                }
            }
            if (root.cameras != null) {
                for (int c = 0; c < root.cameras.Length; c++) {
                    var camExt = root.cameras[c].extensions;
                    int node;
                    if (camExt == null || camExt.MPEG_viewport_recommended == null || !cameraNodes.TryGetValue(c, out node)) continue;
                    foreach (var vp in camExt.MPEG_viewport_recommended.GetViewports()) {
                        AddViewport(context, vp, node);
                    }
                }
            }
        }

        // "perspective" or "orthographic" from the camera object of a node; null when the object does not say
        static string CameraTypeOf(Root root, int node) {
            int cam = root.nodes[node].camera;
            if (root.cameras == null || cam < 0 || cam >= root.cameras.Length) return null;
            try {
                return root.cameras[cam].GetCameraType() == GLTFast.Schema.Camera.Type.Orthographic ? "orthographic" : "perspective";
            } catch (System.Exception) {
                return null;
            }
        }

        static void AddViewport(MpegSceneContext context, MpegViewport vp, int cameraNode) {
            if (cameraNode < 0) {
                MpegWarn.Once("viewport/camera", "MPEG_viewport_recommended: the scene has no camera node to drive");
                return;
            }
            GameObject nodeObject;
            if (!context.NodeObjects.TryGetValue(cameraNode, out nodeObject)) return;
            var camera = nodeObject.GetComponentInChildren<UnityEngine.Camera>(true);
            if (camera == null) {
                MpegWarn.Once("viewport/component", "MPEG_viewport_recommended: the camera node has no camera component");
                return;
            }
            var go = context.NewObject("MPEG_viewport_recommended " + (string.IsNullOrEmpty(vp.name) ? "" : vp.name));
            go.AddComponent<MpegViewportRecommendedController>().Init(context, vp, nodeObject.transform, camera, CameraTypeOf(context.Root, cameraNode));
        }

        // 5.3.2
        static void SetUpMeshLinking(MpegSceneContext context) {
            var root = context.Root;
            if (root.meshes == null) return;
            for (int m = 0; m < root.meshes.Length; m++) {
                var link = root.meshes[m].extensions != null ? root.meshes[m].extensions.MPEG_mesh_linking : null;
                if (link == null || !link.IsPresent) continue;
                if (link.correspondence < 0 || link.mesh < 0 || link.pose < 0) {
                    MpegWarn.Once("mesh_linking/mandatory", "MPEG_mesh_linking: correspondence, mesh and pose are mandatory (Table 12), the extension is ignored");
                    continue;
                }
                if (link.mesh >= root.meshes.Length || link.mesh == m) {
                    MpegWarn.Once("mesh_linking/mesh", "MPEG_mesh_linking: mesh " + link.mesh + " is not a valid shadow mesh");
                    continue;
                }
                foreach (int n in context.NodeIndices) {
                    if (root.nodes[n].mesh != m) continue;
                    context.NewObject("MPEG_mesh_linking " + n).AddComponent<MpegMeshLinkingController>().Init(context, m, link, context.NodeObjects[n]);
                }
            }
        }

        // 8.4.2.2, 8.4.3
        static void SetUpPunctualLights(MpegSceneContext context) {
            var root = context.Root;
            var ext = root.extensions != null ? root.extensions.MPEG_lights_punctual : null;
            if (ext == null || ext.light < 0) return;
            var khr = root.extensions.KHR_lights_punctual;
            if (khr == null || khr.lights == null || ext.light >= khr.lights.Length) {
                MpegWarn.Once("lights_punctual/index", "MPEG_lights_punctual: light " + ext.light + " is not an item of KHR_lights_punctual");
                return;
            }
            foreach (int n in context.NodeIndices) {
                var node = root.nodes[n];
                if (node.extensions == null || node.extensions.KHR_lights_punctual == null || node.extensions.KHR_lights_punctual.light != ext.light) continue;
                var light = context.NodeObjects[n].GetComponentInChildren<Light>(true);
                if (light == null) continue;
                context.NewObject("MPEG_lights_punctual " + n).AddComponent<MpegPunctualLightController>().Init(context, ext, khr.lights[ext.light], light);
            }
        }

        // 8.4.2.1, 8.4.3
        static void SetUpTextureBasedLights(MpegSceneContext context, SceneExtensions sceneExt) {
            var root = context.Root;
            var fileExt = root.extensions != null ? root.extensions.MPEG_lights_texture_based : null;
            if (fileExt == null || fileExt.lights == null || fileExt.lights.Length == 0) return;
            var selected = new List<int>();
            if (sceneExt != null && sceneExt.MPEG_lights_texture_based != null && sceneExt.MPEG_lights_texture_based.light != null) {
                selected.AddRange(sceneExt.MPEG_lights_texture_based.light);
            }
            if (selected.Count == 0) {
                MpegWarn.Once("lights_texture/scene", "MPEG_lights_texture_based: the scene object lists no light (Table 57), no image-based light is placed");
                return;
            }
            if (selected.Count > 1) {
                MpegWarn.Once("lights_texture/multiple", "MPEG_lights_texture_based: the scene lists several lights, only the first one is used as the environment");
            }
            int index = selected[0];
            if (index < 0 || index >= fileExt.lights.Length) {
                MpegWarn.Once("lights_texture/index", "MPEG_lights_texture_based: light " + index + " is not an item of the lights array");
                return;
            }
            context.NewObject("MPEG_lights_texture_based").AddComponent<MpegTextureBasedLightController>().Init(context, fileExt.lights[index]);
        }

        // 8.3
        static void SetUpAvatars(MpegSceneContext context) {
            var root = context.Root;
            foreach (int n in context.NodeIndices) {
                var avatar = root.nodes[n].extensions != null ? root.nodes[n].extensions.MPEG_node_avatar : null;
                if (avatar == null || !avatar.IsPresent) continue;
                context.NodeObjects[n].AddComponent<MpegNodeAvatarComponent>().Init(context, n, avatar);
            }
        }

        // Annex H and G.1.1: avatar and V3C data are listed as supported so that a file that requires them loads,
        // with the documented fallback. Said once per scene when the file requires them.
        static void WarnRequiredFallbacks(Root root) {
            if (root.extensionsRequired == null) return;
            foreach (var name in root.extensionsRequired) {
                if (name == "MPEG_node_avatar") {
                    MpegWarn.Once("required/avatar", "MPEG_node_avatar is listed in extensionsRequired: the player does not reconstruct avatars, the meshes of the avatar nodes render as ordinary glTF");
                } else if (name == "MPEG_primitive_V3C") {
                    MpegWarn.Once("required/v3c", "MPEG_primitive_V3C is listed in extensionsRequired: the player does not reconstruct V3C content, the raw data described by the primitive attributes is used (G.1.1)");
                }
            }
        }

        // G.1.1: without V3C reconstruction the raw data described by the primitive attributes is used
        static void CheckV3C(Root root) {
            if (root.meshes == null) return;
            for (int m = 0; m < root.meshes.Length; m++) {
                var prims = root.meshes[m].primitives;
                if (prims == null) continue;
                for (int p = 0; p < prims.Length; p++) {
                    var v3c = prims[p].extensions != null ? prims[p].extensions.MPEG_primitive_V3C : null;
                    if (v3c == null || !v3c.IsPresent) continue;
                    if (v3c._MPEG_V3C_CONFIG < 0 || v3c._MPEG_V3C_AD == null || v3c._MPEG_V3C_AD.accessor < 0 || v3c._MPEG_V3C_GVD_MAPS == null) {
                        MpegWarn.Once("v3c/mandatory/" + m + "/" + p, "MPEG_primitive_V3C on mesh " + m + " primitive " + p + ": _MPEG_V3C_CONFIG, _MPEG_V3C_AD and _MPEG_V3C_GVD_MAPS are mandatory (Table 60)");
                    }
                    if (root.extensionsUsed == null || System.Array.IndexOf(root.extensionsUsed, "MPEG_primitive_V3C") < 0) {
                        MpegWarn.Once("v3c/used", "MPEG_primitive_V3C: usage of the extension must be listed in extensionsUsed (G.1.1)");
                    }
                    if (v3c._MPEG_V3C_AVD != null) {
                        foreach (var avd in v3c._MPEG_V3C_AVD) {
                            if (avd == null || avd.maps == null) {
                                MpegWarn.Once("v3c/avd/" + m + "/" + p, "MPEG_primitive_V3C on mesh " + m + " primitive " + p + ": an _MPEG_V3C_AVD entry needs maps (Table 62)");
                            }
                        }
                    }
                    MpegWarn.Once("v3c/fallback", "MPEG_primitive_V3C is not reconstructed by this player; the raw data described by the primitive attributes is used (G.1.1)");
                }
            }
        }
    }
}
