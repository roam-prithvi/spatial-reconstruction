Shader "Roam/ViewSpaceDepthReplacement"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "Depth"
            Tags { "LightMode"="SRPDefaultUnlit" } 

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float clipW       : TEXCOORD0; 
                float viewPosZ    : TEXCOORD1; 
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS);
                float3 posVS = TransformWorldToView(posWS);
                float4 posCS = TransformWorldToHClip(posWS);

                OUT.positionCS = posCS;
                OUT.clipW      = posCS.w; 
                OUT.viewPosZ   = posVS.z; 
                return OUT;
            }

            float frag(Varyings IN) : SV_Target
            {
                bool isOrtho = (unity_OrthoParams.w != 0.0);
                float eyeDepth = isOrtho ? (-IN.viewPosZ) : IN.clipW;
                return eyeDepth;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
