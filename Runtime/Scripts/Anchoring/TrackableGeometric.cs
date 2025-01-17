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
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif
using System.Linq;
using System.Reflection;
using UnityEngine.InputSystem.XR;

namespace GLTFast
{
    /// <summary>
    /// Trackable defined as geometric horizontal or vertical plane 
    /// </summary>
    /// 
    public class TrackableGeometric : MonoBehaviour, IMpegTrackable
    {
#if UNITY_ANDROID
        private ARPlaneManager m_ArPlaneManager;
        private ARAnchorManager m_AnchorManager = null;
        private ARPlane m_Plane = null;
        private TrackableId m_Id;
        private Trackable.GeometricConstraint m_Geoconstraint;
        private ARAnchor m_Anchor = null;
        private bool m_SupportClassification = true;
        private List<GameObject> m_GoToAttached = new List<GameObject>();
        private bool m_Attached = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredSpaceOk = false;
        private bool m_RequiredAnchoring = false;
        // private bool m_RequiredAlignedNotScale =false;
        private bool m_RequiredAlignedAndScale = false;
        private bool m_RequiredSpace = false;
        private Bounds m_PlaneBounds;
        private Bounds m_SceneBounds;
        private Vector3 m_ScaleFactor = Vector3.one;
        private bool m_ApplyScale = false;

        public void InitFromGltf(Trackable track)
        {
            m_Geoconstraint = track.geometricConstraint;
            if (Application.isEditor)
            {
                DumpAttributs();
            }
        }

        public bool EnsureConfiguration()
        {
            GameObject arSession = ARUtilities.GetSessionOrigin();

            m_AnchorManager = FindObjectOfType<ARAnchorManager>(true);
            if (m_AnchorManager == null)
            {
                m_AnchorManager = arSession.AddComponent<ARAnchorManager>();
            }
            m_AnchorManager.enabled = true;

            m_ArPlaneManager = FindObjectOfType<ARPlaneManager>();
            if (m_ArPlaneManager == null)
            {
                m_ArPlaneManager = arSession.AddComponent<ARPlaneManager>();
            }
            m_ArPlaneManager.enabled = true;

            ARSessionOrigin _origin = arSession.GetComponent<ARSessionOrigin>();
            UnityEngine.Camera _cam = _origin.camera;

            if (_cam.GetComponent<ARCameraBackground>() == null)
            {
                Debug.Log("ARCameraBackground == null. Creating one");
                _cam.gameObject.AddComponent<ARCameraBackground>();
            }
            if (_cam.GetComponent<ARCameraManager>() == null)
            {
                Debug.Log("ARCameraManager == null. Creating one");
                _cam.gameObject.AddComponent<ARCameraManager>();
            }
            if (_cam.GetComponent<TrackedPoseDriver>() == null)
            {
                Debug.Log("TrackedPoseDriver == null. Creating one");
                _cam.gameObject.AddComponent<TrackedPoseDriver>();
            }


            // Use XR camera prior to any other cameras
            if (_cam != null)
            {
                UnityEngine.Camera[] _cameras = FindObjectsOfType<UnityEngine.Camera>();
                for (int i = 0; i < _cameras.Length; i++)
                {
                    if (_cameras[i] != _cam)
                    {
                        _cameras[i].enabled = false;
                    }
                }
            }

            _origin.camera = _cam;

            Transform _destination = _origin.transform.GetChild(0);
            _cam.transform.SetParent(_destination);
            Debug.Log($"Set camera as a child of {_destination.name}");


            return true;
        }

        public void Init()
        {
            if (!EnsureConfiguration())
            {
                throw new System.Exception("Can't start TrackableGeometric. Something went wrong in the configuration");
            }
            var res = m_ArPlaneManager.descriptor;

            //check if classification is supported
            if (res == null)
            {
                Debug.LogError("TrackableGeometric::Classification is not supported: ");
                m_SupportClassification = false;
            }
            else
            {
                m_SupportClassification = m_ArPlaneManager.descriptor.supportsClassification;
            }

            //set detection mode
            if (m_Geoconstraint == Trackable.GeometricConstraint.HORIZONTAL_PLANE)
            {
                m_ArPlaneManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
            }
            else
            {
                m_ArPlaneManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Vertical;
            }

            m_ArPlaneManager.planesChanged += PlanesChanged;
        }

