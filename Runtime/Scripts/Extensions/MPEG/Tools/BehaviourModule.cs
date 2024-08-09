using System;
using System.Collections.Generic;
using UnityEngine;


namespace GLTFast
{
    public struct MPEG_BehaviourEvent
    {

    }

    public class BehaviourModule : IMPEG_Module<MPEG_BehaviourEvent>
    {
        private static Dictionary<int, Action<MPEG_BehaviourEvent>> m_Events;
        private static Dictionary<IMpegInteractivityBehavior, int> m_Behaviours;
        private static BehaviourModule m_Instance;

        public static BehaviourModule GetInstance()
        {
            if (m_Instance == null)
            {
                m_Instance = new BehaviourModule();
                m_Events = new Dictionary<int, Action<MPEG_BehaviourEvent>>();
                m_Behaviours = new Dictionary<IMpegInteractivityBehavior, int>();
            }
            return m_Instance;
        }

        internal void OnBehaviourOccurs(IMpegInteractivityBehavior _behaviour, MPEG_BehaviourEvent _event)
        {
            int _index;

            if (!m_Behaviours.ContainsKey(_behaviour))
            {
                throw new Exception("Not referenced by this  module");
            }

            _index = m_Behaviours[_behaviour];

            if (!m_Events.ContainsKey(_index))
            {
                return;
            }

            m_Events[_index]?.Invoke(_event);
        }

        internal void AddBehaviour(int _index, IMpegInteractivityBehavior _behavior)
        {
            m_Behaviours.Add(_behavior, _index);
        }

        public void Register(Action<MPEG_BehaviourEvent> _action, int _index)
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