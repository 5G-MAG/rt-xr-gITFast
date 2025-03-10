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

using UnityEngine;

namespace GLTFast.Schema
{    
    /// <summary>
    /// In complement to the interactivity objects defined in 
    /// the glTF scene-level extension, some additional data could 
    /// be provided at the level of the affected glTF nodes to 
    /// specialize the trigger activation
    /// </summary>    
    [System.Serializable]
    public class MpegNodeInteractivity
    {
        [System.Serializable]
        public class Trigger
        {
            /// <summary>
            /// Defines the type of the trigger
            /// </summary>
            public TriggerType type;

            /// <summary>
            /// the index of the mesh element that provides the
            /// collider geometry for the current node.
            /// The collider mesh may reference a material.
            /// </summary>
            public int collider;

            /// <summary>
            /// If True, the collider is defined as a static collider.
            /// </summary>
            public bool isStatic;

            /// <summary>
            /// Indicates if the object shall be considered
            /// by the physics simulation
            /// </summary>
            public bool usePhysics;

            /// <summary>
            /// Indicates if the gravity affects the object
            /// </summary>
            public bool useGravity;

            /// <summary>
            /// Mass of the object in kilogram
            /// </summary>
            public float mass;

            /// <summary>
            /// Provides the ratio of the final to initial 
            /// relative velocity between two objects after they collide.
            /// </summary>
            public float restitution;

            /// <summary>
            /// Unitless friction coefficient as defined in the Coulomb
            /// friction model. Friction is the quantity which prevents 
            /// surfaces from sliding off each other. StaticFriction is
            /// used when the object is lying still. 
            /// It will prevent the object from starting to move
            /// </summary>
            public float staticFriction;

            /// <summary>
            /// Unitless friction coefficient as defined in the Coulomb
            /// friction model. When a large enough force is applied to the
            /// object, the dynamicFriction is used, and will attempt to slow
            /// down the object while in contact with another.
            /// </summary>
            public float dynamicFriction;

            /// <summary>
            /// List of primitives used to activate the proximity or collision trigger
            /// </summary>
            public GLTFast.Schema.Trigger.Primitive[] primitives;

            /// <summary>
            /// Indicates if occlusion by other nodes should be considered
            /// </summary>
            public bool allowOcclusion;

            /// <summary>
            /// The weight applied
            /// to the distanceUpperLimit parameter defined at scene level
            /// </summary>
            public float upperDistanceWeight;

            /// <summary>
            /// The weight applied
            /// to the distanceLowerLimit parameter defined at scene level
            /// </summary>
            public float lowerDistanceWeight;

            /// <summary>
            /// Provides additional information related to the user 
            /// inputs (eg 'max speed = 0.5')
            /// </summary>
            public string[] userInputParameters;

            /// <summary>
            /// The visibility computation shall take into account both 
            /// the occultation by other node(s) and the camera frustrum.
            /// If the allowsPartialOcclusion Boolean is TRUE, then a partial
            /// visibility of this node activates the trigger.
            /// If the allowsPartialOcclusion Boolean is FALSE, then this node
            /// shall be fully in the camera frustrum and not be occluded by
            /// any other node(s) except the nodes listed in the nodes array
            /// to activate the trigger.
            /// </summary>
            public bool allowsPartialOcclusion;

            /// <summary>
            /// Set of nodes that shall not be considered for the visibility
            /// computation, when the allowsPartialOcclusion is FALSE.
            /// </summary>
            public int[] nodes;

            /// <summary>
            /// Index of the mesh in the scene meshes array that will
            /// be used to compute visibility.
            /// </summary>
            public int mesh;

            internal void GltfSerialize(JsonWriter _writer)
            {
                if (type == TriggerType.TRIGGER_COLLISION)
                {
                    _writer.AddProperty("collider", collider);
                    _writer.AddProperty("static", isStatic);

                    if (usePhysics)
                    {
                        _writer.AddProperty("useGravity", useGravity);
                        _writer.AddProperty("mass", mass);
                        _writer.AddProperty("restitution", restitution);
                        _writer.AddProperty("staticFriction", staticFriction);
                        _writer.AddProperty("dynamicFriction", dynamicFriction);
                    }
                }
                else if (type == TriggerType.TRIGGER_PROXIMITY)
                {
                    _writer.AddProperty("allowOcclusion", allowOcclusion);
                    _writer.AddProperty("upperDistanceWeight", upperDistanceWeight);
                    _writer.AddProperty("lowerDistanceWeight", lowerDistanceWeight);
                    _writer.AddArray("primitives");
                    for(int i = 0; i < primitives.Length; i++)
                    {
                        _writer.AddProperty($"{primitives[i]}");
                    }
                    _writer.CloseArray();
                }
                else if(type == TriggerType.TRIGGER_USER_INPUT)
                {
                    _writer.AddArray("userInputParameters");
                    for(int i = 0; i < userInputParameters.Length; i++)
                    {
                        _writer.AddProperty($"{userInputParameters[i]}");
                    }
                    _writer.CloseArray();
                }
                else if(type == TriggerType.TRIGGER_VISIBILITY)
                {
                    _writer.AddProperty("allowsPartialOcclusion", allowsPartialOcclusion);
                    _writer.AddArray("nodes");
                    for(int i = 0; i < nodes.Length; i++)
                    {
                        _writer.AddProperty($"{nodes[i]}");
                    }
                    _writer.CloseArray();
                }
                _writer.AddProperty($"{mesh}");
            }
        }

        /// <summary>
        /// Array of node triggers. Only distinct types are allowed.
        /// The minimum size of this array is 1, and the maximum size is the size of
        /// trigger types as defined in the specification.
        /// </summary>
        public Trigger[] triggers;

        internal void GltfSerialize(JsonWriter _writer)
        {
            _writer.AddObject();
            for (int i = 0; i < triggers.Length; i++)
            {
                triggers[i].GltfSerialize(_writer);
            }
            _writer.Close();
        }
    }
}