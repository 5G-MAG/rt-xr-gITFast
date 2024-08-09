using GLTFast.Schema;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    public enum TrackableEventType
    {
        ADDED,
        UPDATED,
        REMOVED
    }

    public struct MPEG_TrackableEvent
    {
        public TrackableType trackableType;
        public TrackableEventType trackableEventType;
    }

    public class TrackableModule : IMPEG_Module<MPEG_TrackableEvent>
    {
        private static Dictionary<int, Action<MPEG_TrackableEvent>> m_Events;
        private static Dictionary<IMpegTrackable, int> m_Trackables;
        private static TrackableModule m_Instance;

        public static TrackableModule GetInstance()
        {
            if (m_Instance == null)
            {
                m_Instance = new TrackableModule();
                m_Events = new Dictionary<int, Action<MPEG_TrackableEvent>>();
                m_Trackables = new Dictionary<IMpegTrackable, int>();
            }
            return m_Instance;
        }

        internal void OnAnchoringOccurs(IMpegTrackable _behaviour, MPEG_TrackableEvent _event)
        {
            int _index;

            if (!m_Trackables.ContainsKey(_behaviour))
            {
                throw new Exception("Not referenced by this  module");
            }

            _index = m_Trackables[_behaviour];

            if (!m_Events.ContainsKey(_index))
            {
                return;
            }

            m_Events[_index]?.Invoke(_event);
        }

        internal void AddTrackable(int _index, IMpegTrackable _behavior)
        {
            m_Trackables.Add(_behavior, _index);
        }

        public void Register(Action<MPEG_TrackableEvent> _action, int _index)
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