// SPDX-FileCopyrightText: 2023 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Descriptor of a glTF scene instance
    /// </summary>
    public class GameObjectSceneInstance
    {

        /// <summary>
        /// List of instantiated cameras
        /// </summary>
        public IReadOnlyList<Camera> Cameras => m_Cameras;
        /// <summary>
        /// List of instantiated lights
        /// </summary>
        public IReadOnlyList<Light> Lights => m_Lights;

        /// <summary>
        /// Enables controlling and applying materials variants.
        /// </summary>
        public MaterialsVariantsControl MaterialsVariantsControl { get; private set; }

#if UNITY_ANIMATION
        /// <summary>
        /// <see cref="Animation" /> component. Is null if scene has no
        /// animation clips.
        /// Only available if the built-in Animation module is enabled.
        /// </summary>
        public Animation LegacyAnimation { get; private set; }
#endif

        List<Camera> m_Cameras;
        List<Light> m_Lights;


        public List<SpatialAudioSource> audioSources { get; private set; }
        public AudioListener audioListener { get; private set; }

        //// IDCC
        private List<IMpegInteractivityTrigger> m_InteractivityTriggers;
        private List<IMpegInteractivityAction> m_InteractivityActions;
        private BehaviorController m_BehaviorController;
        private List<IMpegTrackable> m_Trackables;
        private List<IMpegAnchor> m_Anchors;
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

        /// <summary>
        /// Adds a camera
        /// </summary>
        /// <param name="camera">Camera to be added</param>
        internal void AddCamera(Camera camera)
        {
            if (m_Cameras == null)
            {
                m_Cameras = new List<Camera>();
            }
            m_Cameras.Add(camera);
        }

        internal void AddLight(Light light)
        {
            if (m_Lights == null)
            {
                m_Lights = new List<Light>();
            }
            m_Lights.Add(light);
        }

        internal void SetMaterialsVariantsControl(MaterialsVariantsControl control)
        {
            MaterialsVariantsControl = control;
        }

#if UNITY_ANIMATION
        internal void SetLegacyAnimation(Animation animation) {
            LegacyAnimation = animation;
        }
#endif


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

            internal void DestroyInstance()
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
}
