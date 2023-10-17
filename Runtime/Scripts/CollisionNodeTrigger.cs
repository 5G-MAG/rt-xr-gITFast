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
    ///  Represent the collision trigger at the node level
    /// </summary>
    public class CollisionNodeTrigger : MonoBehaviour
    {
        private bool m_HasInit = false;
        private List<GameObject> m_Target;

        public bool Collides => m_Collides;

        private bool m_Collides = false;

        private Rigidbody m_Rb;
        private PhysicMaterial m_PhysicMat;

        public void Init()
        {
            Debug.Log("Init collision detector");
            if (!m_HasInit)
            {
                m_Target = new List<GameObject>();
                m_HasInit = true;

                // Check if this object has every components to perform collision
                if (!gameObject.TryGetComponent(out m_Rb))
                {
                    m_Rb = gameObject.AddComponent<Rigidbody>();
                }
            }

            // Set the default values for rigidbody
            m_Rb.useGravity = false;
            m_Rb.isKinematic = false;

            // Assign default collider as mesh node (if set)
            if (!TryGetComponent<Collider>(out Collider _col))
            {
                GLTFast.Schema.Node node = VirtualSceneGraph.GetNodeFromGameObject(gameObject);
                int meshIndex = node.mesh;

                if (meshIndex < 0)
                {
                    int nodeIndex = VirtualSceneGraph.GetNodeIndexFromGameObject(gameObject);
                    Debug.LogWarning($"No mesh found for node index: {nodeIndex}. Assign default box collider");
                    BoxCollider _collider = gameObject.AddComponent<BoxCollider>();
                }
                else
                {
                    MeshCollider _meshCol = gameObject.AddComponent<MeshCollider>();
                    UnityEngine.Mesh _msh = VirtualSceneGraph.GetMeshFromIndex(meshIndex);
                    _meshCol.sharedMesh = _msh;
                    _meshCol.convex = true;
                }
            }

            m_Rb.mass = 1.0f;
        }

        public void InitExtension(MpegNodeInteractivity.Trigger node_info)
        {
            Init();

            if (!TryGetComponent(out Collider _col))
            {
                MeshCollider _meshCol = gameObject.AddComponent<MeshCollider>();
                UnityEngine.Mesh _msh = VirtualSceneGraph.GetMeshFromIndex(node_info.collider);
                _meshCol.sharedMesh = _msh;
                _meshCol.convex = true;
            }
            if (node_info.isStatic)
            {
                m_Rb.isKinematic = true;
            }

            if (node_info.usePhysics)
            {
                m_Rb.useGravity = node_info.useGravity;
                m_Rb.mass = node_info.mass;
                if (TryGetComponent<MeshCollider>(out MeshCollider _meshCol))
                {
                    m_PhysicMat = new PhysicMaterial();
                    m_PhysicMat.bounciness = node_info.restitution;
                    m_PhysicMat.staticFriction = node_info.staticFriction;
                    m_PhysicMat.dynamicFriction = node_info.dynamicFriction;
                    _meshCol.material = m_PhysicMat;
                }
            }
        }

        public void AddTarget(GameObject obj)
        {
            if (!m_Target.Contains(obj) && obj != gameObject)
            {
                m_Target.Add(obj);
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (m_Target.Contains(other.gameObject))
            {
                m_Collides = true;
            }
        }

        private void OnCollisionStay(Collision other)
        {
            if (m_Target.Contains(other.gameObject))
            {
                m_Collides = true;
            }
        }

        private void OnCollisionExit(Collision other)
        {
            if (m_Target.Contains(other.gameObject))
            {
                m_Collides = false;
            }
        }
    }
}
