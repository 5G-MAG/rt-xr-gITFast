// Schema classes for MPEG_lights_punctual (8.4.2.2, Table 58), MPEG_lights_texture_based
// (8.4.2.1, Tables 55 to 57) and MPEG_node_avatar (8.3.2, Tables 53 and 54).

namespace GLTFast.Schema {

    /// <summary>
    /// MPEG_lights_punctual, glTF file level extension. "nature" is read as a string (registry
    /// schema); Table 58 lists the numeric values PHYSICAL = 0 to UNSPECIFIED = 3.
    /// </summary>
    [System.Serializable]
    public class MpegLightsPunctual {
        public int light = -1;
        public string nature;
        public int color_accessor = -1;
        public int intensity_accessor = -1;
        public int range_accessor = -1;
    }

    /// <summary>
    /// Item of the lights array of MPEG_lights_texture_based (Table 56).
    /// </summary>
    [System.Serializable]
    public class MpegTextureBasedLight {
        public string name;
        public string nature;
        public float[] position;
        public string projection;
        public int rotationAccessor = -1;
        public int intensityAccessor = -1;
        public int irradianceAccessor = -1;
        public int[] specularImages;
    }

    /// <summary>
    /// MPEG_lights_texture_based, glTF file level extension (Table 55).
    /// </summary>
    [System.Serializable]
    public class MpegLightsTextureBased {
        public MpegTextureBasedLight[] lights;
    }

    /// <summary>
    /// MPEG_lights_texture_based, scene level extension (Table 57).
    /// </summary>
    [System.Serializable]
    public class MpegLightsTextureBasedScene {
        public int[] light;
    }

    /// <summary>
    /// Mapping object of MPEG_node_avatar (Table 54).
    /// </summary>
    [System.Serializable]
    public class MpegAvatarMapping {
        public string path;
        public int node = -1;
    }

    /// <summary>
    /// MPEG_node_avatar, extension of a node object (Table 53).
    /// </summary>
    [System.Serializable]
    public class MpegNodeAvatar {
        public string type;
        public MpegAvatarMapping[] mappings;

        public bool IsPresent {
            get { return !string.IsNullOrEmpty(type) || mappings != null; }
        }
    }
}
