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
            #pragma multi_compile_instancing

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

            half3 UnpackNormalScale(half4 packedNormal, half scale)
            {
                half3 normal;
                normal.xy = (packedNormal.ag * 2.0 - 1.0) * scale;
                normal.z  = sqrt(max(1.0 - saturate(dot(normal.xy, normal.xy)), 0.0));
                return normal;
            }

            half3 EvaluateDirectPBR(
                Light light,
                half3 normalWS,
                half3 viewWS,
                half3 diffuseColor,
                half3 f0,
                half roughness)
            {
                half3 H = SafeNormalize(float3(light.direction) + float3(viewWS));

                half NdotL = saturate(dot(normalWS, light.direction));
                half NdotH = saturate(dot(normalWS, H));
                half NdotV = saturate(dot(normalWS, viewWS));
                half LdotH = saturate(dot(light.direction, H));

                half D = D_GGX(NdotH, roughness);
                half V = V_SmithJointApprox(NdotL, NdotV, roughness);
                half3 F = F_Schlick(f0, LdotH);

                half3 specular = D * V * F;
                half3 diffuse = diffuseColor * INV_PI;

                half3 baseRadiance = light.color
                                   * light.distanceAttenuation
                                   * light.shadowAttenuation
                                   * NdotL;

                return (diffuse + specular) * baseRadiance;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                VertexNormalInputs tbn = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = tbn.normalWS;
                output.tangentWS = tbn.tangentWS;
                output.bitangentWS = tbn.bitangentWS;

                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseMapSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseMapSample.rgb * _BaseColor.rgb;

                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                half metallic = mask.r * _Metallic;
                half ao = lerp(1.0, mask.g, _OcclusionStrength);
                half smoothness = mask.a * _Smoothness;
                half perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);

                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _NormalStrength);

                float3x3 tbn = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS)
                );
                half3 normalWS = normalize(TransformTangentToWorldDir(normalTS, tbn));

                half3 viewWS = SafeNormalize(input.viewWS);

                half roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
                roughness = max(roughness, HALF_MIN_SQRT);

                half3 f0 = ComputeF0(albedo, metallic);
                half3 diffuseColor = ComputeDiffuseColor(albedo, metallic);
                half NdotV = saturate(dot(normalWS, viewWS));

                half3 directColor = half3(0, 0, 0);

                Light mainLight = GetMainLight(input.positionWS, normalWS);
                directColor += EvaluateDirectPBR(
                    mainLight,
                    normalWS,
                    viewWS,
                    diffuseColor,
                    f0,
                    roughness
                );

                int additionalCount = GetAdditionalLightsCount();
                for (int i = 0; i < additionalCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    directColor += EvaluateDirectPBR(
                        addLight,
                        normalWS,
                        viewWS,
                        diffuseColor,
                        f0,
                        roughness
                    );
                }

                half3 indirectDiffuse = SampleSH(normalWS) * diffuseColor * ao;

                half3 envBRDF = F_SchlickRoughness(f0, NdotV, perceptualRoughness);
                half3 indirectSpecular = SampleEnvironmentReflection(normalWS, viewWS, perceptualRoughness)
                                       * envBRDF * ao;

                half3 emission = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, input.uv).rgb
                               * _EmissiveColor.rgb;

                half3 finalColor = directColor + indirectDiffuse + indirectSpecular + emission;
                return half4(finalColor, 1.0);
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

            #include "Includes/ShadowCasterPass.hlsl"
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

            #include "Includes/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
