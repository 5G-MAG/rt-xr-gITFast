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

#if MAF_MEDIA_PIPELINES

using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace GLTFast {

    using maf;
    using mafComponentType = maf.ComponentType;

    public class MediaPlayer : MonoBehaviour
    {
        
        public bool autoPlay = false;

        static BufferInfo GetBufferInfo(MediaPipelineConfig cfg, int accessorIdx)
        {
            Schema.Accessor ac = cfg.accessors[accessorIdx];
            Schema.BufferView bv = cfg.bufferViews[ac.bufferView];
            Schema.Buffer b = cfg.buffers[bv.buffer];

            var bi = new BufferInfo();
            bi.bufferId = bv.buffer;
            bi.offset = ac.byteOffset + bv.byteOffset;
            // NOTE: When two or more accessors use the same buffer view, this field MUST be defined. No sanity check performed here. 
            // When 'byteStride' is not defined, data is tightly packed.
            bi.stride = bv.byteStride > 0 ? bv.byteStride : 0;

            switch (ac.componentType)
            {
                case Schema.GltfComponentType.Byte:
                    bi.componentType = mafComponentType.BYTE;
                    break;
                case Schema.GltfComponentType.Float:
                    bi.componentType = mafComponentType.FLOAT;
                    break;
                case Schema.GltfComponentType.Short:
                    bi.componentType = mafComponentType.SHORT;
                    break;
                case Schema.GltfComponentType.UnsignedByte:
                    bi.componentType = mafComponentType.UNSIGNED_BYTE;
                    break;
                case Schema.GltfComponentType.UnsignedInt:
                    bi.componentType = mafComponentType.UNSIGNED_INT;
                    break;
                case Schema.GltfComponentType.UnsignedShort:
                    bi.componentType = mafComponentType.UNSIGNED_SHORT;
                    break;
            }

            switch (ac.GetAttributeType())
            {
                case Schema.GltfAccessorAttributeType.SCALAR:
                    bi.type = SampleType.SCALAR;
                    break;
                case Schema.GltfAccessorAttributeType.VEC2:
                    bi.type = SampleType.VEC2;
                    break;
                case Schema.GltfAccessorAttributeType.VEC3:
                    bi.type = SampleType.VEC3;
                    break;
                case Schema.GltfAccessorAttributeType.VEC4:
                    bi.type = SampleType.VEC4;
                    break;
                case Schema.GltfAccessorAttributeType.MAT2:
                    bi.type = SampleType.MAT2;
                    break;
                case Schema.GltfAccessorAttributeType.MAT3:
                    bi.type = SampleType.MAT3;
                    break;
                case Schema.GltfAccessorAttributeType.MAT4:
                    bi.type = SampleType.MAT4;
                    break;
            }

            AttributeType attributeType;
            if (cfg.accessorAttributeType.TryGetValue(accessorIdx, out attributeType))
                bi.attributeType = attributeType;
            
            return bi;
        }

        static BufferInfoArray GetMafBufferInfoArray(MediaPipelineConfig cfg)
        {
            var res = new BufferInfoArray();
            foreach (int a in cfg.accessors.Keys)
            {
                res.Add(GetBufferInfo(cfg, a));
            }
            return res;
        }

        static bool ShallIgnoreBufferTracks(Schema.Media m)
        {
            // as specified in 5.2.3.2 - table 9 Definition of MPEG_buffer_circular extension
            // MPEG_buffer_circular.tracks:  [...] When alternatives elements indicated by a source contain a different number of track items in the tracks element, this element shall not be present [...]
            // also, MPEG_buffer_circular.tracks implies Media tracks are defined
            int trackCount = -1;
            foreach (Schema.MediaAlternative ma in m.alternatives)
            {
                if (ma.tracks == null)
                {
                    return true;
                }

                if (trackCount < 0)
                {
                    trackCount = ma.tracks.Length;
                }
                else
                {
                    if (ma.tracks.Length != trackCount)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        static MediaInfo GetMafMediaInfo(Schema.Media m, MediaPipelineConfig cfg)
        {
            var mi = new MediaInfo();
            if(m.name != null)
            {
                mi.name = m.name;
            }

            var trackIdxToBufferIdx = new Dictionary<int, List<int>>();

            if (!ShallIgnoreBufferTracks(m))
            {
                foreach (var buff in cfg.buffers)
                {
                    if (buff.Value.extensions.MPEG_buffer_circular.tracks != null)
                    {
                        foreach (int trackIdx in buff.Value.extensions.MPEG_buffer_circular.tracks)
                        {
                            if (!trackIdxToBufferIdx.ContainsKey(trackIdx))
                            {
                                trackIdxToBufferIdx[trackIdx] = new List<int>();
                            }
                            trackIdxToBufferIdx[trackIdx].Add(buff.Key);
                        }
                    }
                }
            }

            if (trackIdxToBufferIdx.Count > 0)
            {
                // 5.2.3.2 @ table 9 Definition of MPEG_buffer_circular extension
                // Index of a track of a media entry, indicated by source and listed by MPEG_media extension, used as the source for the input data to this buffer.
                // [...]
                // When tracks array contains multiple tracks, the media pipeline should perform the necessary processing of all referenced tracks to generate the requested data format of the buffer.
                foreach (Schema.MediaAlternative ma in m.alternatives)
                {
                    var alt = new AlternativeLocation();
                    alt.mimeType = ma.mimeType;
                    alt.uri = ma.uri;
                    List<int> buffIdxList;
                    for (int i = 0; i < ma.tracks.Length; i++)
                    {
                        // can only specify one buffer per track accoridng to MAF api.
                        // the spec doesn't mention explicitly how to handle the situation where one track would decode to multiple buffers
                        if (!trackIdxToBufferIdx.ContainsKey(i))
                        {
                            Debug.LogError($"failed to map track '{i}' to buffer for media alternative: '{ma.uri}'");
                        } else
                        {
                            buffIdxList = trackIdxToBufferIdx[i];
                            foreach (int buffIdx in buffIdxList)
                            {
                                Schema.MediaTrack mt = ma.tracks[i];
                                var t = new Track();
                                t.track = mt.track;
                                t.bufferId = buffIdx;
                                t.id = -1; // unsure about this
                                alt.tracks.Add(t);
                            }
                        }
                    }
                    mi.alternatives.Add(alt);
                }

            }
            else
            {
                // default strategy, let the media pipeline handle track to buffer mapping by not specifying tracks.
                //
                // 5.2.3.2 @ Table 9 � Definition of MPEG_buffer_circular extension
                // When tracks element is not present, the media pipeline should perform the necessary processing
                // of all tracks of the MPEG_media entry listed by source element to generate the requested data format of the buffer.
                //
                // 6.2 MAF API @ Table 23 � API Structure and Parameter Semantics
                // The tracks indicate which streams / tracks / representations from the referenced media alternative are to be accessed by this pipeline.
                // If none is specified, it shall be assumed that all components of the referenced media alternative are to be accessed.
                //
                // pass all tracks without specifying bufferId, leave track/buffer mapping up to pipeline implementation
                foreach (Schema.MediaAlternative ma in m.alternatives)
                {
                    var alt = new AlternativeLocation();
                    alt.mimeType = ma.mimeType;
                    alt.uri = ma.uri;
                    if (ma.tracks != null)
                    {
                        for (int i = 0; i < ma.tracks.Length; i++)
                        {
                            Schema.MediaTrack mt = ma.tracks[i];
                            var t = new Track();
                            t.track = mt.track;
                            t.bufferId = -1; // assume -1 = auto
                            t.id = -1;
                            alt.tracks.Add(t);
                        }
                    }
                    mi.alternatives.Add(alt);
                }

            }

            return mi;
        }

        public static MediaPlayer Create(Schema.Media media, MediaPipelineConfig cfg, GameObject go = null)
        {
            MediaPlayer mp;
            try
            {
                if (go == null)
                {
                    go = new GameObject();
                }
                mp = go.AddComponent<MediaPlayer>();
                mp.createPipeline(media, cfg);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
            mp.autoPlay = media.autoplay;
            return mp;
        }

        /*************************************************************************************************************************/

        IMediaPipeline pipeline;
        MediaInfo mediaInfo;
        Dictionary<int, MediaBufferHandler> mediaBuffers;
        Dictionary<int, List<VideoTexture>> videoTextures; // one buffer, possibly mutliple textures
        Dictionary<int, MafAudioReader> audioReaders; // one buffer, one MafAudioSource
        // Dictionary<int, List<SpatialAudioSource>> audioSources = new Dictionary<int, List<SpatialAudioSource>>();

        private TimeInfo timeInfo;
        private ViewInfo viewInfo;
        private MafAudioReader audioSync;

        public double CurrentTime { get; private set; }
        public bool Error { get; private set; }

        public static Dictionary<int, MediaBufferHandler> CreateMediaBuffers(MediaPipelineConfig cfg, BufferInfoArray bufferInfo, bool allocate = true)
        {
            var layout = new Dictionary<int, BufferInfoArray>();
            foreach (var bi in bufferInfo)
            {
                if (!layout.ContainsKey(bi.bufferId))
                {
                    layout[bi.bufferId] = new BufferInfoArray();
                }
                layout[bi.bufferId].Add(bi);
            }
            var res = new Dictionary<int, MediaBufferHandler>();

            foreach (int bufferId in cfg.buffers.Keys)
            {
                float suggestedUpdaterate = cfg.GetSuggestedUpdateRate(bufferId);
                res[bufferId] = new MediaBufferHandler(layout[bufferId], suggestedUpdaterate);
                if (allocate)
                {
                    var buff = cfg.buffers[bufferId];
                    res[bufferId].Initialize(false);
                    res[bufferId].Allocate((byte)buff.extensions.MPEG_buffer_circular.count, buff.byteLength);
                }
            }

            if (allocate) {
                foreach (var bi in bufferInfo)
                {
                    bi.handler = res[bi.bufferId].Handler;
                }
            }
            return res;
        }

        private void createPipeline(Schema.Media media, MediaPipelineConfig cfg)
        {
            MediaPipelineFactoryPlugin.RegisterAll();

            BufferInfoArray bufferInfo = GetMafBufferInfoArray(cfg);
            mediaBuffers = CreateMediaBuffers(cfg, bufferInfo);
            mediaInfo = GetMafMediaInfo(media, cfg);
            pipeline = MediaPipelineFactory.getInstance().createMediaPipeline(mediaInfo, bufferInfo);
            if (pipeline == null)
            {
                throw new Exception("Unsupported media type - failed to create maf pipeline.");
            }
        }

        public MediaBufferHandler GetBuffer(int bufferId)
        {
            return mediaBuffers[bufferId];
        }

        public void AddVideoTexture(VideoTexture vt)
        {
            // there could be multiple video textures for the same buffer,
            // eg. slices, or because Texture2D instantiates gltf.Texture + gltf.Sampler
            if(videoTextures == null)
            {
                videoTextures = new Dictionary<int, List<VideoTexture>>();
                videoTextures[vt.bufferId] = new List<VideoTexture>();
            }
            else if (!videoTextures.ContainsKey(vt.bufferId))
            {
                videoTextures[vt.bufferId] = new List<VideoTexture>();
            }
            videoTextures[vt.bufferId].Add(vt);
        }

        public void AddAudioSource(SpatialAudioSource aSrc)
        {
            // Configure SpatialAudioSource `aSrc` to read audio samples from the media player. 
            // The player uses `aSrc.BufferId` to select the BufferHandler to read from.
            // Audio channel layout is infered from the BufferInfo list which was used to initialize the media.
            if (audioReaders == null)
            {
                audioReaders = new Dictionary<int, MafAudioReader>();
            }
            if (!audioReaders.ContainsKey(aSrc.BufferId))
            {
                // assume one buffer = one audio source
                var handler = mediaBuffers[aSrc.BufferId];
#if ENABLE_AUDIO_CLIP_READER
                audioReaders[aSrc.BufferId] = new AudioClipReader(handler, handler.BufferInfo, aSrc.SampleRate);
#else
                audioReaders[aSrc.BufferId] = new AudioFilterReader(handler, handler.BufferInfo, aSrc.SampleRate);
#endif
            }
            
            audioReaders[aSrc.BufferId].AddAudioSource(aSrc);
            if (audioSync == null)
            {
                audioSync = audioReaders[aSrc.BufferId];
            }
        }

        public void Play(TimeInfo t = null, ViewInfo v = null)
        {
            if (pipeline == null)
            {
                throw new Exception("Pipeline not initialized. Use MediaPipeline.Create() to initialize a Media Player");
            }
            
            autoPlay = false;

            if (timeInfo == null)
            {
                timeInfo = new TimeInfo();
                timeInfo.timeOffset = 0;
            }
            if (viewInfo == null)
            {
                viewInfo = new ViewInfo();
            }
            pipeline.startFetching(timeInfo, viewInfo);
            CurrentTime = -1;
            if (audioReaders != null)
            {
                foreach (var aSrc in audioReaders.Values)
                {
                    aSrc.Play();
                }
            }
        }

        public void Stop()
        {
            if (pipeline == null)
            {
                return;
            }
            if (audioReaders != null)
            {
                foreach (var aSrc in audioReaders.Values)
                {
                    aSrc.Stop();
                }
            }
            pipeline.stopFetching();
        }

        // Dispose() stops media pipeline and properly dispose of the media pipeline.
        // After calling Dispose(), the MediaPlayer instance is not longer usable and should no longer be referenced.
        public void Dispose()
        {
            if (pipeline == null)
            {
                return;
            }
            Stop();
            pipeline.Dispose();
            pipeline = null;

            foreach (var mb in mediaBuffers.Values)
            {
                mb.Dispose();
            }
            mediaBuffers.Clear();
        }

        public void Update()
        {
            if (pipeline == null)
            {
                return;
            }
            var state = pipeline.state();
            if (state == PipelineState.ACTIVE)
            {
                if (CurrentTime < 0)
                {
                    CurrentTime = 0;
                }
                else
                {
                    CurrentTime += Time.deltaTime;
                }
                
                foreach (var mb in mediaBuffers.Values)
                {
                    if ((videoTextures != null) && videoTextures.ContainsKey(mb.bufferId))
                    {
                        double ts = audioSync != null ? audioSync.CurrentTime : CurrentTime;
                        if (mb.UpdatePresentationFrame(ts) != null)
                        {
                            var data = mb.GetNativeArray(mb.PresentationFrame);
                            foreach (var vt in videoTextures[mb.bufferId])
                            {
                                vt.UpdateData(data);
                            }
                        }
                    }
                }

            } else if (state == PipelineState.ERROR)
            {
                if (!Error)
                {
                    Debug.LogWarning("/!\\ pipeline error !");
                }
                Error = true;
            }
        }

    };


}

#endif
