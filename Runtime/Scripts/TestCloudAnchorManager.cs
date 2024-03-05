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
using UnityEngine.Events;


using UnityEngine.Android;
using Google.XR.ARCoreExtensions;
using UnityEngine.UI;

public class UnityEventResolver : UnityEvent<Transform>{}
public class TestCloudAnchorManager : Singleton<TestCloudAnchorManager>
{
    [SerializeField] private Image m_ImgStatus;
    [SerializeField]
    private Camera arCamera = null;

    [SerializeField] private ARAnchorManager arAnchorManager = null;
    private ARAnchor pendingHostAnchor = null;
    private string anchorToResolve;

    private UnityEventResolver resolver = null;
    private HostCloudAnchorPromise _hostPromise;
    private HostCloudAnchorResult _hostResult;
    private IEnumerator _hostCoroutine = null;
    private bool hostingDone = false;
    private bool hostingPending = false;
    private GameObject goToAttached = null;
    private bool _successed = false;

    public void SetCamera(Camera cam)
    {
        arCamera = cam;
    } 
   
    private void Awake() 
    {
        resolver = new UnityEventResolver();   
        resolver.AddListener((t) => TestCloudAnchorPlacementManager.Instance.ReCreatePlacement(t));
        anchorToResolve = "";
    }
    void Start()
    {
        
    }

    private Pose GetCameraPose()
    {
        if(arCamera == null)
            Debug.LogError("AR Camera Null");
        return new Pose(arCamera.transform.position,
            arCamera.transform.rotation);
    }
#region Anchor Cycle

    public void QueueAnchor(ARAnchor arAnchor)
    {
        pendingHostAnchor = arAnchor;
    }
    void HostPrepare()
    {
        if (hostingPending)
            return;

        hostingDone = false;
        Debug.Log($"HostPrepare executing");

        
        int qualityState = 2;

        Debug.Log("Estimate feature map quality for hosting");
        FeatureMapQuality quality = arAnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());
        Debug.Log("Done estimage. Quality: " + quality);
        switch(quality) {
            case FeatureMapQuality.Insufficient: m_ImgStatus.color = Color.red; break;
            case FeatureMapQuality.Sufficient: m_ImgStatus.color = new Color(1.0f, 0.5f, 0.5f); break;
            case FeatureMapQuality.Good: m_ImgStatus.color = Color.green; break;
        }
        qualityState = (int)quality;

        if(quality !=FeatureMapQuality.Sufficient)
        {
            Debug.Log("Current mapping quality: " + quality);
            return;
        }

        // Start hosting:
        Debug.Log("Processing...Mapping quality has reached sufficient threshold,creating Cloud Anchor.");
        Debug.Log("FeatureMapQuality has reached {0}, triggering CreateCloudAnchor."+ (FeatureMapQuality)quality);

        // HostCloudAnchorPromise promise = ARAnchorManagerExtensions.HostCloudAnchorAsync(arAnchorManager,pendingHostAnchor,10);
        // ARCloudAnchor anc = arAnchorManager.HostCloudAnchor(pendingHostAnchor, 1);
        // if(anc == null)
        // {
        //     Debug.LogError("AR CLoud anchr is null");
        // } else {
        //     Debug.LogError("SUCCESS: " + anc.trackableId);
        //     m_ImgStatus.color = Color.green;
        // }

        HostCloudAnchorPromise promise = ARAnchorManagerExtensions.HostCloudAnchorAsync(arAnchorManager, pendingHostAnchor, 1);
        hostingPending = true;

        if (promise.State == PromiseState.Done)
        {
            Debug.LogFormat("Failed to host a Cloud Anchor.");
            OnAnchorHostedFinished(false);
        }
        else
        {
            _hostPromise = promise;
            Debug.Log(" Promise State: "+promise.State);
            _hostCoroutine = HostAnchor();
            StartCoroutine(_hostCoroutine);
        }
    }
    private IEnumerator HostAnchor()
    {
        yield return _hostPromise;
        Debug.Log("HostAnchor");
        _hostResult = _hostPromise.Result;
        _hostPromise = null;

        if (_hostResult.CloudAnchorState == CloudAnchorState.Success)
        {
            OnAnchorHostedFinished(true, _hostResult.CloudAnchorId);
            Debug.Log("Anchor Space: Pos: " +  pendingHostAnchor.transform.position + " Quat: " + pendingHostAnchor.transform.rotation);
            hostingDone = true;
            //Test
            if(goToAttached!=null)
            {
                goToAttached.transform.SetParent(pendingHostAnchor.transform,false);
                Debug.Log("Attach to anchor go :"+ goToAttached.transform.position + " " +goToAttached.transform.rotation);
            }
            else
                Debug.Log("Nothing to Attach to anchor");
        }
        else
        {
            OnAnchorHostedFinished(false, _hostResult.CloudAnchorState.ToString());
        }
    }

    private void OnAnchorHostedFinished(bool success, string response = null)
    {
        if (success)
        {    
            Debug.Log("Succeed to host the Cloud Anchor: {0}.: "+ response);
            _successed = true;
        }
        else
        {
            Debug.Log("Failed to host the Cloud Anchor"+response); 
        }
    }
      
#endregion
    public bool HostingDone()
    {
        return _successed;
    }

    // Update is called once per frame
    void Update()
    {
        if(!hostingDone && pendingHostAnchor!=null)
        {
            Debug.Log("Prepare Anchor");
            HostPrepare();
        }
    }
    public void OnDisable()
    {

        if (_hostCoroutine != null)
        {
            StopCoroutine(_hostCoroutine);
        }
        _hostCoroutine = null;

        if (_hostPromise != null)
        {
            _hostPromise.Cancel();
        }
        _hostPromise = null;

        _hostResult = null;
  
        TestCloudAnchorPlacementManager.Instance.UpdatePlaneVisibility(false);    
    }  

    public void SetGameObject(GameObject go)
    {
        Debug.Log("SetGameObject: "+go.name);
        goToAttached = go;
    }    
}

