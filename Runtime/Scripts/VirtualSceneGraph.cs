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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Helper class that maps glTF entities to Unity ones
    /// </summary>
    public static class VirtualSceneGraph
    {
        private static CustomDictionary<int, GameObject> m_NodeGameObject
            = new CustomDictionary<int, GameObject>();

        private static CustomDictionary<int, UnityEngine.Mesh> m_MeshIndexMesh
            = new CustomDictionary<int, UnityEngine.Mesh>();

        private static CustomDictionary<int, UnityEngine.Material> m_MaterialIndexMaterial
            = new CustomDictionary<int, UnityEngine.Material>();

        private static CustomDictionary<int, UnityEngine.Texture2D> m_TextureIndexTexture
            = new CustomDictionary<int, UnityEngine.Texture2D>();

        private static Transform sceneTransform;
        private static CustomDictionary<int, AnimationClip> m_AnimationIndexAnimationClip
            = new CustomDictionary<int, AnimationClip>();

        private static CustomDictionary<string, Animation> m_AnimationIndexAnimation
            = new CustomDictionary<string, Animation>();

        private static CustomDictionary<int, UnityEngine.Camera> m_CameraIndexCamera
            = new CustomDictionary<int, UnityEngine.Camera>();

        private static CustomDictionary<int, IMpegInteractivityBehavior> m_BehaviorIndexBehavior
            = new CustomDictionary<int, IMpegInteractivityBehavior>();

        private static CustomDictionary<int, IMpegInteractivityTrigger> m_TriggerIndexTrigger
            = new CustomDictionary<int, IMpegInteractivityTrigger>();

        private static CustomDictionary<int, IMpegInteractivityAction> m_ActionIndexAction
            = new CustomDictionary<int, IMpegInteractivityAction>();

        private static CustomDictionary<int, IMpegTrackable> m_TrackableIndexTrackable
            = new CustomDictionary<int, IMpegTrackable>();
        private static CustomDictionary<int, IMpegAnchor>    m_AnchorIndexAnchor 
            = new CustomDictionary<int, IMpegAnchor>();
        private static CustomDictionary<int, MpegAnchorObject> m_AnchorObjectIndexAnchorObject
            = new CustomDictionary<int, MpegAnchorObject>();

        public static Root root;

        public static void SetRoot(Root _root)
        {
            root = _root;
        }

        // Nodes and game objects
        public static void AssignGameObjectToNode(int nodeIndex, GameObject item, int node)
        {
            m_NodeGameObject.Add(nodeIndex, node, item);
        }

        public static Node GetNodeFromNodeIndex(int node)
        {
            return root.nodes[node];
        }

        public static Node GetNodeFromGameObject(GameObject go)
        {
            int nodeIndex = GetNodeIndexFromGameObject(go);
            return root.nodes[nodeIndex];
        }

        public static int GetNodeIndexFromGameObject(GameObject go)
        {
            return m_NodeGameObject.GetKeyFromValue(go);
        }

        public static GameObject GetGameObjectFromIndex(int node)
        {
            return m_NodeGameObject.GetValueFromKey(node);
        }

        public static int GetNodeCount()
        {
            return m_NodeGameObject.values.Count;
        }

        public static GameObject[] GetGameObjectsFromIndexes(int[] nodes)
        {
            GameObject[] targets = new GameObject[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                targets[i] = GetGameObjectFromIndex(nodes[i]);
            }
            return targets;
        }

        // Mesh indexes and meshes

        public static void AssignMeshToMeshIndex(int meshIndex, UnityEngine.Mesh mesh)
        {
            m_MeshIndexMesh.Add(meshIndex, meshIndex, mesh);
        }

        public static UnityEngine.Mesh GetMeshFromIndex(int mesh_index)
        {
            return m_MeshIndexMesh.GetValueFromKey(mesh_index);
        }

        // Material indexes and materials
        public static void AssignMaterialIndexToMaterial(int materialIndex, UnityEngine.Material mat)
        {
            m_MaterialIndexMaterial.Add(materialIndex, materialIndex, mat);
        }

        public static UnityEngine.Material GetMaterialFromIndex(int materialIndex)
        {
            return m_MaterialIndexMaterial.GetValueFromKey(materialIndex);
        }

        // Texture indexes and textures
        public static void AssignTextureIndexToTexture(int textureIndex, UnityEngine.Texture2D tex)
        {
            m_TextureIndexTexture.Add(textureIndex, textureIndex, tex);
        }

        public static UnityEngine.Texture2D GetTextureFromIndex(int textureIndex)
        {
            return m_TextureIndexTexture.GetValueFromKey(textureIndex);
        }

        public static void AssignAnimationIndexToAnimationClip(int animationIndex, AnimationClip animation)
        {
            m_AnimationIndexAnimationClip.Add(animationIndex, animationIndex, animation);
        }

        public static void AssignSceneTransform(Transform _sceneTransform)
        {
            sceneTransform = _sceneTransform;
        }

        public static Transform GetSceneTransform()
        {
            return sceneTransform;
        }

        public static AnimationClip GetAnimationClipFromIndex(int animationIndex)
        {
            return m_AnimationIndexAnimationClip.GetValueFromKey(animationIndex);
        }

        public static void AssignAnimationIndexToAnimation(int animationIndex, string clipIndex, Animation animation)
        {
            m_AnimationIndexAnimation.Add(animationIndex, clipIndex, animation);
        }

        public static Animation[] GetAllAnimations()
        {
            return m_AnimationIndexAnimation.GetAllValues();
        }

        public static Animation GetAnimationFromClipIndex(int animationIndex)
        {
            AnimationClip clip = GetAnimationClipFromIndex(animationIndex);
            Animation anim = m_AnimationIndexAnimation.GetValueFromKey(clip.name);
            return anim;
        }
        public static void AssignCameraIndexToCamera(int cameraNode, UnityEngine.Camera camera)
        {
            m_CameraIndexCamera.Add(cameraNode, cameraNode, camera);
        }
        public static UnityEngine.Camera GetCameraByIndex(int cameraNode)
        {
            return m_CameraIndexCamera.GetValueFromKey(cameraNode);
        }

        public static IMpegInteractivityBehavior GetBehaviorByIndex(int index)
        {
            return m_BehaviorIndexBehavior.GetValueFromKey(index);
        }

        public static void AssignBehaviorIndexToBehavior(IMpegInteractivityBehavior bhv, int index)
        {
            m_BehaviorIndexBehavior.Add(index, index, bhv);
        }

        public static IMpegInteractivityBehavior[] GetBehaviors()
        {
            return m_BehaviorIndexBehavior.values.ToArray();
        }

        internal static void AssignTriggerToIndex(IMpegInteractivityTrigger trigger, int index)
        {
            m_TriggerIndexTrigger.Add(index, index, trigger);
        }

        public static IMpegInteractivityTrigger GetTriggerFromIndex(int index)
        {
            return m_TriggerIndexTrigger.GetValueFromKey(index);
        }

        internal static void AssignActionToIndex(IMpegInteractivityAction action, int index)
        {
            m_ActionIndexAction.Add(index, index, action);
        }

        public static IMpegInteractivityAction GetActionFromIndex(int index)
        {
            return m_ActionIndexAction.GetValueFromKey(index);
        }

        public static IMpegInteractivityAction[] GetActions()
        {
            return m_ActionIndexAction.GetAllValues();
        }

        internal static void AssignTrackableToIndex(IMpegTrackable track, int index)
        {
            m_TrackableIndexTrackable.Add(index, index, track);
        }
        public static IMpegTrackable GetTrackableFromIndex(int index)
        {
            return m_TrackableIndexTrackable.GetValueFromKey(index);
        }
        internal static void AssignAnchorToIndex(IMpegAnchor anch, int index)
        {
            m_AnchorIndexAnchor.Add(index, index, anch);
        }
        public static IMpegAnchor GetAnchorFromIndex(int index)
        {
            return m_AnchorIndexAnchor.GetValueFromKey(index);
        }
        internal static void AssignAnchorObjectToIndex(MpegAnchorObject anchorOb, int index)
        {
            m_AnchorObjectIndexAnchorObject.Add(index, index, anchorOb);
        }
        public static MpegAnchorObject GetAnchorObjectFromIndex(int index)
        {
            return m_AnchorObjectIndexAnchorObject.GetValueFromKey(index);
        }
        public static int GetAnchorObjectCount()
        {
            return m_AnchorObjectIndexAnchorObject.values.Count;
        }
        public static List<int> GetAnchorObjectKeys()
        {
            return m_AnchorObjectIndexAnchorObject.keys;
        }
        
        public static void ResetAll()
        {
            root = null;
            sceneTransform = null;
            
            m_ActionIndexAction.ClearAll();
            m_TriggerIndexTrigger.ClearAll();
            m_BehaviorIndexBehavior.ClearAll();
            m_CameraIndexCamera.ClearAll();
            m_AnimationIndexAnimation.ClearAll();
            m_AnimationIndexAnimationClip.ClearAll();
            m_TextureIndexTexture.ClearAll();
            m_MaterialIndexMaterial.ClearAll();
            m_MeshIndexMesh.ClearAll();
            m_NodeGameObject.ClearAll();
            m_TrackableIndexTrackable.ClearAll();
            m_AnchorIndexAnchor.ClearAll();
            m_AnchorObjectIndexAnchorObject.ClearAll();
        }
    }

    public class CustomDictionary<T, U>
    {
        // Store the index of each scene description entities (considered as an ID)
        public List<int> indexes;

        // Store the scene description entities
        public List<T> keys;

        // Store the game engine entities
        public List<U> values;

        public CustomDictionary()
        {
            indexes = new List<int>();
            keys = new List<T>();
            values = new List<U>();
        }

        public void Remove(int index)
        {
            // Remove index, keys and value from the list
            indexes.RemoveAt(index);
            keys.RemoveAt(index);
            values.RemoveAt(index);
        }

        public void Add(int index, T key, U value)
        {
            indexes.Add(index);
            keys.Add(key);
            values.Add(value);
        }

        internal T GetKeyFromValue(U item)
        {
            int index = values.IndexOf(item);
            return keys[index];
        }

        internal U GetValueFromKey(T item)
        {
            int index = keys.IndexOf(item);
            if(index == -1)
            {
                Debug.LogError($"Failed getting value from key: {item}");
            }
            return values[index];
        }

        internal U[] GetAllValues()
        {
            return values.ToArray();
        }

        public void ClearAll()
        {
            indexes.Clear();
            values.Clear();
            keys.Clear();
        }
    }
}