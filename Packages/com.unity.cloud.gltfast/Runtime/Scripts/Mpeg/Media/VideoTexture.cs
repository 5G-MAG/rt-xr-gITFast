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

        // Number of components of the pixels the media pipeline stores for a format.
        // The planar YCbCr formats are converted to RGB by the pipeline.
        public static int ComponentCount(string format)
        {
            return format switch
            {
                RED => 1,
                GREEN => 1,
                BLUE => 1,
                DEPTH_COMPONENT => 1,
                RG => 2,
                RGBA => 4,
                BGRA => 4,
                _ => 3,
            };
        }

        public static GLTFast.Schema.GltfAccessorAttributeType AccessorType(int components)
        {
            switch (components)
            {
                case 1: return GLTFast.Schema.GltfAccessorAttributeType.SCALAR;
                case 2: return GLTFast.Schema.GltfAccessorAttributeType.VEC2;
                case 4: return GLTFast.Schema.GltfAccessorAttributeType.VEC4;
                default: return GLTFast.Schema.GltfAccessorAttributeType.VEC3;
            }
        }

        // Formats other than RGB that the media pipeline produces directly.
        public static bool TryGetAttributeType(string format, out maf.AttributeType type)
        {
            switch (format)
            {
                case RED: type = maf.AttributeType.TEXTURE_RED; return true;
                case GREEN: type = maf.AttributeType.TEXTURE_GREEN; return true;
                case BLUE: type = maf.AttributeType.TEXTURE_BLUE; return true;
                case RG: type = maf.AttributeType.TEXTURE_RG; return true;
                case RGBA: type = maf.AttributeType.TEXTURE_RGBA; return true;
                case BGR: type = maf.AttributeType.TEXTURE_BGR; return true;
                case BGRA: type = maf.AttributeType.TEXTURE_BGRA; return true;
                case DEPTH_COMPONENT: type = maf.AttributeType.TEXTURE_DEPTH_COMPONENT; return true;
                default: type = maf.AttributeType.TEXTURE_RGB; return false;
            }
        }

    }

    public class VideoTexture
    {

        // brightness factor applied to the picture (application setting, 1 leaves the picture as it is)
        public static float Gain = 1f;

        public UnityEngine.Texture texture;
        public int bufferId;
        public string bufferFormat;
        public bool vflip = false;

        private ComputeShader shader;
        private Texture2D texSrc;
        private RenderTexture texOut;
        private Texture2D target;

        private int kernel;

        // size of the picture currently held by texSrc and texOut
        private int pictureWidth;
        private int pictureHeight;
        // layout of the pixels the media pipeline stores for the format
        private readonly TextureFormat sourceFormat;
        private readonly int bytesPerPixel;
        private readonly int shaderMode;
        // timed accessor information header at the start of each buffer frame
        private readonly bool timedHeader;
        private readonly bool immutable;
        private readonly int dataOffset;
        private bool sizeWarningLogged = false;

        public VideoTexture(Texture2D tex, int buffer, int width, int height, string fmt, bool flip = false, bool hasTimedHeader = false, bool headerImmutable = true, int staticDataOffset = 0)
        {
            // the pre-existing texture, is expected to
            texture = tex;
            target = tex;
            bufferFormat = fmt;

            // the media pipeline buffer from which we read texture
            bufferId = buffer;

            timedHeader = hasTimedHeader;
            immutable = headerImmutable;
            dataOffset = staticDataOffset;

            // pixel layout of the buffer data, and how the shader turns it into an RGBA texture
            switch (fmt)
            {
                case VideoBufferFormat.RED:
                    sourceFormat = TextureFormat.R8; bytesPerPixel = 1; shaderMode = 2;
                    break;
                case VideoBufferFormat.GREEN:
                    sourceFormat = TextureFormat.R8; bytesPerPixel = 1; shaderMode = 3;
                    break;
                case VideoBufferFormat.BLUE:
                    sourceFormat = TextureFormat.R8; bytesPerPixel = 1; shaderMode = 4;
                    break;
                case VideoBufferFormat.RG:
                    sourceFormat = TextureFormat.RG16; bytesPerPixel = 2; shaderMode = 5;
                    break;
                case VideoBufferFormat.DEPTH_COMPONENT:
                    sourceFormat = TextureFormat.R8; bytesPerPixel = 1; shaderMode = 6;
                    break;
                case VideoBufferFormat.RGBA:
                    sourceFormat = TextureFormat.RGBA32; bytesPerPixel = 4; shaderMode = 0;
                    break;
                case VideoBufferFormat.BGRA:
                    sourceFormat = TextureFormat.RGBA32; bytesPerPixel = 4; shaderMode = 1;
                    break;
                case VideoBufferFormat.BGR:
                    sourceFormat = TextureFormat.RGB24; bytesPerPixel = 3; shaderMode = 1;
                    break;
                default:
                    sourceFormat = TextureFormat.RGB24; bytesPerPixel = 3; shaderMode = 0;
                    break;
            }

            // check that we can perform compute shader
            Assert.IsTrue((SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) == CopyTextureSupport.RTToTexture);

            // use a compute shader to flip texture and/or perform color space conversion
            shader = (ComputeShader)Resources.Load("FlipTexture");
            kernel = shader.FindKernel("CSMain");

            AllocatePicture(width, height);
        }

        // (Re)creates the textures holding a picture of the given size.
        private void AllocatePicture(int width, int height)
        {
            if (texSrc != null)
            {
                UnityEngine.Object.Destroy(texSrc);
            }
            if (texOut != null)
            {
                texOut.Release();
                UnityEngine.Object.Destroy(texOut);
            }

            // loads raw maf buffer to a texture prior to further processing
            texSrc = new Texture2D(width, height, sourceFormat, false, true);

            // texOut = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            texOut = new RenderTexture(width, height, 0, DefaultFormat.LDR);
            texOut.enableRandomWrite = true;
            Assert.IsTrue(texOut.Create());

            pictureWidth = width;
            pictureHeight = height;
        }

        public RenderTexture GetRenderTexture()
        {
            return texOut;
        }

        // Uploads a picture of the declared size, stored at the start of data.
        public void UpdateData(NativeArray<byte> data)
        {
            UploadPicture(data, pictureWidth, pictureHeight);
        }

        // Uploads the picture of a buffer frame. The picture is stored at the size output by the decoder: the frame header gives
        // that size (Table 25 dimensions flag), otherwise the declared size is used; the texture takes the size of the picture
        // (clause 5.3.1.3). When the accessor has a timed accessor information header, the header gives where the pixels are.
        public void UpdateFrame(maf.Frame frame, NativeArray<byte> frameData)
        {
            int width = pictureWidth;
            int height = pictureHeight;
            if ((frame.extraFrameInfoFlags & 0x01) != 0 && frame.width > 0 && frame.height > 0)
            {
                width = (int)frame.width;
                height = (int)frame.height;
            }

            int offset = dataOffset;
            int stride = 0;
            if (timedHeader)
            {
                var header = new maf.BufferInfoHeader();
                header.immutable = immutable;
                header.componentType = maf.ComponentType.UNSIGNED_BYTE;
                header.type = (maf.SampleType)(VideoBufferFormat.ComponentCount(bufferFormat) - 1);
                header.ReadAccessorHeader(frame.data);
                // the dynamic accessor and bufferView information take precedence over the scene description (5.2.2.3)
                offset = (int)(header.bufferViewByteOffset + header.byteOffset);
                stride = (int)header.bufferViewByteStride;
                header.Dispose();
            }

            int rowBytes = width * bytesPerPixel;
            // 5.2.2.3: the stride of the data may change from frame to frame; 0 means tightly packed
            int size = stride > rowBytes ? stride * (height - 1) + rowBytes : rowBytes * height;
            if (offset < 0 || offset + size > frameData.Length)
            {
                if (!sizeWarningLogged)
                {
                    Debug.LogWarning("video frame holds " + frameData.Length + " bytes, a " + width + " x " + height + " picture at offset " + offset + " needs " + size);
                    sizeWarningLogged = true;
                }
                return;
            }
            if (stride > rowBytes)
            {
                // rows are stored with padding: repack them
                var packed = new NativeArray<byte>(rowBytes * height, Allocator.Temp);
                for (int y = 0; y < height; y++)
                {
                    NativeArray<byte>.Copy(frameData, offset + y * stride, packed, y * rowBytes, rowBytes);
                }
                UploadPicture(packed, width, height);
                packed.Dispose();
                return;
            }
            UploadPicture(frameData.GetSubArray(offset, size), width, height);
        }

        private void UploadPicture(NativeArray<byte> data, int width, int height)
        {
            if (width != pictureWidth || height != pictureHeight)
            {
                AllocatePicture(width, height);
            }
            if (target.width != width || target.height != height)
            {
                // the texture takes the size of the picture
                // without a mip chain: the picture is copied into level 0 only, and the static image of the scene may be of another size
                target.Reinitialize(width, height, target.format, false);
                target.Apply(false, false);
            }

            // TODO: as it turns out calling unity's API must be done from its main thread ! so GPU upload will indeed be an issue,
            // investigate alternative implementation based on: https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.IssuePluginCustomTextureUpdateV2.html
            texSrc.LoadRawTextureData(data); // pass CPU buffer to unity
            texSrc.Apply(); // process and upload to GPU, slow ...

            // the compute shader is shared by all video textures: set its inputs for this one
            shader.SetInt("width", width);
            shader.SetInt("height", height);
            shader.SetInt("mode", shaderMode);
            shader.SetFloat("gain", Gain);
            shader.SetTexture(kernel, "texSrc", texSrc);
            shader.SetTexture(kernel, "texOut", texOut);
            // FIXME; yuv to rgb conversion
            shader.Dispatch(kernel, (width + 7) / 8, (height + 7) / 8, 1);
            Graphics.CopyTexture(texOut, 0, 0, target, 0, 0);
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