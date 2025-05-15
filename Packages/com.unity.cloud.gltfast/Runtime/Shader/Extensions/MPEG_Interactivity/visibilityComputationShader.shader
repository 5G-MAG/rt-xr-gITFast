
Shader "InterDigital/visibilityComputationShader"
{
	Properties
	{

	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		// visibility computation
		Pass
		{
			CGPROGRAM
			// to use the built-in helper functions
			#include "UnityCG.cginc"
			// use "vert" function as the vertex shader
			#pragma vertex vert
			// use "frag" function as the pixel (fragment) shader
			#pragma fragment frag

			#pragma target 4.5

			// vertex shader inputs
			struct vertIn
			{
				float4 vertex : POSITION; // vertex position
			};

			// vertex shader outputs
			struct vertOut
			{
				float3 worldPos : TEXCOORD0; // world position
				float4 projPos  : SV_POSITION; // clip space position
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

				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				// calculates the clip space position
				o.projPos = UnityObjectToClipPos(v.vertex.xyz);

				return o;

			}

			sampler2D sceneDepthTexture;
			RWStructuredBuffer<int> visiblePixelsBuffer : register(u1); // size = 2: (maxVisible, visible) pixels
			float4x4 worldToViewMatrix;
			float4x4 worldToScreenMatrix;
			float visibilityBias;
			float cameraZFar;

			// fragment shader
			fragOut frag(vertOut i)
			{
				fragOut o;

				// add a visible pixel to the maxVisiblePixels counter
				InterlockedAdd(visiblePixelsBuffer[0], 1);

				//
				// check if the pixel is occluded
				//

				// retrieve the pixel world coordinates
				float4 worldPosition = float4(i.worldPos, 1.0f);

				// calculates the input world position in the camera screen space
				float4 screenPosition = mul(worldToScreenMatrix, worldPosition);
				screenPosition /= screenPosition.w;

				// retrieves the normalized linear eye depth information stored in the camera depth map
				float4 cameraDepthwMapTextureCoords = float4((screenPosition.xy + 1.0) / 2.0, 0.0f, 0.0f);
				float eyeCameraDepth = tex2D(sceneDepthTexture, cameraDepthwMapTextureCoords).x;
				eyeCameraDepth *= cameraZFar;
				
				// calculates the input world position in the camera eye space
				float4 cameraEyePosition = mul(worldToViewMatrix, worldPosition);
				cameraEyePosition.xyz /= cameraEyePosition.w;

				// checks if the pixel world position is not occluded. The cameraEyePosition.z < 0
				if (cameraEyePosition.z > (-eyeCameraDepth - visibilityBias))
				{
					// is visible
					InterlockedAdd(visiblePixelsBuffer[1], 1);
				}


				// outputs a red color
				o.color = float4(1.0, 0.0, 0.0, 1.0);

				return o;

			}
			ENDCG
		}

	}
	Fallback off
}
