// MPEG_node_avatar (ISO/IEC 23090-14:2024(E), 8.3): a node whose referenced meshes are
// reconstructed from an avatar representation identified by a URN. Reconstruction of an avatar
// representation is not defined by the text (the representation scheme defines the components),
// so the meshes render as ordinary glTF. This component keeps the parsed type and the
// mappings from avatar path (8.3.4) to node objects, validates the path rule of 8.3.2, and
// lets an application or an interactivity action look up a part of the avatar by its path.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast {

    using Schema;

    public class MpegNodeAvatarComponent : MonoBehaviour {

        /// <summary>URN of the MPEG reference avatar (8.3.3).</summary>
        public const string ReferenceAvatarType = "urn:mpeg:sd:2023:avatar";

        public string AvatarType { get; private set; }
        public bool IsReferenceAvatar { get { return AvatarType == ReferenceAvatarType; } }

        /// <summary>Node object by normalised avatar path (leading "/").</summary>
        public IReadOnlyDictionary<string, GameObject> Parts { get { return m_Parts; } }

        readonly Dictionary<string, GameObject> m_Parts = new Dictionary<string, GameObject>();

        public static string NormalisePath(string path) {
            if (string.IsNullOrEmpty(path)) return "";
            return path[0] == '/' ? path : "/" + path;
        }

        public void Init(MpegSceneContext context, int nodeIndex, MpegNodeAvatar avatar) {
            AvatarType = avatar.type;
            if (string.IsNullOrEmpty(avatar.type)) {
                MpegWarn.Once("avatar/type", "MPEG_node_avatar: type is mandatory (Table 53)");
            }
            if (avatar.mappings == null) {
                MpegWarn.Once("avatar/mappings", "MPEG_node_avatar: mappings is mandatory (Table 53)");
                return;
            }
            var root = context.Root;
            var pathOfNode = new Dictionary<int, string>();
            foreach (var mapping in avatar.mappings) {
                if (mapping == null || string.IsNullOrEmpty(mapping.path) || mapping.node < 0) {
                    MpegWarn.Once("avatar/mapping", "MPEG_node_avatar: a mapping needs path and node (Table 54)");
                    continue;
                }
                string path = NormalisePath(mapping.path);
                GameObject go;
                if (!context.NodeObjects.TryGetValue(mapping.node, out go)) {
                    MpegWarn.Once("avatar/node" + mapping.node, "MPEG_node_avatar: node " + mapping.node + " of path " + path + " is not part of the scene");
                    continue;
                }
                m_Parts[path] = go;
                pathOfNode[mapping.node] = path;
            }

            // 8.3.2: the path of a parent node shall be a prefix of the path of its child nodes
            foreach (var kv in pathOfNode) {
                var children = root.nodes[kv.Key].children;
                if (children == null) continue;
                foreach (var c in children) {
                    string childPath;
                    if (pathOfNode.TryGetValue((int)c, out childPath) && !IsPrefix(kv.Value, childPath)) {
                        MpegWarn.Once("avatar/prefix" + kv.Key + "/" + c, "MPEG_node_avatar: the path " + kv.Value + " of node " + kv.Key + " is not a prefix of the path " + childPath + " of its child node " + c);
                    }
                }
            }

            if (!IsReferenceAvatar) {
                MpegWarn.Once("avatar/type_unknown", "MPEG_node_avatar: avatar type '" + avatar.type + "' is not the MPEG reference avatar and is not known to this player");
            }
            MpegWarn.Once("avatar/reconstruction", "MPEG_node_avatar: reconstruction from an avatar representation is not implemented, the meshes of the node render as ordinary glTF");
        }

        static bool IsPrefix(string parent, string child) {
            if (!child.StartsWith(parent, StringComparison.Ordinal)) return false;
            return child.Length == parent.Length || child[parent.Length] == '/' || parent.EndsWith("/", StringComparison.Ordinal);
        }

        /// <summary>Node object of an avatar part, or null.</summary>
        public GameObject Find(string path) {
            GameObject go;
            return m_Parts.TryGetValue(NormalisePath(path), out go) ? go : null;
        }
    }
}
