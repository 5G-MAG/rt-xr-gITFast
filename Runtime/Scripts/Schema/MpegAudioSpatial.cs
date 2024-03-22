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

#nullable enable

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.ComponentModel;

namespace GLTFast.Schema
{

    [System.Serializable]
    public class MpegAudioSpatialSource
    {
        public int id = -1;

        public string type = "Object"; // required
        public int targetSampleRate = -1; // required
        public int[]? accessors; // required

        public float pregain = 0;
        public float playbackSpeed = 1;

        public string attenuation = "linearDistance";

        public float referenceDistance = 1;
        public float[]? attenuationParameters;

        public int[]? reverbFeed;
        public float[]? reverbFeedGain;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }

    }


    [System.Serializable]
    public class MpegAudioSpatialListener
    {
        public int id = -1;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

    [System.Serializable]
    public class MpegAudioSpatialReverb
    {
        public int id = -1;
        public MpegSpatialAudioReverbProperties[]? properties; // required

        public bool? bypass; // = true;
        public float? predelay;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

    [System.Serializable]
    public class MpegSpatialAudioReverbProperties
    {
        public float frequency;
        public float RT60;
        public float DSR;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

    [System.Serializable]
    public class MpegAudioSpatial
    {
        public MpegAudioSpatialSource[]? sources;
        public MpegAudioSpatialListener? listener = null;
        public MpegAudioSpatialReverb[]? reverbs;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

}