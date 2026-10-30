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
#if UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif
using System.Reflection;

namespace GLTFast
{
    /// <summary>
    ///The width and length of the marker 2D span the xz-plane of the anchor instance's local coordinate system. 
    ///The origin of the local coordinate system is located at the center of the detected marker 2D surface.  
    /// </summary>
    public class TrackableMarker2D : MonoBehaviour, IMpegTrackable
    {
        public const int MAX_NUM_IMAGES = 1;
#if UNITY_ANDROID
        private int m_MarkerNode;
        private ARTrackedImageManager m_TrackedImageManager;
        private XRReferenceImageLibrary m_XrReferenceImageLibrary;
        private ARTrackedImage imgTrack = null; //Only one marker
        
        private TrackableId m_Id = TrackableId.invalidId;

        private GameObject m_Anchor = null;
        private Texture2D m_DefaultTexture;
        private List<GameObject> m_GoToAttach = new List<GameObject>();
        private bool m_IsAttached = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredAnchoring = false;
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

        public void InitFromGltf(Trackable track)
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

            if (obj == null)
            {
                throw new Exception("Can't Find Session Origin");
            }

            m_TrackedImageManager = FindFirstObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include); 

            if (m_TrackedImageManager == null)
            {
                Debug.Log("TrackableMarker2D::ARTrackedImageManager not found - adding component");
                m_TrackedImageManager = obj.AddComponent<ARTrackedImageManager>();
            }

            // several marker 2D trackables share one library: the library is created once, each marker adds its image to it
            if (m_TrackedImageManager.referenceLibrary == null) {
                m_TrackedImageManager.referenceLibrary = m_TrackedImageManager.CreateRuntimeLibrary(m_XrReferenceImageLibrary);
            }
            m_TrackedImageManager.requestedMaxNumberOfMovingImages = MAX_NUM_IMAGES;
            m_TrackedImageManager.enabled = true;
            // m_TrackedImageManager.trackedImagePrefab = GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Plane);
            m_TrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;

            //retrieve the node to get image    
            Node node = VirtualSceneGraph.root.nodes[m_MarkerNode];
            //Retrieve mesh
            Schema.Mesh meshSCH = VirtualSceneGraph.root.meshes[node.mesh];
            //Get material
            Schema.MeshPrimitive[] primitives = new MeshPrimitive[meshSCH.primitives.Length];
            for (int i = 0; i < meshSCH.primitives.Length; i++)
            {
                primitives[i] = (MeshPrimitive)meshSCH.primitives[i].Clone();
                matIndex = primitives[i].material;
                Schema.Material matSCH = VirtualSceneGraph.root.materials[matIndex];
                if (matSCH.pbrMetallicRoughness.baseColorTexture != null)
                {
                    texIndex = matSCH.pbrMetallicRoughness.baseColorTexture.index;
                    Schema.Texture texSch = VirtualSceneGraph.root.textures[texIndex];
                    sourceIndex = texSch.source;
                    UnityEngine.Texture2D tex = VirtualSceneGraph.GetTextureFromIndex(sourceIndex);
                    Size size = new Size(tex.width, tex.height);
                    Debug.Log($"Add marker image for texture {tex} of size {size}");
                    AddMarkerImage(tex, size);
                }
            }
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
            m_ImageWidth = MarkerWidthFromNode();
            // a width set by the user for the marker on their screen or print overrides the scene's geometry (0 = use the scene)
            float cm = PlayerPrefs.GetFloat("set.markerwidthcm", 0f);
            if (cm > 0f) m_ImageWidth = cm / 100f;
            Debug.Log($"TrackableMarker2D::AddMarkerImage... physical width {(m_ImageWidth.HasValue ? m_ImageWidth.Value.ToString("0.###") + " m" : "unknown")}");
            StartCoroutine(AddImageJob(_texture, _size));
        }

        // Width of the marker in metres, from the marker node's geometry (position bounds times node scale).
        // ARCore needs it to estimate the pose of the image; without it the image is found but its pose stays unknown.
        private float? m_ImageWidth;
        // the reference image of this marker: tracked images of other markers are not ours
        private string m_ImageName;

        private float? MarkerWidthFromNode()
        {
            try
            {
                Node node = VirtualSceneGraph.root.nodes[m_MarkerNode];
                if (node.mesh < 0) return null;
                var prim = VirtualSceneGraph.root.meshes[node.mesh].primitives[0];
                var acc = VirtualSceneGraph.root.accessors[prim.attributes.POSITION];
                if (acc.min == null || acc.max == null || acc.min.Length < 3) return null;
                float sx = node.scale != null && node.scale.Length >= 3 ? Mathf.Abs(node.scale[0]) : 1f;
                float w = (acc.max[0] - acc.min[0]) * sx;
                return w > 0.001f ? w : (float?)null;
            }
            catch (Exception) { return null; }
        }

