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
using UnityEngine;

#if UNITY_ANDROID
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Reflection;
#endif

namespace GLTFast
{
    /// <summary>
    /// Trackable defined as floor plane 
    /// </summary>
    public class TrackableFloor : MonoBehaviour, IMpegTrackable
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
        private MPEG_TrackableEvent m_TrackableEvent;


        public void InitFromGltf(Trackable  track)
        {
            //No parameters from GLTF
            if (Application.isEditor)
            {
                DumpAttributs();
            }
        }

        private void Awake()
        {
            m_AnchorManager = FindObjectOfType<ARAnchorManager>(true);
            GameObject obj = ARUtilities.GetSessionOrigin();

            if(m_AnchorManager == null)
            {
                m_AnchorManager = obj.AddComponent<ARAnchorManager>();
            }
            m_AnchorManager.enabled = true;

            m_ArPlaneManager = FindObjectOfType<ARPlaneManager>(true);
            if(m_ArPlaneManager == null)
            {
                m_ArPlaneManager = obj.AddComponent<ARPlaneManager>();
                m_ArPlaneManager.planePrefab = Resources.Load<GameObject>("Plane");
            }
            m_ArPlaneManager.enabled = true;
        }

        public void Init()
        {
            var res  = m_ArPlaneManager.descriptor;
            
            //check if classification is supported
            if(res == null)
            {
                Debug.LogWarning("Classification is not supported: ");
                m_SupportClassification = false;
            }
            else
            {
                m_SupportClassification = m_ArPlaneManager.descriptor.supportsClassification;
            }
            
            m_TrackableEvent = new MPEG_TrackableEvent();
            m_TrackableEvent.trackableType = Schema.TrackableType.TRACKABLE_FLOOR;
            //force horizontal detection
            m_ArPlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            m_ArPlaneManager.planesChanged += PlanesChanged;  
        }

        private void InvokeTrackableEvent(TrackableEventType _type, Vector3 _pos, Quaternion _rot)
        {
            m_TrackableEvent.trackableEventType = _type;
            m_TrackableEvent.anchorPosition = _pos;
            m_TrackableEvent.anchorRotation = _rot;
            TrackableModule.GetInstance().OnAnchoringOccurs(this, m_TrackableEvent);
        }

        private void PlanesChanged(ARPlanesChangedEventArgs arg)
        {
            if(arg.added != null)
            {
                if(arg.added.Count==0)
                    return;

                //Floor detection
                ARPlane tempPlane = arg.added[0];
                Vector3 _anchorPosition = tempPlane.gameObject.transform.position;
                Quaternion _anchorRotation = tempPlane.gameObject.transform.rotation;

                if (m_SupportClassification && tempPlane.classification == PlaneClassification.Floor)
                {
                    //Kept first detected
                    if(m_Plane == null)
                    {
                        m_Plane = tempPlane;
                        m_Id = tempPlane.trackableId;
                        var res = BuildAnchorInternal();
                        if(!res)
                        {
                            m_Plane = null;    
                        }
                    }
                }
                else
                {
                    if(m_Plane == null)
                    {
                        m_Plane = tempPlane;
                        m_Id = tempPlane.trackableId;
                        var res = BuildAnchorInternal();
                        if(!res)
                        {
                            m_Plane = null; 
                        }
                    }
                }

                InvokeTrackableEvent(TrackableEventType.ADDED, _anchorPosition, _anchorRotation);
            }
            else if(arg.removed!= null)
            {
                if(arg.removed.Count == 0)
                    return;

                ARPlane tempPlane = arg.removed[0];
                Vector3 _anchorPosition = tempPlane.gameObject.transform.position;
                Quaternion _anchorRotation = tempPlane.gameObject.transform.rotation;

                if (tempPlane.trackableId == m_Id)
                {
                    m_Plane = null;
                    m_Id = TrackableId.invalidId;
                    RemoveAnchor();
                }
                InvokeTrackableEvent(TrackableEventType.REMOVED, _anchorPosition, _anchorRotation);
            }
            else if(arg.updated != null )
            {
                if(arg.updated.Count == 0)
                    return;  

                ARPlane tempPlane = arg.updated[0];
                Vector3 _anchorPosition = tempPlane.gameObject.transform.position;
                Quaternion _anchorRotation = tempPlane.gameObject.transform.rotation;

                if (tempPlane.trackableId == m_Id)
                {
                    m_Plane = tempPlane;
                    m_Id = tempPlane.trackableId;
                }
                InvokeTrackableEvent(TrackableEventType.UPDATED, _anchorPosition, _anchorRotation);
            }
        }

