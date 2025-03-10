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
    public class MpegBufferCircular
    {
        public int media = -1;
        public int count = 2;
        public int source;
        public int[] tracks;

        internal void GltfSerialize(JsonWriter writer) {
            throw new System.NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

}