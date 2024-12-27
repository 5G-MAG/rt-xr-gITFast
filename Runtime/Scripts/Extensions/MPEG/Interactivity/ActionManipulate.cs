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
    /// Manipulates the transformation of one or multiple gameObjects
    /// </summary>
    public class ActionManipulate : MonoBehaviour, IMpegInteractivityAction
    {
        public float Delay => m_Delay;
        private float m_Delay;
        private Vector3 m_Axises;
        private GameObject[] m_Targets;
        private Schema.Action.ManipulateActionType m_CurrentManipulateType;
        private InputAction m_InputAction;
        private object m_ReadInputValue;
        private bool m_IsInputPerformed;
        private Vector3 m_Position;
        private MPEG_ActionManipulateEvent m_ManipulateEvent;

        private UnityEngine.Camera Camera
        {
            get
            {
                if (m_Camera == null)
                {
                    m_Camera = FindObjectOfType<UnityEngine.Camera>();
                }
                return m_Camera;
            }
        }
        private UnityEngine.Camera m_Camera;

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Schema.Action action)
        {
            string binding = UserInputSceneTrigger.GetBindingFromUserInputDescription(action.userInputDescription);
            m_InputAction = new InputAction(action.userInputDescription, binding: binding);
            m_InputAction.performed += OnInputPerformed;
            m_InputAction.canceled += OnInputCanceled;
            m_InputAction.Enable();

            m_Axises = action.axis;
            m_Targets = VirtualSceneGraph.GetGameObjectsFromIndexes(action.nodes);
            m_Delay = action.delay;
            m_CurrentManipulateType = action.manipulateActionType;
            m_Camera = Camera.main;
            m_ManipulateEvent = new MPEG_ActionManipulateEvent();
            m_ManipulateEvent.inputAction = m_InputAction;
        }

        private void OnInputCanceled(InputAction.CallbackContext _cbk)
        {
            m_IsInputPerformed = false;
            m_ReadInputValue = _cbk.ReadValueAsObject();
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

        private void Execute()
        {
            if (!m_IsInputPerformed)
            {
                return;
            }

            switch (m_CurrentManipulateType)
            {
                case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_FREE: ManipulateFree(); break;
                case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_ROTATE: ManipulateRotate(); break;
                case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_SCALE: ManipulateScale(); break;
                case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_SLIDE: ManipulateSlide(); break;
                case Schema.Action.ManipulateActionType.ACTION_MANIPULATE_TRANSLATE: ManipulateTranslate(); break;
            }

            ActionModule.GetInstance().OnActionOccurs(this, m_ManipulateEvent);
        }

        private void ManipulateFree()
        {
            ManipulateTranslate();
            ManipulateRotate();
            ManipulateScale();
        }

        private void ManipulateSlide()
        {
            ManipulateTranslate();
        }

        private void ManipulateTranslate()
        {
            // Get the type of the read input value first
            Type _t = m_ReadInputValue.GetType();

            if (_t == typeof(Vector3))
            {
                Vector3 _position = (Vector3)m_ReadInputValue;
                MovePosition(_position);
            }
            if (_t == typeof(Vector2))
            {
                // When 2D, we assume that this is a cursor, and doing
                // the manipulation restricted to screen space
                Vector3 _position = (Vector2)m_ReadInputValue;
                _position.z = 1.0f;
                Vector3 _screenPos = Camera.ScreenToWorldPoint(_position);
                MovePosition(_screenPos);
            }
        }

        private void ManipulateScale()
        {
            // Get the type of the read input value first
            Type _t = m_ReadInputValue.GetType();

            if (_t == typeof(Vector3))
            {
                Vector3 _scale = (Vector3)m_ReadInputValue;
                MoveScale(_scale);
            }
        }

        private void ManipulateRotate()
        {
            // Get the type of the read input value first
            Type _t = m_ReadInputValue.GetType();

            if (_t == typeof(Vector3))
            {
                Vector3 _euler = (Vector3)m_ReadInputValue;
                Quaternion _rotation = Quaternion.Euler(_euler);
                MoveRotation(_rotation);
            }
            if (_t == typeof(Quaternion))
            {
                Quaternion _rotation = (Quaternion)m_ReadInputValue;
                MoveRotation(_rotation);
            }
        }

        private void MovePosition(Vector3 _pos)
        {
            if(m_Axises != Vector3.zero)
            {
                // Move restricted to axis
                _pos.x *= m_Axises.x;
                _pos.y *= m_Axises.y;
                _pos.z *= m_Axises.z;
            }
            
            for (int i = 0; i < m_Targets.Length; i++)
            {
                // Move unrestricted
                m_Targets[i].transform.position = _pos;
            }
        }

        private void MoveRotation(Quaternion _rot)
        {
            if (m_Axises != Vector3.zero)
            {
                // TODO: Rotate restricted to axis X, Y, Z
            }

            for (int i = 0; i < m_Targets.Length; i++)
            {
                m_Targets[i].transform.rotation = _rot;
            }
        }

        private void MoveScale(Vector3 _sca)
        {
            for(int i = 0; i < m_Targets.Length; i++)
            {
                m_Targets[i].transform.localScale = _sca;
            }
        }

        private IEnumerator StartWithDelay(float _time)
        {
            yield return new WaitForSeconds(_time);
            Execute();
        }
    }
}