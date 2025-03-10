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
    public enum TrackableType
    {
        /// <summary>
        /// A trackable of type TRACKABLE_FLOOR is an anchor with a plane that spans the xz-plane in the anchor’s local coordinate system.
        /// </summary>
        TRACKABLE_FLOOR = 0,
        /// <summary>
        /// A trackable of type TRACKABLE_VIEWER is a trackable that corresponds to the viewer’s pose.
        /// </summary>
        TRACKABLE_VIEWER = 1,
        /// <summary>
        /// A trackable of type TRACKABLE_CONTROLLER is a trackable that corresponds to one of the active controllers.
        /// </summary>
        TRACKABLE_CONTROLLER = 2,
        /// <summary>
        /// The width and length of a plane span the xz-plane of the anchor instance's local coordinate system. 
        /// </summary>
        TRACKABLE_PLANE = 3,
        /// <summary>
        /// The width and length of the marker 2D span the xz-plane of the anchor instance's local coordinate system. 
        /// </summary>
        TRACKABLE_MARKER_2D = 4,
        /// <summary>
        /// For 3D models, the origin is the center of the mesh. The X, Y, and Z axes correspond to the axes of the world space.
        /// </summary>
        TRACKABLE_MARKER_3D = 5,
        /// <summary>
        /// The y-axis matches the direction of gravity as detected by the device's motion sensing hardware, y points downward.
        /// </summary>
        TRACKABLE_MARKER_GEO = 6,
        /// <summary>
        /// The application-defined trackable object must have a right-handed coordinate space.
        /// </summary>
        TRACKABLE_APPLICATION = 7
    }
    
    [Serializable]
    public class MpegAnchor
    {
        public GLTFast.Schema.Trackable[] trackables;
        public GLTFast.Schema.Anchor[] anchors;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();

            writer.AddArray("trackables");
            foreach (Trackable trck in trackables)
            {
                trck.GltfSerialize(writer);
            }
            writer.CloseArray();

            writer.AddArray("anchors");
            foreach (Anchor anch in anchors)
            {
                anch.GltfSerialize(writer);
            }
            writer.CloseArray();
            writer.Close();
        }
    }

    [Serializable]
    public class Trackable
    {
        public enum GeometricConstraint
        {
            /// <summary>
            ///  geometricConstraint flag
            /// </summary>
            HORIZONTAL_PLANE = 0,

            /// <summary>
            ///  geometricConstraint flag
            /// </summary>
            VERTICAL_PLANE = 1
        }
        /// <summary>
        /// Defines the trackable type
        /// </summary>
        public TrackableType type;

        /// <summary>
        /// a path that describes the action space as specified by the OpenXR specification 
        /// </summary>
        public string path;

        /// <summary>
        /// the geometricConstraint flag may take one of the following values defines as horizontal plane or vertical plane:
        /// </summary>
        public GeometricConstraint geometricConstraint;

        /// <summary>
        /// Index to the node in the nodes array in which the marker geometry and texture are described.
        /// </summary>
        public int markerNode;

        /// <summary>
        /// Array of 3 float numbers giving the longitude, the latitude, and the elevation of the geolocation of the center, 
        /// </summary>
        public float[] coordinates;

        /// <summary>
        /// An application-defined trackable id, that is known to the application.
        /// </summary>
        public string trackableId;

        /// <summary>
        /// Describes the user body part and gesture related to the input
        /// </summary>
        public string userInputDescription;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            writer.AddProperty("type", type);
            writer.AddProperty("path", path);
            writer.AddProperty("geometricConstraint", geometricConstraint);
            writer.AddProperty("markerNode",markerNode);
            writer.AddArray("nodes");
            for (int i = 0; i < coordinates.Length; ++i)
            {
                writer.AddProperty($"{coordinates[i]}");
            }
            writer.CloseArray();
            writer.AddProperty("trackableId",trackableId);
            writer.Close();
        }
    }

    [Serializable]
    public class Anchor
    {
        public enum Aligned
        {
            /// <summary>
            ///  not used
            /// </summary>
            NOT_USED=0,

            /// <summary>
            ///  the bounding box of the virtual assets attached to that anchor is aligned to the bounding box of the real world available space associated with the trackable as estimated by the XR runtime.
            /// </summary>
            ALIGNED_NOTSCALED=1,

            /// <summary>
            ///  the bounding box of the virtual assets attached to that anchor is aligned and scaled to match the bounding box of the real world available space associated with the trackable as estimated by the XR runtime.
            /// </summary>
            ALIGNED_SCALED=2,

        }
        /// <summary>
        /// Index of the trackable in the trackables array that will be used for this anchor
        /// </summary>
        public int trackable;

        /// <summary>
        /// Indicates if AR anchoring is required for the rendering of the associated nodes. 
        /// </summary>
        public bool requiresAnchoring;

        /// <summary>
        /// Determine the activation status of somes nodes
        /// </summary>
        public float[] minimumRequiredSpace;

        /// <summary>
        /// Aligned flag
        /// </summary>
        public Aligned aligned;

        /// <summary>
        /// Indices of the actions in the actions array of the interactivity extension to be executed once the pose of this anchor is determined.
        /// </summary>
        public int[] actions;

        /// <summary>
        /// Reference to an item in the lights array of the MPEG_lights_texture_based extension.
        /// </summary>
        public int light;

        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            writer.AddProperty("trackable", trackable);
            writer.AddProperty("requiresAnchoring", requiresAnchoring);
            writer.AddProperty("minimumRequiredSpace", minimumRequiredSpace);
            writer.AddProperty("aligned",aligned);
            writer.AddArray("actions");
            for (int i = 0; i < actions.Length; ++i)
            {
                writer.AddProperty($"{actions[i]}");
            }
            writer.CloseArray();
            writer.AddProperty("light",light);
            writer.Close();
        }
    }
}