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
using UnityEngine;
using UnityEngine.Events;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARCore;
using System.Collections.Generic;
using System.Reflection;

namespace GLTFast
{    
    /// <summary>
    /// The application-defined trackable object must have a right-handed coordinate space.
    /// </summary>
    public class AnchorEventResolver: UnityEvent<Transform>{};
    public class TrackableApplication : MonoBehaviour, IMpegTrackable
    {
        private TrackableId m_Id = TrackableId.invalidId;
        private string m_AnchorToResolve = "";
        private ARCloudAnchor m_CloudAnchor = null;
        private AnchorEventResolver m_AnchorEventResolver = null;
        private bool m_AccessInternetGranted = false;
        private List<ResolveCloudAnchorPromise> m_ResolvePromises =
            new List<ResolveCloudAnchorPromise>();
        private List<ResolveCloudAnchorResult> m_ResolveResults =
            new List<ResolveCloudAnchorResult>();
        private ARAnchorManager m_ArAnchorManager = null;
        private ARPlaneManager m_PlaneManager;
        private GameObject m_PlacedPrefab = null;
        private bool m_ResolveDone = false;
        List<GameObject> m_GoToAttached;
        private IEnumerator m_ResolveCoroutine = null;
        private bool m_Attached = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredAnchoring=false;
        // private bool m_RequiredAlignedNotScale =false;
        // private bool m_RequiredAlignedAndScale =false;
        // private bool m_RequiredSpace = false;
        
        private void Awake()
        {
            m_AnchorEventResolver = new AnchorEventResolver();
            m_AnchorEventResolver.AddListener((t) => ARPlacementManager.Instance.ReCreatePlacement(t));
            ARCorePermissionManager.RequestPermission("android.permission.INTERNET", OnAccessInternetPermissionGranted);
            
            GameObject arOrigin = ARUtilities.GetSessionOrigin();

            m_ArAnchorManager = FindObjectOfType<ARAnchorManager>(true);
            if(m_ArAnchorManager == null)
            {
                m_ArAnchorManager = arOrigin.AddComponent<ARAnchorManager>();
            }
            m_ArAnchorManager.enabled = true;

            //get the prefab if any
            m_PlacedPrefab = m_ArAnchorManager.anchorPrefab;

            m_PlaneManager = FindObjectOfType<ARPlaneManager>(true);
            if(m_PlaneManager == null)
            {
                m_PlaneManager = arOrigin.AddComponent<ARPlaneManager>();
            }
            m_PlaneManager.enabled = true;
        }

        private void OnAccessInternetPermissionGranted(string _permission, bool _status)
        {
            Debug.Log("TrackableApplication::OnAccessInternetPermissionGranted: " + _status);
            m_AccessInternetGranted = _status;
        }

