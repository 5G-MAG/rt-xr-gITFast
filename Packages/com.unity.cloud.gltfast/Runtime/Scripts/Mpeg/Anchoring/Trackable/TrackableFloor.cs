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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif
using System.Reflection;

namespace GLTFast
{
    /// <summary>
    /// Trackable defined as floor plane 
    /// </summary>
    public class TrackableFloor : MonoBehaviour, IMpegTrackable, IPlaceableTrackable
    {
#if UNITY_ANDROID
        private ARPlaneManager m_ArPlaneManager = null;
        private ARAnchorManager m_AnchorManager = null;
        private ARPlane m_Plane = null;
        private TrackableId m_Id;
        private ARAnchor m_Anchor = null;
        private bool m_SupportClassification = true;
        private List<GameObject> m_GoToAttached = new List<GameObject>();
        private bool m_Attached = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredSpaceOk = false;
        private bool m_RequiredAnchoring = false;
        // private bool m_RequiredAlignedNotScale = false;
        private bool m_RequiredAlignedAndScale = false;
        private bool m_IsGoActivated = false;
        private bool m_RequiredSpace = false;
        private Bounds m_PlaneBounds;
        private Bounds m_SceneBounds;
        private Vector3 m_ScaleFactor = Vector3.one;
        private bool m_ApplyScale = false;
        private Pose? m_PlacementPose = null;
        private float m_UserScale = 1f;
        private bool m_PlacedOnWall = false;
        private AnchorHolder m_Holder;
        private Transform HolderTransform { get { return m_Holder != null ? m_Holder.transform : m_Anchor.transform; } }
        private float m_UserYaw = 0f;
        private Dictionary<GameObject, Vector3> m_OriginalPositions = new Dictionary<GameObject, Vector3>();
        private Dictionary<GameObject, Quaternion> m_OriginalRotations = new Dictionary<GameObject, Quaternion>();
        private Dictionary<GameObject, Vector3> m_OriginalScales = new Dictionary<GameObject, Vector3>();

        public void InitFromGltf(Trackable  track)
        {
            //No parameters from GLTF
            if (Application.isEditor)
            {
                DumpAttributs();
            }
        }

        private bool EnsureConfiguration()
        {
            GameObject obj = ARUtilities.GetSessionOrigin();

            m_AnchorManager = FindFirstObjectByType<ARAnchorManager>(FindObjectsInactive.Include); 
            if(m_AnchorManager == null)
            {
                m_AnchorManager = obj.AddComponent<ARAnchorManager>();
            }
            m_AnchorManager.enabled = true;

            m_ArPlaneManager = FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include); 
            if(m_ArPlaneManager == null)
            {
                m_ArPlaneManager = obj.AddComponent<ARPlaneManager>();
            }
            m_ArPlaneManager.enabled = true;

            return true;
        }

        public  void Init()
        {  
            if (!EnsureConfiguration())
            {
                throw new System.Exception("Can't start TrackablFloor. Something went wrong in the configuration");
            }
            var res  = m_ArPlaneManager.descriptor;
            
            //check if classification is supported
            if(res == null)
            {
                Debug.LogWarning("Classification is not supported by this platform, but it is required by TRACKABLE_FLOOR.");
                m_SupportClassification = false;
            }
            else
            {
                m_SupportClassification = m_ArPlaneManager.descriptor.supportsClassification;
            }
                
            //force horizontal detection
            // m_ArPlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            m_ArPlaneManager.planesChanged += PlanesChanged;  
            AnchorPlacement.Register(this);
        }

        private bool IsFloorCandidate(ARPlane p)
        {
            if (p.alignment != PlaneAlignment.HorizontalUp)
            {
                return false;
            }
            return !m_SupportClassification || p.classification == PlaneClassification.Floor;
        }

