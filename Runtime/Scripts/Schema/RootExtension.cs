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
    /// glTF root extensions 
    /// </summary>
    [System.Serializable]
    public class RootExtension {
        
        /// <inheritdoc cref="LightsPunctual"/>
        public LightsPunctual KHR_lights_punctual;

        // MPEG_media extension
        public MpegMediaExtension MPEG_media;

        // MPEG_scene_interactivity extension
        public MpegSceneInteractivity MPEG_scene_interactivity;

        internal void GltfSerialize(JsonWriter writer) {
            writer.AddObject();
            if(KHR_lights_punctual!=null) {
                writer.AddProperty("KHR_lights_punctual");
                KHR_lights_punctual.GltfSerialize(writer);
            }
            if (MPEG_media != null)
            {
                writer.AddProperty("MPEG_media");
                MPEG_media.GltfSerialize(writer);
            }
            if(MPEG_scene_interactivity != null)
            {
                writer.AddProperty("MPEG_scene_interactivity");
                MPEG_scene_interactivity.GltfSerialize(writer);
            }
            writer.Close();
        }
    }
}
