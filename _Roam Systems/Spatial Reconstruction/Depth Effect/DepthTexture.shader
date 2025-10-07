Shader "Roam/DepthTexture"
{
	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment frag
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			float4 _NearColor;
			float4 _FarColor;
			float _ShadowInfluence;
			float _NearDistance;
			float _FarDistance;
			TEXTURE2D(_DepthCurveLUT);
			SAMPLER(sampler_DepthCurveLUT);

            /// Summary: Applies depth-based texture with custom distance range and shadow influence
            float4 frag (Varyings i) : SV_TARGET
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 

				// Sample and linearize depth
#if UNITY_REVERSED_Z
				float depth = SampleSceneDepth(i.texcoord);
#else
				float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.texcoord));
#endif
				// Convert to world space depth
				float eyeDepth = LinearEyeDepth(depth, _ZBufferParams);
				
				// Remap world depth to custom near/far range
				float normalizedDepth = saturate((eyeDepth - _NearDistance) / (_FarDistance - _NearDistance));

				// Sample curve LUT with normalized depth
				float remappedDepth = SAMPLE_TEXTURE2D(_DepthCurveLUT, sampler_DepthCurveLUT, float2(normalizedDepth, 0.5)).r;

				// Reconstruct world position for shadow sampling
				float3 worldPos = ComputeWorldSpacePosition(i.texcoord, depth, UNITY_MATRIX_I_VP);
				
				// Sample main light shadows
				float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
				Light mainLight = GetMainLight(shadowCoord);
				float shadowAttenuation = mainLight.shadowAttenuation;

				// Blend base depth color with shadow influence
				float4 baseColor = lerp(_NearColor, _FarColor, remappedDepth);
				return lerp(baseColor, baseColor * shadowAttenuation, _ShadowInfluence);
            }
            ENDHLSL
        }
    }
}
