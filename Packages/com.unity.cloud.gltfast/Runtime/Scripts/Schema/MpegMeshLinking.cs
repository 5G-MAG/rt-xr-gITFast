// Schema classes for MPEG_mesh_linking (5.3.2, Table 12) and MPEG_primitive_V3C (G.1.2, Tables 60 to 62).

namespace GLTFast.Schema {

    /// <summary>
    /// MPEG_mesh_linking, extension of a mesh object (the dependent mesh).
    /// </summary>
    [System.Serializable]
    public class MpegMeshLinking {
        /// <summary>Timed accessor with the correspondence values (SCALAR, unsigned component type).</summary>
        public int correspondence = -1;
        /// <summary>Index of the shadow mesh in the meshes array.</summary>
        public int mesh = -1;
        /// <summary>Timed accessor with the pose transformations (FLOAT, MAT4).</summary>
        public int pose = -1;
        /// <summary>Optional timed accessor with the morph target weights of the shadow mesh (FLOAT, SCALAR).</summary>
        public int weights = -1;

        public bool IsPresent {
            get { return correspondence >= 0 || mesh >= 0 || pose >= 0 || weights >= 0; }
        }
    }

    /// <summary>
    /// _MPEG_V3C_AD (Table 61).
    /// </summary>
    [System.Serializable]
    public class MpegPrimitiveV3CAtlasData {
        public string buffer_format;
        public int accessor = -1;
    }

    /// <summary>
    /// One entry of _MPEG_V3C_AVD (Table 62).
    /// </summary>
    [System.Serializable]
    public class MpegPrimitiveV3CAttribute {
        public int type;
        public int[] maps;
    }

    /// <summary>
    /// MPEG_primitive_V3C, extension of a mesh primitive (Table 60). Parsed only: V3C
    /// reconstruction is not implemented, the primitive attributes are used instead (G.1.1).
    /// _MPEG_V3C_CAD is not parsed (its members are defined in ISO/IEC 23090-5).
    /// </summary>
    [System.Serializable]
    public class MpegPrimitiveV3C {
        public int _MPEG_V3C_CONFIG = -1;
        public MpegPrimitiveV3CAtlasData _MPEG_V3C_AD;
        public int[] _MPEG_V3C_GVD_MAPS;
        public int _MPEG_V3C_OVD_MAP = -1;
        public MpegPrimitiveV3CAttribute[] _MPEG_V3C_AVD;

        public bool IsPresent {
            get {
                return _MPEG_V3C_CONFIG >= 0
                    || _MPEG_V3C_GVD_MAPS != null
                    || _MPEG_V3C_OVD_MAP >= 0
                    || _MPEG_V3C_AVD != null
                    || (_MPEG_V3C_AD != null && _MPEG_V3C_AD.accessor >= 0);
            }
        }
    }
}
