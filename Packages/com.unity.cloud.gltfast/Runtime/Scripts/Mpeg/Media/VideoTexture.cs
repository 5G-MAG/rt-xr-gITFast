/*
 * Copyright (c) 2023 MotionSpell
 * Licensed under the License terms and conditions for use, reproduction,
 * and distribution of 5GMAG software (the “License”).
 * You may not use this file except in compliance with the License.
 * You may obtain a copy of the License at https://www.5g-mag.com/license .
 * Unless required by applicable law or agreed to in writing, software distributed under the License is
 * distributed on an “AS IS” BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and limitations under the License.
 */

using System.IO;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

using Unity.Collections;


namespace GLTFast
{

    public class VideoTexturePassThrough
    {
        public Texture2D texture;
        public int bufferId;

        public VideoTexturePassThrough(Texture2D tex2D, int buffer)
        {
            texture = tex2D;
            bufferId = buffer;
        }

        public void UpdateData(NativeArray<byte> data)
        {
            texture.LoadRawTextureData(data); // supply data to unity
            texture.Apply(); // upload to GPU
        }

        public void SaveTexturePNG(string path)
        {
            byte[] bytes = texture.EncodeToPNG();
            string dirPath = Path.GetDirectoryName(path);
            if (Path.GetExtension(path) != ".png")
            {
                path = Path.ChangeExtension(path, ".png");
            }
            if (!System.IO.Directory.Exists(dirPath))
            {
                System.IO.Directory.CreateDirectory(dirPath);
            }
            System.IO.File.WriteAllBytes(path, bytes);
        }

    }

    public static class VideoBufferFormat
    {
        // additional yuv formats may be implemented,
        // see: https://registry.khronos.org/vulkan/specs/1.3-extensions/man/html/VkFormat.html
        public const string
            RED = "RED",
            GREEN = "GREEN",
            BLUE = "BLUE",
            RG = "RG",
            RGB = "RGB",
            RGBA = "RGBA",
            BGR = "BGR",
            BGRA = "BGRA",
            DEPTH_COMPONENT = "DEPTH_COMPONENT",
            // With planar formats, we must assume contiguous memory
            VK_FORMAT_G8_B8_R8_3PLANE_420_UNORM = "VK_FORMAT_G8_B8_R8_3PLANE_420_UNORM",
            G8_B8_R8_3PLANE_420_UNORM = VK_FORMAT_G8_B8_R8_3PLANE_420_UNORM,
            VK_FORMAT_G8_B8R8_2PLANE_420_UNORM_KHR = "VK_FORMAT_G8_B8R8_2PLANE_420_UNORM_KHR",
            G8_B8R8_2PLANE_420_UNORM_KHR = VK_FORMAT_G8_B8R8_2PLANE_420_UNORM_KHR;

        public static bool IsSupportedFormat(string format)
        {
            return format switch
            {
                RED => true,
                GREEN => true,
                BLUE => true,
                RG => true,
                RGB => true,
                RGBA => true,
                BGR => true,
                BGRA => true,
                DEPTH_COMPONENT => true,
                G8_B8_R8_3PLANE_420_UNORM => true,
                G8_B8R8_2PLANE_420_UNORM_KHR => true,
                _ => false,
            };
        }

        public static bool IsMultiPlanarFormat(string format)
        {
            return format switch
            {
                G8_B8_R8_3PLANE_420_UNORM => true,
                G8_B8R8_2PLANE_420_UNORM_KHR => true,
                _ => false,
            };
        }

    }

    public class VideoTexture
    {

        public UnityEngine.Texture texture;
        public int bufferId;
        public string bufferFormat;
        public bool vflip = false;
        
        private ComputeShader shader;
        private Texture2D texSrc;
        private RenderTexture texOut;

        private int kernel;

