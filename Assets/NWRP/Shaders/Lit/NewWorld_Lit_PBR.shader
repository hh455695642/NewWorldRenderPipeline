// ============================================================
// NewWorld/Lit/PBR
//
// 基于物理的渲染（Physically Based Rendering）—— 金属工作流。
//
// 直接光: Cook-Torrance BRDF (GGX + SmithJoint + Schlick)
// 间接漫反射: SH 球谐
// 间接镜面反射: 反射探针 Cubemap
//
// 使用 LightingModels/StandardPBR.hlsl 便捷层，
// 一行 EvaluateStandardPBR() 即可完成全部计算。
//
// 属性:
//   _Albedo    - 基础颜色（金属时也作为 F0 反射色）
//   _Metallic  - 金属度 [0=电介质, 1=金属]
//   _Roughness - 粗糙度 [0=镜面, 1=粗糙]
// ============================================================

Shader "NewWorld/Lit/PBR"
{
    Properties
    {
        _Albedo    ("Albedo", Color)             = (1, 1, 1, 1)
        _Metallic  ("Metallic", Range(0, 1))     = 0.0
        _Roughness ("Roughness", Range(0, 1))    = 0.5
        [ToggleUI] _ReceiveShadows ("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI] _CastShadows ("Cast Realtime Shadows", Float) = 1.0
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
                float3 viewWS      : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half3 _Albedo;
                half  _Metallic;
                half  _Roughness;
                half  _ReceiveShadows;
                half  _CastShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
            #include "../../ShaderLibrary/LightingModels/StandardPBR.hlsl"
            #undef NWRP_MATERIAL_RECEIVE_SHADOWS

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS  = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS      = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 viewWS   = SafeNormalize(IN.viewWS);

                // 一行完成全部 PBR 计算（直接光 + 间接光 + 所有光源）
                half3 color = EvaluateStandardPBR(
                    normalWS, viewWS, IN.positionWS,
                    _Albedo, _Metallic, _Roughness
                );

                return half4(color, 1.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_MainLightShadowCasterCull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowCasterVert
            #pragma fragment ShadowCasterFrag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half3 _Albedo;
                half  _Metallic;
                half  _Roughness;
                half  _ReceiveShadows;
                half  _CastShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_CAST_SHADOWS _CastShadows
            #include "Includes/ShadowCasterPass.hlsl"
            #undef NWRP_MATERIAL_CAST_SHADOWS
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half3 _Albedo;
                half  _Metallic;
                half  _Roughness;
                half  _ReceiveShadows;
                half  _CastShadows;
            CBUFFER_END

            #include "Includes/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
