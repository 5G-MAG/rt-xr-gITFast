/*
 * Copyright (c) 2023 MotionSpell
 * Licensed under the License terms and conditions for use, reproduction,
 * and distribution of 5GMAG software (the “License”).
 * You may not use this file except in compliance with the License.
 * You may obtain a copy of the License at https://www.5g-mag.com/license .
 * Unless required by applicable law or agreed to in writing, software distributed under the License is
 * distributed on an “AS IS” BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and limitations under the License.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

#if MAF_MEDIA_PIPELINES

using maf;

namespace GLTFast {

    public class MediaImport
    {

        private static List<MediaPlayer> mediaPlayers = new();
        public static List<MediaPlayer> MediaPlayers { get { return mediaPlayers; } }
        
        static void ApplyBaseUri(Schema.Media m, Uri baseUri)
        {
            foreach (Schema.MediaAlternative ma in m.alternatives)
            {
                Uri uri = UriHelper.GetUriString(ma.uri, baseUri);
                if (uri.IsFile)
                {
                    ma.uri = uri.LocalPath; // .Replace("\\\\", "\\");
                }
                else
                {
                    ma.uri = uri.ToString();
                }
            }
        }

        public static void CreateMediaPlayers(Schema.Root root, Uri baseUri = null)
        {
            List<MediaPipelineConfig> configs = MediaImport.GetMediaPipelineConfigs(root); // one config per media
            for (var c = 0; c < configs.Count; c++)
            {
                Schema.Media m = root.Extensions.MPEG_media.media[c];
                if (baseUri != null)
                {
                    ApplyBaseUri(m, baseUri);
                }
                MediaPlayer mp = MediaPlayer.Create(m, configs[c]);
                if (mp == null)
                {
                    Debug.LogError("Failed to create media player");
                }
                mediaPlayers.Add(mp);
            }
        }

        public static List<MediaPipelineConfig> GetMediaPipelineConfigs(Schema.Root root)
        {
            Dictionary<int, maf.AttributeType> attribTypeMap = GetAccessorAttributeTypeMap(root);
            var ext = root.Extensions?.MPEG_media;
            if (ext == null || ext.media == null)
            {
                return new List<MediaPipelineConfig>();
            }
            
            var res = new List<MediaPipelineConfig>(ext.media.Length);
            for (int i = 0; i < ext.media.Length; i++)
            {
                res.Add(GetMediaPipelineConfig(root, i, attribTypeMap));
            }
            return res;
        }

        public static Dictionary<int, maf.AttributeType> GetAccessorAttributeTypeMap(Schema.Root root)
        {
            // returns a dictionnary mapping accessor index to maf Attribute type
            var map = new Dictionary<int, maf.AttributeType>();
            // primitive attributes
            Schema.Mesh[] meshes = root.meshes;
            for (int m = 0; m < meshes.Length; m++)
            {
                Schema.Mesh mesh = meshes[m];
                for (int p = 0; p < mesh.primitives.Length; p++)
                {
                    Schema.Attributes attribs = meshes[m].primitives[p].attributes;

                    if (attribs.POSITION != -1)
                    {
                        map[attribs.POSITION] = maf.AttributeType.ATTRIB_POSITION;
                    }
                    if (attribs.NORMAL != -1)
                    {
                        map[attribs.NORMAL] = maf.AttributeType.ATTRIB_NORMAL;
                    }
                    if (attribs.TANGENT != -1)
                    {
                        map[attribs.TANGENT] = maf.AttributeType.ATTRIB_TANGENT;
                    }
                    // assumes accessor index increases in matching order with the attribute's index suffix (*_0, *_1, ...)
                    if (attribs.TEXCOORD_0 != -1)
                    {
                        map[attribs.TEXCOORD_0] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_1 != -1)
                    {
                        map[attribs.TEXCOORD_1] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_2 != -1)
                    {
                        map[attribs.TEXCOORD_2] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_3 != -1)
                    {
                        map[attribs.TEXCOORD_3] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_4 != -1)
                    {
                        map[attribs.TEXCOORD_4] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_5 != -1)
                    {
                        map[attribs.TEXCOORD_5] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_6 != -1)
                    {
                        map[attribs.TEXCOORD_6] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_7 != -1)
                    {
                        map[attribs.TEXCOORD_7] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.TEXCOORD_8 != -1)
                    {
                        map[attribs.TEXCOORD_8] = maf.AttributeType.ATTRIB_TEXCOORD;
                    }
                    if (attribs.COLOR_0 != -1)
                    {
                        map[attribs.COLOR_0] = maf.AttributeType.ATTRIB_COLOR;
                    }
                    if (attribs.JOINTS_0 != -1)
                    {
                        map[attribs.JOINTS_0] = maf.AttributeType.ATTRIB_JOINTS;
                    }
                    if (attribs.WEIGHTS_0 != -1)
                    {
                        map[attribs.WEIGHTS_0] = maf.AttributeType.ATTRIB_WEIGHTS;
                    }

                }
            }

            // texture formats, shouldn't be signaled as "Attribute"
            Schema.Texture[] textures = root.textures;
            if (textures != null)
            {
                for (int t = 0; t < textures.Length; t++)
                {
                    Schema.MpegTextureVideo tex = textures[t].Extensions?.MPEG_texture_video;
                    if (tex != null)
                    {
                        if (tex.format == VideoBufferFormat.RGB)
                        {
                            // accessor should supply vec3 unsigned bytes
                            map[tex.accessor] = maf.AttributeType.TEXTURE_RGB;
                        }
                        else if (tex.format == VideoBufferFormat.G8_B8_R8_3PLANE_420_UNORM)
                        {
                            // accessor should supply scalar unsigned bytes
                            map[tex.accessor] = maf.AttributeType.TEXTURE_GBR_3PLANE_420_UNORM;
                        }
                        /*
                         * else
                        {
                            // the pipeline is expected to decode to the proper format without any hint
                            map[tex.accessor] = maf.AttributeType.UNDEFINED;
                        }
                        */
                    }
                }
            }

            return map;
        }

        public static MediaPipelineConfig GetMediaPipelineConfig(Schema.Root root, int mediaIndex, Dictionary<int, maf.AttributeType> accessorAttribTypeMap)
        {
            /// <summary>
            /// collect gltf definitions that are referencing the given media idx
            /// </summary> 
            var m = new MediaPipelineConfig();

            // lookup media buffers
            Schema.Buffer[] buffers = root.buffers;
            for (int i = 0; i < buffers.Length; i++)
            {
                Schema.Buffer buff = buffers[i];
                if (buff.extensions.MPEG_buffer_circular != null)
                {
                    if (buff.extensions.MPEG_buffer_circular.media < 0)
                    {
                        Debug.LogWarningFormat("Undefined media index on gltf buffer[%d].extension['MPEG_buffer_circular']  ", i);
                        continue;
                    }
                    if (buff.extensions.MPEG_buffer_circular.media == mediaIndex)
                    {
                        m.buffers[i] = buff;
                    }
                }
            }
            if (m.buffers.Count == 0)
            {
                Debug.LogWarningFormat("no MPEG_buffer_circular found referencing media[%d]", mediaIndex);
            }

            // lookup bufferviews referencing circular buffers
            Schema.BufferView[] bufferViews = root.bufferViews;
            for (int j = 0; j < bufferViews.Length; j++)
            {
                Schema.BufferView bv = bufferViews[j];
                if (m.buffers.ContainsKey(bv.buffer))
                {
                    m.bufferViews[j] = bv;
                }
            }
            if (m.bufferViews.Count == 0)
            {
                Debug.LogWarningFormat("no bufferView found referencing media[%d]", mediaIndex);
            }

            // lookup accessors referencing circular buffers
            Schema.Accessor[] accessors = root.accessors;
            for (int k = 0; k < accessors.Length; k++)
            {
                Schema.Accessor acc = accessors[k];
                if (m.bufferViews.ContainsKey(acc.bufferView))
                {
                    if (acc.extensions.MPEG_accessor_timed == null)
                    {
                        Debug.LogWarningFormat("media accessor references 'MPEG_buffer_circular' but doesn't implement 'MPEG_accessor_timed'");
                    }
                    else if (acc.extensions.MPEG_accessor_timed.bufferView >= 0)
                    {
                        m.bufferViews[acc.extensions.MPEG_accessor_timed.bufferView] = bufferViews[acc.extensions.MPEG_accessor_timed.bufferView]; // timed accessor info header
                    }
                    m.accessors[k] = acc;
                }
                if (accessorAttribTypeMap.ContainsKey(k))
                {
                    m.accessorAttributeType[k] = accessorAttribTypeMap[k];
                }
            }
            if (m.accessors.Count == 0)
            {
                Debug.LogWarningFormat("no accessor found referencing media[%d]", mediaIndex);
            }

            return m;
        }

        public static Dictionary<int, Schema.Texture> GetSourceVideoTextures(Schema.Root root)
        {
            Schema.Texture[] sourceTextures = root.textures;
            var videoTextures = new Dictionary<int, Schema.Texture>();
            for (int t = 0; t < sourceTextures.Length; t++)
            {
                if (sourceTextures[t].extensions.MPEG_texture_video != null)
                {
                    videoTextures[t] = sourceTextures[t];
                }
            }
            return videoTextures;
        }

    } // MediaImport

    public class MediaPipelineConfig
    {
        // Media pipeline configuration extracted from GLTF document
        public Dictionary<int, Schema.Buffer> buffers { get; } = new Dictionary<int, Schema.Buffer>();
        public Dictionary<int, Schema.BufferView> bufferViews { get; } = new Dictionary<int, Schema.BufferView>();
        public Dictionary<int, Schema.Accessor> accessors { get; } = new Dictionary<int, Schema.Accessor>();
        public Dictionary<int, maf.AttributeType> accessorAttributeType { get; } = new Dictionary<int, maf.AttributeType>();
        
        public float GetSuggestedUpdateRate(int bufferId)
        {
            float suggestedUpdateRate = -1;

            foreach (var acc in accessors.Values)
            {
                if (bufferViews[acc.bufferView].buffer == bufferId)
                {
                    if(suggestedUpdateRate < 0)
                    {
                        suggestedUpdateRate = acc.extensions.MPEG_accessor_timed.suggestedUpdateRate;
                    }
                    else if (suggestedUpdateRate != acc.extensions.MPEG_accessor_timed.suggestedUpdateRate)
                    {
                        Debug.LogError("multiple timed accessor referencing the same buffer have different update rates");
                    }
                }
            }

            return suggestedUpdateRate;
        }

    } // MediaPipelineConfig

}

#endif
