// ============================================================
// NewWorld/Lit/Ambient
//
// 环境光演示 Shader。展示三种环境光模式：
// - Color 模式:    unity_AmbientSky 单色
// - Gradient 模式: 天空/赤道/地面三色渐变
// - Skybox 模式:   Unity 将 SH 球谐数据写入 unity_SHA* / unity_SHB* / unity_SHC
//
// 本 Shader 同时展示 SH 球谐求值和简单三色渐变两种方式。
// 可在 Window > Lighting > Environment > Environment Lighting 中切换模式。
// ============================================================

Shader "NewWorld/Lit/Ambient"
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

            // 球谐光照 L0+L1+L2 求值（适用于所有环境光模式）
            // Unity 将环境光数据编码到 SH 系数中，
            // 无论是 Color / Gradient / Skybox 模式都能正确工作
            half3 SampleSH_Simple(half3 normalWS)
            {
                // L0 + L1
                half4 n = half4(normalWS, 1.0);
                half3 res;
                res.r = dot(unity_SHAr, n);
                res.g = dot(unity_SHAg, n);
                res.b = dot(unity_SHAb, n);

                // L2
                half4 vB = normalWS.xyzz * normalWS.yzzx;
                res.r += dot(unity_SHBr, vB);
                res.g += dot(unity_SHBg, vB);
                res.b += dot(unity_SHBb, vB);

                half vC = normalWS.x * normalWS.x - normalWS.y * normalWS.y;
                res += unity_SHC.rgb * vC;

                return max(0.0, res);
            }

            // 三色渐变环境光（仅 Gradient 模式有效果）
            half3 GradientAmbient(half3 normalWS)
            {
                half3 ambient = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(normalWS.y));
                ambient = lerp(ambient, unity_AmbientGround.rgb, saturate(-normalWS.y));
                return ambient;
            }

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

                // 使用 SH 球谐（兼容所有环境光模式）
                half3 ambient = SampleSH_Simple(normalWS);

                half3 color = _BaseColor.rgb * ambient;

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
