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

namespace GLTFast.Schema
{
    // List of haptic action nodes
    [Serializable]
    public class HapticActionNode
    {
        /// <summary>
        /// Id of the node in the glTF nodes array
        /// </summary>
        public int node;

        /// <summary>
        /// Index to a haptic object in the hapticObjects array of the 
        /// MPEG_haptic extension
        /// </summary>
        public int hapticObject;

        /// <summary>
        /// Body part mask specifying where on the body the signal can be rendered.
        /// </summary>
        public int actionLocation;

        /// <summary>
        /// Specifies whether the action should trigger a washout (reset to the origin) 
        /// of the associated devices
        /// </summary>
        public bool washout;

        /// <summary>
        /// Used with a Collision trigger. If True, the rendering engine shall use collision
        /// information to estimate the desired location of the haptic feedback on the body.
        /// If false, the signal shall be rendered based on the information specified in the Haptic file.
        /// </summary>
        public bool useCollider;

        /// <summary>
        /// List of haptic material modalities that shall be rendered.
        /// </summary>
        public HapticMaterialModality[] materialHapticModality;

        /// <summary>
        /// List of Haptic Action Media.
        /// </summary>
        public HapticActionMedia[] hapticActionMedias;
    }

    [Serializable]
    public class HapticActionMedia
    {
        /// <summary>
        /// Index in the accessors array of the associated haptic data
        /// </summary>
        public int mediaIndex;

        /// <summary>
        /// Indices of the perceptions of the media that shall be rendered.
        /// If the list if empty all perceptions shall be rendered
        /// </summary>
        public int[] perceptionIndices;

        /// <summary>
        /// List of haptic modalities that can be rendered
        /// </summary>
        public HapticModality hapticModality;

        /// <summary>
        /// One element of that defines the control of the haptic rendering.
        /// </summary>
        public HapticControl hapticControl;

        /// <summary>
        /// Specifies if the haptic rendering of the data should be 
        /// continuously looping.
        /// </summary>
        public bool loop;
    }

    /// <summary>
    /// Determine the activation status of somes nodes
    /// </summary>
    public enum ActivationStatus
    {
        /// <summary>
        /// The node shall be processed by the application
        /// </summary>
        ENABLED = 0,

        /// <summary>
        /// The node shall be skipped by the application
        /// </summary>
        DISABLED = 1
    }

    /// <summary>
    /// Defines the control of the haptic rendering
    /// </summary>
    public enum HapticControl
    {
        /// <summary>
        /// Start the rendering of the haptic data from time 0
        /// or from any other time provided by a control
        /// </summary>
        HAPTIC_PLAY = 0,

        /// <summary>
        /// Pause the rendering of the haptic data
        /// </summary>
        HAPTIC_PAUSE = 1,

        /// <summary>
        /// Resume the rendering of the haptic data from the last pause position
        /// </summary>
        HAPTIC_RESUME = 2,

        /// <summary>
        /// Stop the rendering of the haptic data
        /// </summary>
        HAPTIC_STOP = 3
    }

    /// <summary>
    /// Haptic material modality that can be rendered
    /// </summary>
    public enum HapticMaterialModality
    {
        STIFFNESS = 0,
        FRICTION = 1,
        VIBROTACTILE_TEXTURE = 2,
        TEMPERATURE = 3,
        VIBRATION = 4,
        CUSTOM = 5
    }

    /// <summary>
    /// Haptic modality that can be rendered
    /// </summary>
    public enum HapticModality
    {
        PRESSURE = 0,
        ACCELERATION = 1,
        VELOCITY = 2,
        POSITION = 3,
        TEMPERATURE = 4,
        VIBROTACTILE = 5,
        WATER = 6,
        WIND = 7,
        FORCE = 8,
        ELECTROTACTILE = 9,
        VIBROTACTILE_TEXTURE = 10,
        STIFFNESS = 11,
        FRICTION = 12,
        OTHER = 13
    }

