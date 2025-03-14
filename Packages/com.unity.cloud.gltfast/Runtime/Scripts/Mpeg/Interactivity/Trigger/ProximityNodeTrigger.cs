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
using System;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Represent the proximity trigger at the node level
    /// </summary>
    public class ProximityNodeTrigger : MonoBehaviour
    {
        private float m_DistanceLowerLimit;
        private float m_DistanceUpperLimit;

        private float m_UpperDistanceWeight;
        private float m_LowerDistanceWeight;
        private bool m_AllowOcclusion;
        private GameObject[] m_Primitives;

        private float m_LowerDistance;
        private float m_UpperDistance;

        public void InitSceneAndNodeLevelExtension(MpegNodeInteractivity.Trigger trigger, Trigger _proximityTrigger)
        {
            m_DistanceLowerLimit = _proximityTrigger.distanceLowerLimit;
            m_DistanceUpperLimit = _proximityTrigger.distanceUpperLimit;

            m_AllowOcclusion = trigger.allowOcclusion;
            m_UpperDistanceWeight = trigger.upperDistanceWeight;
            m_LowerDistanceWeight = trigger.lowerDistanceWeight;
            m_Primitives = new GameObject[trigger.primitives.Length];

            // Lower and upper distance are multiplied by the weight
            m_UpperDistance *= m_UpperDistanceWeight;
            m_LowerDistance *= m_LowerDistanceWeight;
        }

        public void InitSceneLevelExtension(Trigger _proximityTrigger)
        {
            m_DistanceLowerLimit = _proximityTrigger.distanceLowerLimit;
            m_DistanceUpperLimit = _proximityTrigger.distanceUpperLimit;

            m_LowerDistance = m_DistanceLowerLimit;
            m_UpperDistance = m_DistanceUpperLimit;
        }

        internal bool IsClose(GameObject _reference)
        {
            float _distance = Vector3.Distance(_reference.transform.position,
                transform.position);

            if(_distance < m_LowerDistance || _distance > m_UpperDistance)
            {
                return false;
            }

            return true;
        }
    }
}