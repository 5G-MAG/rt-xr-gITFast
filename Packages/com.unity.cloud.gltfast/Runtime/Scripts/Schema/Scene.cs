// SPDX-FileCopyrightText: 2023 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

namespace GLTFast.Schema
{

    /// <summary>
    /// Scene, the top level hierarchy object.
    /// </summary>
    [System.Serializable]
    public class Scene : NamedObject
    {

        /// <summary>
        /// The indices of all root nodes
        /// </summary>
        public uint[] nodes;

        /// <inheritdoc cref="SceneExtensions"/>
        public SceneExtensions extensions = new SceneExtensions();

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            GltfSerializeName(writer);
            writer.AddArrayProperty("nodes", nodes);
            if(extensions != null)
            {
                writer.AddProperty("extensions");
                extensions.GltfSerialize(writer);
            }
            writer.Close();
        }
    }

    /// <summary>
    /// Scene extensions
    /// </summary>
    [System.Serializable]
    public class SceneExtensions
    {
        public MpegSceneInteractivity MPEG_scene_interactivity;
        public MpegAnchorObject MPEG_anchor = null;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            if (MPEG_scene_interactivity != null)
            {
                writer.AddProperty("MPEG_scene_interactivity");
                MPEG_scene_interactivity.GltfSerialize(writer);
            }
            writer.Close();
        }
    }

}
