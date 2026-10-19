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
        private List<System.Action<GameObject>> m_GameEngineActions;

        // Targets currently in contact (collision) or overlapping (trigger volume): the flag stays set
        // until the last of them leaves.
        private readonly HashSet<GameObject> m_Touching = new HashSet<GameObject>();

        public bool Collides
        {
            get
            {
                m_Touching.RemoveWhere(g => g == null);
                return m_Touching.Count > 0;
            }
        }

        private Rigidbody m_Rb;
#if UNITY_6000_0_OR_NEWER
        private PhysicsMaterial m_PhysicMat;
#else
        private PhysicMaterial m_PhysicMat;
#endif        
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
                m_GameEngineActions = new List<System.Action<GameObject>>();
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
                    UnityEngine.Mesh _msh = VirtualSceneGraph.TryGetMeshOfGltfMesh(meshIndex);
                    if (_msh == null) _msh = VirtualSceneGraph.GetMeshFromIndex(meshIndex);
                    _meshCol.sharedMesh = _msh;
                    _meshCol.convex = true;
                }
            }

            m_Rb.mass = 1.0f;
        }

        public void InitExtension(MpegNodeInteractivity.Trigger node_info)
        {
            // Table 52 collider: the mesh that provides the collider geometry of this node, one collider
            // for each primitive of that mesh, also when no node uses the mesh. When the mesh does not
            // exist, Init() falls back to the node mesh.
            if (!TryGetComponent(out Collider _existing))
            {
                UnityEngine.Mesh[] _colliderMeshes = VirtualSceneGraph.GetMeshesOfGltfMesh(node_info.collider);
                int _added = 0;
                for (int m = 0; m < _colliderMeshes.Length; m++)
                {
                    if (_colliderMeshes[m] == null) continue;
                    MeshCollider _colMeshCol = gameObject.AddComponent<MeshCollider>();
                    _colMeshCol.sharedMesh = _colliderMeshes[m];
                    _colMeshCol.convex = true;
                    _added++;
                }
                if (_added == 0)
                {
                    Debug.LogWarning($"Collider mesh {node_info.collider} does not exist, using the node mesh as collider");
                }
            }

            Init();

            // isStatic: static collider. usePhysics false: the physics simulation does not move the
            // node (all axes frozen), the collider still takes part in collision detection.
            m_Rb.isKinematic = node_info.isStatic;
            if (!node_info.usePhysics && !node_info.isStatic)
            {
                m_Rb.useGravity = false;
                m_Rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            if (node_info.usePhysics)
            {
                m_Rb.useGravity = node_info.useGravity;
                m_Rb.mass = node_info.mass;
                // Table 52 damping: V(t+dt) = V(t)*(1-damping*dt), the Unity drag formula.
                m_Rb.drag = Mathf.Max(0f, node_info.linearDamping);
                m_Rb.angularDrag = Mathf.Max(0f, node_info.angularDamping);
                if (node_info.needPreciseCollisionDetection && !node_info.isStatic)
                {
                    m_Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
                if (TryGetComponent<MeshCollider>(out MeshCollider _meshCol))
                {
#if UNITY_6000_0_OR_NEWER
                    m_PhysicMat = new PhysicsMaterial();
#else
                    m_PhysicMat = new PhysicMaterial();
#endif
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

        private void Touch(GameObject other)
        {
            if (!m_Target.Contains(other)) return;
            m_Touching.Add(other);
            for (int i = 0; i < m_GameEngineActions.Count; i++)
            {
                m_GameEngineActions[i]?.Invoke(other);
            }
        }

        private void Untouch(GameObject other)
        {
            m_Touching.Remove(other);
        }

        private void OnDisable() { m_Touching.Clear(); }

        private void OnCollisionEnter(Collision other) { Touch(other.gameObject); }
        private void OnCollisionStay(Collision other) { Touch(other.gameObject); }
        private void OnCollisionExit(Collision other) { Untouch(other.gameObject); }

        // Trigger volumes (the primitives of a collision trigger) report overlaps, not contacts
        private void OnTriggerEnter(Collider other) { Touch(other.gameObject); }
        private void OnTriggerStay(Collider other) { Touch(other.gameObject); }
        private void OnTriggerExit(Collider other) { Untouch(other.gameObject); }

        public void AddCollisionCallback(System.Action<GameObject> _act) {
            m_GameEngineActions.Add(_act);
        }
    }
}
