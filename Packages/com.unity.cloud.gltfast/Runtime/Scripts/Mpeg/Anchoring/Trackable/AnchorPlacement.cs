/*
* Placement of anchored content chosen by the user.
* ISO/IEC 23090-14:2024 does not say which detected plane a floor or plane trackable uses,
* so the application may let the user pick it by tapping.
*/

using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace GLTFast
{
#if UNITY_ANDROID
    /// <summary>
    /// The kind of surface a trackable asks for: TRACKABLE_FLOOR is the floor, TRACKABLE_PLANE a horizontal or vertical plane
    /// as its geometricConstraint says.
    /// </summary>
    public enum PlacementSurface { Floor, Horizontal, Vertical }

    /// <summary>A trackable that can be anchored where the user chooses on a detected plane.</summary>
    public interface IPlaceableTrackable
    {
        /// <summary>True while no anchor exists yet.</summary>
        bool AwaitingPlacement { get; }

        /// <summary>The surface the trackable asks for.</summary>
        PlacementSurface Surface { get; }

        /// <summary>True when <paramref name="plane"/> is a surface of the kind the trackable asks for.</summary>
        bool Accepts(ARPlane plane);

        /// <summary>Anchors the content on <paramref name="plane"/> at <paramref name="pose"/>. False when the plane does not fit the trackable.</summary>
        bool PlaceAt(ARPlane plane, Pose pose);

        /// <summary>Anchors the content at a spot found without a plane (an estimated surface point); <paramref name="onWall"/> tells a vertical surface from a floor.</summary>
        bool PlaceAtPose(Pose pose, bool onWall);

        /// <summary>Releases the anchor so the content can be placed again.</summary>
        void ResetPlacement();

        /// <summary>Applies the size and the turn the user gave to the placed scene (turn in degrees about the vertical axis of the anchor).</summary>
        void SetUserTransform(float scale, float yawDegrees);
    }

    public static class AnchorPlacement
    {
        /// <summary>When true floor and plane trackables wait for <see cref="TryPlace"/> instead of taking the first suitable plane.</summary>
        public static bool TapToPlace = false;

        /// <summary>
        /// When true floor and horizontal plane content is turned toward the viewer when it is placed.
        /// The specification defines the transform from the TRS of the anchored node only, so this is off by default.
        /// </summary>
        public static bool FaceUserOnPlacement = false;

        /// <summary>
        /// When true the anchor created for a spot the user tapped is attached to the detected plane (it follows the plane as ARCore refines it).
        /// Default false: a free-standing anchor at the tapped point, which does not move when planes are merged or resized.
        /// </summary>
        public static bool AnchorToPlane = false;

        /// <summary>
        /// When true the user may put the scene on any horizontal upward surface or on a wall, whatever trackable the scene declares.
        /// The specification does not say which detected surface a floor or plane trackable uses, so the application lets the user choose.
        /// </summary>
        public static bool PlaceOnAnySurface = true;

        /// <summary>
        /// When true a scene placed by the user sits on the spot that was tapped: its lowest point on a floor or table, its back against a wall,
        /// whatever vertical offset the file gives its anchored node. Off: the file's own offset is kept.
        /// </summary>
        public static bool GroundOnPlacement = true;

        /// <summary>When true the placed scene does not move: taps and gestures are ignored.</summary>
        public static bool Locked = false;

        public static float UserScale { get; private set; } = 1f;
        public static float UserYaw { get; private set; } = 0f;

        /// <summary>Sets the size (1 = as authored) and the turn about the vertical axis (degrees) of the placed scene.</summary>
        public static void SetUserTransform(float scale, float yawDegrees)
        {
            UserScale = Mathf.Clamp(scale, 0.05f, 20f);
            UserYaw = yawDegrees;
            for (int i = 0; i < s_Trackables.Count; i++) s_Trackables[i].SetUserTransform(UserScale, UserYaw);
        }

        public static bool IsUserSurface(ARPlane p)
        {
            return p.alignment == PlaneAlignment.HorizontalUp || p.alignment == PlaneAlignment.Vertical;
        }

        /// <summary>
        /// Orientation of content placed by the user: upright, facing the viewer. On a wall the content stands against it with its front
        /// leaving the wall; on a floor or table it stands facing the viewer.
        /// </summary>
        public static Quaternion UserRotation(ARPlane plane, Vector3 position, Vector3 cameraPosition)
        {
            Vector3 up = plane.transform.up;
            if (plane.alignment == PlaneAlignment.Vertical)
            {
                Vector3 n = plane.transform.up;
                if (Vector3.Dot(n, cameraPosition - position) < 0f) n = -n;
                Vector3 flat = Vector3.ProjectOnPlane(n, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(cameraPosition - position, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
                return Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
            Vector3 toCamera = Vector3.ProjectOnPlane(cameraPosition - position, up);
            if (toCamera.sqrMagnitude < 1e-4f) toCamera = plane.transform.forward;
            return Quaternion.LookRotation(toCamera.normalized, up);
        }

        private static readonly List<IPlaceableTrackable> s_Trackables = new List<IPlaceableTrackable>();

        public static void Register(IPlaceableTrackable t)
        {
            if (t != null && !s_Trackables.Contains(t)) s_Trackables.Add(t);
        }

        public static void Unregister(IPlaceableTrackable t)
        {
            s_Trackables.Remove(t);
        }

        public static void Clear()
        {
            s_Trackables.Clear();
            s_DetectionRequested = false;
            UserScale = 1f; UserYaw = 0f; Locked = false;
        }

        /// <summary>True when at least one trackable of the loaded scene can be placed by the user.</summary>
        public static bool HasPlaceable { get { return s_Trackables.Count > 0; } }

        /// <summary>True when at least one placeable trackable has no anchor yet.</summary>
        public static bool AnyAwaiting
        {
            get
            {
                for (int i = 0; i < s_Trackables.Count; i++)
                    if (s_Trackables[i].AwaitingPlacement) return true;
                return false;
            }
        }

        /// <summary>
        /// The surface the next placement is for: that of the first trackable still waiting, or of the first trackable when all are placed
        /// (placing again moves the scene). Null when the scene has no placeable trackable.
        /// </summary>
        public static PlacementSurface? NextSurface
        {
            get
            {
                for (int i = 0; i < s_Trackables.Count; i++)
                    if (s_Trackables[i].AwaitingPlacement) return s_Trackables[i].Surface;
                return s_Trackables.Count > 0 ? s_Trackables[0].Surface : (PlacementSurface?)null;
            }
        }

        public static bool SurfaceIsWall(PlacementSurface? s) { return s == PlacementSurface.Vertical; }

        /// <summary>
        /// True when the user may place on <paramref name="plane"/>: any floor, table or wall with <see cref="PlaceOnAnySurface"/>, else a
        /// surface that a waiting trackable (any trackable when all are placed) asks for.
        /// </summary>
        public static bool Accepts(ARPlane plane)
        {
            if (plane == null) return false;
            if (PlaceOnAnySurface) return IsUserSurface(plane);
            bool awaiting = AnyAwaiting;
            for (int i = 0; i < s_Trackables.Count; i++)
            {
                var t = s_Trackables[i];
                if ((!awaiting || t.AwaitingPlacement) && t.Accepts(plane)) return true;
            }
            return false;
        }

        /// <summary>
        /// Adds the plane orientations a trackable needs to those the plane manager detects. Several trackables share one manager,
        /// so a mode is added, not set. With <see cref="PlaceOnAnySurface"/> both orientations are detected.
        /// </summary>
        public static void RequestDetection(ARPlaneManager manager, PlaneDetectionMode mode)
        {
            if (manager == null) return;
            if (PlaceOnAnySurface) mode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            var current = manager.requestedDetectionMode;
            // the first trackable of a scene replaces whatever the session started with; the next ones add to it
            var next = s_DetectionRequested ? (current | mode) : mode;
            s_DetectionRequested = true;
            if (next != current) manager.requestedDetectionMode = next;
            Debug.Log("AnchorPlacement: plane detection " + next);
        }
        private static bool s_DetectionRequested = false;

        /// <summary>Places every trackable that is waiting (or all of them when <paramref name="moveExisting"/>).</summary>
        public static bool TryPlace(ARPlane plane, Pose pose, bool moveExisting)
        {
            if (Locked && !AnyAwaiting) return false;
            bool placed = false;
            for (int i = 0; i < s_Trackables.Count; i++)
            {
                var t = s_Trackables[i];
                if (t.AwaitingPlacement || moveExisting)
                    placed |= t.PlaceAt(plane, pose);
            }
            return placed;
        }

        /// <summary>Places every waiting trackable (or all) at an estimated surface point.</summary>
        public static bool TryPlacePose(Pose pose, bool onWall, bool moveExisting)
        {
            if (Locked && !AnyAwaiting) return false;
            bool placed = false;
            for (int i = 0; i < s_Trackables.Count; i++)
            {
                var t = s_Trackables[i];
                if (t.AwaitingPlacement || moveExisting)
                    placed |= t.PlaceAtPose(pose, onWall);
            }
            return placed;
        }

        /// <summary>Orientation of content placed on an estimated surface point whose normal is <paramref name="normal"/>.</summary>
        public static Quaternion UserRotationFromNormal(Vector3 normal, bool onWall, Vector3 position, Vector3 cameraPosition)
        {
            if (onWall)
            {
                Vector3 n = normal;
                if (Vector3.Dot(n, cameraPosition - position) < 0f) n = -n;
                Vector3 flat = Vector3.ProjectOnPlane(n, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(cameraPosition - position, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
                return Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
            Vector3 toCamera = Vector3.ProjectOnPlane(cameraPosition - position, Vector3.up);
            if (toCamera.sqrMagnitude < 1e-4f) toCamera = Vector3.forward;
            return Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        public static void ResetAll()
        {
            for (int i = 0; i < s_Trackables.Count; i++) s_Trackables[i].ResetPlacement();
        }
    }
#endif
}