        /// <summary>
        /// return true if the trackable is detected
        /// If an interrupt action is defined, it is played
        /// </summary>
        public bool Detect()
        {
            return m_Plane != null;
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
                return m_Plane.transform;
            }
            else
            {
                return null;
            }
        }

        private bool BuildAnchorInternal()
        {
            Debug.Log("TrackableFloor::BuildAnchorInternal");
            if(m_RequiredSpace)
            {
                var res = CheckRequiredSpace(m_Plane);
                if(!res)
                {
                    return false;
                }
            }
            Debug.Log("TrackableFloor::_requiredAlignedAndScale "+ m_RequiredAlignedAndScale);
            if(m_RequiredAlignedAndScale)
            {
                Debug.Log("TrackableFloor::Start computeSceneAABB");
                CheckAlignedAndScale(m_Plane);
            }

            m_Anchor = m_AnchorManager.AttachAnchor(m_Plane,new Pose(m_Plane.transform.position,m_Plane.transform.rotation));
            if(!m_Attached)
            {
                foreach (GameObject go  in m_GoToAttached)
                {
                    m_IsGoActivated = true;
                    go.SetActive(true);
                    Debug.Log("GO Active:" +go.activeSelf);
                    go.transform.SetParent(m_Anchor.gameObject.transform,false);
                    
                    if(m_ApplyScale)
                    {
                        go.transform.localScale = m_ScaleFactor;
                        Debug.Log("Apply Scale:" +m_ScaleFactor);
                        foreach(Transform t in go.GetComponentsInChildren<Transform>())
                        {
                            t.localScale = m_ScaleFactor;
                        }
                    }                    
                }
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
                go.transform.SetParent(m_Anchor.gameObject.transform,false);
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

        public void RequiredAlignedAndScale(Anchor.Aligned aligned)
        {
            Debug.Log("TrackableFloor::RequiredAlignedAndScale "+aligned);
            // if(aligned == Anchor.Aligned.ALIGNED_NOTSCALED)
            // {
            //     m_RequiredAlignedNotScale = true;
            // }
            // if(aligned == Anchor.Aligned.ALIGNED_SCALED)
            // {
            //     m_RequiredAlignedAndScale = true;
            // }
            Debug.Log("TrackableFloor::RequiredAlignedAndScale "+aligned);
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

            float height = 2.50f;
            m_PlaneBounds.Expand(new Vector3(0.0f,height,0.0f));
            Debug.Log("TrackableFloor::bounds"+m_PlaneBounds);
            Debug.Log("TrackableFloor::bounds size"+m_PlaneBounds.size);
        }

        private bool CheckRequiredSpace(ARPlane plane)
        {
            ComputePlaneAABB(plane);
            Debug.Log("TrackableFloor::requiredSpacetoCheck"+m_RequiredSpaceToCheck);
            if((m_PlaneBounds.size.x >= m_RequiredSpaceToCheck.x) && (m_PlaneBounds.size.y >= m_RequiredSpaceToCheck.y) && (m_PlaneBounds.size.z >= m_RequiredSpaceToCheck.z))
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
                Transform  trans;
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
            //now compute scale factor
            float xScale=1.0f;
            float yScale=1.0f;
            float zScale=1.0f;
            if(m_PlaneBounds.size.x != 0.0f)
            {
                xScale = m_SceneBounds.size.x/m_PlaneBounds.size.x;
            }
            if(m_PlaneBounds.size.y != 0.0f)
            {
                yScale = m_SceneBounds.size.y/m_PlaneBounds.size.y;
            }
            if(m_PlaneBounds.size.z != 0.0f)
            {
                zScale = m_SceneBounds.size.z/m_PlaneBounds.size.z;
            }

            float[] scales  = new float[3];
            scales[0] = xScale;
            scales[1] = yScale;
            scales[2] = zScale;
            var scaleMax = scales.Max();
            //if scene  space is sup to plane space (maxVal > 1.0), rescale the scene
            if(scaleMax>1.0f)
            {
                m_ScaleFactor.x = 1.0f / scaleMax;
                m_ScaleFactor.y = 1.0f / scaleMax;
                m_ScaleFactor.z = 1.0f / scaleMax;
                m_ApplyScale = true;
            }
            Debug.Log("TrackableFloor::bounds scale"+m_ScaleFactor);
        }

        void Update()
        {
            if(m_Anchor == null)
            {
                if(m_RequiredAnchoring && m_IsGoActivated)
                {
                    foreach(GameObject go in m_GoToAttached)
                    {
                        go.SetActive(false);
                        // TODO: Show planes because it seems that nothing happen
                        // Same on geometric
                    }
                    m_IsGoActivated = false;
                }
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