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
using Media = GLTFast.Schema.Media;
using MediaAlternative = GLTFast.Schema.MediaAlternative;

namespace GLTFast {

    using maf;
    using mafComponentType = maf.ComponentType;

    public class MediaPlayer : MonoBehaviour
    {
        
        public bool autoPlay = false;
        
        private bool playing;
        public bool Playing
        {
            get { return playing; }
        }
          

        /// <summary>
        /// When true media whose autoplay is set do not start by themselves: the application starts them (the user presses play).
        /// The specification says autoplay media start as soon as they are ready, so this is an application choice.
        /// </summary>
        public static bool HoldAutoplay = false;
        // ISO/IEC 23090-14:2024(E), clause 4.3: "The scene activation time of that document may be set externally or may be determined by the user and
        // is considered the presentation time T0". In an AR scene the user activates the scene by having it anchored or placed: the media of such a
        // scene are neither shown (TimedObjectGate) nor heard (AudioListener.volume) until its content is shown. They are not started later: a
        // live stream started late delivers no picture.
        public static bool AwaitContentShown = false;
        public static bool ContentShown = false;

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
            else if (Array.IndexOf(MpegTimedAccessors.GetWanted(), accessorIdx) >= 0)
                // an accessor that a scene extension reads as timed metadata (animation timing, mesh linking, viewport)
                bi.attributeType = AttributeType.METADATA_ACCESSOR;

            // MPEG_accessor_timed: a bufferView holds the timed accessor information header, written before the data of each frame
            Schema.MpegAccessorTimed timedAccessor = ac.extensions != null ? ac.extensions.MPEG_accessor_timed : null;
            if (timedAccessor != null)
            {
                bi.timedHeader = timedAccessor.bufferView >= 0;
                bi.immutable = timedAccessor.immutable;
            }
            bi.normalized = ac.normalized;

            // maximum picture size of a video texture
            Vector2Int textureSize;
            if (cfg.textureMaxSize.TryGetValue(accessorIdx, out textureSize))
            {
                bi.maxWidth = (uint)Mathf.Max(0, textureSize.x);
                bi.maxHeight = (uint)Mathf.Max(0, textureSize.y);
            }

            // sample rate the media pipeline resamples audio to
            int sampleRate;
            if (cfg.targetSampleRate.TryGetValue(accessorIdx, out sampleRate))
            {
                bi.targetSampleRate = sampleRate;
            }
            float speed;
            if (cfg.playbackSpeed.TryGetValue(accessorIdx, out speed))
            {
                bi.playbackSpeed = speed;
            }

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
                foreach (Schema.MediaAlternative ma in OrderAlternatives(m.alternatives))
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
                foreach (Schema.MediaAlternative ma in OrderAlternatives(m.alternatives))
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
            mp.ConfigureTiming(media);
            return mp;
        }

        // MPEG_media timing properties (Table 4).
        private void ConfigureTiming(Media media)
        {
            mediaStartTimeOffset = Math.Max(0.0, media.startTimeOffset);
            mediaEndTimeOffset = media.endTimeOffset;
            mediaLoop = media.loop;
            if (media.autoplay)
            {
                // "When present and set to True, startTime shall not be present or shall be ignored by the client."
                mediaStartTime = -1;
                if (media.startTime >= 0)
                {
                    Debug.LogWarning("MPEG_media.startTime is ignored when autoplay is true");
                }
                mediaAutoplayGroup = media.autoplayGroup;
            }
            else
            {
                mediaStartTime = media.startTime;
                mediaAutoplayGroup = -1;
                if (media.autoplayGroup >= 0)
                {
                    Debug.LogWarning("MPEG_media.autoplayGroup is ignored when autoplay is not true");
                }
            }
            startScheduled = mediaStartTime >= 0;
        }

        // true when the pipeline is ready to start fetching (a failed pipeline does not hold back its autoplayGroup)
        private bool IsReadyToPlay
        {
            get
            {
                if (pipeline == null)
                {
                    return false;
                }
                PipelineState s = pipeline.state();
                return s == PipelineState.READY || s == PipelineState.ERROR;
            }
        }

