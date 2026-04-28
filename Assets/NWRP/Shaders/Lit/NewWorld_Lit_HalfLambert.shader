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
        [ToggleUI] _ReceiveShadows ("Receive Realtime Shadows", Float) = 1.0
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
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _ReceiveShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
            #include "../../ShaderLibrary/Lighting.hlsl"
            #undef NWRP_MATERIAL_RECEIVE_SHADOWS

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS  = positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);

                Light light = GetMainLight(IN.positionWS, normalWS);
                half3 debugColor;
                if (TryGetMainLightShadowDebugOverride(light, debugColor))
                {
                    return half4(debugColor, 1.0);
                }

                half3 lightColor = light.color * light.distanceAttenuation * light.shadowAttenuation;

                // Half-Lambert: 把 [-1,1] 映射到 [0.5, 1]，再平方
                half NdotL = dot(normalWS, light.direction);
                half halfLambert = pow(NdotL * 0.5 + 0.5, 2.0);

                half3 diffuse = _BaseColor.rgb * lightColor * halfLambert;

                return half4(diffuse, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
