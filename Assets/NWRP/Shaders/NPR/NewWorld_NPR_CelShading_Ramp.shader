// ============================================================
// NewWorld/NPR/CelShading (Ramp)
//
// 最基础的卡通着色：用 Lambert NdotL 作为 UV 坐标
// 在一维 Ramp 纹理上采样，将平滑光照映射为离散色阶。
//
// Ramp 纹理决定了色阶数量和过渡风格——
// 硬边 Ramp 得到经典赛璐璐风格，软边 Ramp 得到柔和卡通。
//
// 架构验证：仅 include Lighting.hlsl，不碰 BRDF.hlsl，
// 证明 NWRP 的分层设计支持完全自定义光照。
// ============================================================

Shader "NewWorld/NPR/CelShading (Ramp)"
{
    Properties
    {
        _BaseColor ("Base Color", Color)       = (1, 1, 1, 1)
        [NoScaleOffset] _Ramp ("Ramp Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "NewWorldForward"
            Tags { "LightMode" = "NewWorldForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../../ShaderLibrary/Core.hlsl"
            #include "../../ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_Ramp);
            SAMPLER(sampler_Ramp);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                Light light = GetMainLight();

                // NdotL 作为 Ramp 采样坐标
                float NdotL = saturate(dot(normalWS, light.direction));
                half4 ramp = SAMPLE_TEXTURE2D(_Ramp, sampler_Ramp, float2(NdotL, NdotL));

                half4 color = ramp * _BaseColor;
                return color;
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
