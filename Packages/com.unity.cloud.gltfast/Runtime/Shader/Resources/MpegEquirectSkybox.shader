// Equirectangular sky used by MPEG_lights_texture_based when the built-in Skybox/Panoramic shader is not part of
// the player build. It sits in a Resources folder so that Unity includes it in every build; Shader.Find finds it
// by name. Properties match the ones the light controller sets (_MainTex, _Exposure, _Rotation in degrees).
Shader "MPEG/EquirectSkybox"
{
    Properties
    {
        _MainTex ("Equirectangular (2D)", 2D) = "grey" {}
        _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            half _Exposure;
            float _Rotation;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float3 RotateAroundY(float3 d, float degrees)
            {
                float a = degrees * UNITY_PI / 180.0;
                float s, c;
                sincos(a, s, c);
                return float3(c * d.x + s * d.z, d.y, -s * d.x + c * d.z);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.dir = RotateAroundY(v.vertex.xyz, _Rotation);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float2 uv = float2(atan2(d.x, d.z) / (2.0 * UNITY_PI) + 0.5, asin(d.y) / UNITY_PI + 0.5);
                half4 tex = tex2Dlod(_MainTex, float4(uv, 0, 0));
                half3 c = DecodeHDR(tex, _MainTex_HDR) * _Exposure;
                return half4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