        private void PlanesChanged(ARPlanesChangedEventArgs arg)
        {
            // a plane can be added before its alignment or classification is known, so updated planes are candidates too
            if (m_Plane == null && arg.updated.Count > 0 && !AnchorPlacement.TapToPlace)
            {
                foreach (ARPlane p in arg.updated)
                {
                    if (m_Plane == null && IsFloorCandidate(p))
                    {
                        m_Plane = p;
                        m_Id = p.trackableId;
                        if (!BuildAnchorInternal())
                        {
                            m_Plane = null;
                            m_Id = TrackableId.invalidId;
                        }
                    }
                }
            }

            if(arg.added.Count > 0)
            {
                Debug.Log("TrackableFloor::PlanesChanged - added : " + arg.added.Count);
                if (m_Plane == null && !AnchorPlacement.TapToPlace){
                    foreach (ARPlane p in arg.added){
                        if (m_Plane == null && IsFloorCandidate(p))
                        {
                            m_Plane = p;
                            m_Id = p.trackableId;
                            if(!BuildAnchorInternal())
                            {
                                m_Plane = null;
                                m_Id = TrackableId.invalidId;
                            }
                        }
                    }
                } else {
                    Debug.Log("TrackableFloor::PlanesChanged - Ignoring " + arg.added.Count + " added planes.");
                }
                foreach (ARPlane p in arg.added){
                    if (p != m_Plane && !AnchorPlacement.TapToPlace){
                        // visualize only the plane being tracked (all planes stay visible while the user chooses)
                        p.gameObject.SetActive(false);
                    }
                }

            }

            if(m_Plane != null && arg.removed.Count > 0 && !AnchorPlacement.TapToPlace)
            {
                foreach (ARPlane p in arg.removed){
                    if (p.trackableId == m_Id)
                    {
                        m_Plane = null;
                        m_Id = TrackableId.invalidId;
                        RemoveAnchor();
                    }
                }
                Debug.Log("TrackableFloor::PlanesChanged - Removed : " + arg.removed.Count);
            }

            if(m_Plane != null && arg.updated.Count > 0)
            {
                foreach (ARPlane p in arg.updated){
                    if (p.trackableId == m_Id && !AnchorPlacement.TapToPlace)
                    {
                        p.gameObject.SetActive(true);
                        // @TODO: ensure aligned and scaled
                    }
                }
                Debug.Log("TrackableFloor::PlanesChanged - Removed : " + arg.removed.Count);
            }
        }

        /// <summary>
        /// return true if the trackable is detected
        /// If an interrupt action is defined, it is played
        /// </summary>
        public bool Detect()
        {
            return m_Plane != null || m_Anchor != null;
        }

        /// <summary>
        /// return true if the trackable is tracked
        /// </summary>
        public Transform Track()
        {
            if(m_Anchor == null)
            {
                return null;
            }
            if(m_Anchor.trackingState== TrackingState.Tracking)
            {
                return m_Plane != null ? m_Plane.transform : HolderTransform;
            }
            else
            {
                return null;
            }
        }

        private bool BuildAnchorInternal()
        {
            if(m_RequiredSpace && m_Plane != null)
            {
                var res = CheckRequiredSpace(m_Plane);
                if(!res)
                {
                    return false;;
                }
            }
            if(m_RequiredAlignedAndScale && m_Plane != null)
            {
                CheckAlignedAndScale(m_Plane);
            }

            if (m_PlacementPose.HasValue && !AnchorPlacement.AnchorToPlane)
            {
                // the user chose the spot: a free-standing anchor there stays put while planes are refined or merged
                m_Anchor = m_AnchorManager.AddAnchor(m_PlacementPose.Value);
            }
            if (m_Anchor == null)
            {
                if (m_Plane != null) m_Anchor = m_AnchorManager.AttachAnchor(m_Plane, m_PlacementPose ?? new Pose(m_Plane.transform.position, m_Plane.transform.rotation));
            }
            if (m_Anchor == null)
            {
                return false;
            }
            if (m_Holder == null)
            {
                m_Holder = new GameObject("MpegAnchoredScene").AddComponent<AnchorHolder>();
            }
            m_Holder.transform.SetParent(m_Anchor.transform.parent, false);
            m_Holder.Bind(m_Anchor, OnAnchorLost);
            if(!m_Attached)
            {
                foreach (GameObject go  in m_GoToAttached)
                {
                    m_IsGoActivated = true;
                    go.SetActive(true);
                    go.transform.SetParent(HolderTransform, false);
                    if (!m_OriginalPositions.ContainsKey(go))
                    {
                        m_OriginalPositions[go] = go.transform.localPosition;
                        m_OriginalRotations[go] = go.transform.localRotation;
                    }
                    
                    if(m_ApplyScale)
                    {
                        if (!m_OriginalScales.ContainsKey(go)) m_OriginalScales[go] = go.transform.localScale;
                        go.transform.localScale = Vector3.Scale(m_OriginalScales[go], m_ScaleFactor);
                    } else if(AnchorPlacement.FaceUserOnPlacement) {
                        Vector3 targetPostition = new Vector3(UnityEngine.Camera.main.transform.position.x, go.transform.position.y, UnityEngine.Camera.main.transform.position.z);
                        go.transform.LookAt(targetPostition);
                    }             
                }
                ApplyUserTransform();
                // Instantiate(Resources.Load<GameObject>("Debug/axes"), m_Anchor.gameObject.transform, false);
                m_Attached = true;
            }
            return true; 
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
            m_GoToAttached.Add(go);
            if(m_Anchor != null && !m_Attached)
            {
                Debug.Log("TrackableFloor::GO Active:" +go.activeSelf);
                go.transform.SetParent(HolderTransform, false);
                go.SetActive(true);
            }         
        }

