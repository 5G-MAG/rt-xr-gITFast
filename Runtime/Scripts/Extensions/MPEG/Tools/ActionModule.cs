using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GLTFast {
    public interface MPEG_ActionEvent { }

    public struct MPEG_ActionManipulateEvent: MPEG_ActionEvent
    {
        public InputAction inputAction;
    }


    public class ActionModule : IMPEG_Module<MPEG_ActionEvent>
    {
        private static Dictionary<int, Action<MPEG_ActionEvent>> m_Events;
        private static Dictionary<IMpegInteractivityAction, int> m_Actions;

        private static ActionModule m_Instance;

        public static ActionModule GetInstance()
        {
            if (m_Instance == null)
            {
                m_Instance = new ActionModule();
                m_Events = new Dictionary<int, Action<MPEG_ActionEvent>>();
                m_Actions = new Dictionary<IMpegInteractivityAction, int>();
            }
            return m_Instance;
        }

        internal void OnActionOccurs(IMpegInteractivityAction _trigger, MPEG_ActionEvent _event)
        {
            int _index;

            if (!m_Actions.ContainsKey(_trigger))
            {
                throw new Exception("Not referenced by this  module");
            }

            _index = m_Actions[_trigger];

            if (!m_Events.ContainsKey(_index))
            {
                return;
            }

            m_Events[_index]?.Invoke(_event);
        }

        internal void AddTrigger(int _index, IMpegInteractivityAction _action)
        {
            m_Actions.Add(_action, _index);
        }

        public void Register(Action<MPEG_ActionEvent> _action, int _index)
        {
            if (m_Events.ContainsKey(_index))
            {
                Debug.LogWarning($"Trying to register a method for the same key: {_index}. Previous method will be overwritten");
                m_Events[_index] = _action;
            }
            else
            {
                m_Events.Add(_index, _action);
            }
        }

        public void Unregister(int _index)
        {
            if (!m_Events.ContainsKey(_index))
            {
                Debug.LogError($"This module doesn't contains any method for index {_index}.");
                return;
            }
            m_Events.Remove(_index);
        }
    }
}