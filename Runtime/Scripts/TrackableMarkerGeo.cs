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
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using UnityEngine.XR.ARCore;
using Google.XR.ARCoreExtensions;
using System.Reflection;

namespace GLTFast
{    
    /// <summary>
    /// The y-axis matches the direction of gravity as detected by the device's motion sensing hardware, y points downward.
    ///The x- and z-axes match the longitude and latitude directions. -Z points to true north -X points west. 
    /// </summary>
    public class TrackableMarkerGeo : MonoBehaviour, IMpegTrackable
    {
        private Vector3 m_Coordinates;
        private ARAnchorManager m_AnchorManager = null;
        private TrackableId m_Id = TrackableId.invalidId;
        private ARGeospatialAnchor m_Anchor = null;
        private AREarthManager m_EarthManager = null;
        private bool m_AccessFineLocationGranted;
        private bool m_AccessCoarseLocationGranted;
        private bool m_AccessInternetGranted;
        private bool m_PhoneStateGranted;
        private bool m_IsLocationInitialized;
        // private bool m_WaitingForLocationService = false;
        private GameObject m_ArOrigin;
        private List<GameObject> m_GoToAttached;    
        private bool m_Attached = false;
        private float m_Longitude = -1.0f;
        private float m_Latitude = -1.0f;
        private float m_Altitude = -1.0f;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredAnchoring = false;
        // private bool m_RequiredAlignedNotScale =false;
        // private bool m_RequiredAlignedAndScale =false;
        // private bool m_RequiredSpace = false;

        public void InitFromGltf(Trackable  track)
        {
            Debug.Log("TrackableMarkerGeo::InitFromGltf: Marker Geo");
            //get coordinates
            m_Coordinates.x= track.coordinates[0];
            m_Coordinates.y= track.coordinates[1];
            m_Coordinates.z= track.coordinates[2];
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

        IEnumerator Start()
        {
            while(!LoaderUtility.Initialize())
            {
                Debug.Log("TrackableMarkerGeo::Initializing...");
                yield return null;
            }
            
            if(m_ArOrigin == null)
            {
                m_ArOrigin = ARUtilities.GetSessionOrigin();
            }

            // Check duplicates
            m_EarthManager = FindObjectOfType<AREarthManager>(true);
            if(m_EarthManager == null)
            {
                m_EarthManager = m_ArOrigin.AddComponent<AREarthManager>();
            }
            // Force the activation
            m_EarthManager.enabled = true;

            FeatureSupported _support = m_EarthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
        
            while(_support == FeatureSupported.Unknown)
            {
                Debug.Log("TrackableMarkerGeo::Waiting for feature support to status...");
                _support = m_EarthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
                yield return new WaitForSeconds(1);
            }

            if(_support == FeatureSupported.Unsupported)
            {
                throw new System.Exception($"TrackableMarkerGeo::Geospatial feature not supported : {_support}");
            }
            else
                Debug.Log($"TrackableMarkerGeo::Geospatial feature  support : {_support}");
            
            // Location services
            if (!Input.location.isEnabledByUser)
            {
                Debug.LogError("TrackableMarkerGeo::LocationService not available, aborting!");
                yield break;
            }
            
            ARCorePermissionManager.RequestPermission("android.permission.READ_PHONE_STATE", OnReadPhoneStatePermissionGranted);
            ARCorePermissionManager.RequestPermission("android.permission.ACCESS_FINE_LOCATION", OnAccessFineLocationPermissionGranted);
            ARCorePermissionManager.RequestPermission("android.permission.ACCESS_COARSE_LOCATION", OnAccessCoarseLocationPermissionGranted);
            ARCorePermissionManager.RequestPermission("android.permission.INTERNET", OnAccessInternetPermissionGranted);
            Debug.Log("TrackableMarkerGeo::Initializing Done");
        }

        private void OnReadPhoneStatePermissionGranted(string arg1, bool _status)
        {
            Debug.Log("TrackableMarkerGeo::OnReadPhoneStatePermissionGranted: " + _status);
            m_PhoneStateGranted = _status;
        }

        private void OnAccessInternetPermissionGranted(string _permission, bool _status)
        {
            Debug.Log("TrackableMarkerGeo::OnAccessInternetPermissionGranted: " + _status);
            m_AccessInternetGranted = _status;
        }

        private void OnAccessCoarseLocationPermissionGranted(string _permission, bool _status)
        {
            Debug.Log("TrackableMarkerGeo::OnAccessCoarseLocationPermissionGranted: " + _status);
            m_AccessCoarseLocationGranted = _status;
        }

        private void OnAccessFineLocationPermissionGranted(string _permission, bool _status)
        {
            Debug.Log("TrackableMarkerGeo::OnAccessFineLocationPermissionGranted: " + _status);
            m_AccessFineLocationGranted = _status;
        }

        public void Init()
        {
            Debug.Log("TrackableMarkerGeo::Init ARGeospatialAnchor");
            m_GoToAttached = new List<GameObject>();

            if(m_ArOrigin == null)
            {
                m_ArOrigin = ARUtilities.GetSessionOrigin();
            }

            m_AnchorManager = FindObjectOfType<ARAnchorManager>(true);
            if(m_AnchorManager == null)
            {
                m_AnchorManager = m_ArOrigin.AddComponent<ARAnchorManager>(); 
            }
            m_AnchorManager.enabled = true;
        }

        public bool Detect()
        {
            return m_Anchor != null;
        }

        public Transform Track()
        {
            if(m_Anchor == null)
            {
                return null;
            }

            if(m_Anchor.trackingState == TrackingState.Tracking)
            {
                return m_Anchor.transform;
            }
            
            return null;
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
                go.transform.SetParent(m_Anchor.gameObject.transform,false);
                go.SetActive(true);
            }   
        }

