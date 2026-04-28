// ============================================================
// NewWorld/NPR/CelShading (Procedural)
//
// 程序化卡通着色：无需 Ramp 纹理，通过参数控制色阶。
// 使用 smoothstep + fwidth 实现抗锯齿的色阶过渡。
//
// 特性：
// - 二分色漫反射：亮面/暗面各一个颜色
// - 程序化高光：Blinn-Phong 基础 + smoothstep 硬边化
// - fwidth 自适应抗锯齿：近看远看都清晰
//
// 参考: Half-Lambert 漫反射 + Blinn-Phong 高光的程序化 Ramp 化
// ============================================================

Shader "NewWorld/NPR/CelShading (Procedural)"
{
    Properties
    {
        _BaseColor               ("Base Color (Lit)", Color)        = (1, 1, 1, 1)
        _BackColor               ("Back Color (Shadow)", Color)     = (0.3, 0.3, 0.4, 1)
        _BackRange               ("Shadow Range", Range(0, 1))      = 0.5
        _DiffuseRampSmoothness   ("Diffuse Edge Smoothness", Range(0, 1)) = 0.05
        _SpecularColor           ("Specular Color", Color)          = (1, 1, 1, 1)
        _SpecularRange           ("Specular Range", Range(0, 1))    = 0.5
        _SpecularRampSmoothness  ("Specular Edge Smoothness", Range(0, 1)) = 0.05
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
                float3 viewWS      : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half3 _BaseColor;
                half3 _BackColor;
                half  _BackRange;
                half  _DiffuseRampSmoothness;
                half3 _SpecularColor;
                half  _SpecularRange;
                half  _SpecularRampSmoothness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS      = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 viewWS   = SafeNormalize(IN.viewWS);

                Light light = GetMainLight();
                half3 lightColor = light.color * light.distanceAttenuation;

                // ── 漫反射色阶 ──────────────────────────────────
                // Half-Lambert → smoothstep 硬边化
                half halfLambert = saturate(dot(normalWS, light.direction) * 0.5 + 0.5);
                half diffuseRamp = smoothstep(0, max(_DiffuseRampSmoothness, 0.005),
                                              halfLambert - _BackRange);

                half3 mainColor = _BaseColor * lightColor;
                half3 diffuse = lerp(_BackColor, mainColor, diffuseRamp);

                // ── 高光色阶 ────────────────────────────────────
                // Blinn-Phong 半角 → fwidth 自适应抗锯齿
                float3 halfVec = SafeNormalize(float3(light.direction) + float3(viewWS));
                half NdotH = saturate(dot(normalWS, halfVec));

                half w = fwidth(NdotH) * 2.0 + _SpecularRampSmoothness;
                half specularRamp = smoothstep(0, w, NdotH + _SpecularRange - 1.0);

                // 防止背光面出现高光
                specularRamp *= diffuseRamp;
                half3 specular = specularRamp * _SpecularColor * lightColor;

                // ── 环境光 ──────────────────────────────────────
                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _BaseColor;

                return half4(diffuse + specular + ambient, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