        private void PlanesChanged(ARPlanesChangedEventArgs arg)
        {
            if (arg.added.Count == 0)
            {
                return;
            }

            ARPlane tempPlane = arg.added[0];
            if (m_SupportClassification && tempPlane.classification == PlaneClassification.Floor)
            {
                if (m_Plane == null)
                {
                    m_Plane = tempPlane;
                    m_Id = tempPlane.trackableId;
                    var res = BuildAnchorInternal();
                    if (!res)
                    {
                        m_Plane = null;
                    }
                    else
                    {
                        foreach (GameObject go in m_GoToAttached)
                        {
                            go.transform.position = m_Anchor.transform.position;
                            go.transform.rotation = m_Anchor.transform.rotation;
                        }
                    }
                }
            }
            else
            {
                //Kept first detected
                if (m_Plane == null)
                {
                    m_Plane = tempPlane;
                    m_Id = tempPlane.trackableId;

                    var res = BuildAnchorInternal();
                    if (!res)
                    {
                        m_Plane = null;
                    }
                    else
                    {
                        foreach (GameObject go in m_GoToAttached)
                        {
                            go.transform.position = m_Anchor.transform.position;
                            go.transform.rotation = m_Anchor.transform.rotation;
                        }
                    }
                }
            }

            if (arg.removed.Count == 0)
            {
                return;
            }

            tempPlane = arg.removed[0];
            if (tempPlane.trackableId == m_Id)
            {
                m_Plane = null;
                m_Id = TrackableId.invalidId;
                RemoveAnchor();
            }

            if (arg.updated.Count == 0)
            {
                return;
            }

            tempPlane = arg.updated[0];
            if (tempPlane.trackableId == m_Id)
            {
                m_Plane = tempPlane;
                m_Id = tempPlane.trackableId;
            }
        }

        public bool Detect()
        {
            return m_Plane != null;
        }

        public Transform Track()
        {
            if (m_Anchor == null)
            {
                return null;
            }

            if (m_Anchor.trackingState == TrackingState.Tracking)
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
            Debug.Log("TrackableGeometric::BuildAnchorInternal");
            if (m_RequiredSpace)
            {
                var res = CheckRequiredSpace(m_Plane);
                if (!res)
                {
                    return false; ;
                }
            }
            Debug.Log("TrackableGeometric::_requiredAlignedAndScale " + m_RequiredAlignedAndScale);
            if (m_RequiredAlignedAndScale)
            {
                Debug.Log("TrackableGeometric::Start computeSceneAABB");
                CheckAlignedAndScale(m_Plane);
            }

            m_Anchor = m_AnchorManager.AttachAnchor(m_Plane, new Pose(m_Plane.transform.position, m_Plane.transform.rotation));

            if (!m_Attached)
            {
                foreach (GameObject go in m_GoToAttached)
                {
                    go.SetActive(true);
                    Debug.Log("TrackableGeometric::GO(" + go.name + ") Active:" + go.activeSelf);
                    go.transform.position = m_Anchor.transform.position;
                    go.transform.rotation = m_Anchor.transform.rotation;
                    if (m_ApplyScale)
                    {
                        go.transform.localScale = m_ScaleFactor;
                        Debug.Log("TrackableGeometric::Apply Scale:" + m_ScaleFactor);
                        foreach (Transform t in go.GetComponentsInChildren<Transform>())
                        {
                            t.localScale = m_ScaleFactor;
                        }
                    }
                }
                m_Attached = true;
            }
            //UpdatePlaneVisibility(false);  
            return true;
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
            m_GoToAttached.Add(go);
            if (m_Anchor != null && !m_Attached)
            {
                Debug.Log("TrackableGeometric::AttachNodeToAnchor: " + go.name);
                go.transform.position = m_Anchor.transform.position;
                go.transform.rotation = m_Anchor.transform.rotation;
                go.SetActive(true);
            }
        }

