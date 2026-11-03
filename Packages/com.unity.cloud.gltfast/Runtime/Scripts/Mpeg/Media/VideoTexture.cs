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

        // Number of components of the accessor elements the media pipeline stores for a format.
        // The planar YCbCr formats are stored as bytes (SCALAR), see PlanarSize.
        public static int ComponentCount(string format)
        {
            return format switch
            {
                RED => 1,
                GREEN => 1,
                BLUE => 1,
                DEPTH_COMPONENT => 1,
                G8_B8_R8_3PLANE_420_UNORM => 1,
                G8_B8R8_2PLANE_420_UNORM_KHR => 1,
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

        // Bytes of a picture in a planar YCbCr 4:2:0 format: a luma plane, then the chroma planes at half the width and
        // height (rounded up), as the media pipeline stores them
        public static int PlanarSize(int width, int height)
        {
            return width * height + 2 * ((width + 1) / 2) * ((height + 1) / 2);
        }

        // The pixel format the media pipeline outputs (BufferInfo.outputMedia.format, ISO/IEC 23090-14, clause 6.2)
        public static maf.ColorFormat ToColorFormat(string format)
        {
            switch (format)
            {
                case RED: return maf.ColorFormat.RED;
                case GREEN: return maf.ColorFormat.GREEN;
                case BLUE: return maf.ColorFormat.BLUE;
                case RG: return maf.ColorFormat.RG;
                case RGBA: return maf.ColorFormat.RGBA;
                case BGR: return maf.ColorFormat.BGR;
                case BGRA: return maf.ColorFormat.BGRA;
                case DEPTH_COMPONENT: return maf.ColorFormat.DEPTH_COMPONENT;
                case G8_B8_R8_3PLANE_420_UNORM: return maf.ColorFormat.G8_B8_R8_3PLANE_420_UNORM;
                case G8_B8R8_2PLANE_420_UNORM_KHR: return maf.ColorFormat.G8_B8R8_2PLANE_420_UNORM;
                default: return maf.ColorFormat.RGB;
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
        // the picture, or the luma plane of a planar YCbCr format
        private Texture2D texSrc;
        // the chroma planes of a planar YCbCr format: Cb and Cr, or both interleaved in texCb
        private Texture2D texCb;
        private Texture2D texCr;
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
        private readonly bool planar;
        private readonly bool twoPlanes;
        // conversion of the YCbCr samples to RGB (MPEG_sampler_YCbCr)
        private readonly int ycbcrModel;
        private readonly bool ycbcrNarrow;
        private readonly float kr;
        private readonly float kb;
        // timed accessor information header at the start of each buffer frame
        private readonly bool timedHeader;
        private readonly bool immutable;
        private readonly int dataOffset;
        private bool sizeWarningLogged = false;

        // ycbcr: the YCbCr conversion of a planar format, BT.709 narrow range when null
        public VideoTexture(Texture2D tex, int buffer, int width, int height, string fmt, bool flip = false, bool hasTimedHeader = false, bool headerImmutable = true, int staticDataOffset = 0, Schema.MpegSamplerYCbCr ycbcr = null)
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
                case VideoBufferFormat.G8_B8_R8_3PLANE_420_UNORM:
                    sourceFormat = TextureFormat.R8; bytesPerPixel = 1; shaderMode = 7; planar = true;
                    break;
                case VideoBufferFormat.G8_B8R8_2PLANE_420_UNORM_KHR:
                    sourceFormat = TextureFormat.R8; bytesPerPixel = 1; shaderMode = 8; planar = true; twoPlanes = true;
                    break;
                default:
                    sourceFormat = TextureFormat.RGB24; bytesPerPixel = 3; shaderMode = 0;
                    break;
            }

            if (planar)
            {
                var model = ycbcr != null ? ycbcr.Model : Schema.MpegSamplerYCbCr.YCbCrModel.YCBCR_709;
                var range = ycbcr != null ? ycbcr.Range : Schema.MpegSamplerYCbCr.YCbCrRange.NARROW;
                ycbcrModel = (int)model;
                ycbcrNarrow = range == Schema.MpegSamplerYCbCr.YCbCrRange.NARROW;
                // luma coefficients of the colour models (ITU-R BT.601, BT.709, BT.2020)
                switch (model)
                {
                    case Schema.MpegSamplerYCbCr.YCbCrModel.YCBCR_601:
                        kr = 0.299f; kb = 0.114f;
                        break;
                    case Schema.MpegSamplerYCbCr.YCbCrModel.YCBCR_2020:
                        kr = 0.2627f; kb = 0.0593f;
                        break;
                    default:
                        kr = 0.2126f; kb = 0.0722f;
                        break;
                }
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
            if (texCb != null)
            {
                UnityEngine.Object.Destroy(texCb);
                texCb = null;
            }
            if (texCr != null)
            {
                UnityEngine.Object.Destroy(texCr);
                texCr = null;
            }
            if (texOut != null)
            {
                texOut.Release();
                UnityEngine.Object.Destroy(texOut);
            }

            // loads raw maf buffer to a texture prior to further processing
            texSrc = new Texture2D(width, height, sourceFormat, false, true);
            if (planar)
            {
                int chromaWidth = (width + 1) / 2;
                int chromaHeight = (height + 1) / 2;
                if (twoPlanes)
                {
                    texCb = new Texture2D(chromaWidth, chromaHeight, TextureFormat.RG16, false, true);
                }
                else
                {
                    texCb = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false, true);
                    texCr = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false, true);
                }
            }

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
            // 5.2.2.3: the stride of the data may change from frame to frame; 0 means tightly packed. The planes of a planar
            // format are always tightly packed.
            if (planar)
            {
                stride = 0;
            }
            int size = planar ? VideoBufferFormat.PlanarSize(width, height) : (stride > rowBytes ? stride * (height - 1) + rowBytes : rowBytes * height);
            // a frame holds the picture in the requested format, and nothing more: the last row may keep its padding
            int maxLength = offset + (stride > rowBytes ? stride * height : size);
            if (offset < 0 || offset + size > frameData.Length || frameData.Length > maxLength)
            {
                if (!sizeWarningLogged)
                {
                    if (offset >= 0 && frameData.Length > maxLength)
                    {
                        // e.g. a pipeline that does not know the format falls back to RGB
                        Debug.LogWarning("video buffer " + bufferId + ": the media pipeline wrote " + (frameData.Length - offset) + " bytes for a " + width + " x " + height + " picture, the format " + bufferFormat + " needs " + size + ": the media pipeline does not output the requested format (BufferInfo.outputMedia.format), the video frames are not shown");
                    }
                    else
                    {
                        Debug.LogWarning("video buffer " + bufferId + ": the frame holds " + frameData.Length + " bytes, a " + width + " x " + height + " picture in the format " + bufferFormat + " at offset " + offset + " needs " + size + ", the video frames are not shown");
                    }
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
            if (planar)
            {
                int lumaSize = width * height;
                int chromaSize = ((width + 1) / 2) * ((height + 1) / 2);
                texSrc.LoadRawTextureData(data.GetSubArray(0, lumaSize));
                texSrc.Apply();
                if (twoPlanes)
                {
                    texCb.LoadRawTextureData(data.GetSubArray(lumaSize, 2 * chromaSize));
                    texCb.Apply();
                }
                else
                {
                    texCb.LoadRawTextureData(data.GetSubArray(lumaSize, chromaSize));
                    texCb.Apply();
                    texCr.LoadRawTextureData(data.GetSubArray(lumaSize + chromaSize, chromaSize));
                    texCr.Apply();
                }
            }
            else
            {
                texSrc.LoadRawTextureData(data); // pass CPU buffer to unity
                texSrc.Apply(); // process and upload to GPU, slow ...
            }

            // the compute shader is shared by all video textures: set its inputs for this one
            shader.SetInt("width", width);
            shader.SetInt("height", height);
            shader.SetInt("mode", shaderMode);
            shader.SetFloat("gain", Gain);
            shader.SetInt("ycbcrModel", ycbcrModel);
            shader.SetInt("ycbcrNarrow", ycbcrNarrow ? 1 : 0);
            shader.SetFloat("kr", kr);
            shader.SetFloat("kb", kb);
            shader.SetTexture(kernel, "texSrc", texSrc);
            // every texture of the kernel must be set, the chroma planes are only read in the YCbCr modes
            shader.SetTexture(kernel, "texCb", texCb != null ? texCb : texSrc);
            shader.SetTexture(kernel, "texCr", texCr != null ? texCr : texSrc);
            shader.SetTexture(kernel, "texOut", texOut);
            shader.Dispatch(kernel, (width + 7) / 8, (height + 7) / 8, 1);
            Graphics.CopyTexture(texOut, 0, 0, target, 0, 0);
        }

    }

}