        // Starts the media that have autoplay set. The media of an autoplayGroup start in the same frame, as soon as all of them are ready.
        public static void UpdateAutoplay(IEnumerable<MediaPlayer> players)
        {
            if (HoldAutoplay)
            {
                return;
            }
            if (players == null)
            {
                return;
            }
            foreach (MediaPlayer mp in players)
            {
                if (mp == null || !mp.autoPlay || mp.pipeline == null)
                {
                    continue;
                }
                if (mp.mediaAutoplayGroup < 0)
                {
                    mp.Play();
                    continue;
                }
                bool groupReady = true;
                foreach (MediaPlayer other in players)
                {
                    if (other != null && other.autoPlay && other.mediaAutoplayGroup == mp.mediaAutoplayGroup && !other.IsReadyToPlay)
                    {
                        groupReady = false;
                    }
                }
                if (groupReady)
                {
                    foreach (MediaPlayer other in players)
                    {
                        if (other != null && other.autoPlay && other.mediaAutoplayGroup == mp.mediaAutoplayGroup)
                        {
                            other.Play();
                        }
                    }
                }
            }
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

        // MPEG_media timing: startTime is a scene time in seconds, negative when the media does not start by itself at a time
        private double mediaStartTime = -1;
        private double mediaStartTimeOffset = 0;
        private double mediaEndTimeOffset = -1;
        private bool mediaLoop = false;
        private int mediaAutoplayGroup = -1;
        private bool startScheduled = false;
        private bool started = false;
        // presentation time of the scene in seconds. Its zero (T0 of clause 4.3) is the first Update of this player that has
        // a pipeline; the time between the activation of the scene and that Update (the load time) is not counted.
        private double sceneTime = 0;
        private double finishedWait = 0;

        // true once the media has played to its end and every frame has been presented
        public bool Finished { get; private set; }
        public bool Started { get { return started; } }

        /// <summary>The textures the video of this media is drawn into (the video textures of the scene).</summary>
        public IEnumerable<UnityEngine.Texture> VideoTargetTextures
        {
            get
            {
                if (videoTextures == null) yield break;
                foreach (var list in videoTextures.Values)
                    foreach (var vt in list)
                        if (vt != null && vt.texture != null) yield return vt.texture;
            }
        }

        /// <summary>True once a video frame of this media was presented (the media is rendered from T0 + Tinit, clause 4.3).</summary>
        public bool FirstFramePresented { get; private set; }
        /// <summary>The scene time at which the media starts by itself (MPEG_media.startTime), negative when it has none.</summary>
        public double ScheduledStartTime { get { return mediaStartTime; } }

        public double CurrentTime { get; private set; }
        public bool Error { get; private set; }

        // read-only views for the app's pipeline display
        public string PipelineStateName { get { return pipeline == null ? "none" : pipeline.state().ToString(); } }
        /// <summary>Pipeline state and queued frames, for the test log.</summary>
        public string DebugState { get { string c = ""; if (mediaBuffers != null) foreach (var b in mediaBuffers.Values) { var nt = b.GetNextFrameTimestamp(); c += b.Count + "@" + (nt.HasValue ? maf.getTime_s(nt.Value).ToString("0.00") : "-") + "/"; } c += " ff=" + FirstFramePresented + " vtex=" + (videoTextures != null ? videoTextures.Count : 0); return (pipeline != null ? pipeline.state().ToString() : "none") + " frames=" + c; } }
        public ICollection<MediaBufferHandler> Buffers { get { return mediaBuffers != null ? (ICollection<MediaBufferHandler>)mediaBuffers.Values : new List<MediaBufferHandler>(); } }
        public int VideoTextureCount { get { int n = 0; if (videoTextures != null) foreach (var l in videoTextures.Values) n += l.Count; return n; } }
        public int AudioReaderCount { get { return audioReaders != null ? audioReaders.Count : 0; } }
        public bool HasPipeline { get { return pipeline != null; } }

        // MPEG_buffer_circular, clause 5.2.3.1: byteLength is the maximum size of the buffer, which holds count buffer frames.
        // A frame is as large as the bufferViews of the buffer cover, so the buffer holds floor(byteLength / frame size) frames
        // (at least 1), without exceeding count. The media pipeline needs 2 frames: the frame being presented is never
        // overwritten, so a second frame is needed to write into, and the pipeline refuses a capacity below 2. Content whose
        // byteLength holds fewer than 2 frames therefore gets an allocation above byteLength, with a warning.
        static void GetCircularBufferLayout(MediaPipelineConfig cfg, int bufferId, out int frameCount, out uint frameLength)
        {
            var buff = cfg.buffers[bufferId];
            int count = buff.extensions.MPEG_buffer_circular.count;
            if (count > 255)
            {
                throw new Exception("MPEG_buffer_circular.count " + count + " of buffer " + bufferId + " is above the 255 frames supported");
            }
            long frame = 0;
            foreach (var bv in cfg.bufferViews.Values)
            {
                if (bv.buffer == bufferId)
                {
                    frame = Math.Max(frame, (long)bv.byteOffset + bv.byteLength);
                }
            }
            if (frame <= 0)
            {
                frame = buff.byteLength;
            }
            long fits = frame > 0 ? buff.byteLength / frame : 0;
            int n = count;
            if (fits < count)
            {
                Debug.LogWarning("buffer " + bufferId + ": byteLength " + buff.byteLength + " holds " + fits + " frame(s) of " + frame + " bytes, count is " + count);
                n = (int)Math.Max(1, fits);
            }
            if (n < 2)
            {
                Debug.LogWarning("buffer " + bufferId + ": allocating 2 frames, the media pipeline reads one frame while it writes the other");
                n = 2;
            }
            frameCount = n;
            frameLength = (uint)frame;
        }

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
                    res[bufferId].Initialize(false);
                    int frameCount;
                    uint frameLength;
                    GetCircularBufferLayout(cfg, bufferId, out frameCount, out frameLength);
                    res[bufferId].Allocate((byte)frameCount, frameLength);
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

        // Media that no MPEG_buffer_circular references, such as the scene update media of MPEG_scene_dynamic
        // (ISO/IEC 23090-14:2024, 5.2.1.3: the media is processed directly by the Presentation Engine). The pipeline hands over
        // the samples of its metadata track in one buffer without accessor; every frame holds one byte of flags (bit 0: sync
        // sample), one byte update_mode (0xFF: not available) and the sample bytes (MAF.AttributeType.METADATA_SCENE_UPDATE).
        // The number of frames and their size are implementation defaults, not values of the specification.
        public const int SceneUpdateBufferId = 0x7FFF0000;
        public static int SceneUpdateFrameCount = 4;
        public static uint SceneUpdateFrameBytes = 4u * 1024u * 1024u;

        public bool HasSceneUpdateBuffer { get; private set; }
        public MediaBufferHandler SceneUpdateBuffer
        {
            get
            {
                MediaBufferHandler mb;
                return HasSceneUpdateBuffer && mediaBuffers != null && mediaBuffers.TryGetValue(SceneUpdateBufferId, out mb) ? mb : null;
            }
        }

        private void createSceneUpdatePipeline(Schema.Media media)
        {
            var info = new BufferInfo();
            info.bufferId = SceneUpdateBufferId;
            info.componentType = mafComponentType.UNSIGNED_BYTE;
            info.type = SampleType.SCALAR;
            info.offset = 0;
            info.stride = 0;
            info.timedHeader = false;
            info.attributeType = AttributeType.METADATA_SCENE_UPDATE;

            var layout = new BufferInfoArray();
            layout.Add(info);
            var buffer = new MediaBufferHandler(layout);
            buffer.Initialize(false);
            buffer.Allocate((byte)SceneUpdateFrameCount, SceneUpdateFrameBytes);

            BufferInfoArray bufferInfo = new BufferInfoArray();
            bufferInfo.Add(info);
            foreach (var bi in bufferInfo)
            {
                bi.handler = buffer.Handler;
            }
            mediaBuffers = new Dictionary<int, MediaBufferHandler>();
            mediaBuffers[SceneUpdateBufferId] = buffer;

            // no tracks are passed: the pipeline maps the buffer to the metadata track of the media
            var mi = new MediaInfo();
            if (media.name != null)
            {
                mi.name = media.name;
            }
            foreach (Schema.MediaAlternative ma in media.alternatives)
            {
                var alt = new AlternativeLocation();
                alt.mimeType = ma.mimeType;
                alt.uri = ma.uri;
                mi.alternatives.Add(alt);
            }
            mediaInfo = mi;
            pipeline = MediaPipelineFactory.getInstance().createMediaPipeline(mediaInfo, bufferInfo);
            if (pipeline == null)
            {
                throw new Exception("Unsupported media type - failed to create maf pipeline.");
            }
            HasSceneUpdateBuffer = true;
        }

        private void createPipeline(Schema.Media media, MediaPipelineConfig cfg)
        {
            MediaPipelineFactoryPlugin.RegisterAll();

            if (cfg.buffers.Count == 0 && cfg.accessors.Count == 0)
            {
                createSceneUpdatePipeline(media);
                return;
            }

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

        // Table 3: the alternatives are "alternatives of the same media" and the client picks the one it can use. The container types
        // this player's media access function opens (mimeType without its parameters, such as profiles, Table 5) come first, in the order of
        // the file; the others follow in the order of the file as a last resort.
        static readonly string[] SupportedMimeTypes = {
            "video/mp4", "audio/mp4", "application/mp4", "application/vnd.apple.mpegurl", "application/x-mpegurl",
            "audio/mpegurl", "application/dash+xml", "video/mp2t", "audio/mpeg", "audio/aac", "video/quicktime"
        };

        static IEnumerable<MediaAlternative> OrderAlternatives(Schema.MediaAlternative[] alternatives)
        {
            if (alternatives == null) return new MediaAlternative[0];
            var ordered = new List<MediaAlternative>();
            var rest = new List<MediaAlternative>();
            foreach (var a in alternatives)
            {
                string type = (a.mimeType ?? "").Split(';')[0].Trim().ToLowerInvariant();
                if (Array.IndexOf(SupportedMimeTypes, type) >= 0) ordered.Add(a); else rest.Add(a);
            }
            ordered.AddRange(rest);
            return ordered;
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
            started = true;
            startScheduled = false;
            Finished = false;
            finishedWait = 0;

            if (timeInfo == null)
            {
                timeInfo = new TimeInfo();
            }
            // the media pipeline generates the media from startTimeOffset to endTimeOffset, and starts over when it loops
            timeInfo.startTime = Math.Max(0.0, mediaStartTime);
            timeInfo.timeOffset = mediaStartTimeOffset;
            timeInfo.endTimeOffset = mediaEndTimeOffset;
            timeInfo.loop = mediaLoop;
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
            playing = true;
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
            playing = false;
        }

        public void Pause()
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
            playing = false;
        }
        
        // MEDIA_PAUSE / MEDIA_RESUME (ISO/IEC 23090-14:2024, Table 40). The pipeline keeps fetching;
        // pausing silences the audio readers and freezes the presentation clock and the video
        // frames (Update() does not run while the component is disabled).
        bool pausedByControl;
        public bool IsPausedByControl { get { return pausedByControl; } }

        public void PausePlayback()
        {
            if (pipeline == null || pausedByControl)
            {
                return;
            }
            pausedByControl = true;
            if (audioReaders != null)
            {
                foreach (var r in audioReaders.Values)
                {
                    r.Active = false;
                }
            }
            enabled = false;
        }

        public void ResumePlayback()
        {
            if (pipeline == null || !pausedByControl)
            {
                return;
            }
            pausedByControl = false;
            enabled = true;
            if (audioReaders != null)
            {
                foreach (var r in audioReaders.Values)
                {
                    r.Active = true;
                }
            }
        }

        // Dispose() stops media pipeline and properly dispose of the media pipeline.
        // After calling Dispose(), the MediaPlayer instance is not longer usable and should no longer be referenced.
        public void Dispose()
        {
            if (pipeline == null)
            {
                return;
            } else {
                Stop();
                pipeline.Dispose();
                pipeline = null;
            }
            foreach (var mb in mediaBuffers.Values)
            {
                mb.Dispose();
            }
            mediaBuffers.Clear();

            // a disposed player must not stay in the scene: it would still be found and listed
            UnityEngine.Object.Destroy(this);
        }

        // The media has finished when its buffers have stayed empty for a moment after the pipeline went IDLE.
        // The audio readers then stop, nothing more is going to be written.
        private void UpdateFinished()
        {
            foreach (var mb in mediaBuffers.Values)
            {
                if (mb.IsNotEmpty())
                {
                    finishedWait = 0;
                    return;
                }
            }
            finishedWait += Time.unscaledDeltaTime;
            if (finishedWait < 0.5)
            {
                return;
            }
            Finished = true;
            if (audioReaders != null)
            {
                foreach (var r in audioReaders.Values)
                {
                    r.Stop();
                }
            }
        }

        public void Update()
        {
            if (pipeline == null)
            {
                return;
            }
            sceneTime += Time.deltaTime;
            if (startScheduled && !started && sceneTime >= mediaStartTime)
            {
                // MPEG_media.startTime: the time at which the rendering of the media begins
                Play();
            }
            var state = pipeline.state();
            // at the end of the media the pipeline is IDLE, the frames it wrote are still presented
            bool playedOut = started && state == PipelineState.IDLE;
            if (state == PipelineState.ACTIVE || playedOut)
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
                            FirstFramePresented = true;
                            var data = mb.GetNativeArray(mb.PresentationFrame);
                            foreach (var vt in videoTextures[mb.bufferId])
                            {
                                vt.UpdateFrame(mb.PresentationFrame, data);
                            }
                        }
                    }
                }

                if (playedOut && !Finished)
                {
                    UpdateFinished();
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

    }; // class MediaPlayer

} // GLTFast

#endif
