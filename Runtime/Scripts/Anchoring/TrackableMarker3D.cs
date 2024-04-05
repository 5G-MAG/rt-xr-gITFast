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
using System.Reflection;

namespace GLTFast
{    
    /// <summary>
    /// The origin is the center of the mesh. The X, Y, and Z axes correspond to the axes of the world space.
    /// </summary>
    public class TrackableMarker3D : MonoBehaviour, IMpegTrackable
    {
        private int m_MarkerNode; 
        private ARAnchor m_Anchor = null;
        private TrackableId m_Id = TrackableId.invalidId;
        private List<GameObject> m_GoToAttached;
        private bool m_Attached = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredAnchoring = false;
        // private bool m_RequiredAlignedNotScale =false;
        // private bool m_RequiredAlignedAndScale =false;
        // private bool m_RequiredSpace = false;

        public void InitFromGltf(Trackable  track)
        {
           m_MarkerNode = track.markerNode;
           //retrieve the node to track
           if (Application.isEditor)
           {
                DumpAttributs();
           }
        }
        
        public void Init()
        {
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
           if(m_Anchor == null)
           {
                return null;
           }
            if(m_Anchor.trackingState== TrackingState.Tracking)
            {
                return m_Anchor.transform;
            }
            else
            {
                return null;
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
            m_GoToAttached.Add(go);
            if(m_Anchor != null && !m_Attached)
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
                    foreach(GameObject go in m_GoToAttached)
                    {
                        go.SetActive(false);
                    }
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

        public void Dispose()
        {
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
    }
}