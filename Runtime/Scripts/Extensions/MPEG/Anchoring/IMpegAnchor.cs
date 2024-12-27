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

using GLTFast.Schema;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Any behavior should implement this interface to be 
    /// compatible with the Mpeg interactivity extensions
    /// </summary>
    public interface IMpegAnchor
    {
        /// <summary>
        /// Initialize MpegAnchor based on the MpegAnchor extension
        /// And the parsed Behavior
        /// </summary>
        public void Init(Anchor anc);
        public int GetTrackableIndex();
        public void AttachNodeToAnchor(GameObject go);
        public void SetUp();
        public void DumpAttributs();
        void Dispose();
    }
}