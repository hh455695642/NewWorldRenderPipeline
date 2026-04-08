// ============================================================
// NewWorld/Lit/StandardLit
//
// NWRP 标准 PBR 材质 Shader —— 面向生产的完整光照。
//
// 贴图通道约定：
//   _BaseMap      — RGB = Albedo, A = Alpha（预留）
//   _MaskMap      — R = Metallic, G = AO, B = 预留(默认白), A = Smoothness
//   _NormalMap    — 切线空间法线贴图（OpenGL 格式）
//   _EmissiveMap  — RGB = 自发光颜色
//
// 光照模型：
//   直接光  = Cook-Torrance (GGX + SmithJoint + Schlick)
//   间接漫反射 = SH 球谐 × AO
//   间接镜面 = 反射探针 × 环境 BRDF
//   自发光  = EmissiveMap × EmissiveColor（叠加到最终输出）
//
// 使用 BRDF.hlsl 积木函数手动组装，展示比 EvaluateStandardPBR()
// 更精细的控制（AO、法线贴图、自发光）。
// ============================================================

Shader "NewWorld/Lit/StandardLit"
{
    Properties
    {
        [Header(Surface)]
        _BaseColor      ("Base Color",  Color)          = (1, 1, 1, 1)
        _BaseMap        ("Base Map",    2D)             = "white" {}

        [Header(Mask)]
        [NoScaleOffset]
        _MaskMap        ("Mask Map (R=Metal G=AO A=Smooth)", 2D) = "white" {}
        _Metallic       ("Metallic",    Range(0, 1))    = 0.0
        _Smoothness     ("Smoothness",  Range(0, 1))    = 0.5
        _OcclusionStrength ("AO Strength", Range(0, 1)) = 1.0

        [Header(Normal)]
        [NoScaleOffset]
        _NormalMap      ("Normal Map",  2D)             = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0

        [Header(Emission)]
        [NoScaleOffset]
        _EmissiveMap    ("Emissive Map", 2D)            = "black" {}
        [HDR]
        _EmissiveColor  ("Emissive Color", Color)       = (0, 0, 0, 1)
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
            #include "../../ShaderLibrary/BRDF.hlsl"
            #include "../../ShaderLibrary/GlobalIllumination.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 viewWS      : TEXCOORD5;
            };

            // ── 材质属性 ────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _BaseMap_ST;
                half   _Metallic;
                half   _Smoothness;
                half   _OcclusionStrength;
                half   _NormalStrength;
                half4  _EmissiveColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);        SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissiveMap);    SAMPLER(sampler_EmissiveMap);

            // ── 法线贴图解码 ────────────────────────────────────
            half3 UnpackNormalScale(half4 packedNormal, half scale)
            {
                half3 normal;
                normal.xy = (packedNormal.ag * 2.0 - 1.0) * scale;
                normal.z  = sqrt(max(1.0 - saturate(dot(normal.xy, normal.xy)), 0.0));
                return normal;
            }

            // ── 单光源直接 PBR（预计算 roughness/f0/diffuseColor 版） ──
            half3 EvaluateDirectPBR(Light light, half3 normalWS, half3 viewWS,
                                     half3 diffuseColor, half3 f0, half roughness)
            {
                half3 H = SafeNormalize(float3(light.direction) + float3(viewWS));

                half NdotL = saturate(dot(normalWS, light.direction));
                half NdotH = saturate(dot(normalWS, H));
                half NdotV = saturate(dot(normalWS, viewWS));
                half LdotH = saturate(dot(light.direction, H));

                half  D = D_GGX(NdotH, roughness);
                half  V = V_SmithJointApprox(NdotL, NdotV, roughness);
                half3 F = F_Schlick(f0, LdotH);

                half3 specular = D * V * F;
                half3 diffuse  = diffuseColor * INV_PI;

                half3 radiance = light.color
                               * light.distanceAttenuation
                               * light.shadowAttenuation
                               * NdotL;

                return (diffuse + specular) * radiance;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS  = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);

                // TBN 矩阵
                VertexNormalInputs tbn = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS    = tbn.normalWS;
                OUT.tangentWS   = tbn.tangentWS;
                OUT.bitangentWS = tbn.bitangentWS;

                OUT.viewWS = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── 采样贴图 ────────────────────────────────────
                half4 baseMapSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = baseMapSample.rgb * _BaseColor.rgb;

                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv);
                half metallic   = mask.r * _Metallic;
                half ao         = lerp(1.0, mask.g, _OcclusionStrength);
                half smoothness = mask.a * _Smoothness;
                half perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);

                // ── 法线贴图 ────────────────────────────────────
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _NormalStrength);

                float3x3 tbn = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                half3 normalWS = normalize(TransformTangentToWorldDir(normalTS, tbn));

                half3 viewWS = SafeNormalize(IN.viewWS);

                // ── PBR 参数准备 ────────────────────────────────
                half roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
                roughness = max(roughness, HALF_MIN_SQRT);

                half3 f0 = ComputeF0(albedo, metallic);
                half3 diffuseColor = ComputeDiffuseColor(albedo, metallic);
                half NdotV = saturate(dot(normalWS, viewWS));

                // ── 直接光 ──────────────────────────────────────
                half3 directColor = half3(0, 0, 0);

                // 主光源
                Light mainLight = GetMainLight();
                directColor += EvaluateDirectPBR(mainLight, normalWS, viewWS,
                                                  diffuseColor, f0, roughness);

                // 附加光源
                int count = GetAdditionalLightsCount();
                for (int i = 0; i < count; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS);
                    directColor += EvaluateDirectPBR(addLight, normalWS, viewWS,
                                                      diffuseColor, f0, roughness);
                }

                // ── 间接光 ──────────────────────────────────────
                // 间接漫反射 (SH × AO)
                half3 indirectDiffuse = SampleSH(normalWS) * diffuseColor * ao;

                // 间接镜面 (反射探针)
                half3 envBRDF = F_SchlickRoughness(f0, NdotV, perceptualRoughness);
                half3 indirectSpecular = SampleEnvironmentReflection(normalWS, viewWS, perceptualRoughness)
                                       * envBRDF * ao;

                // ── 自发光 ──────────────────────────────────────
                half3 emission = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, IN.uv).rgb
                               * _EmissiveColor.rgb;

                // ── 最终合成 ────────────────────────────────────
                half3 finalColor = directColor + indirectDiffuse + indirectSpecular + emission;

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
