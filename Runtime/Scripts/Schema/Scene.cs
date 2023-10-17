// Copyright 2020-2022 Andreas Atteneder
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

namespace GLTFast.Schema {

    /// <summary>
    /// Scene, the top level hierarchy object.
    /// </summary>
    [System.Serializable]
    public class Scene : NamedObject {
        
        /// <summary>
        /// The indices of all root nodes
        /// </summary>
        public uint[] nodes;

        /// <inheritdoc cref="SceneExtensions"/>
        public SceneExtensions extensions = new SceneExtensions();

        internal void GltfSerialize(JsonWriter writer) {
            writer.AddObject();
            GltfSerializeRoot(writer);
            writer.AddArrayProperty("nodes",nodes);

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
