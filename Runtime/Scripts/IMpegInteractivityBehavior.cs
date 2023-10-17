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

namespace GLTFast
{
    /// <summary>
    /// Any behavior should implement this interface to be 
    /// compatible with the Mpeg interactivity extensions
    /// </summary>
    public interface IMpegInteractivityBehavior
    {
        /// <summary>
        /// Initialise MpegInteractivityBehavior based on the MpegSceneInteractivity extension
        /// And the parsed Behavior
        /// </summary>
        /// <param name="ext">Scene level reference</param>
        /// <param name="bhv">Current behavior reference</param>
        public void InitializeBehavior(GLTFast.Schema.Behavior bhv);

        /// <summary>
        /// Prevent the Behavior for running until it is started again.
        /// If an interrupt action is defined, it is played
        /// </summary>
        public void Interrupt();

        /// <summary>
        /// Return whether or not behavior triggers respond to validation criteria
        /// </summary>
        public bool AreTriggersActived();

        /// <summary>
        /// Activate related actions whenever a trigger is activated
        /// </summary>
        public void ActivateActions();

        /// <summary>
        /// Associate game engine action to interactivity framework action
        /// </summary>
        public void AddGameEngineAction(System.Action action);
    }
}