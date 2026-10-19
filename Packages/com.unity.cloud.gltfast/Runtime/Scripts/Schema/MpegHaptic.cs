using System;

namespace GLTFast.Schema
{
    /// <summary>
    /// MPEG_haptic root extension (ISO/IEC 23090-14:2024, Annex G.3.2, Tables 72 and 73).
    /// </summary>
    [Serializable]
    public class MpegHaptic
    {
        /// <summary>List of haptic objects; null when the extension is absent from the file.</summary>
        public MpegHapticObject[] hapticObjects;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

    [Serializable]
    public class MpegHapticObject
    {
        /// <summary>
        /// Accessors to one or more media sources in the MPEG_media media array containing haptic media files.
        /// </summary>
        public int[] accessors;
    }

    /// <summary>
    /// MPEG_haptic_material root extension (Annex G.3.3, Tables 76 and 77).
    /// </summary>
    [Serializable]
    public class MpegHapticMaterial
    {
        /// <summary>List of haptic materials; null when the extension is absent from the file.</summary>
        public MpegHapticMaterialItem[] materials;

        internal void GltfSerialize(JsonWriter writer)
        {
            throw new NotImplementedException($"GltfSerialize missing on {GetType()}");
        }
    }

    [Serializable]
    public class MpegHapticMaterialItem
    {
        public string name;

        /// <summary>Index of the hapticObjects element used by Reference textures, -1 when absent.</summary>
        public int haptic = -1;

        public MpegHapticTexture[] stiffness;
        public MpegHapticTexture[] friction;
        public MpegHapticTexture[] vibrotactileTexture;
        public MpegHapticTexture[] temperature;
        public MpegHapticTexture[] vibration;
        public MpegHapticTexture[] custom;
    }

    /// <summary>
    /// A haptic texture and its type (registry schema: High_Resolution, Low_Resolution, Reference, Other).
    /// </summary>
    [Serializable]
    public class MpegHapticTexture
    {
        public TextureInfo texture;

        /// <summary>Texture type; the registry default is High_Resolution.</summary>
        public string type = "High_Resolution";
    }

    /// <summary>
    /// MPEG_haptic_material mesh extension (Annex G.3.3, Table 78).
    /// </summary>
    [Serializable]
    public class MpegHapticMaterialMesh
    {
        /// <summary>Index in the materials array of the root extension, -1 when absent.</summary>
        public int hapticMaterialIndex = -1;
    }

    /// <summary>
    /// glTF mesh extensions
    /// </summary>
    [Serializable]
    public class MeshExtensions
    {
        public MpegHapticMaterialMesh MPEG_haptic_material;
        // MPEG_mesh_linking (ISO/IEC 23090-14:2024, 5.3.2)
        public MpegMeshLinking MPEG_mesh_linking;
    }
}
