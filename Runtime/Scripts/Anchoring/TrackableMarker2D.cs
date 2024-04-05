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

using GLTFast.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Reflection;

namespace GLTFast
{    
    /// <summary>
    ///The width and length of the marker 2D span the xz-plane of the anchor instance's local coordinate system. 
    ///The origin of the local coordinate system is located at the center of the detected marker 2D surface.  
    /// </summary>
    public class TrackableMarker2D : MonoBehaviour, IMpegTrackable
    {
        private int m_MarkerNode; 
        private ARTrackedImageManager m_TrackedImageManager;
        private XRReferenceImageLibrary m_XrReferenceImageLibrary;
        private ARTrackedImage imgTrack = null; //Only one marker
        private TrackableId m_Id = TrackableId.invalidId;

        private GameObject m_Anchor = null;
        private Texture2D m_DefaultTexture;
        private List<GameObject> m_GoToAttach;
        private bool m_IsAttached = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredAnchoring=false;
        // private bool m_RequiredAlignedNotScale =false;
        // private bool m_RequiredAlignedAndScale =false;
        // private bool m_RequiredSpace = false;

        /// <summary>
        /// If an image is detected but no source texture can be found,
        /// this texture is used instead.
        /// </summary>
        public Texture2D defaultTexture
        {
            get { return m_DefaultTexture; }
            set { m_DefaultTexture = value; }
        }
        
        public void InitFromGltf(Trackable  track)
        {
            m_MarkerNode = track.markerNode;
            if (Application.isEditor)
            {
                DumpAttributs();
            }
        }
    
        public void Init()
        {
            int matIndex = -1;
            int texIndex = -1;
            int sourceIndex = -1;

            Debug.Log("TrackableMarker2D::Init");

            GameObject obj = ARUtilities.GetSessionOrigin();

            if(obj == null)
            {
                throw new Exception("Can't Find Session Origin");
            }

            m_TrackedImageManager = FindObjectOfType<ARTrackedImageManager>(true);

            if(m_TrackedImageManager == null)
            {
                m_TrackedImageManager = obj.AddComponent<ARTrackedImageManager>();
            }

            m_TrackedImageManager.referenceLibrary = m_TrackedImageManager.CreateRuntimeLibrary(m_XrReferenceImageLibrary);
            m_TrackedImageManager.requestedMaxNumberOfMovingImages = 1;
            m_TrackedImageManager.enabled = true;
            m_TrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
            
            //retrieve the node to get image    
            Node node =  VirtualSceneGraph.root.nodes[m_MarkerNode];
            //Retrieve mesh
            Schema.Mesh meshSCH =  VirtualSceneGraph.root.meshes[node.mesh];
            //Get material
            Schema.MeshPrimitive [] primitives  = new MeshPrimitive[meshSCH.primitives.Length];
            for(int i = 0; i <meshSCH.primitives.Length;i++)
            {
                primitives[i]= (MeshPrimitive) meshSCH.primitives[i].Clone();
                matIndex = primitives[i].material;
                Schema.Material matSCH =  VirtualSceneGraph.root.materials[matIndex];
                if(matSCH.pbrMetallicRoughness.baseColorTexture!= null)
                {
                    texIndex = matSCH.pbrMetallicRoughness.baseColorTexture.index;
                    Schema.Texture texSch =  VirtualSceneGraph.root.textures[texIndex];
                    sourceIndex = texSch.source;
                    UnityEngine.Texture2D tex =  VirtualSceneGraph.GetTextureFromIndex(sourceIndex);
                    Size size = new Size(tex.width, tex.height);
                    Debug.Log($"Add marker image for texture {tex} of size {size}");
                    AddMarkerImage(tex, size);
                }
            }     
            m_GoToAttach = new List<GameObject>();
        }

        public void RequiredSpace(UnityEngine.Vector3 requiredSpace)
        {
            // m_RequiredSpace = true;
            m_RequiredSpaceToCheck = requiredSpace;
        }

