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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Reflection;

namespace GLTFast
{    
    /// <summary>
    /// A trackable of type TRACKABLE_VIEWER is a trackable that corresponds to the viewer’s pose. 
    /// </summary>
    public class TrackableViewer : MonoBehaviour, IMpegTrackable
    {
        private TrackableId m_Id = TrackableId.invalidId;
        private GameObject m_GoRef = null;
        private List<GameObject> m_GoToAttached;
        private UnityEngine.Camera m_TrackedOb = null;
        private bool m_Attached = false;
        private bool m_TrackStatus = false;
        private Vector3 m_RequiredSpaceToCheck = Vector3.zero;
        private bool m_RequiredAnchoring=false;
        // private bool m_RequiredAlignedNotScale = false;
        // private bool m_RequiredAlignedAndScale = false;
        // private bool m_RequiredSpace = false;

        public void InitFromGltf(Trackable  track)
        {
            if (Application.isEditor)
            {
                DumpAttributs();
            }
        }
        
        public void Init()
        {
            m_GoToAttached = new List<GameObject>();

           //get viewer pose; viewer = camera
            m_TrackedOb = FindObjectOfType<UnityEngine.Camera>();

            if(m_TrackedOb == null)
            {
                throw new Exception("No ARCamera");
            }
           
            m_GoRef = new GameObject();

            //will set game object at meter in front of the camera
            m_GoRef.transform.position = Vector3.zero;
        }

        public bool Detect()
        {
            return m_Attached;
        }

        public Transform Track()
        {
            if(m_Attached)
            {
                return m_TrackedOb.transform;
            }
            return null;
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

        private void BuildAnchorInternal()
        {
            if(m_GoToAttached.Count>0)
            {
                if(!m_Attached)
                {
                    foreach (GameObject go  in m_GoToAttached)
                    {
                        go.transform.SetParent(m_GoRef.transform,false);
                        go.SetActive(true);
                    }
                    m_Attached = true;
                }
            }  
        }

        public void RemoveAnchor()
        {
            if(m_TrackedOb != null)
                m_TrackedOb = null;
            m_Id = TrackableId.invalidId;
        }
        
        public void AttachNodeToTrackable(GameObject go)
        {
            m_GoToAttached.Add(go);
            if(m_TrackStatus == true)
            {
                go.transform.SetParent(m_GoRef.transform,false);
                go.SetActive(true);
            } 
        }  
        
        void Update()
        {
            if(!m_TrackStatus)
            {
                if(!m_TrackedOb.isActiveAndEnabled)
                {
                    if(m_RequiredAnchoring)
                    {
                        foreach(GameObject go in m_GoToAttached)
                        {
                            go.SetActive(false);
                        }
                    }
                    return;
                }
                    
                m_TrackedOb = FindObjectOfType<UnityEngine.Camera>();
                
                if(m_TrackedOb == null)
                {
                    throw new Exception("No ARCamera");
                }

                m_GoRef.transform.SetParent(m_TrackedOb.gameObject.transform,false);
                Debug.Log("Attached to anchor");
                BuildAnchorInternal();
                m_TrackStatus = true;
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
            if(m_GoRef != null)
            {
                Destroy(m_GoRef);
            }

            if(m_GoToAttached != null)
            {
                for(int i = 0; i < m_GoToAttached.Count; i++)
                {
                    Destroy(m_GoToAttached[i]);
                }
            }

            m_TrackedOb = null;

            Destroy(gameObject);
        }
    }
}