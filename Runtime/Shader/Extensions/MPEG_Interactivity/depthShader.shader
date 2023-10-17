
Shader "InterDigital/depthShader" {
	Properties
	{

	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{

			CGPROGRAM
			// to use the built-in helper functions
			#include "UnityCG.cginc"
			// use "vert" function as the vertex shader
			#pragma vertex vert
			// use "frag" function as the pixel (fragment) shader
			#pragma fragment frag

			#pragma target 3.0

			// vertex shader inputs
			struct vertIn
			{
				float4 vertex : POSITION; // vertex position
			};

			// vertex shader outputs
			struct vertOut
			{
				float depth       : TEXCOORD0;   // linear depth
				float4 projPos    : SV_POSITION; // clip space position
			};

			// fragment shader outputs
			struct fragOut
			{
				float4 color : SV_Target;
			};


			// vertex shader
			vertOut vert(vertIn v)
			{
				vertOut o;

				// computes the linear depth
				o.depth = COMPUTE_DEPTH_01;

				// calculates the clip space position
				o.projPos = UnityObjectToClipPos(v.vertex.xyz);

				return o;

			}


			// fragment shader
			fragOut frag(vertOut i)
			{
				fragOut o;

				// outputs the linear depth
				o.color = float4(i.depth, 0.0, 0.0, 0.0);
				
				return o;

			}
			ENDCG
		}
	}
	Fallback off
}
