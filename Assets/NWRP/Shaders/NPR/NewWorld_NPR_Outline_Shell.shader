// ============================================================
// NewWorld/NPR/Outline (Shell Method)
//
// 双 Pass 背面扩张描边（Shell Method / Back Facing）。
//
// Pass 1: NewWorldForward — 正常渲染（纯白演示，可替换为任何光照）
// Pass 2: NewWorldOutline — Cull Front，顶点沿法线方向扩张，渲染背面为描边色
//
// 支持两种模式：
// - 常规模式: 观察空间法线扩张，近大远小
// - 像素模式: 裁剪空间偏移，屏幕上等宽描边
//
// C# 管线已注册 "NewWorldOutline" ShaderTagId，
// 在不透明物体之后自动绘制描边 Pass。
// ============================================================

Shader "NewWorld/NPR/Outline (Shell Method)"
{
    Properties
    {
        _BaseColor    ("Base Color", Color)    = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 0.5
        [Toggle] _PixelWidth ("Pixel Width", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        // ── Pass 1: 正常前向渲染 ───────────────────────────────
        Pass
        {
            Name "NewWorldForward"
            Tags { "LightMode" = "NewWorldForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"
            #include "../../ShaderLibrary/Lighting.hlsl"

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _OutlineColor;
                half  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                Light light = GetMainLight();
                half NdotL = saturate(dot(normalWS, light.direction));
                half3 color = _BaseColor.rgb * light.color * NdotL;
                return half4(color, 1.0);
            }

            ENDHLSL
        }

        // ── Pass 2: 描边（背面扩张） ──────────────────────────────
        Pass
        {
            Name "NewWorldOutline"
            Tags { "LightMode" = "NewWorldOutline" }

            Cull Front  // 只渲染背面

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature __ _PIXELWIDTH_ON

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _OutlineColor;
                half  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #ifdef _PIXELWIDTH_ON
                    // ── 像素等宽模式 ────────────────────────────
                    // 在裁剪空间中偏移，保证屏幕上宽度恒定
                    float3 normalHCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                    // 乘 w 抵消透视除法，除 _ScreenParams 匹配像素单位
                    float2 outlineOffset = (_OutlineWidth * OUT.positionHCS.w)
                                          / (_ScreenParams.xy * 0.5);
                    OUT.positionHCS.xy += normalize(normalHCS.xy) * outlineOffset;
                #else
                    // ── 常规模式（观察空间扩张） ──────────────────
                    // 近大远小，更自然的 3D 描边
                    float3 positionVS = TransformWorldToView(positionWS);
                    float3 normalVS = TransformWorldToViewDir(normalWS);
                    // 将法线 z 设为恒定值，使背面更扁平，减少遮挡正面
                    normalVS.z = -0.4;
                    positionVS += normalize(normalVS) * _OutlineWidth;
                    OUT.positionHCS = TransformWViewToHClip(positionVS);
                #endif

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