    /// <summary>
    /// Defines the trigger type
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// Collision trigger
        /// </summary>
        TRIGGER_COLLISION = 0,
        /// <summary>
        /// Proximity trigger
        /// </summary>
        TRIGGER_PROXIMITY = 1,
        /// <summary>
        /// User input trigger
        /// </summary>
        TRIGGER_USER_INPUT = 2,
        /// <summary>
        /// Visibility trigger
        /// </summary>
        TRIGGER_VISIBILITY = 3
    }

    /// <summary>
    /// One element that defines the type of the action
    /// </summary>
    public enum ActionType
    {
        /// <summary>
        /// Set activation status of a node
        /// </summary>
        ACTION_ACTIVATE = 0,
        /// <summary>
        /// Set transform to a node
        /// </summary>
        ACTION_TRANSFORM = 1,
        /// <summary>
        /// Block the transform of a node
        /// </summary>
        ACTION_BLOCK = 2,
        /// <summary>
        /// Select and control an animation
        /// </summary>
        ACTION_ANIMATION = 3,
        /// <summary>
        /// Select and control a media
        /// </summary>
        ACTION_MEDIA = 4,
        /// <summary>
        /// Select a manipulate action
        /// </summary>
        ACTION_MANIPULATE = 5,
        /// <summary>
        /// Set new material to nodes
        /// </summary>
        ACTION_SET_MATERIAL = 6,
        /// <summary>
        /// Set haptic feedbacks on a set of nodes
        /// </summary>
        ACTION_SET_HAPTIC = 7,
        /// <summary>
        /// Set avatar related actions
        /// </summary>
        ACTION_SET_AVATAR = 8
    }

    public enum TriggerActivationControl
    {
        /// <summary>
        /// Activate when the conditions are first met
        /// </summary>
        TRIGGER_ACTIVATE_FIRST_ENTER = 0,
        /// <summary>
        /// Activate each time the conditions are first met
        /// </summary>
        TRIGGER_ACTIVATE_EACH_ENTER = 1,
        /// <summary>
        /// Activate as long as the conditions are met
        /// </summary>
        TRIGGER_ACTIVE_ON = 2,
        /// <summary>
        /// When the conditions are first no longer met
        /// </summary>
        TRIGGER_ACTIVATE_FIRST_EXIT = 3,
        /// <summary>
        /// Activate each time when the conditions are no longer met
        /// </summary>
        TRIGGER_ACTIVATE_EACH_EXIT = 4,
        /// <summary>
        /// Activate as long as the conditions are not met
        /// </summary>
        TRIGGER_ACTIVATE_OFF = 5
    }

    /// <summary>
    /// Describe the animation behavior
    /// </summary>
    public enum AnimationControl
    {
        /// <summary>
        /// Play the animation
        /// </summary>
        ANIMATION_PLAY = 0,
        /// <summary>
        /// Pause the animation
        /// </summary>
        ANIMATION_PAUSE = 1,
        /// <summary>
        /// Resume the animation
        /// </summary>
        ANIMATION_RESUME = 2,
        /// <summary>
        /// Stop the animation
        /// </summary>
        ANIMATION_STOP = 3
    }

    /// <summary>
    /// Defines the control of the media
    /// </summary>
    public enum MediaControl
    {
        /// <summary>
        /// Play the media from time 0 or from any other time provided by a control.
        /// </summary>
        MEDIA_PLAY = 0,
        /// <summary>
        /// Pause the media
        /// </summary>
        MEDIA_PAUSE = 1,
        /// <summary>
        /// Resume the media from the last pause position
        /// </summary>
        MEDIA_RESUME = 2,
        /// <summary>
        /// Stop the media
        /// </summary>
        MEDIA_STOP = 3
    }

    /// <summary>
    /// Primitives used to activate the proximity or collision trigger
    /// </summary>
    public enum PrimitiveType
    {
        BV_CUBOID = 0,
        BV_PLANE_REGION = 1,
        BV_CYLINDER_REGION = 2,
        BV_CAPSULE_REGION = 3,
        BV_SPHEROID = 4
    }

    [Serializable]
    public class MpegSceneInteractivity
    {
        public Trigger[] triggers;
        public Action[] actions;
        public GLTFast.Schema.Behavior[] behaviors;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();

            writer.AddArray("triggers");
            foreach (Trigger tr in triggers)
            {
                tr.GltfSerialize(writer);
            }
            writer.CloseArray();

            writer.AddArray("actions");
            foreach (Action ac in actions)
            {
                ac.GltfSerialize(writer);
            }
            writer.CloseArray();

            writer.AddArray("behaviors");
            foreach (Behavior bhv in behaviors)
            {
                bhv.GltfSerialize(writer);
            }
            writer.CloseArray();
            writer.Close();
        }
    }

    [Serializable]
    public class Trigger
    {
        [Serializable]
        public class Primitive
        {
            // Main properties
            /// <summary>
            /// Describes the type of primitive used to activate the proximity trigger.
            /// The available options are. BV_CUBOID, BV_PLANE_REGION, BV_CYLINDER_REGION,
            /// BV_CAPSULE_REGION, and the default BV_SPHEROID.
            /// </summary>
            public PrimitiveType type;

            /// <summary>
            /// Defines the region of intersection within the primitive.
            /// If zero, then all area of the primitive activates the trigger. 
            /// Otherwise, the region of intersection decreases following the normal 
            /// direction of all sides of the primitive from its centroid.
            /// For the capsule primitive, it should be applied over the radius,
            /// top, and base attributes.
            /// </summary>
            public int boundary;

            /// <summary>
            /// Floating-point 4x4 matrix that defines the initial orientation,
            /// translation, and scale of a primitive. Formatted in column-major order.
            /// The primitive shall follow x+ for width, y+ for height, z+ for length.
            /// The matrix transformation allows to transform any primitive after initialization.
            /// </summary>
            public Matrix4x4 transformationMatrix;

            /// <summary>
            /// Width of the box
            /// </summary>
            public float width;

            /// <summary>
            /// Height of the box
            /// </summary>
            public float height;

            /// <summary>
            /// Length of the box
            /// </summary>
            public float length;

            /// <summary>
            /// Centroid 3D coordinates(x,y,z) of the box
            /// </summary>
            public Vector3 centroid;

            /// <summary>
            /// Radius of the cylinder, or radius of the sphere
            /// </summary>
            public float radius;

            /// <summary>
            /// Centroid 3D coordinates(x,y,z) of the base semi-sphere of the capsule
            /// </summary>
            public Vector3 baseCentroid;

            /// <summary>
            /// Centroid 3D Coordinates(x,y,z) of the top semi-sphere of the capsule
            /// </summary>
            public Vector3 topCentroid;
        }

        /// <summary>
        /// Defines the trigger type
        /// </summary>
        public TriggerType type;

        /// <summary>
        /// Nodes considered for trigger calculation
        /// </summary>
        public int[] nodes;

        /// <summary>
        /// List of primitives used to activate the proximity or collision trigger
        /// </summary>
        public Primitive[] primitives;

        /// <summary>
        /// Index of the node consider for the proximity evaluation
        /// </summary>
        public int? referenceNode;

        /// <summary>
        /// Threshold min in meters for the node proximity calculation
        /// </summary>
        public float distanceLowerLimit;

        /// <summary>
        /// Threshold max in meters for the node proximity calculation
        /// </summary>
        public float distanceUpperLimit;

        /// <summary>
        /// Describes the user body part and gesture related to the input
        /// </summary>
        public string userInputDescription;

        /// <summary>
        /// Index of the node containing a camera for which the visibilities
        /// are determined
        /// </summary>
        public int cameraNode;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            writer.AddProperty("type", type);
            writer.AddProperty("userInputDescription", userInputDescription);
            writer.AddArray("nodes");
            for (int i = 0; i < nodes.Length; ++i)
            {
                writer.AddProperty($"{nodes[i]}");
            }
            writer.CloseArray();
            writer.Close();
        }
    }

    [Serializable]
    public class Action
    {
        /// <summary>
        /// Defines the action manipulate type
        /// </summary>
        public enum ManipulateActionType
        {
            /// <summary>
            /// The nodes follow the user pointing device and its rotation
            /// </summary>
            ACTION_MANIPULATE_FREE = 0,
            /// <summary>
            /// The nodes move linearly along the provided axis by following 
            /// the user pointing device
            /// </summary>
            ACTION_MANIPULATE_SLIDE = 1,
            /// <summary>
            /// The nodes translate by following the user pointing device
            /// </summary>
            ACTION_MANIPULATE_TRANSLATE = 2,
            /// <summary>
            /// The nodes rotate around the provided axis by following the 
            /// user pointing device
            /// </summary>
            ACTION_MANIPULATE_ROTATE = 3,
            /// <summary>
            /// Performs a central scaling of the nodes by following the user 
            /// pointing device 
            /// </summary>
            ACTION_MANIPULATE_SCALE = 4
        }

        /// <summary>
        /// One element that defines the type of the action
        /// </summary>
        public ActionType type;

        /// <summary>
        /// Duration of delay in second before executing the action
        /// </summary>
        public float delay;

        /// <summary>
        /// Determine the activation status of somes nodes
        /// </summary>
        public ActivationStatus activationStatus;

        /// <summary>
        /// Indices of the nodes in the nodes array to set the activation status
        /// </summary>
        public int[] nodes;

        /// <summary>
        /// Transform matrix to apply to the nodes
        /// </summary>
        public float[] transform;

        /// <summary>
        /// Index of the animation in the animations array to be considered
        /// </summary>
        public int animation;

        /// <summary>
        /// One element that defines the control of the animation
        /// </summary>
        public AnimationControl animationControl;

        /// <summary>
        /// Index of the media in the MPEG media array to be considered
        /// </summary>
        public int media;

        /// <summary>
        /// Describe the user input related to the manipulation action.
        /// The format shall follow the OpenXR input path description. An example
        /// is "/user/hand/left/aim/pose"
        /// </summary>
        public string userInputDescription;

        /// <summary>
        /// On element that defines the control of the media
        /// </summary>
        public MediaControl mediaControl;

        /// <summary>
        /// One element that defines the action manipulate type
        /// </summary>
        public ManipulateActionType manipulateActionType;

        /// <summary>
        /// xyz coordinates of the axis used for rotation and sliding. 
        /// Relative to the local space created by the USER_INPUT trigger activation
        /// </summary>
        public Vector3 axis;

        /// <summary>
        /// Index of the material in the materials array to apply to the nodes
        /// </summary>
        public int material;

        /// <summary>
        /// List of haptic action nodes
        /// </summary>
        public HapticActionNode[] hapticActionNodes;

        /// <summary>
        /// The avatarAction is a URN that uniquely identifies the avatar action.
        /// </summary>
        public string avatarAction;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            writer.Close();
        }
    }

    /// <summary>
    /// A Behavior is composed of a pair of triggers and actions
    /// </summary>
    [Serializable]
    public class Behavior
    {
        public enum ActionsControl
        {
            /// <summary>
            /// Each defined action is executed sequentially in the order of the actions array
            /// </summary>
            SEQUENTIAL = 0,

            /// <summary>
            /// The defined actions are executed concurrently
            /// </summary>
            PARALLEL = 1
        }

        /// <summary>
        /// Indices of the triggers in the triggers array considered for this behavior
        /// </summary>
        public int[] triggers;

        /// <summary>
        /// Indices of the actions in the actions array considered for this behavior
        /// </summary>
        public int[] actions;

        /// Set of logical operations to apply to the triggers
        public string triggersCombinationControl;

        /// <summary>
        /// Indicates when the combination of the triggers shall 
        /// be activated for launching actions.
        /// </summary>
        public TriggerActivationControl triggersActivationControl;

        /// <summary>
        /// Index Defines the way to execute the defined actions
        /// </summary>
        public ActionsControl actionsControl;

        /// <summary>
        /// Weight associated to the behavior. Used to select 
        /// a behavior when several behaviors are active at same time for one node
        /// </summary>
        public int priority;

        /// <summary>
        /// Index of the action in the actions array to be executed if the behavior
        /// is still on-going and is no more defined in a newly received scene update
        /// </summary>
        public int? interruptAction;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            writer.AddArray("triggers");

            foreach (int tr in triggers)
            {
                writer.AddProperty($"{tr}");
            }

            writer.CloseArray();
            writer.AddArray("actions");
            foreach (int act in actions)
            {
                writer.AddProperty($"{act}");
            }
            writer.CloseArray();
            writer.AddProperty("triggersCombinationControl", triggersCombinationControl);
            writer.AddProperty("triggersActivationControl", triggersActivationControl);
            writer.AddProperty("actionsControl", actionsControl);
            writer.Close();
        }
    }

}