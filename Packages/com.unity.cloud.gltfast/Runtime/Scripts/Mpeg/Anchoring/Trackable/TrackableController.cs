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
using UnityEngine.XR;
#if UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif
using System.Reflection;

namespace GLTFast
{
    /// <summary>
    /// A trackable of type TRACKABLE_CONTROLLER is a trackable that corresponds to one of the active controllers. Three controller types are defined: grip, aim, and palm.
    /// </summary>
    public class TrackableController : MonoBehaviour, IMpegTrackable
    {
#if UNITY_ANDROID
        private string m_Path;
        private TrackableId m_Id = TrackableId.invalidId;
        private ARAnchor m_Anchor = null;
        // The pose of the controller, followed every frame; its children are the attached nodes.
        private GameObject m_PoseHolder = null;
        private bool m_PoseKnown = false;
        private XRNode m_Node = XRNode.RightHand;
        List<GameObject> m_GoToAttached = new List<GameObject>();
        private bool m_Attached = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredAnchoring=false;
        // private bool m_RequiredAlignedNotScale =false;
        // private bool m_RequiredAlignedAndScale =false;
        // private bool m_RequiredSpace = false;

        public void InitFromGltf(Trackable  track)
        {
           //find the controller to track
           m_Path = track.path;
           // an OpenXR path such as /user/hand/left/input/grip/pose
           if (!string.IsNullOrEmpty(m_Path) && m_Path.Contains("/user/hand/left")) m_Node = XRNode.LeftHand;
           else m_Node = XRNode.RightHand;
           if (Application.isEditor)
           {
                DumpAttributs();
           }
        }
        
        public void Init()
        {
           //find the controller to track
           m_GoToAttached = new List<GameObject>();
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

        // The pose of the tracked controller in world space. UnityEngine.XR reports the grip pose; the aim and palm
        // subpaths of the path are not distinguished (they use the grip pose).
        private bool TryGetControllerPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            var device = InputDevices.GetDeviceAtXRNode(m_Node);
            if (!device.isValid) return false;
            bool tracked;
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out tracked) && !tracked) return false;
            Vector3 p; Quaternion r;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out p)) return false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out r)) return false;
            GameObject origin = null;
            try { origin = ARUtilities.GetSessionOrigin(); } catch (System.Exception) { }
            if (origin != null) { position = origin.transform.TransformPoint(p); rotation = origin.transform.rotation * r; }
            else { position = p; rotation = r; }
            return true;
        }

        void Update()
        {
            Vector3 pos; Quaternion rot;
            m_PoseKnown = TryGetControllerPose(out pos, out rot);
            if (m_PoseKnown)
            {
                if (m_PoseHolder == null) m_PoseHolder = new GameObject("ControllerPose");
                m_PoseHolder.transform.SetPositionAndRotation(pos, rot);
                foreach (GameObject go in m_GoToAttached)
                {
                    if (go == null) continue;
                    if (go.transform.parent != m_PoseHolder.transform) go.transform.SetParent(m_PoseHolder.transform, false);
                    if (!go.activeSelf) go.SetActive(true);
                }
                return;
            }
            // Without a controller pose and with requiresAnchoring, the assets stay skipped until a pose is known.
            if (m_RequiredAnchoring)
            {
                foreach (GameObject go in m_GoToAttached)
                {
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                    }
                }
            }
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

        public bool  Detect()
        {
            return true;
        }

        public Transform Track()
        {
            return m_PoseKnown && m_PoseHolder != null ? m_PoseHolder.transform : null;
        }
        
        public void RemoveAnchor()
        {
            if (m_PoseHolder != null) Destroy(m_PoseHolder);
            m_PoseHolder = null;
            m_PoseKnown = false;
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
            if (m_PoseKnown && m_PoseHolder != null)
            {
                go.transform.SetParent(m_PoseHolder.transform, false);
                go.SetActive(true);
            }
            else if(m_Anchor != null && !m_Attached)
            {
                go.transform.SetParent(m_Anchor.transform,false);
                go.SetActive(true);
            } 
        }
        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            attributs.Add("path","path");
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
            if (m_PoseHolder != null) Destroy(m_PoseHolder);
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