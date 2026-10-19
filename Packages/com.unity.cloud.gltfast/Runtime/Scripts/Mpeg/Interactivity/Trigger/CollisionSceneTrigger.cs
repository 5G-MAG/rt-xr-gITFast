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

        public void Dispose()
        {
            Destroy(gameObject);
        }

        public void Init(Trigger trigger)
        {
            m_CollisionsDetectors = new List<CollisionNodeTrigger>();
            if(trigger.nodes == null)
            {
                throw new System.Exception("No nodes detected in the collision trigger");
            }

            // Build the collisions normally
            if(trigger.primitives == null)
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
                // Build collisions according to primitives (Table 33: "primitives" used to activate the
                // trigger). Reading: primitive i is a trigger volume in world space, placed by its
                // transformationMatrix, and node i activates the trigger while its collider overlaps it.
                for(int i = 0; i < trigger.primitives.Length; i++)
                {
                    if (i >= trigger.nodes.Length)
                    {
                        Debug.LogWarning("Collision trigger: primitive " + i + " has no node at the same index and is ignored");
                        continue;
                    }

                    GameObject _primitive = CreateCollisionMeshFromPrimitive(trigger.primitives[i]);
                    GameObject _node = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[i]);

                    Matrix4x4 m = trigger.primitives[i].TransformationMatrixValue;
                    _primitive.transform.SetPositionAndRotation(m.GetPosition(), m.rotation);
                    _primitive.transform.localScale = m.lossyScale;

                    // The detector sits on the node, the primitive is its only target
                    CollisionNodeTrigger _detector = InitCollisionDetector(ref _node);
                    _detector.AddTarget(_primitive);
                }
            }
        }

        public bool MeetConditions()
        {
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
                            primitive.CentroidVector);
                    break;

                case Schema.PrimitiveType.BV_PLANE_REGION:
                    target = PrimitivesHelper.PrimitivesCreatePlaneGameObject(primitive.width,
                        primitive.height,
                        primitive.CentroidVector);
                    break;

                case Schema.PrimitiveType.BV_CYLINDER_REGION:
                    target = PrimitivesHelper.PrimitivesCreateCylinderGameObject(primitive.radius,
                        primitive.length,
                        primitive.CentroidVector);
                    break;

                case Schema.PrimitiveType.BV_CAPSULE_REGION:
                    target = PrimitivesHelper.PrimitivesCreateCapsuleGameObject(primitive.radius,
                        primitive.BaseCentroidVector,
                        primitive.TopCentroidVector);
                    break;

                case Schema.PrimitiveType.BV_SPHEROID:
                    target = PrimitivesHelper.PrimitivesCreateSpheroidGameObject(primitive.radius,
                        primitive.CentroidVector);
                    break;
            }

            // Remove renderer from those meshes
            target.GetComponent<Renderer>().enabled = false;
            if (target.TryGetComponent(out Collider col))
            {
                Destroy(col);
            }

            // A trigger volume: it reports overlaps and does not push the node. A kinematic body lets the
            // overlap with a static node be reported too.
            // TODO: Create a custom collision system that handles boundary
            MeshCollider mshCol = target.AddComponent<MeshCollider>();
            mshCol.sharedMesh = target.GetComponent<MeshFilter>().mesh;
            mshCol.convex = true;
            mshCol.isTrigger = true;
            Rigidbody volumeBody = target.AddComponent<Rigidbody>();
            volumeBody.isKinematic = true;
            volumeBody.useGravity = false;

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

                // If the node extension has a collision trigger, it specifies the collision detector behavior
                var _nodeTrigger = NodeInteractivityLookup.Find(_nd, TriggerType.TRIGGER_COLLISION);
                if (_nodeTrigger != null)
                {
                    _collisionDetector.InitExtension(_nodeTrigger);
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
            // Table 33: "Any detection of collision shall activate the trigger".
            bool result = false;

            for (int i = 0; i < m_CollisionsDetectors.Count; i++)
            {
                if (m_CollisionsDetectors[i].Collides)
                {
                    result = true;
                    break;
                }
            }
            m_Collides = result;
        }
    }
}