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
    /// Any trackable should implement this interface to be 
    /// compatible with the Mpeg anchor extensions
    /// </summary>
    public interface IMpegTrackable
    {
        /// <summary>
        /// Initialize trackable based on MpegAnchor 
        /// extension parsed Trackable
        /// /// </summary>
        public void InitFromGltf(Trackable track);
        public void Init();
        public bool Detect();
        public Transform Track();
        public void AttachNodeToTrackable(GameObject go); 
        public void RequiredSpace(Vector3 requiredSpace);
        public void RequiredAnchoring(bool requiredAnchoring);
        public void RequiredAlignedAndScale(Anchor.Aligned aligned);
        public void RemoveAnchor();
        public void DumpAttributs();
        void Dispose();
    }
}