        private void UpdatePlaneVisibility(bool visible)
        {
            foreach (var plane in m_ArPlaneManager.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
        }

        public void RequiredSpace(UnityEngine.Vector3 requiredSpace)
        {
            m_RequiredSpace = true;
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

        private void ComputePlaneAABB(ARPlane plane)
        {
            var lenght = plane.boundary.Length;

            m_PlaneBounds = new Bounds();
            for (int i = 0; i < lenght; i++)
            {
                var vec = plane.boundary[i];
                //add  an y value (floor is in (xz) plane)
                float y = 0.0f;
                Vector3 vec3 = new Vector3(vec.x, y, vec.y);
                m_PlaneBounds.Encapsulate(vec3);
            }
            float height = 2.50f;
            m_PlaneBounds.Expand(new Vector3(0.0f, height, 0.0f));
            Debug.Log("TrackableGeometric::bounds" + m_PlaneBounds);
            Debug.Log("TrackableGeometric::bounds size" + m_PlaneBounds.size);
        }

        private bool CheckRequiredSpace(ARPlane plane)
        {
            ComputePlaneAABB(plane);
            Debug.Log("TrackableGeometric::requiredSpacetoCheck" + m_RequiredSpaceToCheck);
            if ((m_PlaneBounds.size.x >= m_RequiredSpaceToCheck.x) && (m_PlaneBounds.size.y >= m_RequiredSpaceToCheck.y) && (m_PlaneBounds.size.z >= m_RequiredSpaceToCheck.z))
            {
                m_RequiredSpaceOk = true;
            }
            return m_RequiredSpaceOk;
        }

        private void ComputeSceneAABB()
        {
            m_SceneBounds = new Bounds();
            Debug.Log("TrackableGeometric::computeSceneAABB");
            foreach (GameObject go in m_GoToAttached)
            {
                Transform trans;
                MeshFilter meshFilter;
                Bounds boundsInt;
                if (go.TryGetComponent<Transform>(out trans))
                {
                    if (trans.TryGetComponent<MeshFilter>(out meshFilter))
                    {
                        if (meshFilter.mesh != null)
                        {
                            boundsInt = meshFilter.mesh.bounds;
                            m_SceneBounds.Encapsulate(boundsInt);
                        }
                    }
                    foreach (Transform t in trans.GetComponentsInChildren<Transform>())
                    {
                        if (t.TryGetComponent<MeshFilter>(out meshFilter))
                        {
                            if (meshFilter.mesh != null)
                            {
                                boundsInt = meshFilter.mesh.bounds;
                                m_SceneBounds.Encapsulate(boundsInt);
                            }
                        }
                    }
                }
            }
            Debug.Log("TrackableGeometric::bounds scene" + m_SceneBounds);
        }

        private void CheckAlignedAndScale(ARPlane plane)
        {
            ComputeSceneAABB();
            ComputePlaneAABB(plane);
            //now compute scale factor
            float xScale = 1.0f;
            float yScale = 1.0f;
            float zScale = 1.0f;
            if (m_PlaneBounds.size.x != 0.0f)
            {
                xScale = m_SceneBounds.size.x / m_PlaneBounds.size.x;
            }
            if (m_PlaneBounds.size.y != 0.0f)
            {
                yScale = m_SceneBounds.size.y / m_PlaneBounds.size.y;
            }
            if (m_PlaneBounds.size.z != 0.0f)
            {
                zScale = m_SceneBounds.size.z / m_PlaneBounds.size.z;
            }

            float[] scales = new float[3];
            scales[0] = xScale;
            scales[1] = yScale;
            scales[2] = zScale;
            var scaleMax = scales.Max();
            //if scene  space is sup to plane space (maxVal > 1.0), rescale the scene
            if (scaleMax > 1.0f)
            {
                m_ScaleFactor.x = 1.0f / scaleMax;
                m_ScaleFactor.y = 1.0f / scaleMax;
                m_ScaleFactor.z = 1.0f / scaleMax;
                m_ApplyScale = true;
            }
            Debug.Log("TrackableGeometric::bounds scale" + m_ScaleFactor);
        }

        void Update()
        {
            if (m_Anchor == null)
            {
                if (m_RequiredAnchoring)
                {
                    //foreach (GameObject go in m_GoToAttached)
                    //{
                    //    go.SetActive(false);
                    //}
                }
                else
                {
                    foreach (GameObject go in m_GoToAttached)
                    {
                        go.SetActive(true);
                        go.transform.position = m_Anchor.transform.position;
                        go.transform.rotation = m_Anchor.transform.rotation;
                    }
                }

                // Try get anchor
                if (m_Plane != null)
                {
                    m_Anchor = m_AnchorManager.AttachAnchor(m_Plane, new Pose(m_Plane.transform.position, m_Plane.transform.rotation));
                }
            }
        }

        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            attributs.Add("m_geoconstraint", "m_geoconstraint");
            var res = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log(this.GetType().Name + ": Editor Mode Dump all attributes of instance");
            foreach (var item in res)
            {
                if (attributs.ContainsKey(item.Name))
                    Debug.Log(item.Name + " : " + item.GetValue(this));
            }
        }

        public void Dispose()
        {
            if (m_ArPlaneManager != null)
            {
                m_ArPlaneManager.planesChanged -= PlanesChanged;
                Destroy(m_ArPlaneManager);
            }

            if (m_AnchorManager != null)
            {
                Destroy(m_AnchorManager);
            }

            if (m_Plane != null)
            {
                Destroy(m_Plane);
            }

            if (m_Anchor != null)
            {
                Destroy(m_Anchor);
            }

            if (m_GoToAttached != null)
            {
                for (int i = 0; i < m_GoToAttached.Count; i++)
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