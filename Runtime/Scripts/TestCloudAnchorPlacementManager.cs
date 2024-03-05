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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLTFast;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARCore;

#if UNITY_ANDROID
using UnityEngine.Android;
using Google.XR.ARCoreExtensions;
#endif

public class TestCloudAnchorPlacementManager : Singleton<TestCloudAnchorPlacementManager>
{
    [SerializeField]
    private Camera arCamera;

    [SerializeField]
    private GameObject placedPrefab = null;

    private GameObject placedGameObject = null;

    private ARRaycastManager arRaycastManager = null;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private ARAnchorManager arAnchorManager = null;
    private ARPlaneManager arPlaneManager = null;
    private ARAnchor anchor = null;
    
    
    private float _timeSinceStart;
    private const float _startPrepareTime = 3.0f;
    // Start is called before the first frame update
    void Awake() 
    {
        arRaycastManager = FindObjectOfType<ARRaycastManager>();
        arAnchorManager = FindObjectOfType<ARAnchorManager>();
        arPlaneManager = FindObjectOfType<ARPlaneManager>();
        arPlaneManager.enabled = true;
    }
     bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        if(Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if(touch.phase == TouchPhase.Began)
            {
                Debug.Log("In TryGetTouchPosition: " + touch.phase);
                touchPosition = touch.position;
                return true;
            }
        }
        touchPosition = default;
        return false;
    }
    // Start is called before the first frame update
    void Start()
    {
        TestCloudAnchorManager.Instance.SetCamera(arCamera);
    }

    // Update is called once per frame
    void Update()
    {

        // Give ARCore some time to prepare for hosting or resolving.
        // Only allow the screen to sleep when not tracking.
        var sleepTimeout = SleepTimeout.NeverSleep;
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            sleepTimeout = SleepTimeout.SystemSetting;
        }
        

        Screen.sleepTimeout = sleepTimeout;
        if (_timeSinceStart < _startPrepareTime)
        {
            _timeSinceStart += Time.deltaTime;
            return;
        }
        if(TestCloudAnchorManager.Instance.HostingDone())
        {
            Debug.Log("PJ UpdatePlaneVisibility");
            UpdatePlaneVisibility(false);
        }  
        
        if(!TryGetTouchPosition(out Vector2 touchPosition))
            return;
        if(anchor != null)
            return;

        if(placedGameObject != null)
            return;
        Debug.Log("In RayCast");
        //if(arRaycastManager.Raycast(touchPosition, hits, UnityEngine.XR.ARSubsystems.TrackableType.FeaturePoint))
        if(arRaycastManager.Raycast(touchPosition, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon))
        {
            ARPlane plane = arPlaneManager.GetPlane(hits[0].trackableId);
            if (plane == null)
            {
                Debug.LogWarningFormat("Failed to find the ARPlane with TrackableId {0}",
                    hits[0].trackableId);
                return;
            }
            var hitPose = hits[0].pose;

            if(anchor == null)
            {
                anchor = arAnchorManager.AttachAnchor(plane, hitPose);
                Debug.Log("PJ Add anchor");
                TestCloudAnchorManager.Instance.QueueAnchor(anchor);
            }
            
        }   
    }
    public void ReCreatePlacement(Transform transform)
    {
        placedGameObject = Instantiate(placedPrefab, transform.position, transform.rotation);
        placedGameObject.transform.parent = transform;
    }
    public void UpdatePlaneVisibility(bool visible)
    {
        foreach (var plane in arPlaneManager.trackables)
        {
                plane.gameObject.SetActive(visible);
        }
    }
    public void OnDisable()
    {
           
        if (anchor != null)
        {
            Destroy(anchor.gameObject);
            anchor = null;
        }

        UpdatePlaneVisibility(false);
    }
}
