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
            // A path that cannot be bound leaves the trigger never met; it must not abort the scene load.
            try
            {
                string binding = GetBindingFromUserInputDescription(trigger.userInputDescription);
                m_InputAction = new InputAction(trigger.userInputDescription, binding: binding);
                m_InputAction.performed += OnInputPerformed;
                m_InputAction.canceled += OnInputCanceled;
                m_InputAction.Enable();
            }
            catch (Exception e)
            {
                Debug.LogWarning("TRIGGER_USER_INPUT: input '" + trigger.userInputDescription + "' is not usable (" + e.Message + "); the trigger is never met.");
                m_InputAction = null;
            }
            if(trigger.nodes != null)
            {
                m_Targets = VirtualSceneGraph.GetGameObjectsFromIndexes(trigger.nodes);
                for(int i = 0; i < trigger.nodes.Length; i++)
                {
                    Node _node = VirtualSceneGraph.GetNodeFromNodeIndex(trigger.nodes[i]);
                    var _nodeTriggerDef = NodeInteractivityLookup.Find(_node, TriggerType.TRIGGER_USER_INPUT);
                    if(_nodeTriggerDef != null)
                    {
                        UserInputNodeTrigger _nodeTrigger 
                            = m_Targets[i].AddComponent<UserInputNodeTrigger>();

                        _nodeTrigger.Init(_nodeTriggerDef.userInputParameters ?? new string[0]);
                    }
                }
            }
        }

        /// <summary>
        /// Maps a userInputDescription (Table 33: an OpenXR input path, "/user/hand/left/grip" is the example)
        /// to an Input System binding. Also read: "/mouse/...", "/keyboard/...", "/touchscreen/...".
        /// For "/user/hand/{left|right}/[input/]identifier[/component]" the identifier and component are mapped
        /// to the XRController usages of the Input System as follows: select, trigger and trigger/click
        /// to the trigger button; trigger/value to the trigger axis; squeeze, grip, grip/click to the grip button;
        /// grip/value to the grip axis; grip/pose and aim/pose to the device position; menu; thumbstick,
        /// thumbstick/click, thumbstick/touch; trackpad, trackpad/click, trackpad/touch; a and x to the
        /// primary button; b and y to the secondary button.
        /// Throws ArgumentException when the path has no mapping; callers catch it.
        /// </summary>
        public static string GetBindingFromUserInputDescription(string description)
        {
            if(string.IsNullOrEmpty(description)) 
            {
                throw new ArgumentException("Trying to get binding from description, but it's null");
            }

            string[] parts = description.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) throw new ArgumentException("Empty user input path '" + description + "'");

            string first = parts[0].ToLowerInvariant();
            string rest = string.Join("/", parts, 1, parts.Length - 1);
            switch (first)
            {
                case "mouse":
                    return "<Mouse>/" + (rest.Length > 0 ? rest : "leftButton");

                case "keyboard":
                    return "<Keyboard>/" + (rest.Length > 0 ? rest : "anyKey");

                case "touchscreen":
                    return rest.ToLowerInvariant().Contains("position")
                        ? "<Touchscreen>/primaryTouch/position"
                        : "<Touchscreen>/primaryTouch/press";

                case "user":
                    return GetXrBinding(description, parts, false);
            }
            throw new ArgumentException("No input binding for '" + description + "'");
        }

        /// <summary>
        /// The binding of the device rotation for a pose path ("/user/hand/left/aim/pose"), or null when the
        /// path is not a pose. Used by the manipulate action.
        /// </summary>
        public static string GetRotationBindingFromUserInputDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return null;
            string[] parts = description.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || parts[0].ToLowerInvariant() != "user") return null;
            try { return GetXrBinding(description, parts, true); }
            catch (ArgumentException) { return null; }
        }

        private static string GetXrBinding(string description, string[] parts, bool rotation)
        {
            // /user/hand/<left|right>/[input/]<identifier>[/<component>]
            if (parts.Length < 4 || parts[1].ToLowerInvariant() != "hand")
            {
                throw new ArgumentException("Only /user/hand/... input paths are mapped: '" + description + "'");
            }
            string hand = parts[2].ToLowerInvariant() == "left" ? "{LeftHand}"
                : parts[2].ToLowerInvariant() == "right" ? "{RightHand}" : null;
            if (hand == null) throw new ArgumentException("Unknown hand in '" + description + "'");

            int at = 3;
            if (parts[at].ToLowerInvariant() == "input") at++;
            if (at >= parts.Length) throw new ArgumentException("No input identifier in '" + description + "'");
            string id = parts[at].ToLowerInvariant();
            string component = at + 1 < parts.Length ? parts[at + 1].ToLowerInvariant() : null;

            string control = null;
            bool isPose = component == "pose" || (component == null && id == "aim");
            if (isPose)
            {
                if (id != "grip" && id != "aim") throw new ArgumentException("'" + id + "' has no pose in '" + description + "'");
                control = rotation ? "deviceRotation" : "devicePosition";
            }
            else if (rotation)
            {
                throw new ArgumentException("Not a pose: '" + description + "'");
            }
            else
            {
                switch (id)
                {
                    case "select": control = "triggerPressed"; break;
                    case "trigger":
                        control = component == "value" ? "trigger" : component == "touch" ? "triggerTouched" : "triggerPressed"; break;
                    case "squeeze":
                    case "grip":
                        control = component == "value" ? "grip" : "gripPressed"; break;
                    case "menu": control = "menu"; break;
                    case "thumbstick":
                        control = component == "click" ? "thumbstickClicked" : component == "touch" ? "thumbstickTouched"
                            : component == "x" ? "thumbstick/x" : component == "y" ? "thumbstick/y" : "thumbstick"; break;
                    case "trackpad":
                        control = component == "click" ? "trackpadClicked" : component == "touch" ? "trackpadTouched"
                            : component == "x" ? "trackpad/x" : component == "y" ? "trackpad/y" : "trackpad"; break;
                    case "a":
                    case "x": control = "primaryButton"; break;
                    case "b":
                    case "y": control = "secondaryButton"; break;
                }
            }
            if (control == null) throw new ArgumentException("No input binding for '" + description + "'");
            return "<XRController>" + hand + "/" + control;
        }
    }
}