        public void RequiredSpace(UnityEngine.Vector3 requiredSpace)
        {
            Debug.Log("TrackableFloor::RequiredSpace");
            m_RequiredSpace = true;
            m_RequiredSpaceToCheck = requiredSpace;
        }

        public void RequiredAnchoring(bool requiredAnchoring)
        {
            m_RequiredAnchoring = requiredAnchoring;
            Debug.Log("TrackableFloor::RequiredAnchoring "+m_RequiredAnchoring);         
        }

        // @TODO: was not implemented in initial contribution. 
        // alignment is signaled on Anchor and configured on the AnchorInstance object.
        // trackable should only provide bounds
        // ALIGNED_SCALED: the assets are scaled to match the available space (width and depth of the plane).
        // ALIGNED_NOTSCALED: the assets keep their scale; they are aligned because they live in the anchor space.
        public void RequiredAlignedAndScale(Anchor.Aligned aligned)
        {
            m_RequiredAlignedAndScale = aligned == Anchor.Aligned.ALIGNED_SCALED;
        }

        private void ComputePlaneAABB(ARPlane plane)
        {
            var length = plane.boundary.Length;

            m_PlaneBounds = new Bounds();
            for(int i =0; i < length;i++)
            {
                var vec= plane.boundary[i];
                //add  an y value (floor is in (xz) plane)
                float y = 0.0f;
                Vector3 vec3 =  new Vector3(vec.x,y,vec.y);
                m_PlaneBounds.Encapsulate(vec3);
            }
            // The XR runtime estimates a plane, not a volume: the height of the available space is unknown,
            // so minimumRequiredSpace is compared on width and depth only.
            Debug.Log("TrackableFloor::bounds"+m_PlaneBounds);
            Debug.Log("TrackableFloor::bounds size"+m_PlaneBounds.size);
        }

        private bool CheckRequiredSpace(ARPlane plane)
        {
            ComputePlaneAABB(plane);
            Debug.Log("TrackableFloor::requiredSpacetoCheck"+m_RequiredSpaceToCheck);
            if((m_PlaneBounds.size.x >= m_RequiredSpaceToCheck.x) && (m_PlaneBounds.size.z >= m_RequiredSpaceToCheck.z))
            {
                m_RequiredSpaceOk = true;
            } 
            return m_RequiredSpaceOk;
        } 

        private void ComputeSceneAABB()
        {
            m_SceneBounds = new Bounds();
            Debug.Log("TrackableFloor::computeSceneAABB");
            foreach(GameObject go in m_GoToAttached)
            {
                Transform trans;
                MeshFilter meshFilter;
                Bounds boundsInt;
                if(go.TryGetComponent<Transform> (out trans))
                {
                    if(trans.TryGetComponent<MeshFilter>(out meshFilter))
                    {
                        if(meshFilter.mesh != null)
                        {
                            boundsInt = meshFilter.mesh.bounds;
                            m_SceneBounds.Encapsulate(boundsInt);
                        }
                    }
                    foreach (Transform t in trans.GetComponentsInChildren<Transform>())
                    {
                        if(t.TryGetComponent<MeshFilter>(out meshFilter))
                        {
                            if(meshFilter.mesh != null)
                            {
                                boundsInt = meshFilter.mesh.bounds;
                                m_SceneBounds.Encapsulate(boundsInt);
                            }
                        }
                    }
                }
            }
            Debug.Log("TrackableFloor::bounds scene"+m_SceneBounds);
        }
        
