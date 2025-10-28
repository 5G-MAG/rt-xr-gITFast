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
                    if(_node.extensions?.MPEG_node_interactivity != null)
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


        private static string GetDefaultUnityDeviceLayout(string xrpath)
        {
            // TODO: support hand interaction profile may require user configuration.
            string KHR_SIMPLE_PROFILE = "<SimpleProfile>";
            string GENERIC = "<XRController>";
            switch (xrpath){
                case "/input/select/click":	
                    return KHR_SIMPLE_PROFILE;
                case "/input/menu/click":
                    return KHR_SIMPLE_PROFILE;
                case "/input/grip/pose":
                    return KHR_SIMPLE_PROFILE;
                case "/input/aim/pose":
                    return KHR_SIMPLE_PROFILE;
                case "/output/haptic":
                    return KHR_SIMPLE_PROFILE;
                default:
                    return GENERIC;
            }
        }

        private static string OpenXRPathToUnityControlName(string xrpath)
        {
            switch (xrpath){
                // <XRController> - Unity's baseline XR controler
                // https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/input.html
                case "/input/system/click":
                    return "system"; //	Boolean
                case "/input/system/touch":
                    return "systemTouched"; // Boolean
                case "/input/select/click":
                    return "select"; //	Boolean
                case "/input/menu/click":
                    return "menu"; // Boolean
                case "/input/squeeze/value":
                    return "grip"; // Float
                case "/input/squeeze/click":
                    return "gripPressed"; // Boolean
                case "/input/squeeze/force":
                    return "gripForce"; // Boolean
                case "/input/trigger/value":
                    return "trigger"; // Float
                case "/input/trigger/squeeze":
                    return "triggerPressed"; // Boolean
                case "/input/trigger/touch":
                    return "triggerTouched"; // Boolean
                case "/input/thumbstick":
                    return "joystick"; // Vector2
                case "/input/thumbstick/touch":
                    return "joystickTouched"; // Vector2
                case "/input/thumbstick/clicked":
                    return "joystickClicked"; // Vector2
                case "/input/trackpad":
                    return "touchpad"; // Vector2
                case "/input/trackpad/touch":
                    return "touchpadTouched"; // Boolean
                case "/input/trackpad/clicked":
                    return "touchpadClicked"; // Boolean
                case "/input/a/click":
                    return "primaryButton"; // Boolean
                case "/input/a/touch":
                    return "primaryTouched"; //	Boolean
                case "/input/b/click":
                    return "secondaryButton"; // Boolean
                case "/input/b/touch":
                    return "secondaryTouched"; // Boolean
                case "/input/x/click":
                    return "primaryButton"; // Boolean
                case "/input/x/touch":
                    return "primaryTouched"; //	Boolean
                case "/input/y/click":
                    return "secondaryButton"; // Boolean
                case "/input/y/touch":
                    return "secondaryTouched"; // Boolean

                // <SimpleController> - KHR Simple controller profile
                // https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/khrsimplecontrollerprofile.html
                // case "/input/select/click":	
                //     return "select"; //	Boolean
                // case "/input/menu/click":
                //     return "menu"; // Boolean
                case "/input/grip/pose":
                    return "devicePose"; // Pose
                case "/input/aim/pose":
                    return "pointer"; // Pose
                case "/output/haptic":
                    return "haptic"; // Vibrate

                // Hand interaction profile - https://registry.khronos.org/OpenXR/specs/1.1/html/xrspec.html#ext_hand_interaction-profile
                // <HandInteraction> - https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/handinteractionprofile.html
                // case "/input/grip/pose":
                //     return "devicePose"; // Pose
                // case "/input/aim/pose":
                //     return "pointer"; // Pose
                case "/input/pinch_ext/pose":
                    return "pinchPose"; // Pose
                case "/input/poke_ext/pose":
                    return "pokePose"; // Pose
                case "/input/pinch_ext/value":
                    return "pinchValue"; // Float
                case "/input/pinch_ext/ready_ext":
                    return "pinchReady"; // Boolean
                case "/input/aim_activate_ext/value":
                    return "pointerActivateValue"; // Float
                case "/input/aim_activate_ext/ready_ext":
                    return "pointerActivateReady"; // Boolean
                case "/input/grasp_ext/value":
                    return "graspValue"; // Float
                case "/input/grasp_ext/ready_ext":
                    return "graspReady"; // Boolean
                // <HandInteractionPoses> - https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/handcommonposesinteraction.html
                // case "/input/grip/pose":
                // 	return "devicePose"; // Pose
                // case "/input/aim/pose":
                // 	return "pointer"; // Pose
                // case "/input/pinch_ext/pose":
                // 	return "pinchPose"; // Pose
                // case "/input/poke_ext/pose":
                // 	return "pokePose"; // Pose
                // <PalmPose> - https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/features/palmposeinteraction.html
                case "/input/palm_ext/pose":
                    return "palmPose";
            }
            return xrpath;
        }

        public static string GetBindingFromUserInputDescription(string description)
        {
            if(string.IsNullOrEmpty(description)) 
            {
                throw new Exception("Trying to get binding from description, but it's null");
            }

            StringBuilder builder = new StringBuilder();
            // ISO/IEC 23090-14 Table 32 specifies that an XRPath shall be used
            string uhr = "/user/hand/right";
            string uhl = "/user/hand/left";
            // TODO: support hand interraction profile
            if (description.StartsWith(uhr)){
                string subPath = description.Substring(uhr.Length);
                builder.Append(GetDefaultUnityDeviceLayout(subPath));
                builder.Append("{RightHand}/");
                builder.Append(OpenXRPathToUnityControlName(subPath));
            } else if (description.StartsWith(uhl)){
                string subPath = description.Substring(uhr.Length);
                builder.Append(GetDefaultUnityDeviceLayout(subPath));
                builder.Append("{LeftHand}/");
                builder.Append(OpenXRPathToUnityControlName(subPath));
            } else {
                // mouse/keyboard/touchscreen are not standard. 
                string[] userInputDesc = description.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (userInputDesc.Length == 0){
                    Debug.LogWarning("XRPath used in interactivity trigger definition is not valid: " + description);
                    return description;
                }
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
                    default :
                        Debug.LogWarning("XRPath used in interactivity trigger definition is not supported: " + description);
                        return description;
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
            }
            return builder.ToString();
        }
    }
}