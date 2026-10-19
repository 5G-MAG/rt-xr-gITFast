// MPEG_mesh_linking (ISO/IEC 23090-14:2024(E), 5.3.2, 7.4, 7.5 and informative Annex E): a dependent
// mesh is glued to the faces of a shadow mesh and follows the shadow mesh when that is animated.
// MpegMeshLinkingMath holds the equations of Annex E. MpegMeshLinkingController reads the timed
// correspondence, pose and weights accessors and moves the vertices of the dependent mesh.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast {

    using Schema;

    /// <summary>
    /// Equations of Annex E, in Unity space (all positions use the same conversion).
    /// </summary>
    public static class MpegMeshLinkingMath {

        /// <summary>Relative position of a dependent vertex with respect to a shadow face.</summary>
        public struct Binding {
            public bool valid;
            /// <summary>Barycentric weight of the third face vertex (pi,u).</summary>
            public float u;
            /// <summary>Barycentric weight of the second face vertex (pi,v).</summary>
            public float v;
            /// <summary>Barycentric weight of the first face vertex (pi,w).</summary>
            public float w;
            /// <summary>si(t) * di(t): distance to the face plane with the sign of the face normal direction.</summary>
            public float signedDistance;
        }

        /// <summary>
        /// E.2 to E.9: projects the dependent vertex vd onto the plane of the face (vj, vk, vl), taken at the
        /// link pose, and records distance and barycentric coordinates. The barycentric weights are the
        /// orientation-consistent ratios of signed areas, so that w * vj + v * vk + u * vl is the projected point.
        /// </summary>
        public static Binding Bind(Vector3 vd, Vector3 vj, Vector3 vk, Vector3 vl) {
            var b = new Binding();
            Vector3 cross = Vector3.Cross(vk - vj, vl - vj);
            float len = cross.magnitude;
            if (len < 1e-12f) {
                return b;
            }
            Vector3 n = cross / len;                                  // E.2
            Vector3 p = vd + Vector3.Dot(vj - vd, n) * n;             // E.3
            b.signedDistance = Vector3.Dot(vd - p, n);                // E.4 and E.5
            float area = 0.5f * len;
            b.u = 0.5f * Vector3.Dot(Vector3.Cross(vk - vj, p - vj), n) / area;   // E.6
            b.v = 0.5f * Vector3.Dot(Vector3.Cross(p - vj, vl - vj), n) / area;   // E.7 (orientation-consistent)
            b.w = 1f - b.u - b.v;                                     // E.9
            b.valid = true;
            return b;
        }

        /// <summary>E.10 to E.12: the dependent vertex for the face (vj, vk, vl) after the shadow mesh was transformed.</summary>
        public static Vector3 Reconstruct(Binding b, Vector3 vj, Vector3 vk, Vector3 vl) {
            Vector3 p = b.w * vj + b.v * vk + b.u * vl;               // E.10
            Vector3 cross = Vector3.Cross(vk - vj, vl - vj);
            float len = cross.magnitude;
            if (len < 1e-12f) {
                return p;
            }
            return p + b.signedDistance * (cross / len);              // E.11 and E.12
        }

        /// <summary>
        /// E.1: the shadow mesh at the pose of the dependent mesh. Morph targets are added to the rest vertex,
        /// then the vertex is skinned with the pose matrices. A null pose or null bone weights keeps the skinning step out.
        /// </summary>
        public static Vector3[] PoseShadow(Vector3[] rest, BoneWeight[] bones, Vector3[][] morphDeltas, float[] weights, Matrix4x4[] pose) {
            var result = new Vector3[rest.Length];
            for (int i = 0; i < rest.Length; i++) {
                Vector3 v = rest[i];
                if (morphDeltas != null && weights != null) {
                    int targets = Math.Min(weights.Length, morphDeltas.Length);
                    for (int j = 0; j < targets; j++) {
                        if (morphDeltas[j] != null && i < morphDeltas[j].Length) {
                            v += weights[j] * morphDeltas[j][i];
                        }
                    }
                }
                if (pose == null || bones == null || i >= bones.Length) {
                    result[i] = v;
                    continue;
                }
                BoneWeight bw = bones[i];
                Vector3 sum = Vector3.zero;
                float total = 0f;
                Accumulate(pose, bw.boneIndex0, bw.weight0, v, ref sum, ref total);
                Accumulate(pose, bw.boneIndex1, bw.weight1, v, ref sum, ref total);
                Accumulate(pose, bw.boneIndex2, bw.weight2, v, ref sum, ref total);
                Accumulate(pose, bw.boneIndex3, bw.weight3, v, ref sum, ref total);
                result[i] = total > 0f ? sum : v;
            }
            return result;
        }

        static void Accumulate(Matrix4x4[] pose, int bone, float weight, Vector3 v, ref Vector3 sum, ref float total) {
            if (weight <= 0f) return;
            Vector3 t = (bone >= 0 && bone < pose.Length) ? pose[bone].MultiplyPoint3x4(v) : v;
            sum += weight * t;
            total += weight;
        }
    }

    public class MpegMeshLinkingController : MonoBehaviour {

        MpegSceneContext m_Context;
        MpegMeshLinking m_Link;
        GltfComponentType m_CorrespondenceType;

        // Shadow mesh (concatenation of its primitives), Unity space
        Vector3[] m_ShadowRest;
        int[] m_ShadowTriangles;
        BoneWeight[] m_ShadowBones;
        Vector3[][] m_ShadowMorph;

        // Dependent mesh (concatenation of the primitives of the node)
        readonly List<UnityEngine.Mesh> m_DependentMeshes = new List<UnityEngine.Mesh>();
        readonly List<int> m_DependentOffsets = new List<int>();
        Vector3[] m_DependentRest;

        // Unity meshes of the shadow mesh primitives, in the order of the concatenated vertices
        readonly List<UnityEngine.Mesh> m_ShadowMeshes = new List<UnityEngine.Mesh>();

        // Instances of the shadow mesh primitives that the animation drives, one renderer per primitive in
        // primitive order; empty when the scene has no such instance
        readonly List<SkinnedMeshRenderer> m_ShadowHosts = new List<SkinnedMeshRenderer>();
        readonly List<UnityEngine.Mesh> m_BakeTargets = new List<UnityEngine.Mesh>();

        // Position in a pose sample of the matrix for each joint of the skin of the shadow mesh instance.
        // 7.5.2.3: node_index[i] is the node the i-th matrix of a sample applies to. The sample entry is read by the
        // media access function and is handed over with SetPoseNodes; without it the i-th matrix is taken to belong
        // to the i-th joint of the skin (the text does not say how the matrices are ordered, unspecified).
        // 7.5.3.3 does not say whether a matrix includes the inverse bind matrix; it is used as the complete
        // skinning transformation in the space of the shadow mesh (unspecified).
        int[] m_JointToPosition;

        static readonly Dictionary<int, int[]> s_PoseNodes = new Dictionary<int, int[]>();

        /// <summary>
        /// Hands over the node_index list of the 'post' sample entry (7.5.2.2) for the pose accessor, so that matrices
        /// are assigned to the joints of the skin by node and not by position.
        /// </summary>
        public static void SetPoseNodes(int poseAccessor, int[] nodeIndices) {
            lock (s_PoseNodes) {
                s_PoseNodes[poseAccessor] = nodeIndices;
            }
        }

        bool m_Warned;
        bool m_Ready;
        bool m_Modified;

        public void Init(MpegSceneContext context, int dependentMeshIndex, MpegMeshLinking link, GameObject dependentNode) {
            m_Context = context;
            m_Link = link;
            var root = context.Root;

            if (!MpegConvert.CheckAccessor(root, link.pose, GltfComponentType.Float, GltfAccessorAttributeType.MAT4, "MPEG_mesh_linking.pose")) return;
            if (link.weights >= 0 &&
                !MpegConvert.CheckAccessor(root, link.weights, GltfComponentType.Float, GltfAccessorAttributeType.SCALAR, "MPEG_mesh_linking.weights")) {
                link.weights = -1;
            }
            if (link.correspondence >= root.accessors.Length) {
                MpegWarn.Once("mesh_linking/correspondence", "MPEG_mesh_linking: correspondence accessor does not exist");
                return;
            }
            var corr = root.accessors[link.correspondence];
            m_CorrespondenceType = corr.componentType;
            if (corr.GetAttributeType() != GltfAccessorAttributeType.SCALAR ||
                (corr.componentType != GltfComponentType.UnsignedByte && corr.componentType != GltfComponentType.UnsignedShort && corr.componentType != GltfComponentType.UnsignedInt)) {
                MpegWarn.Once("mesh_linking/corrtype", "MPEG_mesh_linking: correspondence must be SCALAR with UNSIGNED_BYTE, UNSIGNED_SHORT or UNSIGNED_INT (7.4.3.4)");
                return;
            }
            MpegTimedAccessors.Want(link.correspondence);
            MpegTimedAccessors.Want(link.pose);
            if (link.weights >= 0) MpegTimedAccessors.Want(link.weights);

            if (!CaptureShadow(link.mesh)) return;
            if (!CaptureDependent(dependentMeshIndex)) return;

            // The animated instance of the shadow mesh, if a node of the scene references it: one skinned
            // renderer for each primitive of the shadow mesh
            foreach (int n in context.NodeIndices) {
                if (root.nodes[n].mesh != link.mesh) continue;
                var renderers = context.NodeObjects[n].GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var hosts = new List<SkinnedMeshRenderer>();
                foreach (var shadowMesh in m_ShadowMeshes) {
                    SkinnedMeshRenderer found = null;
                    foreach (var r in renderers) {
                        if (r.sharedMesh == shadowMesh) { found = r; break; }
                    }
                    if (found == null) { hosts = null; break; }
                    hosts.Add(found);
                }
                if (hosts == null) continue;
                m_ShadowHosts.AddRange(hosts);
                SetUpJointMap(root, n, link.pose);
                break;
            }
            if (m_ShadowHosts.Count == 0) {
                MpegWarn.Once("mesh_linking/host",
                    "MPEG_mesh_linking: no node of the scene instantiates the shadow mesh with a skin, so nothing animates it; the dependent mesh is rendered as a regular mesh (5.3.2.1 NOTE)");
            } else {
                foreach (var h in m_ShadowHosts) m_BakeTargets.Add(new UnityEngine.Mesh());
            }
            m_Ready = true;
        }

        void SetUpJointMap(Root root, int shadowNode, int poseAccessor) {
            int skinIndex = root.nodes[shadowNode].skin;
            if (skinIndex < 0 || root.skins == null || skinIndex >= root.skins.Length || root.skins[skinIndex].joints == null) return;
            var joints = root.skins[skinIndex].joints;
            int[] listed;
            lock (s_PoseNodes) {
                s_PoseNodes.TryGetValue(poseAccessor, out listed);
            }
            m_JointToPosition = new int[joints.Length];
            for (int j = 0; j < joints.Length; j++) {
                m_JointToPosition[j] = j;
                if (listed == null) continue;
                m_JointToPosition[j] = Array.IndexOf(listed, (int)joints[j]);
            }
        }

        bool CaptureShadow(int meshIndex) {
            var meshes = m_Context.Import.GetMeshPrimitiveMeshes(meshIndex);
            if (meshes == null || meshes.Length == 0) {
                MpegWarn.Once("mesh_linking/shadowmesh", "MPEG_mesh_linking: the shadow mesh was not imported");
                return false;
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var bones = new List<BoneWeight>();
            int maxTargets = 0;
            foreach (var m in meshes) {
                if (m == null) continue;
                if (!m.isReadable) {
                    MpegWarn.Once("mesh_linking/readable", "MPEG_mesh_linking: the shadow mesh is not readable");
                    return false;
                }
                maxTargets = Math.Max(maxTargets, m.blendShapeCount);
                m_ShadowMeshes.Add(m);
            }
            m_ShadowMorph = new Vector3[maxTargets][];
            for (int t = 0; t < maxTargets; t++) m_ShadowMorph[t] = new Vector3[0];
            var morphLists = new List<Vector3>[maxTargets];
            for (int t = 0; t < maxTargets; t++) morphLists[t] = new List<Vector3>();

            foreach (var m in meshes) {
                if (m == null) continue;
                int offset = verts.Count;
                int count = m.vertexCount;
                verts.AddRange(m.vertices);
                foreach (int idx in m.triangles) tris.Add(idx + offset);
                var bw = m.boneWeights;
                if (bw != null && bw.Length == count) {
                    bones.AddRange(bw);
                } else {
                    for (int i = 0; i < count; i++) bones.Add(new BoneWeight());
                }
                for (int t = 0; t < maxTargets; t++) {
                    if (t < m.blendShapeCount && m.GetBlendShapeFrameCount(t) > 0) {
                        var dv = new Vector3[count];
                        var dn = new Vector3[count];
                        var dt = new Vector3[count];
                        m.GetBlendShapeFrameVertices(t, 0, dv, dn, dt);
                        float frameWeight = m.GetBlendShapeFrameWeight(t, 0);
                        float k = Mathf.Abs(frameWeight) > 1e-6f ? 1f / frameWeight : 1f;
                        for (int i = 0; i < count; i++) morphLists[t].Add(dv[i] * k);
                    } else {
                        for (int i = 0; i < count; i++) morphLists[t].Add(Vector3.zero);
                    }
                }
            }
            for (int t = 0; t < maxTargets; t++) m_ShadowMorph[t] = morphLists[t].ToArray();
            m_ShadowRest = verts.ToArray();
            m_ShadowTriangles = tris.ToArray();
            m_ShadowBones = bones.ToArray();
            return m_ShadowRest.Length > 0 && m_ShadowTriangles.Length >= 3;
        }

        // The dependent vertex set is the vertices of the primitives of the mesh that carries the extension, in
        // primitive order; meshes of child nodes are not part of it (7.4.1).
        bool CaptureDependent(int meshIndex) {
            var seen = new HashSet<UnityEngine.Mesh>();
            var all = new List<Vector3>();
            var own = m_Context.Import.GetMeshPrimitiveMeshes(meshIndex);
            if (own != null) {
                foreach (var m in own) {
                    AddDependent(m, seen, all);
                }
            }
            if (all.Count == 0 || m_DependentMeshes.Count == 0) {
                MpegWarn.Once("mesh_linking/dependent", "MPEG_mesh_linking: the dependent mesh has no readable vertices");
                return false;
            }
            m_DependentRest = all.ToArray();
            return true;
        }

        void AddDependent(UnityEngine.Mesh mesh, HashSet<UnityEngine.Mesh> seen, List<Vector3> all) {
            if (mesh == null || !mesh.isReadable || !seen.Add(mesh)) return;
            m_DependentMeshes.Add(mesh);
            m_DependentOffsets.Add(all.Count);
            all.AddRange(mesh.vertices);
        }

        int[] ReadCorrespondence(MpegTimedSample s) {
            if (s.data == null) return new int[0];
            int size = m_CorrespondenceType == GltfComponentType.UnsignedByte ? 1 : (m_CorrespondenceType == GltfComponentType.UnsignedShort ? 2 : 4);
            int count = s.count > 0 ? Math.Min(s.count, s.data.Length / size) : s.data.Length / size;
            var result = new int[count];
            for (int i = 0; i < count; i++) {
                if (size == 1) result[i] = s.data[i];
                else if (size == 2) result[i] = BitConverter.ToUInt16(s.data, i * 2);
                else result[i] = (int)BitConverter.ToUInt32(s.data, i * 4);
            }
            return result;
        }

        void Update() {
            // Without an animated shadow mesh the shadow mesh stays at the link pose and the dependent mesh does not move.
            if (!m_Ready || m_ShadowHosts.Count == 0) return;

            MpegTimedSample cs;
            if (!MpegTimedAccessors.TryGetLatest(m_Link.correspondence, out cs)) return;
            int[] faces = ReadCorrespondence(cs);
            // 7.4.1: the number of correspondence values and the number of vertices shall be the same
            if (faces.Length != m_DependentRest.Length) {
                MpegWarn.Once("mesh_linking/count", "MPEG_mesh_linking: correspondence has " + faces.Length + " values for " + m_DependentRest.Length + " vertices, the sample is ignored");
                return;
            }

            // Step 1 (5.3.2.3): the shadow mesh at the pose of the dependent mesh
            Matrix4x4[] pose = null;
            MpegTimedSample ps;
            if (MpegTimedAccessors.TryGetLatest(m_Link.pose, out ps)) {
                var f = ps.ToFloats();
                int n = f.Length / 16;
                // 7.5.3.4: the count of the pose accessor equals number_of_nodes of the sample entry (when it is known)
                int[] listedNodes;
                lock (s_PoseNodes) {
                    s_PoseNodes.TryGetValue(m_Link.pose, out listedNodes);
                }
                if (listedNodes != null && listedNodes.Length != n) {
                    MpegWarn.Once("mesh_linking/posecount", "MPEG_mesh_linking: pose sample has " + n + " matrices, the sample entry lists " + listedNodes.Length + " nodes; the sample is ignored");
                    return;
                }
                var sample = new Matrix4x4[n];
                for (int i = 0; i < n; i++) sample[i] = MpegConvert.Matrix(f, i * 16);
                pose = ToJointOrder(sample);
            }
            float[] weights = null;
            MpegTimedSample ws;
            if (m_Link.weights >= 0 && MpegTimedAccessors.TryGetLatest(m_Link.weights, out ws)) {
                weights = ws.ToFloats();
                // 7.5.1: one weight per morph target of the shadow mesh
                if (weights.Length != m_ShadowMorph.Length) {
                    MpegWarn.Once("mesh_linking/weights", "MPEG_mesh_linking: " + weights.Length + " weights for " + m_ShadowMorph.Length + " morph targets of the shadow mesh");
                }
            }
            Vector3[] linked = MpegMeshLinkingMath.PoseShadow(m_ShadowRest, m_ShadowBones, m_ShadowMorph, weights, pose);

            // Step 2: the shadow mesh as animated, in its own mesh space
            var animatedList = new List<Vector3>(linked.Length);
            for (int h = 0; h < m_ShadowHosts.Count; h++) {
                m_ShadowHosts[h].BakeMesh(m_BakeTargets[h]);
                animatedList.AddRange(m_BakeTargets[h].vertices);
            }
            Vector3[] animated = animatedList.ToArray();
            if (animated.Length != linked.Length) {
                MpegWarn.Once("mesh_linking/baked", "MPEG_mesh_linking: the animated shadow mesh has " + animated.Length + " vertices, " + linked.Length + " were imported; the sample is ignored");
                return;
            }

            var result = new Vector3[m_DependentRest.Length];
            for (int i = 0; i < result.Length; i++) {
                result[i] = m_DependentRest[i];
                int face = faces[i];
                if (face < 0) continue;
                long tri = (long)face * 3;
                if (tri + 2 >= m_ShadowTriangles.Length) continue;
                int a = m_ShadowTriangles[(int)tri], b = m_ShadowTriangles[(int)tri + 1], c = m_ShadowTriangles[(int)tri + 2];
                var binding = MpegMeshLinkingMath.Bind(m_DependentRest[i], linked[a], linked[b], linked[c]);
                if (!binding.valid) continue;
                result[i] = MpegMeshLinkingMath.Reconstruct(binding, animated[a], animated[b], animated[c]);
            }

            for (int m = 0; m < m_DependentMeshes.Count; m++) {
                var mesh = m_DependentMeshes[m];
                if (mesh == null) continue;
                int offset = m_DependentOffsets[m];
                var part = new Vector3[mesh.vertexCount];
                Array.Copy(result, offset, part, 0, part.Length);
                mesh.vertices = part;
                mesh.RecalculateBounds();
                // moved vertices change the surface orientation; the clause does not mention normals
                mesh.RecalculateNormals();
                m_Modified = true;
            }
        }

        // Reorders the matrices of a pose sample by joint of the skin; a joint without a matrix keeps the shadow mesh unskinned for it.
        Matrix4x4[] ToJointOrder(Matrix4x4[] sample) {
            if (m_JointToPosition == null) return sample;
            var ordered = new Matrix4x4[m_JointToPosition.Length];
            for (int j = 0; j < ordered.Length; j++) {
                int pos = m_JointToPosition[j];
                if (pos >= 0 && pos < sample.Length) {
                    ordered[j] = sample[pos];
                } else {
                    ordered[j] = Matrix4x4.identity;
                    if (!m_Warned) {
                        m_Warned = true;
                        MpegWarn.Once("mesh_linking/pose", "MPEG_mesh_linking: a joint of the shadow mesh skin has no matrix in the pose sample; identity is used");
                    }
                }
            }
            return ordered;
        }

        void OnDestroy() {
            foreach (var t in m_BakeTargets) if (t != null) Destroy(t);
            // restore the rest positions so a reload starts from the file content
            if (m_DependentRest == null || !m_Modified) return;
            for (int m = 0; m < m_DependentMeshes.Count; m++) {
                var mesh = m_DependentMeshes[m];
                if (mesh == null) continue;
                var part = new Vector3[mesh.vertexCount];
                Array.Copy(m_DependentRest, m_DependentOffsets[m], part, 0, part.Length);
                mesh.vertices = part;
            }
        }
    }
}