        private IEnumerator AddImageJob(Texture2D _texture, Size _size)
        {
            // Make sure we can call it 
            yield return new WaitForSeconds(0.25f);
            Debug.Log("TrackableMarker2D::AddImageJob");

            var runtimeReferenceImageLibrary = m_TrackedImageManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;

            // Check if supported by the descriptor
            bool _isMutableRuntimeimageLibrarySupported = m_TrackedImageManager.descriptor.supportsMutableLibrary;

            if (!_isMutableRuntimeimageLibrarySupported)
            {
                throw new Exception($"TrackableMarker2D::Descriptor doesn't support Mutable Library.");
            }

            //Start the process of adding the image in the mutable runtime reference image library
            m_ImageName = $"{_texture.name}||{Guid.NewGuid().ToString()}";
            var _job = runtimeReferenceImageLibrary.ScheduleAddImageWithValidationJob(
                _texture,                                           // Texture
                m_ImageName,                                        // Unique name
                m_ImageWidth                                        // real world width of the image in metres
            );

            Debug.Log("TrackableMarker2D::Waiting until the job is complete");

            while (!_job.status.IsComplete())
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
            Debug.Log($"TrackableMarker2D::images added={eventArgs.added.Count} updated={eventArgs.updated.Count} removed={eventArgs.removed.Count}" +
                      (eventArgs.updated.Count > 0 ? $" state={eventArgs.updated[0].trackingState}" : ""));

            if (eventArgs.added.Count > 0)
            {
                if (imgTrack == null)
                {
                    foreach (ARTrackedImage added in eventArgs.added)
                    {
                        if (m_ImageName != null && added.referenceImage.name != m_ImageName) continue;
                        imgTrack = added;
                        m_Id = imgTrack.trackableId;
                        break;
                    }
                }
            }

            if (eventArgs.updated.Count > 0)
            {
                if (m_Anchor == null && imgTrack != null && imgTrack.trackingState == TrackingState.Tracking)
                {
                    BuildAnchorInternal();
                }

                foreach (ARTrackedImage img in eventArgs.updated){
                    if (imgTrack == img && m_Anchor != null && img.trackingState == TrackingState.Tracking){
                        if (!m_Anchor.activeSelf) m_Anchor.SetActive(true);
                        // the image pose is noisy from frame to frame: follow it through a low-pass filter
                        Quaternion targetRot = img.transform.rotation * Quaternion.AngleAxis(180f, Vector3.up);
                        if (!m_PoseInit)
                        {
                            m_Anchor.transform.SetPositionAndRotation(img.transform.position, targetRot);
                            m_PoseInit = true;
                        }
                        else
                        {
                            m_Anchor.transform.position = Vector3.Lerp(m_Anchor.transform.position, img.transform.position, 0.15f);
                            m_Anchor.transform.rotation = Quaternion.Slerp(m_Anchor.transform.rotation, targetRot, 0.15f);
                        }
                    }
                }

            }

            if (eventArgs.removed.Count > 0)
            {
                if (imgTrack != null)
                {
                    foreach (ARTrackedImage img in eventArgs.removed){
                        if (img == imgTrack){
                            ClearInfo(imgTrack);
                            imgTrack = null;
                            m_Id = TrackableId.invalidId;
                            if (m_Anchor != null) m_Anchor.SetActive(false);
                        }
                    }
                }
            }
        }

        void ClearInfo(ARTrackedImage trackedImage)
        {
            // Disable the visual plane if it is not being tracked
            if (trackedImage == null || trackedImage.transform.childCount == 0) return;
            var planeParent = trackedImage.transform.GetChild(0);
            if (planeParent.childCount == 0) return;
            planeParent.GetChild(0).gameObject.SetActive(false);
        }

        public bool Detect()
        {
            return imgTrack != null;
        }

        public Transform Track()
        {
            if (m_Anchor == null || imgTrack == null)
            {
                return null;
            }
            if (imgTrack.trackingState == TrackingState.Tracking)
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

            foreach (GameObject go in m_GoToAttach)
            {
                go.transform.SetParent(m_Anchor.transform, false);
                go.SetActive(true);
            }
            //m_IsAttached = true;
            //if(!m_IsAttached)
            //{
            //}
        }

        private void EnableAnchorInternal()
        {
            if (m_Anchor == null) return;
            foreach (GameObject go in m_GoToAttach)
            {
                if (go == null) continue;
                go.transform.SetParent(m_Anchor.transform, false);
                go.SetActive(true);
            }
        }

        private void DisableAnchorInternal()
        {
            foreach (GameObject go in m_GoToAttach)
            {
                if (go == null) continue;
                // before the marker is seen there is no anchor: the node stays where it is, hidden
                if (m_Anchor != null) go.transform.SetParent(m_Anchor.transform, false);
                go.SetActive(false);
            }
        }