        public VideoTexture(Texture2D tex, int buffer, int width, int height, string fmt, bool flip = false)
        {
            // the pre-existing texture, is expected to  
            texture = tex;
            
            // the media pipeline buffer from which we read texture
            bufferId = buffer;

            // check that we can perform compute shader 
            Assert.IsTrue((SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) == CopyTextureSupport.RTToTexture);

            // loads raw maf buffer to a texture prior to further processing
            texSrc = new Texture2D(width, height, TextureFormat.RGB24, false, true);

            // texOut = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            texOut = new RenderTexture(width, height, 0, DefaultFormat.LDR);
            texOut.enableRandomWrite = true;
            Assert.IsTrue(texOut.Create());

            // use a compute shader to flip texture and/or perform color space conversion
            shader = (ComputeShader)Resources.Load("FlipTexture");
            kernel = shader.FindKernel("CSMain");

            shader.SetInt("width", width);
            shader.SetInt("height", height);
            shader.SetTexture(kernel, "texSrc", texSrc);
            shader.SetTexture(kernel, "texOut", texOut);
        }

        public RenderTexture GetRenderTexture()
        {
            return texOut;
        }

        public void UpdateData(NativeArray<byte> data)
        {
            // TODO: as it turns out calling unity's API must be done from its main thread ! so GPU upload will indeed be an issue,
            // investigate alternative implementation based on: https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.IssuePluginCustomTextureUpdateV2.html
            texSrc.LoadRawTextureData(data); // pass CPU buffer to unity
            texSrc.Apply(); // process and upload to GPU, slow ...
            
            // FIXME; texture format and yuv to rgb conversion
            shader.Dispatch(kernel, texture.width / 8, texture.height / 8, 1);
            Graphics.CopyTexture(texOut, texture); 
        }

    }



    public class VideoTextureYUV
    {
        public Texture2D texture;
        public int bufferId;

        private ComputeShader shader;
        private Texture2D yPlane;
        private Texture2D uPlane;
        private Texture2D vPlane;
        private RenderTexture rgbTex;

        private int kernel;

        public VideoTextureYUV(Texture2D tex2D, int buffer, string videoBufferFormat)
        {
            texture = tex2D;
            bufferId = buffer;
            
            bool supported = SystemInfo.copyTextureSupport == CopyTextureSupport.RTToTexture;

            // 420 planar 8 bit
            TextureFormat planeFmt = TextureFormat.R8; // BAD, we get unsigned bytes not signed int
            yPlane = new Texture2D(texture.width, texture.height, planeFmt, false);
            
            int uvWidth = texture.width / 4;
            int uvHeight = texture.height / 4;
            uPlane = new Texture2D(uvWidth, uvHeight, planeFmt, false);
            vPlane = new Texture2D(uvWidth, uvHeight, planeFmt, false);

            rgbTex = new RenderTexture(texture.width, texture.height, 0);
            rgbTex.graphicsFormat = texture.graphicsFormat;
            rgbTex.enableRandomWrite = true;
            bool ok = rgbTex.Create();

            // compute shader
            shader = (ComputeShader)Resources.Load("YuvToRGB");
            kernel = shader.FindKernel("CSMain");

            shader.SetInt("width", texture.width);
            shader.SetInt("height", texture.height);
            shader.SetTexture(kernel, "yPlane", yPlane);
            shader.SetTexture(kernel, "uPlane", uPlane);
            shader.SetTexture(kernel, "vPlane", vPlane);
            shader.SetTexture(kernel, "result", rgbTex);

        }

        public void UpdateData(NativeArray<byte> data)
        {
            // contiguous planes
            int ySize = yPlane.width + yPlane.height;
            yPlane.LoadRawTextureData(data.GetSubArray(0, ySize));
            yPlane.Apply();
            int uSize = uPlane.width * uPlane.height;
            uPlane.LoadRawTextureData(data.GetSubArray(ySize, uSize));
            uPlane.Apply();
            int vSize = vPlane.width * vPlane.height;
            vPlane.LoadRawTextureData(data.GetSubArray(ySize+uSize, vSize));
            vPlane.Apply();

            // shader.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            shader.Dispatch(kernel, 8, 8, 1);
            Graphics.CopyTexture(rgbTex, texture);
            
        }

    }

}