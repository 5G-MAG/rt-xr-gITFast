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
using UnityEngine;

namespace GLTFast.Schema
{

    [System.Serializable]
    public class MpegMediaExtension
    {
        public Media[] media;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

    [System.Serializable]
    public class Media
    {
        public string name;
        public float startTime;
        public float startTimeOffset;
        public float endTimeOffset;
        public bool autoPlay;
        public uint autoPlayGroup; // bool ?
        public bool loop;
        public bool controls;
        public MediaAlternative[] alternatives;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }


    [System.Serializable]
    public class MediaAlternative
    {
        public string mimeType;
        public string uri;
        public MediaTrack[] tracks;
        // Object extraParams : test case/scenario ?;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }


    [System.Serializable]
    public class MediaTrack
    {
        public string track;
        public string codecs;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

}