        public void RemoveAnchor()
        {
            if (m_Anchor != null)
            {
                Destroy(m_Anchor);
            }

            m_Anchor = null;
            m_Id = TrackableId.invalidId;
        }

        public void AttachNodeToTrackable(GameObject go)
        {
            m_GoToAttach.Add(go);
            if (m_Anchor != null && !m_IsAttached)
            {
                go.transform.SetParent(m_Anchor.gameObject.transform, false);
                go.SetActive(true);
                Debug.Log("TrackableMarker2D::AttachNodeToTrackable");
            }
        }

        private bool m_PoseInit;
        private bool m_PictureHidden;

        private static string ImageKey(Schema.Image img)
        {
            if (img == null) return null;
            string k = !string.IsNullOrEmpty(img.name) ? img.name : img.uri;
            if (string.IsNullOrEmpty(k)) return null;
            k = k.Substring(k.LastIndexOf('/') + 1);
            int dot = k.LastIndexOf('.');
            if (dot > 0) k = k.Substring(0, dot);
            return k.ToLowerInvariant();
        }

        private string MarkerImageKey()
        {
            var root = VirtualSceneGraph.root;
            Node node = root.nodes[m_MarkerNode];
            if (node.mesh < 0) return null;
            foreach (var prim in root.meshes[node.mesh].primitives)
            {
                if (prim.material < 0) continue;
                var bc = root.materials[prim.material].pbrMetallicRoughness?.baseColorTexture;
                if (bc == null) continue;
                int src = root.textures[bc.index].source;
                if (src >= 0 && src < root.images.Length) return ImageKey(root.images[src]);
            }
            return null;
        }

        // Scenes may show the marker picture as ordinary content, which is confusing next to the real marker: with the
        // setting on, nodes whose material uses the same picture as the marker node are not drawn.
        private void HideMarkerPicture()
        {
            m_PictureHidden = true;
            if (PlayerPrefs.GetInt("set.hidemarkerpicture", 1) != 1) return;
            try
            {
                var root = VirtualSceneGraph.root;
                string key = MarkerImageKey();
                if (key == null) return;
                var sceneRoot = VirtualSceneGraph.GetSceneTransform();
                var renderers = sceneRoot != null ? sceneRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                foreach (var r in renderers)
                {
                    var go = r.gameObject;
                    if (!VirtualSceneGraph.IsNodeGameObject(go)) continue;
                    int i = VirtualSceneGraph.GetNodeIndexFromGameObject(go);
                    if (i < 0 || i >= root.nodes.Length || i == m_MarkerNode || root.nodes[i].mesh < 0) continue;
                    bool same = false;
                    foreach (var prim in root.meshes[root.nodes[i].mesh].primitives)
                    {
                        if (prim.material < 0) continue;
                        var bc = root.materials[prim.material].pbrMetallicRoughness?.baseColorTexture;
                        if (bc == null) continue;
                        int src = root.textures[bc.index].source;
                        if (src >= 0 && src < root.images.Length && ImageKey(root.images[src]) == key) same = true;
                    }
                    if (!same) continue;
                    r.enabled = false;
                    Debug.Log("TrackableMarker2D::hid the marker picture on node " + i);
                }
            }
            catch (Exception e) { Debug.Log("TrackableMarker2D::could not hide the marker picture: " + e.Message); }
        }
        // While the image is out of view the content is held by a world anchor, which ARCore keeps aligned as its map
        // improves; a plain transform would drift against the room.
        private ARAnchor m_WorldAnchor;
        private bool m_WorldAnchorPending;

        void LateUpdate()
        {
            if (m_Anchor == null || imgTrack == null) return;
            bool visible = imgTrack.trackingState == TrackingState.Tracking;
            if (visible)
            {
                if (m_WorldAnchor != null) { Destroy(m_WorldAnchor.gameObject); m_WorldAnchor = null; }
                return;
            }
            if (m_WorldAnchor == null)
            {
                if (m_WorldAnchorPending) return;
                var mgr = FindFirstObjectByType<ARAnchorManager>(FindObjectsInactive.Include);
                if (mgr == null)
                {
                    GameObject origin = null;
                    try { origin = ARUtilities.GetSessionOrigin(); } catch (Exception) { }
                    if (origin == null) return;
                    mgr = origin.AddComponent<ARAnchorManager>();
                }
                AddWorldAnchor(mgr, new Pose(m_Anchor.transform.position, m_Anchor.transform.rotation));
                return;
            }
            if (m_WorldAnchor.trackingState != TrackingState.None)
                m_Anchor.transform.SetPositionAndRotation(m_WorldAnchor.transform.position, m_WorldAnchor.transform.rotation);
        }

