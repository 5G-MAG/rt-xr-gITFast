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
                if(_node.extensions.MPEG_node_interactivity != null
                && _node.extensions.MPEG_node_interactivity.triggers[i].type == TriggerType.TRIGGER_VISIBILITY)
                {
                    _detector.InitSceneAndNodeLevelExtension(_node.extensions.MPEG_node_interactivity.triggers[i]);
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