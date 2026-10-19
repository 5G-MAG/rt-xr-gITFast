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

namespace GLTFast
{
    /// <summary>
    /// Represent the visibility trigger at the scene level
    /// </summary>
    public class VisibilitySceneTrigger : MonoBehaviour, IMpegInteractivityTrigger
    {
        private UnityEngine.Camera m_Camera;
        private VisibilityNodeTrigger[] m_Targets;

        public void Dispose()
        {
            for(int i = 0; i < m_Targets.Length; i++)
            {
                m_Targets[i].Dispose();
            }
            Destroy(gameObject);
        }

        public void Init(Trigger trigger)
        {
            Node _n = VirtualSceneGraph.GetNodeFromNodeIndex(trigger.cameraNode);
            int _camIndex = _n.camera;

            m_Camera = VirtualSceneGraph.GetCameraByIndex(_camIndex);
            m_Targets = new VisibilityNodeTrigger[trigger.nodes.Length];

            for(int i = 0; i < trigger.nodes.Length; i++)
            {
                GameObject _targetGo = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[i]);
                VisibilityNodeTrigger _detector = _targetGo.AddComponent<VisibilityNodeTrigger>();

                Node _node = VirtualSceneGraph.GetNodeFromGameObject(_targetGo);
                // node triggers are matched by type ("Only distinct types are allowed")
                var _nodeTrigger = NodeInteractivityLookup.Find(_node, TriggerType.TRIGGER_VISIBILITY);
                if(_nodeTrigger != null)
                {
                    _detector.InitSceneAndNodeLevelExtension(_nodeTrigger);
                }
                else
                {
                    _detector.InitSceneLevelExtension(trigger);
                }

                m_Targets[i] = _detector;
            }
        }

        public bool MeetConditions()
        {
            // Table 33: "The visibility trigger shall be evaluated only if the related camera is active."
            // The camera is active while its node is active in the hierarchy. The Camera component
            // itself is not tested: the scene viewer mirrors the first scene camera on the main camera
            // and disables the component.
            if (m_Camera == null || !m_Camera.gameObject.activeInHierarchy)
            {
                return false;
            }

            bool result = true;
            for (int i = 0; i < m_Targets.Length; i++)
            {
                if (!m_Targets[i].IsVisible(m_Camera))
                {
                    return false;
                }
            }
            return result;
        }
    }
}