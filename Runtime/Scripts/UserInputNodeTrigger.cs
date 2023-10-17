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
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast 
{   
    /// <summary>
    /// Represent the user input trigger at the node level
    /// </summary>
    public class UserInputNodeTrigger : MonoBehaviour
    {
        private string[] m_UserInputParameters;

        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();

        public void Init(string[] _userInputParameters)
        {
            // Format of the user input parameters should be the following
            // key=value;

            // So for each input parameter we need to split the key and the value 
            // with the '=' separator
            // Cache the value for convenience
            m_UserInputParameters = _userInputParameters;
            for(int i = 0; i < _userInputParameters.Length; i++)
            {
                string[] _keyValue = _userInputParameters[i].Split('=');
                
                if(_keyValue.Length > 2)
                {
                    throw new System.Exception("Error whille parsing user input parameters at " +
                        "the user input node trigger level. Key and value should follow the format Key=Value");
                }

                for (int j = 0; j < _keyValue.Length - 1; j++)
                {
                    keys.Add(_keyValue[j]);
                    values.Add(_keyValue[j + 1]);
                }
            }
        }

        public bool Resolve()
        {
            // TODO The application should implement this function
            return true;
        }
    }
}