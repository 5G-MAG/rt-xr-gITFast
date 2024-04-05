﻿// Copyright 2020-2022 Andreas Atteneder
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

// All modification marked by "//// IDCC" are created by InterDigital and subject to the following header
/*
* Copyright (c) 2023 InterDigital
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using System;
using System.Collections.Generic;
using GLTFast.Schema;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using Camera = UnityEngine.Camera;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;


// #if UNITY_EDITOR && UNITY_ANIMATION
// using UnityEditor.Animations;
// #endif

namespace GLTFast {
    
    using Logging;
    using UnityEngine.XR;

    /// <summary>
    /// Generates a GameObject hierarchy from a glTF scene 
    /// </summary>
    public class GameObjectInstantiator : IInstantiator {

        /// <summary>
        /// Descriptor of a glTF scene instance
        /// </summary>
        public class SceneInstance {
            
            /// <summary>
            /// List of instantiated cameras
            /// </summary>
            public List<Camera> cameras { get; private set; }
            /// <summary>
            /// List of instantiated lights
            /// </summary>
            public List<Light> lights { get; private set; }
            
#if UNITY_ANIMATION
            /// <summary>
            /// <see cref="Animation" /> component. Is null if scene has no 
			/// animation clips.
            /// Only available if the built-in Animation module is enabled.
            /// </summary>
            public Animation legacyAnimation { get; private set; }
#endif

            // Keep track of the gameobjects with interactivity extension components
            private List<IMpegInteractivityTrigger> m_InteractivityTriggers;
            private List<IMpegInteractivityAction> m_InteractivityActions;

            // Keep track of the gameobjects with anchoring extension components
            private List<IMpegTrackable> m_Trackables;
            private List<IMpegAnchor> m_Anchors;

            //// IDCC
            public BehaviorController behaviorController
            {
                get
                {
                    if(m_BehaviorController == null)
                    {
                        // Create a behavior controller to handle all behaviors
                        m_BehaviorController = new GameObject("BehaviorController").AddComponent<BehaviorController>();
                        m_BehaviorController.Init();
                    }
                    return m_BehaviorController;
                }
            }
            private BehaviorController m_BehaviorController;

            public List<SpatialAudioSource> audioSources { get; private set; }
            public AudioListener audioListener { get; private set; }

            /// <summary>
            /// Adds a camera
            /// </summary>
            /// <param name="camera">Camera to be added</param>
            internal void AddCamera(Camera camera) {
                if (cameras == null) {
                    cameras = new List<Camera>();
                }
                cameras.Add(camera);
            }
            
            internal void AddLight(Light light) {
                if (lights == null) {
                    lights = new List<Light>();
                }
                lights.Add(light);
            }

            internal void AddAudioSource(SpatialAudioSource aSource)
            {
                if (audioSources == null)
                {
                    audioSources = new List<SpatialAudioSource>();
                }
                audioSources.Add(aSource);
            }

            internal void SetAudioListener(AudioListener aListener)
            {
                audioListener = aListener;
            }

#if UNITY_ANIMATION
            internal void SetLegacyAnimation(Animation animation) {
                legacyAnimation = animation;
            }
#endif
            internal void AddInteractivityTrigger(IMpegInteractivityTrigger go)
            {
                if(m_InteractivityTriggers == null)
                    m_InteractivityTriggers = new List<IMpegInteractivityTrigger>();
                    
                m_InteractivityTriggers.Add(go);
            }

            internal void AddInteractivityAction(IMpegInteractivityAction go)
            {
                if(m_InteractivityActions == null)
                    m_InteractivityActions = new List<IMpegInteractivityAction>();
                    
                m_InteractivityActions.Add(go);
            }

            internal void AddTrackable(IMpegTrackable go)
            {
                if(m_Trackables == null)
                    m_Trackables = new List<IMpegTrackable>();
                    
                m_Trackables.Add(go);
            }

            internal void AddAnchor(IMpegAnchor go)
            {
                if(m_Anchors == null)
                    m_Anchors = new List<IMpegAnchor>();
                    
                m_Anchors.Add(go);
            }

            internal void DestroyInstances()
            {
                if(m_Anchors != null)
                {
                    for(int i = 0; i < m_Anchors.Count; i++)
                    {
                        m_Anchors[i].Dispose();
                    }
                    m_Anchors.Clear();
                    m_Anchors = null;
                }

                if(m_Trackables != null)
                {
                    for(int i = 0; i < m_Trackables.Count; i++)
                    {
                        m_Trackables[i].Dispose();
                    }
                    m_Trackables.Clear();
                    m_Trackables = null;
                }

                if(m_InteractivityTriggers != null)
                {
                    for(int i = 0; i < m_InteractivityTriggers.Count; i++)
                    {
                        m_InteractivityTriggers[i].Dispose();
                    }
                    m_InteractivityTriggers.Clear();
                    m_InteractivityTriggers = null;
                }

                if(m_InteractivityActions != null)
                {
                    for(int i = 0; i < m_InteractivityActions.Count; i++)
                    {
                        m_InteractivityActions[i].Dispose();
                    }
                    m_InteractivityActions.Clear();
                    m_InteractivityActions = null;
                }
                behaviorController.Dispose();
            }
        }
        
        /// <summary>
        /// Instantiation settings
        /// </summary>
        protected InstantiationSettings settings;
        
        /// <summary>
        /// Instantiation logger
        /// </summary>
        protected ICodeLogger logger;
        
        /// <summary>
        /// glTF to instantiate from
        /// </summary>
        protected IGltfReadable gltf;
        
        /// <summary>
        /// Generated GameObjects will get parented to this Transform
        /// </summary>
        protected Transform parent;

        /// <summary>
        /// glTF node index to instantiated GameObject dictionary
        /// </summary>
        protected Dictionary<uint,GameObject> nodes;

        /// <summary>
        /// Transform representing the scene.
        /// Root nodes will get parented to it.
        /// </summary>
        public Transform sceneTransform { get; protected set; }
        
        /// <summary>
        /// Contains information about the latest instance of a glTF scene
        /// </summary>
        public SceneInstance sceneInstance { get; protected set; }

        /// <summary>
        /// Maintain a count of animations
        /// </summary>
        private int m_AnimationCounter;

        /// <summary>
        /// Constructs a GameObjectInstantiator
        /// </summary>
        /// <param name="gltf">glTF to instantiate from</param>
        /// <param name="parent">Generated GameObjects will get parented to this Transform</param>
        /// <param name="logger">Custom logger</param>
        /// <param name="settings">Instantiation settings</param>
        public GameObjectInstantiator(
            IGltfReadable gltf,
            Transform parent,
            ICodeLogger logger = null,
            InstantiationSettings settings = null
            )
        {
            this.gltf = gltf;
            this.parent = parent;
            this.logger = logger;
            this.settings = settings ?? new InstantiationSettings();
        }

        /// <inheritdoc />
        public virtual void BeginScene(
            string name,
            uint[] rootNodeIndices
            ) {
            Profiler.BeginSample("BeginScene");
            
            nodes = new Dictionary<uint, GameObject>();
            sceneInstance = new SceneInstance();
            
            GameObject sceneGameObject;
            if (settings.sceneObjectCreation == InstantiationSettings.SceneObjectCreation.Never
                || settings.sceneObjectCreation == InstantiationSettings.SceneObjectCreation.WhenMultipleRootNodes && rootNodeIndices.Length == 1) {
                sceneGameObject = parent.gameObject;
            }
            else {
                sceneGameObject = new GameObject(name ?? "Scene");
                sceneGameObject.transform.SetParent( parent, false);
                sceneGameObject.layer = settings.layer;
            }
            sceneTransform = sceneGameObject.transform;

            //// IDCC
            VirtualSceneGraph.AssignSceneTransform(sceneTransform);
            //// IDCC
            ///
            Profiler.EndSample();
        }

#if UNITY_ANIMATION
        /// <inheritdoc />
        public void AddAnimation(AnimationClip[] animationClips) {
            if ((settings.mask & ComponentType.Animation) != 0 && animationClips != null) {
                // we want to create an Animator for non-legacy clips, and an Animation component for legacy clips.
                var isLegacyAnimation = animationClips.Length > 0 && animationClips[0].legacy;
// #if UNITY_EDITOR
//                 // This variant creates a Mecanim Animator and AnimationController
//                 // which does not work at runtime. It's kept for potential Editor import usage
//                 if(!isLegacyAnimation) {
//                     var animator = go.AddComponent<Animator>();
//                     var controller = new UnityEditor.Animations.AnimatorController();
//                     controller.name = animator.name;
//                     controller.AddLayer("Default");
//                     controller.layers[0].defaultWeight = 1;
//                     for (var index = 0; index < animationClips.Length; index++) {
//                         var clip = animationClips[index];
//                         // controller.AddLayer(clip.name);
//                         // controller.layers[index].defaultWeight = 1;
//                         var state = controller.AddMotion(clip, 0);
//                         controller.AddParameter("Test", AnimatorControllerParameterType.Bool);
//                         // var stateMachine = controller.layers[0].stateMachine;
//                         // UnityEditor.Animations.AnimatorState entryState = null;
//                         // var state = stateMachine.AddState(clip.name);
//                         // state.motion = clip;
//                         // var loopTransition = state.AddTransition(state);
//                         // loopTransition.hasExitTime = true;
//                         // loopTransition.duration = 0;
//                         // loopTransition.exitTime = 0;
//                         // entryState = state;
//                         // stateMachine.AddEntryTransition(entryState);
//                         // UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath
//                     }
//                     
//                     animator.runtimeAnimatorController = controller;
//                     
//                     // for (var index = 0; index < animationClips.Length; index++) {
//                     //     controller.layers[index].blendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode.Additive;
//                     //     animator.SetLayerWeight(index,1);
//                     // }
//                 }
// #endif // UNITY_EDITOR

                if(isLegacyAnimation) {
                    var animation = sceneTransform.gameObject.AddComponent<Animation>();
                    
                    for (var index = 0; index < animationClips.Length; index++) {
                        var clip = animationClips[index];
                        animation.AddClip(clip,clip.name);
                        if (index < 1) {
                            animation.clip = clip;
                        }
                        //// IDCC
                        VirtualSceneGraph.AssignAnimationIndexToAnimation(m_AnimationCounter++, clip.name, animation);
                        //// IDCC
                    }

                    sceneInstance.SetLegacyAnimation(animation);
                }
                else {
                    sceneTransform.gameObject.AddComponent<Animator>();
                }
            }
        }
#endif // UNITY_ANIMATION

        /// <inheritdoc />
        public void CreateNode(
            uint nodeIndex,
            uint? parentIndex,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale
        ) {
            var go = new GameObject();
            // Deactivate root-level nodes, so half-loaded scenes won't render.
            go.SetActive(parentIndex.HasValue);
            go.transform.localScale = scale;
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.layer = settings.layer;
            nodes[nodeIndex] = go;
            
            go.transform.SetParent(
                parentIndex.HasValue ? nodes[parentIndex.Value].transform : sceneTransform,
                false);

            //// IDCC
            VirtualSceneGraph.AssignGameObjectToNode((int)nodeIndex, go, (int)nodeIndex);
            //// IDCC
        }

        /// <inheritdoc />
        public virtual void SetNodeName(uint nodeIndex, string name) {
            nodes[nodeIndex].name = name ?? $"Node-{nodeIndex}";
        }

        /// <inheritdoc />
        public virtual void AddPrimitive(
            uint nodeIndex,
            string meshName,
            Mesh mesh,
            int[] materialIndices,
            uint[] joints = null,
            uint? rootJoint = null,
            float[] morphTargetWeights = null,
            int primitiveNumeration = 0
        ) {
            if ((settings.mask & ComponentType.Mesh) == 0) {
                return;
            }

            GameObject meshGo;
            if(primitiveNumeration==0) {
                // Use Node GameObject for first Primitive
                meshGo = nodes[nodeIndex];
            } else {
                meshGo = new GameObject(meshName);
                meshGo.transform.SetParent(nodes[nodeIndex].transform,false);
                meshGo.layer = settings.layer;
            }

            Renderer renderer;

            var hasMorphTargets = mesh.blendShapeCount > 0;
            if(joints==null && !hasMorphTargets) {
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.mesh = mesh;
                var mr = meshGo.AddComponent<MeshRenderer>();
                renderer = mr;
            } else {
                var smr = meshGo.AddComponent<SkinnedMeshRenderer>();
                smr.updateWhenOffscreen = settings.skinUpdateWhenOffscreen;
                if (joints != null) {
                    var bones = new Transform[joints.Length];
                    for (var j = 0; j < bones.Length; j++)
                    {
                        var jointIndex = joints[j];
                        bones[j] = nodes[jointIndex].transform;
                    }
                    smr.bones = bones;
                    if (rootJoint.HasValue) {
                        smr.rootBone = nodes[rootJoint.Value].transform;
                    }
                }
                smr.sharedMesh = mesh;
                if (morphTargetWeights!=null) {
                    for (var i = 0; i < morphTargetWeights.Length; i++) {
                        var weight = morphTargetWeights[i];
                        smr.SetBlendShapeWeight(i, weight);
                    }
                }
                renderer = smr;
            }

            var materials = new Material[materialIndices.Length];
            for (var index = 0; index < materials.Length; index++) {
                var material = gltf.GetMaterial(materialIndices[index]) ?? gltf.GetDefaultMaterial();
                materials[index] = material;
            }
            renderer.sharedMaterials = materials;
        }

        /// <inheritdoc />
        public virtual void AddPrimitiveInstanced(
            uint nodeIndex,
            string meshName,
            Mesh mesh,
            int[] materialIndices,
            uint instanceCount,
            NativeArray<Vector3>? positions,
            NativeArray<Quaternion>? rotations,
            NativeArray<Vector3>? scales,
            int primitiveNumeration = 0
        ) {
            if ((settings.mask & ComponentType.Mesh) == 0) {
                return;
            }
            
            var materials = new Material[materialIndices.Length];
            for (var index = 0; index < materials.Length; index++) {
                var material = gltf.GetMaterial(materialIndices[index]) ?? gltf.GetDefaultMaterial();
                material.enableInstancing = true;
                materials[index] = material;
            }

            for (var i = 0; i < instanceCount; i++) {
                var meshGo = new GameObject( $"{meshName}_i{i}" );
                meshGo.layer = settings.layer;
                var t = meshGo.transform;
                t.SetParent(nodes[nodeIndex].transform,false);
                t.localPosition = positions?[i] ?? Vector3.zero;
                t.localRotation = rotations?[i] ?? Quaternion.identity;
                t.localScale = scales?[i] ?? Vector3.one;
                
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.mesh = mesh;
                Renderer renderer = meshGo.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
            }
        }

        /// <inheritdoc />
        public virtual void AddCamera(uint nodeIndex, uint cameraIndex) {
            if ((settings.mask & ComponentType.Camera) == 0) {
                return;
            }
            var camera = gltf.GetSourceCamera(cameraIndex);
            switch (camera.typeEnum) {
            case Schema.Camera.Type.Orthographic:
                var o = camera.orthographic;
                AddCameraOrthographic(
                    nodeIndex,
                    o.znear,
                    o.zfar >=0 ? o.zfar : (float?) null,
                    o.xmag,
                    o.ymag,
                    camera.name,
                    cameraIndex
                );
                break;
            case Schema.Camera.Type.Perspective:
                var p = camera.perspective;
                AddCameraPerspective(
                    nodeIndex,
                    p.yfov,
                    p.znear,
                    p.zfar,
                    p.aspectRatio>0 ? p.aspectRatio : (float?)null,
                    camera.name,
                    cameraIndex
                );
                break;
            }
        }

        void AddCameraPerspective(
            uint nodeIndex,
            float verticalFieldOfView,
            float nearClipPlane,
            float farClipPlane,
            float? aspectRatio,
            string cameraName,
            uint cameraIndex
        ) {
            var cam = CreateCamera(nodeIndex,cameraName,out var localScale);

            cam.orthographic = false;

            // TODO: Move this code elsewhere
            // Look if a XR device is in use
            bool _isXrInUse = false;
            List<XRDisplaySubsystem> xrDisplaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances(xrDisplaySubsystems);
            foreach (var xrDisplay in xrDisplaySubsystems)
            {
                if (xrDisplay.running) {
                    _isXrInUse = true;
                    break;
                }
            }

            // Field of view can't be changed while xr is in use
            if(!_isXrInUse) {
                cam.fieldOfView = verticalFieldOfView * Mathf.Rad2Deg;
            }

            cam.nearClipPlane = nearClipPlane * localScale;
            cam.farClipPlane = farClipPlane * localScale;

            //// IDCC
            VirtualSceneGraph.AssignCameraIndexToCamera((int)cameraIndex, cam);
            //// IDCC
            
            // // If the aspect ratio is given and does not match the
            // // screen's aspect ratio, the viewport rect is reduced
            // // to match the glTFs aspect ratio (box fit)
            // if (aspectRatio.HasValue) {
            //     cam.rect = GetLimitedViewPort(aspectRatio.Value);
            // }
        }

        void AddCameraOrthographic(
            uint nodeIndex,
            float nearClipPlane,
            float? farClipPlane,
            float horizontal,
            float vertical,
            string cameraName,
            uint cameraIndex
        ) {
            var cam = CreateCamera(nodeIndex,cameraName,out var localScale);
            
            var farValue = farClipPlane ?? float.MaxValue;

            cam.orthographic = true;
            cam.nearClipPlane = nearClipPlane * localScale;
            cam.farClipPlane = farValue * localScale;
            cam.orthographicSize = vertical; // Note: Ignores `horizontal`

            // Custom projection matrix
            // Ignores screen's aspect ratio
            cam.projectionMatrix = Matrix4x4.Ortho(
                -horizontal,
                horizontal, 
                -vertical,
                vertical,
                nearClipPlane,
                farValue
            );

            // // If the aspect ratio does not match the
            // // screen's aspect ratio, the viewport rect is reduced
            // // to match the glTFs aspect ratio (box fit)
            // var aspectRatio = horizontal / vertical;
            // cam.rect = GetLimitedViewPort(aspectRatio);
        }

        /// <summary>
        /// Creates a camera component on the given node and returns an approximated
        /// local-to-world scale factor, required to counteract that Unity scales
        /// near- and far-clipping-planes via Transform.
        /// </summary>
        /// <param name="nodeIndex">Node's index</param>
        /// <param name="cameraName">Camera's name</param>
        /// <param name="localScale">Approximated local-to-world scale factor</param>
        /// <returns>The newly created Camera component</returns>
        Camera CreateCamera(uint nodeIndex, string cameraName, out float localScale) {
            var cameraParent = nodes[nodeIndex];
            var camGo = new GameObject(cameraName ?? $"{cameraParent.name}-Camera" ?? $"Camera-{nodeIndex}");
            camGo.layer = settings.layer;
            var camTrans = camGo.transform;
            var parentTransform = cameraParent.transform;
            camTrans.SetParent(parentTransform,false);
            var tmp = Quaternion.Euler(0, 180, 0);
            camTrans.localRotation= tmp;
            var cam = camGo.AddComponent<Camera>();

            VirtualSceneGraph.AssignCameraIndexToCamera((int)nodeIndex, cam);

            // By default, imported cameras are not enabled by default
            // cam.enabled = false;

            sceneInstance.AddCamera(cam);

            var parentScale = parentTransform.localToWorldMatrix.lossyScale;
            localScale = (parentScale.x + parentScale.y + parentScale.y) / 3; 
            
            return cam;
        }

        // static Rect GetLimitedViewPort(float aspectRatio) {
        //     var screenAspect = Screen.width / (float)Screen.height;
        //     if (Mathf.Abs(1 - (screenAspect / aspectRatio)) <= math.EPSILON) {
        //         // Identical aspect ratios
        //         return new Rect(0,0,1,1);
        //     }
        //     if (aspectRatio < screenAspect) {
        //         var w = aspectRatio / screenAspect;
        //         return new Rect((1 - w) / 2, 0, w, 1f);
        //     } else {
        //         var h = screenAspect / aspectRatio;
        //         return new Rect(0, (1 - h) / 2, 1f, h);
        //     }
        // }

        /// <inheritdoc />
        public void AddLightPunctual(
            uint nodeIndex,
            uint lightIndex
        ) {
            if ((settings.mask & ComponentType.Light) == 0) {
                return;
            }
            var lightGameObject = nodes[nodeIndex];
            var lightSource = gltf.GetSourceLightPunctual(lightIndex);

            if (lightSource.typeEnum != LightPunctual.Type.Point) {
                // glTF lights' direction is flipped, compared with Unity's, so
                // we're adding a rotated child GameObject to counteract.
                var tmp = new GameObject($"{lightGameObject.name}_Orientation");
                tmp.transform.SetParent(lightGameObject.transform,false);
                tmp.transform.localEulerAngles = new Vector3(0, 180, 0);
                lightGameObject = tmp;
            }
            var light = lightGameObject.AddComponent<Light>();
            lightSource.ToUnityLight(light, settings.lightIntensityFactor);
            sceneInstance.AddLight(light);
        }
        
        public void AddAudioSources(uint nodeIndex)
        {
            var go = nodes[nodeIndex];
            var aNode = gltf.GetSourceNode((int)nodeIndex);
            
            var root = gltf.GetSourceRoot();
            
            foreach (var srcDef in aNode.extensions.MPEG_audio_spatial.sources)
            {
                var aSrc = go.AddComponent<SpatialAudioSource>() as SpatialAudioSource;
                int bufferId = root.bufferViews[root.accessors[srcDef.accessors[0]].bufferView].buffer;
                aSrc.Configure(srcDef, bufferId);
                sceneInstance.AddAudioSource(aSrc);
            } // end foreach srcDef
        }

        public void AddAudioListener(uint nodeIndex)
        {
            var audioSourceGameObject = nodes[nodeIndex];
            var aLstn = audioSourceGameObject.AddComponent(typeof(AudioListener)) as AudioListener;
            // doesn't check if multiple listeners are configured/enabled
            aLstn.enabled = true;
            aLstn.velocityUpdateMode = AudioVelocityUpdateMode.Auto;
            
            sceneInstance.SetAudioListener(aLstn);
        }

        //// IDCC
        public void AddMPEGInteractivityBehavior(Schema.Behavior bhv, int index)
        {
            GameObject go = new GameObject($"Behavior - {index}");

            // Not useful to have an interface here, but following
            // the same pattern than actions and triggers
            IMpegInteractivityBehavior bhvIf = go.AddComponent<GLTFast.Behavior>();
            bhvIf.InitializeBehavior(bhv);

            sceneInstance.behaviorController.AddBehavior(bhvIf);
            VirtualSceneGraph.AssignBehaviorIndexToBehavior(bhvIf, index);
        }

        public void AddMPEGInteractivityTrigger(GLTFast.Schema.Trigger trigger, int index)
        {
            GameObject go = new GameObject($"{trigger.type} - {index}");
            IMpegInteractivityTrigger triggerIf = null;

            switch (trigger.type)
            {
                case TriggerType.TRIGGER_COLLISION: triggerIf = go.AddComponent<CollisionSceneTrigger>(); break;
                case TriggerType.TRIGGER_PROXIMITY: triggerIf = go.AddComponent<ProximitySceneTrigger>(); break;
                case TriggerType.TRIGGER_USER_INPUT: triggerIf = go.AddComponent<UserInputSceneTrigger>(); break;
                case TriggerType.TRIGGER_VISIBILITY: triggerIf = go.AddComponent<VisibilitySceneTrigger>(); break;
            }

            if (triggerIf == null)
            {
                throw new NotImplementedException($"Couldn't create trigger, type not recognized: {trigger.type}");
            }

            triggerIf.Init(trigger);

            VirtualSceneGraph.AssignTriggerToIndex(triggerIf, index);

            sceneInstance.AddInteractivityTrigger(triggerIf);
        }

        public void AddMPEGInteractivityAction(GLTFast.Schema.Action action, int index)
        {
            GameObject go = new GameObject($"{action.type} - {index}");
            IMpegInteractivityAction actionIf = null;

            switch (action.type)
            {
                case ActionType.ACTION_ACTIVATE: actionIf = go.AddComponent<ActionActivate>(); break;
                case ActionType.ACTION_TRANSFORM: actionIf = go.AddComponent<ActionTransform>(); break;
                case ActionType.ACTION_BLOCK: actionIf = go.AddComponent<ActionBlock>(); break;
                case ActionType.ACTION_ANIMATION: actionIf = go.AddComponent<ActionAnimation>(); break;
                case ActionType.ACTION_MEDIA: actionIf = go.AddComponent<ActionMedia>(); break;
                case ActionType.ACTION_MANIPULATE: actionIf = go.AddComponent<ActionManipulate>(); break;
                case ActionType.ACTION_SET_MATERIAL: actionIf = go.AddComponent<ActionSetMaterial>(); break;
                case ActionType.ACTION_SET_HAPTIC: actionIf = go.AddComponent<ActionSetHaptic>(); break;
                case ActionType.ACTION_SET_AVATAR: actionIf = go.AddComponent<ActionSetAvatar>(); break;
            }

            if (actionIf == null)
            {
                throw new NotImplementedException($"Couldn't create action, type not recognized: {action.type} : {index}");
            }

            actionIf.Init(action);

            VirtualSceneGraph.AssignActionToIndex(actionIf, index);
            sceneInstance.AddInteractivityAction(actionIf);
        }

        public void AddMPEGTrackables(GLTFast.Schema.Trackable trackable, int index) {
#if UNITY_ANDROID
            GameObject go = new GameObject($"{trackable.type} - {index}");
            IMpegTrackable trackIf = null;
            Debug.Log("Tracking Mode:"+ trackable.type);

            switch(trackable.type)
            {
                case TrackableType.TRACKABLE_FLOOR:     trackIf = go.AddComponent<TrackableFloor>(); break;
                case TrackableType.TRACKABLE_VIEWER:     trackIf = go.AddComponent<TrackableViewer>(); break;
                case TrackableType.TRACKABLE_CONTROLLER:    trackIf = go.AddComponent<TrackableController>(); break;
                case TrackableType.TRACKABLE_PLANE:    trackIf = go.AddComponent<TrackableGeometric>(); break;
                case TrackableType.TRACKABLE_MARKER_2D:    trackIf = go.AddComponent<TrackableMarker2D>(); break;
                case TrackableType.TRACKABLE_MARKER_3D:    trackIf = go.AddComponent<TrackableMarker3D>(); break;
                case TrackableType.TRACKABLE_MARKER_GEO:    trackIf = go.AddComponent<TrackableMarkerGeo>(); break;
                case TrackableType.TRACKABLE_APPLICATION:    trackIf = go.AddComponent<TrackableApplication>(); break;
            }

            if (trackIf == null)
            {
                throw new NotImplementedException($"Couldn't create trackable, type not recognized: {trackable.type}");
            }
            Debug.Log("Build Trackable");

            trackIf.InitFromGltf(trackable);
            VirtualSceneGraph.AssignTrackableToIndex(trackIf, index);
            sceneInstance.AddTrackable(trackIf);
#endif
        }

        public void AddMPEGAnchor(GLTFast.Schema.Anchor anchor, int index) {
#if UNITY_ANDROID
            string str = "anchor";
            GameObject go = new GameObject($"{str} - {index}");
            IMpegAnchor anchIf = null;
            anchIf = go.AddComponent<AnchorInstance>();

            if(anchIf == null)
            {
                throw new NotImplementedException($"Couldn't create anchor");
            }
            Debug.Log("Build Anchor");

            anchIf.Init(anchor);
            VirtualSceneGraph.AssignAnchorToIndex(anchIf, index);
            sceneInstance.AddAnchor(anchIf);
#endif
        }

        /// <inheritdoc />
        public virtual void EndScene(uint[] rootNodeIndices) {
            Profiler.BeginSample("EndScene");
            if (rootNodeIndices != null) {
                foreach (var nodeIndex in rootNodeIndices) {
                    nodes[nodeIndex].SetActive(true);
                }
            }
            Profiler.EndSample();
        }

        public void Dispose()
        {
            sceneInstance.DestroyInstances();
        }
    }
}