        private void CheckAlignedAndScale(ARPlane plane)
        {
            ComputeSceneAABB();
            ComputePlaneAABB(plane);
            // uniform scale that makes the assets fit the width and depth of the available space
            float sx = m_SceneBounds.size.x > 0.0f ? m_PlaneBounds.size.x / m_SceneBounds.size.x : float.PositiveInfinity;
            float sz = m_SceneBounds.size.z > 0.0f ? m_PlaneBounds.size.z / m_SceneBounds.size.z : float.PositiveInfinity;
            float s = Mathf.Min(sx, sz);
            if (s > 0.0f && !float.IsInfinity(s))
            {
                m_ScaleFactor = new Vector3(s, s, s);
                m_ApplyScale = true;
            }
            Debug.Log("TrackableFloor::bounds scale" + m_ScaleFactor);
        }

        public bool AwaitingPlacement { get { return m_Anchor == null; } }

        public bool PlaceAt(ARPlane plane, Pose pose)
        {
            if (plane == null || !(AnchorPlacement.PlaceOnAnySurface ? AnchorPlacement.IsUserSurface(plane) : IsFloorCandidate(plane)))
            {
                return false;
            }
            if (m_Anchor != null)
            {
                ResetPlacement();
            }
            m_Plane = plane;
            m_Id = plane.trackableId;
            var viewCamera = UnityEngine.Camera.main;
            Vector3 viewPosition = viewCamera != null ? viewCamera.transform.position : pose.position + plane.transform.up;
            m_PlacementPose = new Pose(pose.position, AnchorPlacement.UserRotation(plane, pose.position, viewPosition));
            m_PlacedOnWall = plane.alignment == PlaneAlignment.Vertical;
            if (!BuildAnchorInternal())
            {
                m_Plane = null;
                m_Id = TrackableId.invalidId;
                m_PlacementPose = null;
                return false;
            }
            return true;
        }

        public bool PlaceAtPose(Pose pose, bool onWall)
        {
            if (m_AnchorManager == null)
            {
                return false;
            }
            if (m_Anchor != null)
            {
                ResetPlacement();
            }
            m_Plane = null;
            m_Id = TrackableId.invalidId;
            m_PlacedOnWall = onWall;
            var viewCamera = UnityEngine.Camera.main;
            Vector3 viewPosition = viewCamera != null ? viewCamera.transform.position : pose.position + pose.up;
            m_PlacementPose = new Pose(pose.position, AnchorPlacement.UserRotationFromNormal(pose.up, onWall, pose.position, viewPosition));
            if (!BuildAnchorInternal())
            {
                m_PlacementPose = null;
                return false;
            }
            return true;
        }

        // ARCore dropped the anchor: the scene stays where it was and a new anchor takes over there.
        private bool OnAnchorLost()
        {
            if (m_AnchorManager == null || m_Holder == null)
            {
                return false;
            }
            var anchor = m_AnchorManager.AddAnchor(new Pose(m_Holder.transform.position, m_Holder.transform.rotation));
            if (anchor == null)
            {
                return false;
            }
            m_Anchor = anchor;
            m_Holder.Bind(anchor, OnAnchorLost);
            return true;
        }

        public void SetUserTransform(float scale, float yawDegrees)
        {
            m_UserScale = scale;
            m_UserYaw = yawDegrees;
            ApplyUserTransform();
        }

