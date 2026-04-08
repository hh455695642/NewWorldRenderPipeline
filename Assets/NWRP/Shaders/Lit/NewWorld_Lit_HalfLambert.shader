// ============================================================
// NewWorld/Lit/HalfLambert
//
// Half-Lambert 漫反射光照。
// 将 NdotL 从 [-1,1] 重映射到 [0,1]，使背光面也有微弱照明，
// 视觉上更柔和，常用于角色渲染和卡通风格。
//
// 公式: diffuse = pow(dot(N,L) * 0.5 + 0.5, 2.0)
// ============================================================

Shader "NewWorld/Lit/HalfLambert"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);

                Light light = GetMainLight();

                // Half-Lambert: 把 [-1,1] 映射到 [0.5, 1]，再平方
                half NdotL = dot(normalWS, light.direction);
                half halfLambert = pow(NdotL * 0.5 + 0.5, 2.0);

                half3 diffuse = _BaseColor.rgb * light.color * halfLambert;

                return half4(diffuse, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
