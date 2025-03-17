// SPDX-FileCopyrightText: 2023 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

// All modification marked by "//// IDCC" are created by InterDigital and subject to the following header
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

namespace GLTFast.Schema
{

    /// <summary>
    /// glTF root extensions
    /// </summary>
    [System.Serializable]
    public class RootExtensions
    {

        /// <inheritdoc cref="LightsPunctual"/>
        // ReSharper disable once InconsistentNaming
        public LightsPunctual KHR_lights_punctual;

        /// <inheritdoc cref="MaterialsVariantsRootExtension"/>
        // ReSharper disable once InconsistentNaming
        public MaterialsVariantsRootExtension KHR_materials_variants;

        //// IDCC
        // MPEG_media extension
        public MpegMediaExtension MPEG_media;

        // MPEG_scene_interactivity extension
        public MpegSceneInteractivity MPEG_scene_interactivity;
        // MPEG anchor extension
        public MpegAnchor MPEG_anchor;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            if (KHR_lights_punctual != null)
            {
                writer.AddProperty("KHR_lights_punctual");
                KHR_lights_punctual.GltfSerialize(writer);
            }
            if (KHR_materials_variants != null)
            {
                writer.AddProperty("KHR_materials_variants");
                KHR_materials_variants.GltfSerialize(writer);
            }
            if (MPEG_media != null)
            {
                writer.AddProperty("MPEG_media");
                MPEG_media.GltfSerialize(writer);
            }
            //// IDCC
            if(MPEG_scene_interactivity != null)
            {
                writer.AddProperty("MPEG_scene_interactivity");
                MPEG_scene_interactivity.GltfSerialize(writer);
            }
            if(MPEG_anchor != null)
            {
                writer.AddProperty("MPEG_anchor");
                MPEG_anchor.GltfSerialize(writer);
            }
            writer.Close();
        }

        /// <summary>
        /// Cleans up invalid parsing artifacts created by <see cref="GltfJsonUtilityParser"/>.
        /// </summary>
        /// <returns>True if element itself still holds value. False if it can be safely removed.</returns>
        public virtual bool JsonUtilityCleanup()
        {
            if (KHR_lights_punctual != null && !KHR_lights_punctual.JsonUtilityCleanup())
            {
                KHR_lights_punctual = null;
            }

            if (KHR_materials_variants != null && !KHR_materials_variants.JsonUtilityCleanup())
            {
                KHR_materials_variants = null;
            }

            if (MPEG_media != null && !MPEG_media.JsonUtilityCleanup())
            {
                MPEG_media = null;
            }

            if(MPEG_scene_interactivity != null && !MPEG_scene_interactivity.JsonUtilityCleanup())
            {
                MPEG_scene_interactivity = null;
            }
            if(MPEG_anchor != null && !MPEG_anchor.JsonUtilityCleanup())
            {
                MPEG_anchor = null;
            }

            return KHR_lights_punctual != null
                || KHR_materials_variants != null
                || MPEG_media != null
                || MPEG_scene_interactivity != null
                || MPEG_anchor != null;
        }
    }
}
