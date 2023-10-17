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
using UnityEngine;

namespace GLTFast
{
    public class ActionSetAvatar : MonoBehaviour, IMpegInteractivityAction
    {
        public float Delay => throw new NotImplementedException();

        public void Init(Schema.Action action)
        {
            throw new NotImplementedException();
        }

        public void Invoke()
        {
            if (Delay > 0.0f)
            {
                TimeUtils.instance.StartTimer(Delay, Execute);
            }
            else
            {
                Execute();
            }
        }

        private void Execute()
        {
            throw new NotImplementedException();
        }
    }
}