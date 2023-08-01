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
#nullable enable

namespace GLTFast.Schema {

    /// <summary>
    /// Texture extensions
    /// </summary>
    [System.Serializable]
    public class TextureExtension {
        
        /// <inheritdoc cref="Extension.TextureBasisUniversal"/>
        public TextureBasisUniversal? KHR_texture_basisu;

        /// <inheritdoc cref="Extension.TextureVideo"/>
        public MpegTextureVideo? MPEG_texture_video;

        internal void GltfSerialize(JsonWriter writer) {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }

        public void Sanitize()
        {
            if (KHR_texture_basisu?.source < 0)
            {
                KHR_texture_basisu = null;
            }
            if (MPEG_texture_video?.accessor < 0)
            {
                MPEG_texture_video = null;
            }
        }
    }

    /// <summary>
    /// Basis Universal texture extension
    /// <seealso cref="Extension.TextureBasisUniversal"/>
    /// </summary>
    [System.Serializable]
    public class TextureBasisUniversal {
        
        /// <summary>
        /// Index of the image which defines a reference to the KTX v2 image
        /// with Basis Universal supercompression.
        /// </summary>
        public int source = -1;
    }
}