        public void RequiredAnchoring(bool requiredAnchoring)
        {
            m_RequiredAnchoring = requiredAnchoring;         
        }

        public void RequiredAlignedAndScale(Anchor.Aligned aligned)
        {
            // if(aligned == Anchor.Aligned.ALIGNED_NOTSCALED)
            // {
            //     m_RequiredAlignedNotScale = true;
            // }
            // if(aligned == Anchor.Aligned.ALIGNED_SCALED)
            // {
            //     m_RequiredAlignedAndScale = true;
            // }
        }
        
        public void AddMarkerImage(Texture2D _texture, Size _size)
        {
            Debug.Log($"TrackableMarker2D::AddMarkerImage...");
            StartCoroutine(AddImageJob(_texture, _size));
        }

        private IEnumerator AddImageJob(Texture2D _texture, Size _size) {
            // Make sure we can call it 
            yield return new WaitForSeconds(0.25f);
            Debug.Log("TrackableMarker2D::AddImageJob");

            var runtimeReferenceImageLibrary = m_TrackedImageManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
            
            // Check if supported by the descriptor
            bool _isMutableRuntimeimageLibrarySupported = m_TrackedImageManager.descriptor.supportsMutableLibrary;
            
            if(!_isMutableRuntimeimageLibrarySupported)
            {
                throw new Exception($"TrackableMarker2D::Descriptor doesn't support Mutable Library.");
            }

            //Start the process of adding the image in the mutable runtime reference image library
            var _job = runtimeReferenceImageLibrary.ScheduleAddImageWithValidationJob(
                _texture,                                           // Texture
                $"{_texture.name}||{Guid.NewGuid().ToString()}",    // Unique name
                0.1f                                                // 10cm
            );

            Debug.Log("TrackableMarker2D::Waiting until the job is complete");
            // Async

            while(!_job.status.IsComplete())
            {
                yield return null;
            }

            Debug.Log("TrackableMarker2D::AddImageJob done");
            
            yield return null;
        }

        /// <summary>
        /// Make the registered objects follow the tracked images
        /// </summary>
        void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
        {
            // Handle added event
            if (eventArgs.added != null)
            {
                if(eventArgs.added.Count>0)
                { 
                    if(imgTrack == null)
                    {
                        imgTrack = eventArgs.added[0];
                        m_Id =imgTrack.trackableId; 
                    }
                }   
            }
                
            // Handle updated event
            if (eventArgs.updated != null)
            {         
                if(eventArgs.updated.Count>0)
                {
                    if(imgTrack != null)
                    {
                        imgTrack = eventArgs.updated[0];
                        m_Id =imgTrack.trackableId;
                        if(imgTrack.trackingState ==TrackingState.Tracking)
                        {
                            if(m_Anchor == null)
                            {
                                BuildAnchorInternal();
                            }
                        }
                        else
                        {
                            m_Anchor.transform.SetPositionAndRotation(imgTrack.transform.position,imgTrack.transform.rotation);
                        }
                    }
                }   
            }
            // Handle removed event
             if (eventArgs.removed != null)
            {
                if(eventArgs.removed.Count>0)
                {
                    if(imgTrack != null)
                    {
                        ClearInfo(imgTrack);
                        imgTrack = null; 
                        m_Id = TrackableId.invalidId;
                    }
                }
            }
        }

        void ClearInfo(ARTrackedImage trackedImage)
        {
            var planeParentGo = trackedImage.transform.GetChild(0).gameObject;
            var planeGo = planeParentGo.transform.GetChild(0).gameObject;
            // Disable the visual plane if it is not being tracked  
            planeGo.SetActive(false);
        }

        public bool  Detect()
        {
            return imgTrack != null;
        }

        public Transform Track()
        {
            if(m_Anchor == null)
            {
                return null;
            }
            if(imgTrack.trackingState== TrackingState.Tracking)
            {
                return m_Anchor.transform;
            }
            else
            {
                return null;
            }
        }
        
