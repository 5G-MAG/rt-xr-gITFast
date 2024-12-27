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
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    ///  Represent the collision trigger at the scene level
    /// </summary>
    public class CollisionSceneTrigger : MonoBehaviour, IMpegInteractivityTrigger
    {
        private List<CollisionNodeTrigger> m_CollisionsDetectors;
        private bool m_Collides;
        private MPEG_CollisionEvent m_CollisionEvent;

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Trigger trigger)
        {
            m_CollisionEvent = new MPEG_CollisionEvent();
            m_CollisionsDetectors = new List<CollisionNodeTrigger>();
            if(trigger.nodes == null)
            {
                throw new System.Exception("No nodes detected in the collision trigger");
            }

            // Build the collisions normally
            if (trigger.primitives == null)
            {
                // Add collision detectors by pairs
                for (int i = 0; i < trigger.nodes.Length; i++)
                {
                    GameObject _obj = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[i]);
                    CollisionNodeTrigger _collision = InitCollisionDetector(ref _obj);
                    for (int j = i + 1; j < trigger.nodes.Length; j++)
                    {
                        GameObject _target = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[j]);
                        CollisionNodeTrigger _col = InitCollisionDetector(ref _target);
                        _collision.AddTarget(_target);
                        _col.AddTarget(_obj);
                    }
                }
            }
            else
            {
                // Build collisions according to primitives
                // The node itself isn't considered for collision
                for(int i = 0; i < trigger.primitives.Length; i++)
                {
                    GameObject _primitive = CreateCollisionMeshFromPrimitive(trigger.primitives[i]);
                    GameObject _node = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[i]);

                    // Move the primitive according to the transformation matrix
                    Matrix4x4 m = trigger.primitives[i].transformationMatrix;
                    Vector3 _pos = m.GetPosition();
                    Quaternion _rot = m.rotation;
                    Vector3 _scale = Vector3.one;

                    // Move the world position of the primitive according
                    // to the associated primitive node
                    _primitive.transform.position = _node.transform.position;
                    _primitive.transform.localPosition = _pos;
                    _primitive.transform.rotation = _rot;
                    _primitive.transform.localScale = _scale;
                    InitCollisionDetector(ref _primitive);
                }
            }
        }

        public bool MeetConditions()
        {
            if(m_Collides)
            {
                CollisionModule.GetInstance().OnTriggerCollisionOccurs(this, m_CollisionEvent);
            }

            return m_Collides;
        }

        private GameObject CreateCollisionMeshFromPrimitive(Trigger.Primitive primitive)
        {
            GameObject target = null;

            switch (primitive.type)
            {
                case GLTFast.Schema.PrimitiveType.BV_CUBOID:
                    target = PrimitivesHelper.PrimitivesCreateCuboidGameObject(primitive.width,
                            primitive.height,
                            primitive.length,
                            primitive.centroid);
                    break;

                case Schema.PrimitiveType.BV_PLANE_REGION:
                    target = PrimitivesHelper.PrimitivesCreatePlaneGameObject(primitive.width,
                        primitive.height,
                        primitive.centroid);
                    break;

                case Schema.PrimitiveType.BV_CYLINDER_REGION:
                    target = PrimitivesHelper.PrimitivesCreateCylinderGameObject(primitive.radius,
                        primitive.length,
                        primitive.centroid);
                    break;

                case Schema.PrimitiveType.BV_CAPSULE_REGION:
                    target = PrimitivesHelper.PrimitivesCreateCapsuleGameObject(primitive.radius,
                        primitive.baseCentroid,
                        primitive.topCentroid);
                    break;

                case Schema.PrimitiveType.BV_SPHEROID:
                    target = PrimitivesHelper.PrimitivesCreateSpheroidGameObject(primitive.radius,
                        primitive.centroid);
                    break;
            }

            // Remove renderer from those meshes
            target.GetComponent<Renderer>().enabled = false;
            if (target.TryGetComponent(out Collider col))
            {
                Destroy(col);
            }

            // For now we are adding a mesh collider that will handle collisions for us
            // TODO: Create a custom collision system that handles boundary
            MeshCollider mshCol = target.AddComponent<MeshCollider>();
            mshCol.sharedMesh = target.GetComponent<MeshFilter>().mesh;
            mshCol.convex = true;

            return target;
        }

        private CollisionNodeTrigger InitCollisionDetector(ref GameObject obj)
        {
            CollisionNodeTrigger _col = null;

            Node _nd = VirtualSceneGraph.GetNodeFromGameObject(obj);

            // Get the component if exists, else create it
            if (obj.TryGetComponent<CollisionNodeTrigger>(out _col)) { }
            else
            {
                CollisionNodeTrigger _collisionDetector = obj.AddComponent<CollisionNodeTrigger>();

                // If the extensions exists, it means that we want to specify the collision detector behavior
                if (_nd.extensions?.MPEG_node_interactivity != null)
                {
                    // Find if there is any collision trigger to be found
                    for (int i = 0; i < _nd.extensions.MPEG_node_interactivity.triggers.Length; i++)
                    {
                        if (_nd.extensions.MPEG_node_interactivity.triggers[i].type == (int)TriggerType.TRIGGER_COLLISION)
                        {
                            _collisionDetector.InitExtension(_nd.extensions.MPEG_node_interactivity.triggers[i]);
                            break;
                        }
                    }
                }
                else
                {
                    _collisionDetector.Init();
                }

                _col = _collisionDetector;
            }

            _col.AddTarget(obj);

            if (!m_CollisionsDetectors.Contains(_col))
            {
                m_CollisionsDetectors.Add(_col);
            }

            return _col;
        }

        private void Update()
        {
            bool result = true;

            for (int i = 0; i < m_CollisionsDetectors.Count; i++)
            {
                if (!m_CollisionsDetectors[i].Collides)
                {
                    result = false;
                }
            }
            m_Collides = result;
        }
    }
}