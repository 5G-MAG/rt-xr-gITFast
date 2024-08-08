using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    public struct MPEG_CollisionEvent
    {
        public int index;
    }

    public class CollisionModule : IMPEG_Module<MPEG_CollisionEvent>
    {
        private static Dictionary<int, Action<MPEG_CollisionEvent>> m_Events;
        private static Dictionary<IMpegInteractivityTrigger, int> m_Triggers;

        private static CollisionModule m_Instance;

        public static CollisionModule GetInstance()
        {
            if (m_Instance == null)
            {
                m_Instance = new CollisionModule();
                m_Events = new Dictionary<int, Action<MPEG_CollisionEvent>>();
                m_Triggers = new Dictionary<IMpegInteractivityTrigger, int>();
            }
            return m_Instance;
        }

        internal void OnTriggerCollisionOccurs(IMpegInteractivityTrigger _trigger, MPEG_CollisionEvent _event)
        {
            int _index;

            if(!m_Triggers.ContainsKey(_trigger))
            {
                throw new Exception("Collision occurs on a trigger that is not referenced by the collision module");
            }

            _index = m_Triggers[_trigger];
            
            if(!m_Events.ContainsKey(_index))
            {
                return;
            }

            m_Events[_index]?.Invoke(_event);
        }

        internal void AddTrigger(int _index, IMpegInteractivityTrigger _trigger)
        {
            m_Triggers.Add(_trigger, _index);
        }

        public void Register(Action<MPEG_CollisionEvent> _action, int _index)
        {
            if(m_Events.ContainsKey(_index))
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
            if(!m_Events.ContainsKey(_index))
            {
                Debug.LogError($"CollisionModule event doesn't contains any method for index {_index}.");
                return;
            }
            m_Events.Remove(_index);
        }
    }
}