        private void BuildAnchorInternal()
        {
            m_Anchor = new GameObject("Anchor");
            m_Anchor.transform.SetPositionAndRotation(imgTrack.transform.position, imgTrack.transform.rotation);
            
            if(!m_IsAttached)
            {
                foreach (GameObject go  in m_GoToAttach)
                {
                    go.transform.SetParent(m_Anchor.transform,false);
                    go.SetActive(true);
                }
                m_IsAttached = true;
            }
        }

        public void RemoveAnchor()
        {
            if(m_Anchor != null)
            {
                Destroy(m_Anchor);
            }
            
            m_Anchor = null;
            m_Id = TrackableId.invalidId;
        }
        
        public void AttachNodeToTrackable(GameObject go)
        {
            m_GoToAttach.Add(go);
            if(m_Anchor != null && !m_IsAttached)
            {
                go.transform.SetParent(m_Anchor.gameObject.transform,false);
                go.SetActive(true);
            }
        }

        void Update()
        {
            if(m_Anchor == null)
            {
                if(m_RequiredAnchoring)
                {
                    foreach(GameObject go in m_GoToAttach)
                    {
                        go.SetActive(false);
                    }
                }
            }  
        }
        
        public void readImage(int index)
        {
            int matIndex1 = -1;
            int texIndex1 = -1;
            int sourceIndex1 = -1;

            Node node =  VirtualSceneGraph.root.nodes[m_MarkerNode];

            //Retrieve mesh
            Schema.Mesh meshSCH =  VirtualSceneGraph.root.meshes[node.mesh];
            //Get material
            Schema.MeshPrimitive [] primitives  = new MeshPrimitive[meshSCH.primitives.Length];
            for(int i = 0; i <meshSCH.primitives.Length;i++)
            {
                Debug.Log("Read Primitives :"+ i);
                primitives[i]= (MeshPrimitive) meshSCH.primitives[i].Clone();
                Debug.Log("Clone Primitives :"+ i);
                matIndex1 = primitives[i].material;
                Debug.Log("Read Material :"+ matIndex1);
                Schema.Material matSCH =  VirtualSceneGraph.root.materials[matIndex1];
                if(matSCH.pbrMetallicRoughness.baseColorTexture!= null)
                {
                    texIndex1 = matSCH.pbrMetallicRoughness.baseColorTexture.index;
                    Schema.Texture texSch =  VirtualSceneGraph.root.textures[texIndex1];
                    sourceIndex1 = texSch.source;
                    UnityEngine.Texture2D tex =  VirtualSceneGraph.GetTextureFromIndex(sourceIndex1); 
                    byte[] bytes=tex.EncodeToPNG();
                    var dirPath = Application.persistentDataPath + "/SaveImages/";
                    Debug.Log("dirPath: "+ dirPath);
                    if(!System.IO.Directory.Exists(dirPath)) {
                        System.IO.Directory.CreateDirectory(dirPath);
                    }
                    System.IO.File.WriteAllBytes(dirPath + "Image" + ".png", bytes);    
                }
            }
        }

        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            attributs.Add("markerNode","markerNode");
            var res = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log(this.GetType().Name+": Editor Mode Dump all attributes of instance");
            foreach(var item in res)
            {
                if(attributs.ContainsKey(item.Name))
                    Debug.Log(item.Name+ " : " + item.GetValue(this));
            }  
        }

        
        /// <summary>
        /// Stops this module from working, empty all references
        /// </summary>
        public void Dispose()
        {
            Debug.Log("TrackableMarker2D::Dispose");

            if(m_TrackedImageManager != null)
            {
                m_TrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
                Destroy(m_TrackedImageManager);
            }

            if(imgTrack != null)
            {
                Destroy(imgTrack);
            }

            StopAllCoroutines();

            Destroy(gameObject);
        }
    }   
}