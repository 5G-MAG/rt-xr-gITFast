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

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GLTFast
{
    /// <summary>
    /// Manipulates the transformation of one or multiple gameObjects by following a pointing
    /// device (ISO/IEC 23090-14:2024, Table 41). The nodes are moved relative to their pose when
    /// the input became active; the device pose is the value read from the input (Vector3 position,
    /// Quaternion rotation or Vector2 cursor).
    /// </summary>
    public class ActionManipulate : MonoBehaviour, IMpegInteractivityAction, IMpegInteractivityStoppable
    {
        public float Delay => m_Delay;
        private float m_Delay;
        private Vector3 m_Axis = Vector3.up;
        private GameObject[] m_Targets;
        private Schema.Action.ManipulateActionType m_CurrentManipulateType;
        private InputAction m_InputAction;
        // Device rotation of a pose path; the position drives the input action above
        private InputAction m_RotationAction;
        private object m_ReadInputValue;
        private bool m_IsInputPerformed;
        private bool m_WarnedNoPose;

        // Pose of the pointing device and of the nodes when the manipulation started
        private bool m_HasStart;
        private bool m_StartHasPos;
        private bool m_StartHasRot;
        private Vector3 m_StartDevicePos;
        private Quaternion m_StartDeviceRot = Quaternion.identity;
        private Vector3[] m_StartPos;
        private Quaternion[] m_StartRot;
        private Vector3[] m_StartScale;

        private UnityEngine.Camera Camera
        {
            get
            {
                if (m_Camera == null)
                {
                    m_Camera = UnityEngine.Camera.main;
                    if (m_Camera == null) m_Camera = FindFirstObjectByType<UnityEngine.Camera>();
                }
                return m_Camera;
            }
        }
        private UnityEngine.Camera m_Camera;

        public void Dispose()
        {
            if (m_InputAction != null)
            {
                m_InputAction.performed -= OnInputPerformed;
                m_InputAction.canceled -= OnInputCanceled;
                m_InputAction.Disable();
                m_InputAction.Dispose();
                m_InputAction = null;
            }
            if (m_RotationAction != null)
            {
                m_RotationAction.Disable();
                m_RotationAction.Dispose();
                m_RotationAction = null;
            }
            Destroy(gameObject);
        }

        public void Init(Schema.Action action)
        {
            try
            {
                string binding = UserInputSceneTrigger.GetBindingFromUserInputDescription(action.userInputDescription);
                m_InputAction = new InputAction(action.userInputDescription, binding: binding);
                m_InputAction.performed += OnInputPerformed;
                m_InputAction.canceled += OnInputCanceled;
                m_InputAction.Enable();

                // A pose also carries a rotation, read each time the action runs
                string rotationBinding = UserInputSceneTrigger.GetRotationBindingFromUserInputDescription(action.userInputDescription);
                if (rotationBinding != null)
                {
                    m_RotationAction = new InputAction(action.userInputDescription + "/rotation", InputActionType.Value, rotationBinding);
                    m_RotationAction.Enable();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("ACTION_MANIPULATE: input '" + action.userInputDescription + "' is not usable (" + e.Message + "); the action has no effect.");
                m_InputAction = null;
            }

            // Table 37: the default axis is Up.
            m_Axis = Vector3.up;
            if (action.axis != null && action.axis.Length >= 3)
            {
                Vector3 axis = new Vector3(action.axis[0], action.axis[1], action.axis[2]);
                if (axis.sqrMagnitude > 1e-12f) m_Axis = axis.normalized;
            }
            m_Targets = VirtualSceneGraph.GetGameObjectsFromIndexes(action.nodes);
            m_Delay = action.delay;
            m_CurrentManipulateType = action.manipulateActionType;
            m_Camera = UnityEngine.Camera.main;
        }

        private void OnInputCanceled(InputAction.CallbackContext _cbk)
        {
            m_IsInputPerformed = false;
            m_HasStart = false;
            m_ReadInputValue = null;
        }

        private void OnInputPerformed(InputAction.CallbackContext _cbk)
        {
            m_IsInputPerformed = true;
            m_ReadInputValue = _cbk.ReadValueAsObject();
        }

        public void Invoke()
        {
            if (Delay > 0.0f)
            {
                StartCoroutine(StartWithDelay(Delay));
            }
            else
            {
                Execute();
            }
        }

        /// <summary>Ends the manipulation when its behavior is interrupted.</summary>
        public void Stop()
        {
            m_HasStart = false;
        }

        private void Execute()
        {
            if (!m_IsInputPerformed || m_ReadInputValue == null || m_Targets == null)
            {
                return;
            }

            Vector3 devicePos;
            Quaternion deviceRot;
            bool hasPos;
            bool hasRot;
            ReadDevicePose(out devicePos, out hasPos, out deviceRot, out hasRot);
            if (!hasPos && !hasRot)
            {
                if (!m_WarnedNoPose)
                {
                    m_WarnedNoPose = true;
                    Debug.LogWarning("ACTION_MANIPULATE: the input value carries no position or rotation, the nodes are not moved.");
                }
                return;
            }

            if (!m_HasStart)
            {
                CaptureStart(devicePos, hasPos, deviceRot, hasRot);
                return;
            }

            Vector3 deltaPos = (hasPos && m_StartHasPos) ? devicePos - m_StartDevicePos : Vector3.zero;
            Quaternion deltaRot = (hasRot && m_StartHasRot) ? deviceRot * Quaternion.Inverse(m_StartDeviceRot) : Quaternion.identity;

            for (int i = 0; i < m_Targets.Length; i++)
            {
                if (m_Targets[i] == null) continue;
                Transform t = m_Targets[i].transform;
                switch (m_CurrentManipulateType)
                {
                    case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_FREE:
                        // The node keeps its pose relative to the device.
                        t.rotation = deltaRot * m_StartRot[i];
                        t.position = hasPos && m_StartHasPos
                            ? devicePos + deltaRot * (m_StartPos[i] - m_StartDevicePos)
                            : m_StartPos[i];
                        break;

                    case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_TRANSLATE:
                        t.position = m_StartPos[i] + deltaPos;
                        break;

                    case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_SLIDE:
                        {
                            // Linear movement along the axis, given in the local space of the device.
                            Vector3 axisWorld = AxisInWorld();
                            t.position = m_StartPos[i] + axisWorld * Vector3.Dot(deltaPos, axisWorld);
                            break;
                        }

                    case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_ROTATE:
                        {
                            // Twist of the device rotation around the axis.
                            Vector3 axisWorld = AxisInWorld();
                            Vector3 v = new Vector3(deltaRot.x, deltaRot.y, deltaRot.z);
                            float angle = 2f * Mathf.Atan2(Vector3.Dot(v, axisWorld), deltaRot.w) * Mathf.Rad2Deg;
                            t.rotation = Quaternion.AngleAxis(angle, axisWorld) * m_StartRot[i];
                            break;
                        }

                    case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_SCALE:
                        {
                            // Central scaling: the ratio of the device distance to the node centre.
                            if (hasPos && m_StartHasPos)
                            {
                                float d0 = Vector3.Distance(m_StartDevicePos, m_StartPos[i]);
                                float d1 = Vector3.Distance(devicePos, m_StartPos[i]);
                                if (d0 > 1e-4f) t.localScale = m_StartScale[i] * (d1 / d0);
                            }
                            break;
                        }
                }
            }
        }

        private Vector3 AxisInWorld()
        {
            Quaternion frame = Quaternion.identity;
            if (m_StartHasRot)
            {
                frame = m_StartDeviceRot;
            }
            else if (Camera != null)
            {
                frame = Camera.transform.rotation;
            }
            Vector3 w = frame * m_Axis;
            return w.sqrMagnitude > 1e-12f ? w.normalized : Vector3.up;
        }

        private void CaptureStart(Vector3 devicePos, bool hasPos, Quaternion deviceRot, bool hasRot)
        {
            m_StartDevicePos = devicePos;
            m_StartHasPos = hasPos;
            m_StartDeviceRot = deviceRot;
            m_StartHasRot = hasRot;
            m_StartPos = new Vector3[m_Targets.Length];
            m_StartRot = new Quaternion[m_Targets.Length];
            m_StartScale = new Vector3[m_Targets.Length];
            for (int i = 0; i < m_Targets.Length; i++)
            {
                if (m_Targets[i] == null) continue;
                Transform t = m_Targets[i].transform;
                m_StartPos[i] = t.position;
                m_StartRot[i] = t.rotation;
                m_StartScale[i] = t.localScale;
            }
            m_HasStart = true;
        }

        private void ReadDevicePose(out Vector3 pos, out bool hasPos, out Quaternion rot, out bool hasRot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            hasPos = false;
            hasRot = false;
            if (m_ReadInputValue is Vector3)
            {
                pos = (Vector3)m_ReadInputValue;
                hasPos = true;
            }
            else if (m_ReadInputValue is Quaternion)
            {
                rot = (Quaternion)m_ReadInputValue;
                hasRot = true;
            }
            else if (m_ReadInputValue is Vector2 && Camera != null)
            {
                // A 2D value is a cursor: take the point under it at the depth of the first node.
                Vector2 cursor = (Vector2)m_ReadInputValue;
                float depth = 1f;
                if (m_Targets.Length > 0 && m_Targets[0] != null)
                {
                    depth = Mathf.Max(0.1f, Camera.WorldToScreenPoint(m_Targets[0].transform.position).z);
                }
                pos = Camera.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, depth));
                hasPos = true;
            }

            if (!hasRot && m_RotationAction != null && m_RotationAction.controls.Count > 0)
            {
                rot = m_RotationAction.ReadValue<Quaternion>();
                hasRot = true;
            }
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}
