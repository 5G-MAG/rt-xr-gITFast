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
using System.IO;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Represent the visibility trigger at the node level
    /// </summary>
    public class VisibilityNodeTrigger : MonoBehaviour
    {
        private bool m_AllowsPartialOcclusion;
        private Renderer[] m_Target;
        private UnityEngine.Material[] m_TargetMaterials;
        private UnityEngine.Mesh m_Mesh;
        private Renderer m_CurrentRenderer;
        private UnityEngine.Material m_CurrentMaterial;

        // shaders
        public Shader depthShader = null;
        public Shader visibilityShader = null;

        // store the number of visible pixels related to the affected node
        private ComputeBuffer m_VisiblePixelsBuffer = null;
        private List<Int32> m_ResetVisiblePixelsBuffer = new List<Int32>();

        // camera used for the depth and visibility computation
        private UnityEngine.Camera m_ComputeCamera;

        // store the scene depth texture
        private RenderTexture m_SceneDepthTexture = null;

        // visibility material related to the visibilityShader
        private UnityEngine.Material m_VisibilityMaterial = null;

        // bias used for the visibility computation
        private float m_VisibilityBias = 0.01f;

        internal void InitSceneAndNodeLevelExtension(MpegNodeInteractivity.Trigger trigger)
        {
            m_AllowsPartialOcclusion = trigger.allowsPartialOcclusion;

            if(trigger.nodes == null)
            {
                m_Target = new Renderer[1];
                m_TargetMaterials = new UnityEngine.Material[1];

                // TODO: Retrieve a gameobject from a mesh index
                int _meshIndex = trigger.mesh;
                int _nodesCount = VirtualSceneGraph.GetNodeCount();
                for(int i = 0; i < _nodesCount; i++)
                {
                    Node _n = VirtualSceneGraph.GetNodeFromNodeIndex(i);
                    if(_n.mesh == _meshIndex)
                    {
                        // found
                        m_Target[0] = VirtualSceneGraph.GetGameObjectFromIndex(i).GetComponent<Renderer>();
                        m_TargetMaterials[0] = m_Target[0].sharedMaterial;
                        break;
                    }
                }
            }
            else
            {
                m_Target = new Renderer[trigger.nodes.Length];
                m_TargetMaterials = new UnityEngine.Material[trigger.nodes.Length];
                for (int i = 0; i < m_Target.Length; i++)
                {
                    GameObject _go = VirtualSceneGraph.GetGameObjectFromIndex(trigger.nodes[i]);
                    m_Target[i] = _go.GetComponent<Renderer>();
                    m_TargetMaterials[i] = m_Target[i].sharedMaterial;
                }
            }

            m_CurrentRenderer = GetComponent<Renderer>();
            m_CurrentMaterial = m_CurrentRenderer.sharedMaterial;
            InitializeNodeTrigger();
        }

        private void InitializeNodeTrigger()
        {
            m_ComputeCamera = new GameObject("ComputeCam").AddComponent<UnityEngine.Camera>();
            m_ComputeCamera.enabled = false;
            // Add elements to handle visibility of this particular node

            // create the _visiblePixelsBuffer
            if (m_VisiblePixelsBuffer == null)
            {
                m_VisiblePixelsBuffer = new ComputeBuffer(2, sizeof(Int32)); // store (maxVisible, visible) nb of pixels
                Graphics.SetRandomWriteTarget(1, m_VisiblePixelsBuffer, true);
                m_ResetVisiblePixelsBuffer.Clear();
                m_ResetVisiblePixelsBuffer.Add(0);
                m_ResetVisiblePixelsBuffer.Add(0);
            }

            depthShader = Shader.Find("InterDigital/depthShader");
            visibilityShader = Shader.Find("InterDigital/visibilityComputationShader");

            // create the _visibilityMaterial
            if (m_VisibilityMaterial == null)
            {
                m_VisibilityMaterial = new UnityEngine.Material(visibilityShader);
            }
        }

        internal void InitSceneLevelExtension(Trigger trigger)
        {
            m_CurrentRenderer = GetComponent<Renderer>();
            m_CurrentMaterial = m_CurrentRenderer.sharedMaterial;
            InitializeNodeTrigger();
        }

        private void GenerateSceneDepthMap(UnityEngine.Camera _camera)
        {
            // copy the visibilityCamera parameters to the _computeCamera
            m_ComputeCamera.CopyFrom(_camera);

            // modify the _computeCamera parameters to render depth only
            DepthTextureMode initialCameraDepthTextureMode = m_ComputeCamera.depthTextureMode;
            m_ComputeCamera.depthTextureMode = DepthTextureMode.None;
            Color initialCameraBackgroundColor = m_ComputeCamera.backgroundColor;
            m_ComputeCamera.backgroundColor = Color.white;
            CameraClearFlags initialCameraClearFlags = m_ComputeCamera.clearFlags;
            m_ComputeCamera.clearFlags = CameraClearFlags.SolidColor;

            // render the depth with the related depth replacement shader
            m_ComputeCamera.SetReplacementShader(depthShader, "RenderType");
            m_ComputeCamera.targetTexture = m_SceneDepthTexture;
            m_ComputeCamera.Render();
            m_ComputeCamera.targetTexture = null;
            m_ComputeCamera.ResetReplacementShader();

            // set the initial inputCamera parameters
            m_ComputeCamera.clearFlags = initialCameraClearFlags;
            m_ComputeCamera.backgroundColor = initialCameraBackgroundColor;
            m_ComputeCamera.depthTextureMode = initialCameraDepthTextureMode;

            // Show depth

            //string debugSceneDepthMapFileName = Path.Combine("C:/Temp/", "sceneDepthMap.png");
            //RenderTexture.active = m_SceneDepthTexture;

            //Texture2D sceneDepthMap = new Texture2D(m_ComputeCamera.pixelWidth, m_ComputeCamera.pixelHeight, TextureFormat.RFloat, false);
            //sceneDepthMap.ReadPixels(new Rect(0.0f, 0.0f, m_ComputeCamera.pixelWidth, m_ComputeCamera.pixelHeight), 0, 0);

            //Texture2D pngSceneDepthMap = new Texture2D(m_ComputeCamera.pixelWidth, m_ComputeCamera.pixelHeight, TextureFormat.RGB24, false);

            //// not optimized at all !!
            //for (int i = 0; i < m_ComputeCamera.pixelWidth; ++i)
            //{
            //    for (int j = 0; j < m_ComputeCamera.pixelHeight; ++j)
            //    {
            //        float greyLevel = sceneDepthMap.GetPixel(i, j).r * 4.0f;
            //        Color col = new Color(greyLevel, greyLevel, greyLevel);
            //        pngSceneDepthMap.SetPixel(i, j, col);
            //    }
            //}

            //byte[] bytesSceneDepthMap = pngSceneDepthMap.EncodeToPNG();
            //File.WriteAllBytes(debugSceneDepthMapFileName, bytesSceneDepthMap);
            //RenderTexture.active = null;
        }


        private bool EvaluateVisibility(Renderer _rd, UnityEngine.Material _previous, UnityEngine.Camera camera)
        {
            m_ComputeCamera.enabled=true;

            // Set a temporary material to the renderer for the computation
            _rd.sharedMaterial = m_VisibilityMaterial;
            GenerateSceneDepthMap(camera);
            int _value = ComputeVisibility(_rd);
            _rd.sharedMaterial = _previous;
            m_ComputeCamera.enabled=false;

            // Either fully occluded(0), fully visible(1) or partialy visible(2)
            bool _result = m_AllowsPartialOcclusion ? (_value == 1 || _value == 2) : _value != 0 && _value == 1;
            return _result;
        }

        private int ComputeVisibility(Renderer _rd)
        {
            // check the position of the affected node with respect to the camera frustrum
            if(_rd == null)
            {
                Debug.LogError("Renderer is null");
                return 0;
            }

            // get the AABB of the affected node
            Bounds bbox = _rd.bounds;
            Vector3 bboxCenter = bbox.center;
            Vector3 bboxExtents = bbox.extents;

            // get the 6 camera frustrum planes
            Plane[] cameraFrustrumPlanes = GeometryUtility.CalculateFrustumPlanes(m_ComputeCamera);

            bool intersect = false;

            for (int i = 0; i < 6; ++i) // for each of the camera frustrum plane
            {
                // get the normal vector of the current plane
                Vector3 planeNormal = cameraFrustrumPlanes[i].normal;
                Vector3 planeAbsNormal = new Vector3(Mathf.Abs(planeNormal.x), Mathf.Abs(planeNormal.y), Mathf.Abs(planeNormal.z));

                // get the distance of the current plane with respect to the origin
                float planeDistance = cameraFrustrumPlanes[i].distance;

                float r = bboxExtents.x * planeAbsNormal.x + bboxExtents.y * planeAbsNormal.y + bboxExtents.z * planeAbsNormal.z;
                float s = planeNormal.x * bboxCenter.x + planeNormal.y * bboxCenter.y + planeNormal.z * bboxCenter.z;

                if (s + r < -planeDistance) // out of the frustrum
                {
                    return -1;
                }

                intersect |= (s - r <= -planeDistance);
            }

            if (intersect) // partialy in the frustrum -> return partialy visible
            {
                return 2;
            }

            // at this stage, the affected node is fully inside the camera frustrum
            //    we need now to check if the affected node is partialy or fully occluded by other nodes
            // reset the indices buffer
            m_VisiblePixelsBuffer.SetData(m_ResetVisiblePixelsBuffer);

            // create a temporary RenderTexture for the visibility computation
            RenderTexture tempTex = RenderTexture.GetTemporary(m_ComputeCamera.pixelWidth, m_ComputeCamera.pixelHeight);

            // set the _visibilityMaterial data
            m_VisibilityMaterial.SetTexture("sceneDepthTexture", m_SceneDepthTexture);
            m_VisibilityMaterial.SetBuffer("visiblePixelsBuffer", m_VisiblePixelsBuffer);
            m_VisibilityMaterial.SetMatrix("worldToViewMatrix", m_ComputeCamera.worldToCameraMatrix);
            m_VisibilityMaterial.SetMatrix("worldToScreenMatrix", m_ComputeCamera.projectionMatrix * m_ComputeCamera.worldToCameraMatrix);
            m_VisibilityMaterial.SetFloat("visibilityBias", m_VisibilityBias);
            m_VisibilityMaterial.SetFloat("cameraZFar", m_ComputeCamera.farClipPlane);

            // set the _visibilityMaterial to the mesh renderer of the affectedNode
            UnityEngine.Material initialMaterial = _rd.sharedMaterial;
            _rd.sharedMaterial = m_VisibilityMaterial;

            // compute visibility
            m_ComputeCamera.targetTexture = tempTex;
            m_ComputeCamera.Render();
            m_ComputeCamera.targetTexture = null;


            // Write visibility image each frame for debug
            
            //string debugTextureFileName = Path.Combine("C:/Temp/", "debugVisibility.png");
            //RenderTexture.active = tempTex;
            //Texture2D tex = new Texture2D(_computeCamera.pixelWidth, _computeCamera.pixelHeight, TextureFormat.RGB24, true);
            //tex.ReadPixels(new Rect(0.0f, 0.0f, _computeCamera.pixelWidth, _computeCamera.pixelHeight), 0, 0);
            //byte[] bytes_reg = tex.EncodeToPNG();
            //File.WriteAllBytes(debugTextureFileName, bytes_reg);
            //RenderTexture.active = null;

            // re-set the _visibilityMaterial
            _rd.sharedMaterial = initialMaterial;

            RenderTexture.ReleaseTemporary(tempTex);

            // retrieve the visiblePixelsBuffer data
            Int32[] visiblePixelsBufferData = new Int32[2];
            m_VisiblePixelsBuffer.GetData(visiblePixelsBufferData);

            // Debugging purpose
            //Debug.Log("maxVisiblePixels = "+ visiblePixelsBufferData[0]);
            //Debug.Log("visiblePixels = " + visiblePixelsBufferData[1]);

            // returns the related integer value. Either fully occluded (0), fully visible (1) or partialy visible (2)
            if (visiblePixelsBufferData[1] == 0)
            {
                return 0;
            }
            else if ((visiblePixelsBufferData[0] - visiblePixelsBufferData[1]) < 3)
            {
                return 1;
            }
            else
            {
                return 2;
            }
        }

        public bool IsVisible(UnityEngine.Camera _camera)
        {
            m_ComputeCamera.transform.position = _camera.transform.position;
            m_ComputeCamera.transform.rotation = _camera.transform.rotation;

            // create the _sceneDepthTexture if necessary
            if (m_SceneDepthTexture == null)
            {
                m_SceneDepthTexture = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 16, RenderTextureFormat.RFloat);
                m_SceneDepthTexture.filterMode = FilterMode.Point;
            }

            // No node extension for this game object
            if(m_Target == null)
            {
                return EvaluateVisibility(m_CurrentRenderer, m_CurrentMaterial, _camera);
            }

            for (int i = 0; i < m_Target.Length; i++)
            {
                bool _result = EvaluateVisibility(m_Target[i], m_TargetMaterials[i], _camera);
                if(!_result)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDestroy()
        {
            m_VisiblePixelsBuffer.Dispose();
        }

        internal void Dispose()
        {
            if(m_ComputeCamera != null)
            {
                Destroy(m_ComputeCamera.gameObject);
            }
            Destroy(gameObject);
        }
    }
}