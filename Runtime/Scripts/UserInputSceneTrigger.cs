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
using System.Text;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace GLTFast
{
    /// <summary>
    /// Represent the user input trigger at the scene level
    /// </summary>
    public class UserInputSceneTrigger : MonoBehaviour, IMpegInteractivityTrigger
    {
        // Require the last unity package to work
        public InputAction inputAction => m_InputAction;
        private InputAction m_InputAction;
        private GameObject[] m_Targets;
        private bool m_IsPerformed;
        private List<UserInputNodeTrigger> m_NodeTriggers = new List<UserInputNodeTrigger>();


        public void Dispose()
        {
            Destroy(gameObject);
        }
        private void OnInputCanceled(InputAction.CallbackContext context)
        {
            m_IsPerformed = false;
        }

        private void OnInputPerformed(InputAction.CallbackContext context)
        {
            m_IsPerformed = true;
            if(m_NodeTriggers.Count > 0)
            {
                for (int i = 0; i < m_NodeTriggers.Count; i++) 
                {
                    // Does the node triggers resolve this input compared
                    // to the given parameters ?
                    if(!m_NodeTriggers[i].Resolve())
                    {
                        m_IsPerformed = false;
                    }
                }
            }
        }

        public bool MeetConditions()
        {
            return m_IsPerformed;
        }

        public void Init(Trigger trigger)
        {
            string binding = GetBindingFromUserInputDescription(trigger.userInputDescription);

            m_InputAction = new InputAction(trigger.userInputDescription, binding: binding);
            m_InputAction.performed += OnInputPerformed;
            m_InputAction.canceled += OnInputCanceled;
            m_InputAction.Enable();
            if(trigger.nodes != null)
            {
                m_Targets = VirtualSceneGraph.GetGameObjectsFromIndexes(trigger.nodes);
                for(int i = 0; i < trigger.nodes.Length; i++)
                {
                    Node _node = VirtualSceneGraph.GetNodeFromNodeIndex(trigger.nodes[i]);
                    if(_node.extensions.MPEG_node_interactivity != null)
                    {
                        if(_node.extensions.MPEG_node_interactivity.triggers[i].type == TriggerType.TRIGGER_USER_INPUT)
                        {
                            UserInputNodeTrigger _nodeTrigger 
                                = m_Targets[i].AddComponent<UserInputNodeTrigger>();

                            _nodeTrigger.Init(_node.extensions.MPEG_node_interactivity.triggers[i].userInputParameters);
                        }
                    }
                }
            }
        }

        public static string GetBindingFromUserInputDescription(string description)
        {
            if(string.IsNullOrEmpty(description)) 
            {
                throw new Exception("Trying to get binding from description, but it's null");
            }

            StringBuilder builder = new StringBuilder();
            string[] userInputDesc = description.Split('/', StringSplitOptions.RemoveEmptyEntries);
            switch (userInputDesc[0].ToLowerInvariant())
            {
                case "mouse":
                    builder.Append("<Mouse>");
                    builder.Append("/");
                    break;

                case "keyboard":
                    builder.Append("<Keyboard>");
                    builder.Append("/");
                    break;

                case "touchscreen":
                    builder.Append("<Touchscreen>");
                    builder.Append("/");
                    builder.Append("Press");
                    break;
            }

            builder.Append(userInputDesc[1]);

            string input = userInputDesc[1].ToLowerInvariant();

            switch (input)
            {
                case "leftbutton":
                    {
                        break;
                    }
                case "position":
                    {
                        break;
                    }
            }

            return builder.ToString();
        }
    }
}