        private async void AddWorldAnchor(ARAnchorManager mgr, Pose pose)
        {
            m_WorldAnchorPending = true;
            var result = await mgr.TryAddAnchorAsync(pose);
            m_WorldAnchorPending = false;
            if (!result.status.IsSuccess()) return;
            // the image came back into view, or this trackable went away, while the anchor was being created
            if (this == null || m_Anchor == null || imgTrack == null || imgTrack.trackingState == TrackingState.Tracking)
            {
                Destroy(result.value.gameObject);
                return;
            }
            m_WorldAnchor = result.value;
        }
        private int m_LastLog = -1;
        void Update()
        {
            if (!m_PictureHidden && m_GoToAttach.Count > 0) HideMarkerPicture();
            int logState = (m_Anchor == null ? 0 : 1) + (m_GoToAttach.Count << 1);
            if (logState != m_LastLog)
            {
                m_LastLog = logState;
                Debug.Log($"TrackableMarker2D::state anchor={(m_Anchor != null)} nodes={m_GoToAttach.Count} requiresAnchoring={m_RequiredAnchoring}");
            }
            if (m_Anchor == null)
            {
                if (m_RequiredAnchoring){
                    DisableAnchorInternal();
                }
            } 
            /*
            else if (imgTrack != null && imgTrack.trackingState == TrackingState.None){
                DisableAnchorInternal();
            }*/
        }

        public void readImage(int index)
        {
            int matIndex1 = -1;
            int texIndex1 = -1;
            int sourceIndex1 = -1;

            Node node = VirtualSceneGraph.root.nodes[m_MarkerNode];

            //Retrieve mesh
            Schema.Mesh meshSCH = VirtualSceneGraph.root.meshes[node.mesh];
            //Get material
            Schema.MeshPrimitive[] primitives = new MeshPrimitive[meshSCH.primitives.Length];
            for (int i = 0; i < meshSCH.primitives.Length; i++)
            {
                // Debug.Log("Read Primitives :" + i);
                primitives[i] = (MeshPrimitive)meshSCH.primitives[i].Clone();
                // Debug.Log("Clone Primitives :" + i);
                matIndex1 = primitives[i].material;
                // Debug.Log("Read Material :" + matIndex1);
                Schema.Material matSCH = VirtualSceneGraph.root.materials[matIndex1];
                if (matSCH.pbrMetallicRoughness.baseColorTexture != null)
                {
                    texIndex1 = matSCH.pbrMetallicRoughness.baseColorTexture.index;
                    Schema.Texture texSch = VirtualSceneGraph.root.textures[texIndex1];
                    sourceIndex1 = texSch.source;
                    UnityEngine.Texture2D tex = VirtualSceneGraph.GetTextureFromIndex(sourceIndex1);
                    byte[] bytes = tex.EncodeToPNG();
                    var dirPath = Application.persistentDataPath + "/SaveImages/";
                    // Debug.Log("dirPath: " + dirPath);
                    if (!System.IO.Directory.Exists(dirPath))
                    {
                        System.IO.Directory.CreateDirectory(dirPath);
                    }
                    System.IO.File.WriteAllBytes(dirPath + "Image" + ".png", bytes);
                }
            }
        }

        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            attributs.Add("markerNode", "markerNode");
            var res = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log(this.GetType().Name + ": Editor Mode Dump all attributes of instance");
            foreach (var item in res)
            {
                if (attributs.ContainsKey(item.Name))
                    Debug.Log(item.Name + " : " + item.GetValue(this));
            }
        }


        /// <summary>
        /// Stops this module from working, empty all references
        /// </summary>
        public void Dispose()
        {
            Debug.Log("TrackableMarker2D::Dispose");

            if (m_TrackedImageManager != null)
            {
                m_TrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
                Destroy(m_TrackedImageManager);
            }

            if (imgTrack != null)
            {
                Destroy(imgTrack);
            }

            StopAllCoroutines();

            Destroy(gameObject);
        }
#else
        public void AttachNodeToTrackable(GameObject go)
        {
            throw new NotImplementedException();
        }

        public bool Detect()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void DumpAttributs()
        {
            throw new NotImplementedException();
        }

        public void Init()
        {
            throw new NotImplementedException();
        }

        public void InitFromGltf(Trackable track)
        {
            throw new NotImplementedException();
        }

        public void RemoveAnchor()
        {
            throw new NotImplementedException();
        }

        public void RequiredAlignedAndScale(Anchor.Aligned aligned)
        {
            throw new NotImplementedException();
        }

        public void RequiredAnchoring(bool requiredAnchoring)
        {
            throw new NotImplementedException();
        }

        public void RequiredSpace(Vector3 requiredSpace)
        {
            throw new NotImplementedException();
        }

        public Transform Track()
        {
            throw new NotImplementedException();
        }
#endif
    }
}