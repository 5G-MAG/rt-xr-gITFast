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
using System.Threading;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GLTFast
{

    using maf;

    public class MediaBufferHandler
    {

        private readonly TimeSpan updateInterval;
        private Timer timer;
        private float updateRate;

        public int bufferId { get; internal set; }
        public readonly BufferInfoArray BufferInfo;

        public BufferHandler Handler { get; private set; }
        public Frame PresentationFrame { get; private set; }
        public ulong PresentationTimedelta { get; private set; }
        public ulong PresentationTimeOffset { get; set; }

        public MediaBufferHandler(BufferInfoArray bufferInfo, float updaterate=25)
        {
            updateRate = updaterate;
            updateInterval = TimeSpan.FromSeconds(1 / updaterate);
            BufferInfo = bufferInfo;
            if (IsInitialized())
            {
                Handler = BufferInfo[0].handler;
                if (Handler.headerLength > 0)
                {
                    // NOTE: not all bufferInfo may imply frame headers, so this check can result in false positives
                    CheckImmutableBufferHeaderLength(BufferInfo);
                }
            }
        }
        
        public void CheckImmutableBufferHeaderLength(BufferInfoArray bufferInfo = null)
        {
            if (bufferInfo == null)
            {
                bufferInfo = BufferInfo;
            }
            if (ComputeMaxImmutableHeaderLength(bufferInfo) > Handler.headerLength)
            {
                throw new Exception("invalid maf buffer header length");
            }
        }

        private static int ComputeMaxImmutableHeaderLength(BufferInfoArray bufferInfo)
        {
            // immutable should be set per set per accessor header, based on the gltf scene description.
            // the total header size may vary in the case of mutable accessors, as min/max could mutate component/sample types.
            int headerLength;
            BufferInfoHeader.ComputeTotalHeadersLength(BufferInfoHeader.CreateAccessorsHeaders(bufferInfo, true), out headerLength);
            return headerLength;
        }

        bool IsInitialized()
        {
            if (BufferInfo.Count == 0)
            {
                return false;
            }
            bool ok = BufferInfo[0].handler != null;
            foreach (var bi in BufferInfo)
            {
                if (ok != (bi.handler != null))
                {
                    throw new Exception("maf buffer handler is partially initialized");
                }
            }
            return ok;
        }

        public void Initialize(bool computeMaxImmutableHeaderLength)
        {
            if (BufferInfo.Count == 0)
            {
                throw new Exception("can not initialize media buffer without buffer info");
            }
            if (IsInitialized())
            {
                throw new Exception("maf buffer handler already initialized");
            }
            Handler = new BufferHandler();
            foreach (var bi in BufferInfo)
            {
                bi.handler = Handler;
                bufferId = bi.bufferId;
            }
            if (computeMaxImmutableHeaderLength)
            {
                Handler.headerLength = ComputeMaxImmutableHeaderLength(BufferInfo);
            }
        }

        public void Allocate(byte count, uint byteLength)
        {
            if(Handler.capacity() != 0)
            {
                throw new Exception("maf buffer handler already alliocated");
            }
            Handler.allocate(count, byteLength);
        }

        public int GetHeaderLength()
        {
            return Handler.headerLength;
        }

        unsafe public NativeArray<byte> GetNativeArray(Frame frame, BufferInfoHeaderArray headers = null)
        {
            var data = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(frame.data.ToPointer(), (int)frame.length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref data, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
            if (headers != null)
            {
                int headerLength;
                BufferInfoHeader.ComputeTotalHeadersLength(headers, out headerLength);
                return data.GetSubArray(headerLength, (int)frame.length - headerLength);
            }
            return data;
        }

        public Frame ReadFrame()
        {
            return Handler.readFrame(); ;
        }

        public bool IsFull()
        {
            return Handler.count() == Handler.capacity();
        }

        public bool IsEmpty()
        {
            return Handler.count() == 0;
        }

        public bool IsNotEmpty()
        {
            return Handler.count() > 0;
        }

        public Frame ReadFrame(ulong timestamp)
        {
            return Handler.readFrame(timestamp);
        }

        public ulong? GetNextFrameTimestamp()
        {
            if (IsEmpty())
            {
                return null;
            }
            return Handler.getFrames()[Handler.getReadIdx()].timestamp;
        }

        public ulong? GetLastFrameTimestamp()
        {
            if (IsEmpty())
            {
                return null;
            }
            int idx = Handler.getReadIdx();
            while (idx < Handler.getWriteIdx())
            {
                idx++;
            }
            return Handler.getFrames()[idx].timestamp;
        }

        public Frame UpdatePresentationFrame(double time)
        {
            if (PresentationFrame != null)
            {
                var frame_time = maf.getTime_s(PresentationFrame.timestamp);
                if (frame_time > time)
                {
                    return null;
                }
            }
            
            Frame frame = Handler.readFrame(time);
            while (frame == null)
            {
                var tmp = Handler.readFrame();
                if (tmp == null)
                {
                    break;
                }
                if (time < maf.getTime_s(tmp.timestamp))
                {
                    frame = tmp;
                    break;
                }
            };

            if (frame != null)
            {
                // Debug.Log("video: " + maf.getTime_s(frame.timestamp));
                PresentationFrame = frame;
            }

            return frame;
        }

        public void ClearPresentationFrameAndTimeOffset()
        {
            PresentationTimeOffset = 0;
            PresentationTimedelta = 0;
            PresentationFrame = null;
        }

        public void Flush()
        {
            Frame frame = Handler.readFrame();
            while(frame != null)
            {
                frame = Handler.readFrame();
            }
        }

        public void Dispose()
        {
            Handler.free();
            Handler.Dispose();
        }
        
    }

}

#endif