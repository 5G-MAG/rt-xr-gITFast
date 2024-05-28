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

using System;

namespace GLTFast.Schema
{
    [Serializable]
    public class MpegAnchorObject
    {
        /// <summary>
        /// Reference to an item in the anchors array of the MPEG_anchor extension
        /// </summary>
        public int anchor = -1;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            writer.AddProperty("anchor", anchor);
            writer.Close();
        }
    }
}