        public void InitFromGltf(Trackable  track)
        {
            Debug.Log("TrackableApplication::InitFromGltf Marker Application:"+track.trackableId);
            m_AnchorToResolve = track.trackableId;
            if (Application.isEditor)
            {
                DumpAttributs();
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
       
        public void Init()
        {
            m_ResolvePromises.Clear();
            m_ResolveResults.Clear();
            m_GoToAttached = new List<GameObject>();

        }

        public bool  Detect()
        {
            if(m_CloudAnchor == null)
            {
                return false;
            }
            if(m_CloudAnchor.trackingState== TrackingState.Tracking)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public Transform  Track()
        {
            if(m_CloudAnchor.trackingState== TrackingState.Tracking)
            {
                return m_CloudAnchor.transform;
            }
            else
            {
                return null;
            }
        }

        public void RemoveAnchor()
        {
            if(m_CloudAnchor != null)
            {
                Destroy(m_CloudAnchor);
            }
            
            m_CloudAnchor = null;
            m_Id = TrackableId.invalidId;
        }
       
        private void ResolvingCloudAnchors()
        {
            // No Cloud Anchor for resolving.
            if (m_AnchorToResolve == "")
            {
                return;
            }

            // There are pending or finished resolving tasks.
            if (m_ResolvePromises.Count > 0 || m_ResolveResults.Count > 0)
            {
                return;
            }

            // ARCore session is not ready for resolving.
            if (ARSession.state != ARSessionState.SessionTracking)
            {
                Debug.LogWarning("TrackableApplication::AR Session not ready for resolving");
                return;
            }

            Debug.Log("TrackableApplication::Attempting to resolve {0} Cloud Anchor(s): {1} "+m_AnchorToResolve);
            
            try
            {
                var promise = m_ArAnchorManager.ResolveCloudAnchorAsync(m_AnchorToResolve);
                if (promise.State == PromiseState.Done)
                {
                    Debug.Log("TrackableApplication::Failed to resolve Cloud Anchor " + m_AnchorToResolve);
                    OnAnchorResolvedFinished(false, m_AnchorToResolve);
                }
                else
                {
                    m_ResolvePromises.Add(promise);
                    m_ResolveCoroutine = ResolveAnchor(m_AnchorToResolve, promise);
                    StartCoroutine(m_ResolveCoroutine);
                }
            }
            catch(NullReferenceException exc)
            {
                Debug.LogError("TrackableApplication::Null ref: " + exc.Message);
                OnAnchorResolvedFinished(false, m_AnchorToResolve);
            }
        }

        private void OnAnchorResolvedFinished(bool success, string cloudId, string response = null)
        {
            if (success)
            {
                Debug.Log("TrackableApplication::Succeed to resolve the Cloud Anchor: {0}."+ m_AnchorToResolve);
                UpdatePlaneVisibility(false);
            }
            else
            {
                Debug.LogError("TrackableApplication::Failed to resolve the Cloud Anchor: {0}."+ m_AnchorToResolve+ " "+response);
            }
        }

        private IEnumerator ResolveAnchor(string cloudId, ResolveCloudAnchorPromise promise)
        {
            Debug.Log("TrackableApplication::Resolve anchor");
            yield return promise;
            var result = promise.Result;
            m_ResolvePromises.Remove(promise);
            m_ResolveResults.Add(result);

            if (result.CloudAnchorState == CloudAnchorState.Success)
            {
                Debug.Log("TrackableApplication::Cloud anchor state: success");
                OnAnchorResolvedFinished(true, cloudId);
                
                if(m_PlacedPrefab != null)
                {
                    Instantiate(m_PlacedPrefab, result.Anchor.transform);         
                }

                m_ResolveDone = true;
                m_Id = result.Anchor.trackableId;
                m_CloudAnchor = result.Anchor;
                if(!m_Attached)
                {
                    foreach (GameObject go  in m_GoToAttached)
                    {
                        Debug.Log("TrackableApplication::Attach GO to Anchor: "+go.name);
                        go.transform.SetParent(m_CloudAnchor.transform,false);
                        go.SetActive(true);
                    }
                    m_Attached = true;
                }
                Debug.Log("TrackableApplication::Anchor Space: Pos: " +  m_CloudAnchor.transform.position + " Quat: " + m_CloudAnchor.transform.rotation);
            }
            else
            {
                OnAnchorResolvedFinished(false, cloudId, result.CloudAnchorState.ToString());
            }
        }

        void Update()
        {
            if(!m_ResolveDone)
            {
                ResolvingCloudAnchors();
            }
            if(!m_Attached&& m_RequiredAnchoring)
            {
                foreach (GameObject go  in m_GoToAttached)
                {
                    go.SetActive(false);
                }
            }
        }

        public void UpdatePlaneVisibility(bool visible)
        {
            foreach (var plane in m_PlaneManager.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
        }
        
        public void OnDisable()
        {
            RemoveAnchor();
            if (m_ResolveCoroutine != null)
            {
                StopCoroutine(m_ResolveCoroutine);
            }
            m_ResolveCoroutine = null;
           
            foreach (var promise in m_ResolvePromises)
            {
                promise.Cancel();
            }

            m_ResolvePromises.Clear();

            foreach (var result in m_ResolveResults)
            {
                if (result.Anchor != null)
                {
                    Destroy(result.Anchor.gameObject);
                }
            }
            m_ResolveResults.Clear();
        }

        public void AttachNodeToTrackable(GameObject go)
        {
            m_GoToAttached.Add(go);
            if(m_CloudAnchor != null && !m_Attached)
            {
                go.transform.SetParent(m_CloudAnchor.gameObject.transform,false);
                go.SetActive(true);
            }
        }

        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            attributs.Add("TrackableApplication::anchorToResolve","anchorToResolve");
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
            Destroy(gameObject);
        }
    }
}