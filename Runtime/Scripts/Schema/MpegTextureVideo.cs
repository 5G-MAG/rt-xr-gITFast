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

namespace GLTFast.Schema {
    
    [System.Serializable]
    public class MpegTextureVideo
    {
        internal void GltfSerialize(JsonWriter writer) {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }

        public int accessor = -1;
        public int width = 0;
        public int height = 0;
        public string format = "RGB";
    
    }

    [System.Serializable]
    public class MpegSamplerYCbCr
    {

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }

        public enum YCbCrModel
        {
            RGB_IDENTITY = 0,
            YCBCR_IDENTITY = 1,
            YCBCR_709 = 2,
            YCBCR_601 = 3,
            YCBCR_2020 = 4
        }

        public enum YCbCrRange
        {
            FULL = 0,
            NARROW = 1
        }

        public enum ChromaFilter
        {
            NEAREST = 0,
            LINEAR = 1,
            CUBIC = 1000015000
        }

        public enum ComponentSwizzle
        {
            IDENTITY = 0,
            ZERO = 1,
            ONE = 2,
            R = 3,
            G = 4,
            B = 5,
            A = 6
        }
        
        public enum ChromaLocation
        {
            COSITED_EVEN = 0,
            MIDPOINT = 1
        }

        public YCbCrModel ycbcrModel = YCbCrModel.RGB_IDENTITY;
        public YCbCrRange ycbcrRange = YCbCrRange.FULL;
        public ChromaFilter chromaFilter = ChromaFilter.NEAREST;
        public ComponentSwizzle[] components = { ComponentSwizzle.IDENTITY, ComponentSwizzle.IDENTITY, ComponentSwizzle.IDENTITY, ComponentSwizzle.IDENTITY };
        public ChromaLocation xChromaOffset = ChromaLocation.COSITED_EVEN;
        public ChromaLocation yChromaOffset = ChromaLocation.COSITED_EVEN;

    }


}