        void Update()
        {
            if(!m_AccessFineLocationGranted && !m_AccessCoarseLocationGranted && !m_AccessInternetGranted)  
            {
                return;
            }

            if(!m_IsLocationInitialized)
            {
                m_IsLocationInitialized = true;
                InitializeLocation();
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                if(m_Anchor == null)
                {
                    if(m_RequiredAnchoring)
                    {
                        foreach(GameObject go in m_GoToAttached)
                        {
                            go.SetActive(false);
                        }
                    }
      
                    Debug.Log("TrackableMarkerGeo::Try Build Anchor: "+m_Coordinates.x);
                    m_Anchor = m_AnchorManager.AddAnchor(m_Coordinates.x, m_Coordinates.y, m_Coordinates.z,Quaternion.identity);
                    if(m_Anchor == null)
                    {
                        Debug.LogError("TrackableMarkerGeo::NullAnchor");
                    }
                    else
                    {
                         if(!m_Attached)
                        {
                            foreach (GameObject go  in m_GoToAttached)
                            {
                                Debug.Log("TrackableMarkerGeo::Attach GO to Anchor: "+go.name);
                                go.transform.SetParent(m_Anchor.transform,false);
                                go.SetActive(true);
                            }
                            m_Id = m_Anchor.trackableId;
                            m_Attached = true;
                        }
                        m_Longitude = Input.location.lastData.longitude;
                        m_Latitude = Input.location.lastData.latitude;
                        m_Altitude = Input.location.lastData.altitude;
                        Debug.Log($"TrackableMarkerGeo::Geospatial coordinates: {m_Longitude} :: {m_Latitude} ::{m_Altitude}");
                    }
                }
            }
            else
            {
                Debug.Log("TrackableMarkerGeo::Input.location.status Not Running");
            }
        }

        private void InitializeLocation()
        {
            Debug.Log("TrackableMarkerGeo::InitializeLocation");
            StartCoroutine(CoroutineInitializeLocation());
        }
        
