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

using Action = GLTFast.Schema.Action;

namespace GLTFast
{
    /// <summary>
    /// Any action should implement this interface to be 
    /// compatible with the Mpeg interactivity extensions
    /// </summary>
    public interface IMpegInteractivityAction
    {
        public float Delay { get; }

        /// <summary>
        /// Invoke the implemented action
        /// </summary>
        void Invoke();

        /// <summary>
        /// Initialize action based on MpegSceneInteractivity 
        /// extension parsed Trigger
        /// </summary>
        /// <param name="action"></param>
        void Init(Action action);
        void Dispose();
    }
}