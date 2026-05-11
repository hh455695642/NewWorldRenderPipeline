Shader "NewWorld/Lit/Glass"
{
    Properties
    {
        [Header(Surface)]
        [MainColor] _BaseColor ("Base Color", Color) = (0.75, 0.95, 1.0, 0.35)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}

        [Header(Normal)]
        [NoScaleOffset]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.5

        [Header(Glass)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 1.0
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4.0
        _FresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.75

        [Header(Shadows)]
        [ToggleUI]
        _ReceiveShadows ("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI]
        _CastShadows ("Cast Realtime Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "NewWorldForward"
            Tags { "LightMode" = "NewWorldForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 viewWS : TEXCOORD5;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _NormalStrength;
                half _Smoothness;
                half _SpecularStrength;
                half _FresnelPower;
                half _FresnelStrength;
                half _ReceiveShadows;
                half _CastShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
            #include "../../ShaderLibrary/Lighting.hlsl"
            #undef NWRP_MATERIAL_RECEIVE_SHADOWS
            #include "../../ShaderLibrary/BRDF.hlsl"
            #include "../../ShaderLibrary/GlobalIllumination.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            half3 UnpackNormalScale(half4 packedNormal, half scale)
            {
                half3 normal;
                normal.xy = (packedNormal.ag * 2.0h - 1.0h) * scale;
                normal.z = sqrt(max(1.0h - saturate(dot(normal.xy, normal.xy)), 0.0h));
                return normal;
            }

            half3 EvaluateGlassDirect(
                Light light,
                half3 normalWS,
                half3 viewWS,
                half3 tint,
                half3 f0,
                half roughness)
            {
                half3 debugColor;
                if (TryGetMainLightShadowDebugOverride(light, debugColor))
                {
                    return debugColor;
                }

                half3 halfWS = SafeNormalize(float3(light.direction) + float3(viewWS));

                half NdotL = saturate(dot(normalWS, light.direction));
                half NdotH = saturate(dot(normalWS, halfWS));
                half NdotV = saturate(dot(normalWS, viewWS));
                half LdotH = saturate(dot(light.direction, halfWS));

                half D = D_GGX(NdotH, roughness);
                half V = V_SmithJointApprox(NdotL, NdotV, roughness);
                half3 F = F_Schlick(f0, LdotH);

                // Keep the diffuse tint intentionally small; glass is mainly specular.
                half3 tintDiffuse = tint * (0.15h * INV_PI);
                half3 specular = D * V * F;
                half3 radiance = light.color
                    * light.distanceAttenuation
                    * light.shadowAttenuation
                    * NdotL;

                return (tintDiffuse + specular) * radiance;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

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

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = baseSample * _BaseColor;
                half3 tint = baseColor.rgb;

                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _NormalStrength);

                float3x3 tbn = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS)
                );
                half3 normalWS = normalize(TransformTangentToWorldDir(normalTS, tbn));
                half3 viewWS = SafeNormalize(input.viewWS);

                half smoothness = saturate(_Smoothness);
                half perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
                half roughness = max(PerceptualRoughnessToRoughness(perceptualRoughness), HALF_MIN_SQRT);
                half3 f0 = kDielectricF0 * clamp(_SpecularStrength, 0.0h, 2.0h);

                half3 directColor = 0.0h.xxx;
                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half3 debugColor;
                if (TryGetMainLightShadowDebugOverride(mainLight, debugColor))
                {
                    return half4(debugColor, baseColor.a);
                }

                directColor += EvaluateGlassDirect(mainLight, normalWS, viewWS, tint, f0, roughness);

                int additionalCount = GetAdditionalLightsCount();
                for (int i = 0; i < additionalCount; i++)
                {
                    Light additionalLight = GetAdditionalLight(i, input.positionWS, normalWS);
                    directColor += EvaluateGlassDirect(additionalLight, normalWS, viewWS, tint, f0, roughness);
                }

                half NdotV = saturate(dot(normalWS, viewWS));
                half fresnel = pow(saturate(1.0h - NdotV), _FresnelPower) * _FresnelStrength;
                half3 indirectDiffuse = SampleSH(normalWS) * tint * 0.15h;
                half3 indirectSpecular = SampleEnvironmentReflection(normalWS, viewWS, perceptualRoughness)
                    * F_SchlickRoughness(f0, NdotV, perceptualRoughness)
                    * (1.0h + fresnel);

                half3 finalColor = directColor + indirectDiffuse + indirectSpecular + tint * fresnel;
                half alpha = saturate(baseColor.a + fresnel * 0.25h);
                return half4(finalColor, alpha);
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
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _NormalStrength;
                half _Smoothness;
                half _SpecularStrength;
                half _FresnelPower;
                half _FresnelStrength;
                half _ReceiveShadows;
                half _CastShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_CAST_SHADOWS _CastShadows
            #include "../../ShaderLibrary/Passes/ShadowCasterPass.hlsl"
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
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _NormalStrength;
                half _Smoothness;
                half _SpecularStrength;
                half _FresnelPower;
                half _FresnelStrength;
                half _ReceiveShadows;
                half _CastShadows;
            CBUFFER_END

            #include "../../ShaderLibrary/Passes/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