        private IEnumerator CoroutineInitializeLocation()
        {
            Debug.Log("TrackableMarkerGeo::CoroutineInitializeLocation");
            // Start location service
            Input.location.Start(10, 20);

            int _triesCount = 0;
            while (Input.location.status == LocationServiceStatus.Initializing)
            {
                // FIXME: Expensive: To see if there is another option
                if(m_RequiredAnchoring)
                {
                    foreach(GameObject go in m_GoToAttached)
                    {
                        go.SetActive(false);
                    }
                }

                _triesCount++;
                yield return new WaitForSeconds(1);
                Debug.Log($"TrackableMarkerGeo::Initializing location service :: {_triesCount} : {Input.location.status}");

                if (_triesCount == 20)
                {
                    Debug.LogError("TrackableMarkerGeo::Exceeded tries count (20)");
                    break;
                }
            }

            Debug.Log($"TrackableMarkerGeo::LocationServiceStatusPass: {Input.location.status}");

            // Connection failed
            if (Input.location.status == LocationServiceStatus.Failed)
            {
                throw new System.Exception("TrackableMarkerGeo::Failed location service start");
            }
            else
            {
                Debug.Log($"TrackableMarkerGeo::Access granted with status : {Input.location.status}");
            }
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning(
                    "TrackableMarkerGeo::Location services aren't running. VPS availability check is not available.");
                yield break;
            }
            m_Longitude = Input.location.lastData.longitude;
            m_Latitude = Input.location.lastData.latitude;
            m_Altitude = Input.location.lastData.altitude;
            
            var location = Input.location.lastData;
            Debug.Log($"TrackableMarkerGeo::Check VPS Availability Location "+location.longitude + " " + location.latitude + " " + location.altitude);

            VpsAvailabilityPromise _promise = AREarthManager.CheckVpsAvailabilityAsync(location.latitude, location.longitude);
        
            while(_promise.State != PromiseState.Done)
            {
                yield return null;
            }

            Debug.Log($"TrackableMarkerGeo::Promise state is {_promise.State}");

            VpsAvailability _availability = _promise.Result;

            if(_availability != VpsAvailability.Available)
            {
                throw new System.Exception($"TrackableMarkerGeo::VPS Availability isn't available: {_availability}");
            }
            Debug.Log($"TrackableMarkerGeo::VPS Availability is {_availability}");
        }
        
    //     private IEnumerator StartLocationService()
    //     {
    //         m_WaitingForLocationService = true;
    // #if UNITY_ANDROID
    //         if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
    //         {
    //             Debug.Log("Go Requesting the fine location permission.");
    //             Permission.RequestUserPermission(Permission.FineLocation);
    //             yield return new WaitForSeconds(3.0f);
    //         }
    // #endif

    //         if (!Input.location.isEnabledByUser)
    //         {
    //             Debug.Log("Go Location service is disabled by the user.");
    //             m_WaitingForLocationService = false;
    //             yield break;
    //         }

    //         Debug.Log("Go Starting location service.");
    //         Input.location.Start();

    //         while (Input.location.status == LocationServiceStatus.Initializing)
    //         {
    //             yield return null;
    //         }
    //         Debug.Log("Go Initialize location service done");

    //         m_WaitingForLocationService = false;
    //         if (Input.location.status != LocationServiceStatus.Running)
    //         {
    //             Debug.LogWarningFormat(
    //                 "Location service ended with {0} status.", Input.location.status);
    //             Input.location.Stop();
    //         }
    //         Debug.Log("Go LocationServiceStatus IN"+Input.location.status);
    //         var pose = m_EarthManager.EarthState == EarthState.Enabled &&
    //             m_EarthManager.EarthTrackingState == TrackingState.Tracking ?
    //             m_EarthManager.CameraGeospatialPose : new GeospatialPose();
    //         Debug.Log("EarthState: "+m_EarthManager.EarthState);

    //     }

        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            attributs.Add("coord","coord");
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
            if(m_AnchorManager != null)
            {
                Destroy(m_AnchorManager);
            }

            if(m_Anchor != null)
            {
                Destroy(m_Anchor);
            }
                
            if(m_EarthManager != null)
            {
                Destroy(m_EarthManager);
            }

            if(m_ArOrigin != null)
            {
                Destroy(m_ArOrigin);
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
    }
}