        // The scene sits on the spot the user tapped: lowest point on the floor or table, back against a wall and centred on the tap.
        private void SitOnSurface()
        {
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (GameObject go in m_GoToAttached)
            {
                if (go == null) continue;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) { bounds = r.bounds; any = true; } else { bounds.Encapsulate(r.bounds); }
                }
            }
            if (!any) return;
            Vector3 anchorPosition = HolderTransform.position;
            Vector3 shift;
            if (!m_PlacedOnWall)
            {
                shift = Vector3.up * (anchorPosition.y - bounds.min.y);
            }
            else
            {
                Vector3 normal = HolderTransform.forward;   // leaves the wall
                Vector3 right = HolderTransform.right;
                Vector3 toCenter = bounds.center - anchorPosition;
                float half = Mathf.Abs(normal.x) * bounds.extents.x + Mathf.Abs(normal.y) * bounds.extents.y + Mathf.Abs(normal.z) * bounds.extents.z;
                shift = right * -Vector3.Dot(toCenter, right) + Vector3.up * -Vector3.Dot(toCenter, Vector3.up) + normal * -(Vector3.Dot(toCenter, normal) - half);
            }
            foreach (GameObject go in m_GoToAttached)
            {
                if (go != null) go.transform.position += shift;
            }
        }

        private void ApplyUserTransform()
        {
            if (m_Holder != null)
            {
                m_Holder.transform.localScale = Vector3.one * m_UserScale;
            }
            Quaternion turn = Quaternion.Euler(0f, m_UserYaw, 0f);
            foreach (GameObject go in m_GoToAttached)
            {
                if (go != null && m_OriginalPositions.ContainsKey(go))
                {
                    go.transform.localPosition = turn * m_OriginalPositions[go];
                    go.transform.localRotation = turn * m_OriginalRotations[go];
                }
            }
            if (AnchorPlacement.GroundOnPlacement && m_PlacementPose.HasValue && m_Holder != null)
            {
                SitOnSurface();
            }
        }

        public void ResetPlacement()
        {
            foreach (GameObject go in m_GoToAttached)
            {
                if (go != null) go.transform.SetParent(null, false);
            }
            if (m_Anchor != null)
            {
                Destroy(m_Anchor.gameObject);
            }
            if (m_Holder != null)
            {
                Destroy(m_Holder.gameObject);
                m_Holder = null;
            }
            m_Anchor = null;
            m_Attached = false;
            m_IsGoActivated = false;
            m_Plane = null;
            m_Id = TrackableId.invalidId;
            m_PlacementPose = null;
            m_ApplyScale = false;
        }

        private float m_NextAdopt;

        void Update()
        {
            // The pose of the anchor is unknown again when ARCore removed the tracked plane (merged into another one): the anchor is set up on
            // a plane that is still tracked, as soon as there is one (8.1.3: the assets are skipped until the pose is known)
            if (m_Plane == null && m_Anchor == null && !AnchorPlacement.TapToPlace && m_ArPlaneManager != null && Time.unscaledTime >= m_NextAdopt)
            {
                m_NextAdopt = Time.unscaledTime + 0.5f;
                foreach (ARPlane candidate in m_ArPlaneManager.trackables)
                {
                    if (candidate == null || candidate.trackingState != TrackingState.Tracking || candidate.subsumedBy != null || !IsFloorCandidate(candidate)) continue;
                    m_Plane = candidate;
                    m_Id = candidate.trackableId;
                    if (!BuildAnchorInternal()) { m_Plane = null; m_Id = TrackableId.invalidId; continue; }
                    Debug.Log("TrackableFloor::re-adopted the plane " + m_Id);
                    break;
                }
            }
            if(m_Anchor == null && m_RequiredAnchoring)
            {
                // requiresAnchoring: the assets are skipped until the pose of the anchor is known
                foreach(GameObject go in m_GoToAttached)
                {
                    if(go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                    }
                }
                m_IsGoActivated = false;
            }
            if (m_Plane != null){

            }
        }

        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            var res = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log(this.GetType().Name+": Editor Mode Dump all attributes of instance");
            foreach(var item in res)
            {
                if(attributs.ContainsKey(item.Name))
                    Debug.Log(item.Name+ " : " + item.GetValue(this));
            }  
        }

        public void Dispose()
        {
            AnchorPlacement.Unregister(this);
            if (m_Holder != null)
            {
                Destroy(m_Holder.gameObject);
                m_Holder = null;
            }
            if(m_ArPlaneManager != null)
            {
                Destroy(m_ArPlaneManager);
            }

            if(m_AnchorManager != null)
            {
                Destroy(m_AnchorManager);
            }

            if(m_Plane != null)
            {
                Destroy(m_Plane);
            }

            if(m_Anchor != null)
            {
                Destroy(m_Anchor);
            }

            if(m_GoToAttached != null)
            {
                for(int i = 0; i < m_GoToAttached.Count; i++)
                {
                    Destroy(m_GoToAttached[i]);
                }
            }

            Destroy(gameObject);
        }
#else
        public void AttachNodeToTrackable(GameObject go)
        {
            throw new System.NotImplementedException();
        }

        public bool Detect()
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public void DumpAttributs()
        {
            throw new System.NotImplementedException();
        }

        public void Init()
        {
            throw new System.NotImplementedException();
        }

        public void InitFromGltf(Trackable track)
        {
            throw new System.NotImplementedException();
        }

        public void RemoveAnchor()
        {
            throw new System.NotImplementedException();
        }

        public void RequiredAlignedAndScale(Anchor.Aligned aligned)
        {
            throw new System.NotImplementedException();
        }

        public void RequiredAnchoring(bool requiredAnchoring)
        {
            throw new System.NotImplementedException();
        }

        public void RequiredSpace(Vector3 requiredSpace)
        {
            throw new System.NotImplementedException();
        }

        public Transform Track()
        {
            throw new System.NotImplementedException();
        }
#endif
    }
}