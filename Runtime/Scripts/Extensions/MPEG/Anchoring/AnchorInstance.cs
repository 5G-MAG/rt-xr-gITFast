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
using System.Reflection;

namespace GLTFast
{    
    /// <summary>
    /// An anchor is a virtual object attached to a tracker
    /// </summary>
    public class AnchorInstance : MonoBehaviour, IMpegAnchor
    {
        private int m_Trackable;
        private bool m_RequiresAnchoring = false;
        private Vector3 m_MinimumRequiredSpace;
        private bool m_MinimumRequiredSpaceSet= false;
        private Anchor.Aligned m_Aligned = Anchor.Aligned.NOT_USED;
        private List<int> m_Actions = new List<int>();
        private int m_LightVal = -1;
        private bool m_ActionLaunched = false;
        private bool m_SetupOK = false;
        
        public void Init(Anchor anc)
        {
            m_Trackable = anc.trackable;
            var rA = anc?.requiresAnchoring;
            if(rA != null)
            {
                m_RequiresAnchoring = anc.requiresAnchoring;
            }

            var mRS = anc?.minimumRequiredSpace;
            if( mRS != null)
            {
                m_MinimumRequiredSpace.x = (float)anc?.minimumRequiredSpace[0];
                m_MinimumRequiredSpace.y = (float)anc?.minimumRequiredSpace[1];
                m_MinimumRequiredSpace.z = (float)anc?.minimumRequiredSpace[2];
                m_MinimumRequiredSpaceSet = true;
            }
            var al = anc?.aligned;
            if(al != null)
            {
                m_Aligned = anc.aligned;
            }
            var ac = anc?.actions;
            if(ac != null)
            {
                for (int i = 0; i <  anc.actions.Length;i++)
                {
                    m_Actions.Add(anc.actions[i]);
                }
            }
            var lV =  anc?.light;
            if(lV != null)
                m_LightVal = anc.light; 

            //DumpAttributs in Editor Mode
            if(Application.isEditor)
            {
                DumpAttributs();
            }
        }

        public int GetTrackableIndex()
        {
            return m_Trackable;
        }

        public void AttachNodeToAnchor(GameObject go)
        {
            Debug.Log("AnchorInstance::Anchor attach Node Set: "+go.name);
            var track = VirtualSceneGraph.GetTrackableFromIndex(m_Trackable);
            track.AttachNodeToTrackable(go);
        }

        // Invoke actions when the anchoring is done the first time
        private void Update()
        {
            if(m_Actions.Count > 0 && m_SetupOK)
            {
                if(Track() && !m_ActionLaunched)
                {
                    for(int i = 0; i < m_Actions.Count; i++)
                    {
                        IMpegInteractivityAction _act = VirtualSceneGraph.GetActionFromIndex(m_Actions[i]);
                        _act.Invoke();
                    }
                    m_ActionLaunched = true;
                }
            }
        }

        public void SetUp()
        {
            Debug.Log("AnchorInstance::Anchor SetUp");
            var track = VirtualSceneGraph.GetTrackableFromIndex(m_Trackable);
            if(m_RequiresAnchoring)
            {
                track.RequiredAnchoring(m_RequiresAnchoring);
            }
            
            if(m_MinimumRequiredSpaceSet)
            {
                track.RequiredSpace(m_MinimumRequiredSpace);
            }

            Debug.Log("AnchorInstance::Anchor SetUp aligned: " + m_Aligned);
            if(m_Aligned == Anchor.Aligned.ALIGNED_SCALED)
            {
                track.RequiredAlignedAndScale(m_Aligned);
            }
            m_SetupOK = true;
        }

        /// <summary>
        /// Return true if the the trackable is detected
        /// </summary>
        public bool CheckTrackable()
        {
            var track = VirtualSceneGraph.GetTrackableFromIndex(m_Trackable);
            if (track.Detect())
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Return true if the the trackable is tracked
        /// </summary>
        public bool Track()
        {
            var track = VirtualSceneGraph.GetTrackableFromIndex(m_Trackable);
            if (track.Track())
            {
                return true;
            }
            return false;
            
        }
        public void DumpAttributs()
        {
            Dictionary<string, string> attributs = new Dictionary<string, string>();
            attributs.Add("trackable","trackable");
            attributs.Add("requiresAnchoring","requiresAnchoring");
            attributs.Add("minimumRequiredSpace","minimumRequiredSpace");
            attributs.Add("aligned","aligned");
            attributs.Add("actions","actions");
            attributs.Add("lightVal","lightVal");

            var res = this.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var res1 = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log(this.GetType().Name+": Editor Mode Dump all attributes of instance");
            foreach(var item in res)
            {
                if(attributs.ContainsKey(item.Name))
                {
                    Debug.Log(item.Name+ " : " + item.GetValue(this));
                    if(item.FieldType.IsGenericType)
                    {
                        if(item.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            var theList = (List<int>) item.GetValue(this); 
                            int index = 0;
                            foreach(var val in theList)
                            {
                                Debug.Log(item.Name +"["+index+"]="+ val);
                                index+=1;
                            }
                        }
                    }
                }    
            }
        }
    
        public void Dispose()
        {
            m_Actions.Clear();
            Destroy(gameObject);
        }
    }
}