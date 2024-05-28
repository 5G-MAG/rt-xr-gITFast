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
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{    
    /// <summary>
    /// Represent the proximity trigger at the scene level
    /// Triggers when the ReferenceNode is above the distance lower limit and 
    /// below the distance upper limit from the other nodes
    /// </summary>
    public class ProximitySceneTrigger : MonoBehaviour, IMpegInteractivityTrigger
    {
        private GameObject m_ReferenceObject;
        private bool m_HasReferenceNode;
        private float m_DistanceLowerLimit;
        private float m_DistanceUpperLimit;
        private List<ProximityNodeTrigger> m_Targets;

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Trigger trigger)
        {
            m_Targets = new List<ProximityNodeTrigger>();
            if(trigger.referenceNode.HasValue)
            {
                m_HasReferenceNode = true;
                m_ReferenceObject = VirtualSceneGraph.GetGameObjectFromIndex(trigger.referenceNode.Value);
            }
            else
            {
                if(UnityEngine.Camera.allCamerasCount == 0)
                {
                    throw new Exception("No reference node attached to the Trigger proximity. Please provide at least one reference node");
                }
            }

            m_DistanceLowerLimit = trigger.distanceLowerLimit;
            m_DistanceUpperLimit = trigger.distanceUpperLimit;

            GetFirstActivatedCamera();


            if (trigger.primitives == null)
            {
                // Create the nodes as usual
                for(int i = 0; i < trigger.nodes.Length; i++)
                {
                    GameObject _target = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[i]);

                    // Initialize proximity detector on all nodes and parse node
                    // level extensions if any
                    ProximityNodeTrigger _proximity = InitProximityDetector(ref _target, trigger);
                    m_Targets.Add(_proximity);
                }
            }
            else
            {
                // We assume that the primitives array is of the same length as node array
                // Add primitives as targets
                for (int i = 0; i < trigger.nodes.Length; i++)
                {
                    Vector3 _nodePos = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[i]).transform.position;

                    GameObject _primitive = null;
                    switch (trigger.primitives[i].type)
                    {
                        case Schema.PrimitiveType.BV_CUBOID:
                            _primitive = PrimitivesHelper.PrimitivesCreateCuboidGameObject(trigger.primitives[i].width,
                                trigger.primitives[i].height,
                                trigger.primitives[i].length,
                                trigger.primitives[i].centroid);
                            break;
                        case Schema.PrimitiveType.BV_PLANE_REGION:
                            _primitive = PrimitivesHelper.PrimitivesCreatePlaneGameObject(trigger.primitives[i].width,
                                trigger.primitives[i].height,
                                trigger.primitives[i].centroid);
                            break;
                        case Schema.PrimitiveType.BV_CYLINDER_REGION:
                            _primitive = PrimitivesHelper.PrimitivesCreateCylinderGameObject(trigger.primitives[i].radius,
                                trigger.primitives[i].length,
                                trigger.primitives[i].centroid);
                            break;
                        case Schema.PrimitiveType.BV_CAPSULE_REGION:
                            _primitive = PrimitivesHelper.PrimitivesCreateCapsuleGameObject(trigger.primitives[i].radius,
                                trigger.primitives[i].baseCentroid,
                                trigger.primitives[i].topCentroid);
                            break;
                        case Schema.PrimitiveType.BV_SPHEROID:
                            _primitive = PrimitivesHelper.PrimitivesCreateSpheroidGameObject(trigger.primitives[i].radius,
                                trigger.primitives[i].centroid);
                            break;
                    }

                    if (_primitive == null)
                    {
                        throw new Exception($"Failed to create Primitive " +
                            $"- type not found: {trigger.primitives[i].type}");
                    }

                    // At the scene level, the primitive applies a transformation
                    // matrix relative to the world. The default trs of the primitive
                    // is then always identity

                    // Move the primitive according to the transformation matrix
                    Matrix4x4 m = trigger.primitives[i].transformationMatrix;
                    Vector3 _pos = m.GetPosition();
                    Quaternion _rot = m.rotation;
                    Vector3 _scale = Vector3.one;

                    _primitive.transform.position = _nodePos;
                    _primitive.transform.localPosition = _pos;
                    _primitive.transform.rotation = _rot;
                    _primitive.transform.localScale = _scale;

                    ProximityNodeTrigger _primitiveProximityDetector =
                        _primitive.AddComponent<ProximityNodeTrigger>();

                    _primitiveProximityDetector.InitSceneLevelExtension(trigger);

                    m_Targets.Add(_primitiveProximityDetector);
                }
            }
        }

        private ProximityNodeTrigger InitProximityDetector(ref GameObject _obj, Trigger _proximityTrigger)
        {
            ProximityNodeTrigger _proximity = _obj.AddComponent<ProximityNodeTrigger>();
            Node _nd = VirtualSceneGraph.GetNodeFromGameObject(_obj);

            // Find if there is any proximity trigger
            // node extenxtion level to be found
            if (_nd.extensions?.MPEG_node_interactivity != null)
            {
                for(int i = 0; i < _nd.extensions.MPEG_node_interactivity.triggers.Length; i++)
                {
                    if (_nd.extensions.MPEG_node_interactivity.triggers[i].type == TriggerType.TRIGGER_PROXIMITY)
                    {
                        _proximity.InitSceneAndNodeLevelExtension(
                            _nd.extensions.MPEG_node_interactivity.triggers[i],
                            _proximityTrigger);
                    }
                }
            }
            else
            {
                _proximity.InitSceneLevelExtension(_proximityTrigger);
            }
            return _proximity;
        }

        private void GetFirstActivatedCamera()
        {
            UnityEngine.Camera[] _cameras = UnityEngine.Camera.allCameras;

            // By default, the reference node is the active camera
            for (int i = 0; i < _cameras.Length; i++)
            {
                // Get the first activated camera by default
                UnityEngine.Camera _cam = _cameras[i];
                m_ReferenceObject = _cam.gameObject;
            }
        }

        private void Update()
        {
            // The proximity trigger should have a reference node and if it doesn't, it
            // should use the active camera
            if(!m_HasReferenceNode)
            {
                GetFirstActivatedCamera();
            }
        }

        public bool MeetConditions()
        {
            bool result = true;
            for (int i = 0; i < m_Targets.Count; i++)
            {
                // TODO: Should take in account the node level extension for each object
                bool _isClose = m_Targets[i].IsClose(m_ReferenceObject);
                if(!_isClose)
                {
                    return false;
                }
                //float distance = Vector3.Distance(m_ReferenceObject.transform.position,
                //    m_Targets[i].transform.position);

                //if (distance < m_DistanceLowerLimit || distance > m_DistanceUpperLimit)
                //{
                //    return false;
                //}
            }
            return result;
        }
    }
}