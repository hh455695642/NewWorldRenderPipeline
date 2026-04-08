Shader "NewWorld/Base/AlphaTest"
{
    Properties
    {
        _AlphaTestTexture ("AlphaTest Texture", 2D) = "white" {}
        _ClipThreshold ("Alpha Test Threshold", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" }

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../../ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_AlphaTestTexture);
            SAMPLER(sampler_AlphaTestTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _AlphaTestTexture_ST;
                float _ClipThreshold;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _AlphaTestTexture);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_AlphaTestTexture, sampler_AlphaTestTexture, IN.uv).r;
                clip(alpha - _ClipThreshold);
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
