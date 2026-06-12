Shader "NewWorld/Terrain/Terrain_PolyBrush_Code"
{
    Properties
    {
        [Header(Albedo)]
        [NoScaleOffset]_AlbedoTexture1("AlbedoMap1", 2D) = "white" {}
        [NoScaleOffset]_AlbedoTexture2("AlbedoMap2", 2D) = "white" {}
        [NoScaleOffset]_AlbedoTexture3("AlbedoMap3", 2D) = "white" {}
        [NoScaleOffset]_AlbedoTexture4("AlbedoMap4", 2D) = "white" {}

        [Header(Normal)]
        [Normal][NoScaleOffset]_NormalMap1("NormalMap1", 2D) = "bump" {}
        [Normal][NoScaleOffset]_NormalMap2("NormalMap2", 2D) = "bump" {}
        [Normal][NoScaleOffset]_NormalMap3("NormalMap3", 2D) = "bump" {}
        [Normal][NoScaleOffset]_NormalMap4("NormalMap4", 2D) = "bump" {}

        [Header(Tiling)]
        _Tilling1("Tilling1", Range(0, 1)) = 0.05
        _Tilling2("Tilling2", Range(0, 1)) = 0.05
        _Tilling3("Tilling3", Range(0, 1)) = 0.05
        _Tilling4("Tilling4", Range(0, 1)) = 0.05
        [ToggleUI]_Use_UV("Use UV", Float) = 0.0

        [Header(Height Blend)]
        _HeightIntensity2("HeightIntensity2", Range(0, 10)) = 1
        _HeightIntensity3("HeightIntensity3", Range(0, 10)) = 1
        _HeightIntensity4("HeightIntensity4", Range(0, 10)) = 1

        [Header(Surface)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.1

        [Header(NWRP)]
        [ToggleUI]_ReceiveShadows("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI]_CastShadows("Cast Realtime Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "NewWorldRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #pragma target 3.0

        #include "../../../../NWRP/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Tilling1;
            float _Tilling2;
            float _Tilling3;
            float _Tilling4;
            half _Use_UV;
            half _HeightIntensity2;
            half _HeightIntensity3;
            half _HeightIntensity4;
            half _Smoothness;
            half _ReceiveShadows;
            half _CastShadows;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../../../NWRP/ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../../../NWRP/ShaderLibrary/BRDF.hlsl"
        #include "../../../../NWRP/ShaderLibrary/GlobalIllumination.hlsl"

        TEXTURE2D(_AlbedoTexture1);
        TEXTURE2D(_AlbedoTexture2);
        TEXTURE2D(_AlbedoTexture3);
        TEXTURE2D(_AlbedoTexture4);
        TEXTURE2D(_NormalMap1);
        TEXTURE2D(_NormalMap2);
        TEXTURE2D(_NormalMap3);
        TEXTURE2D(_NormalMap4);
        SAMPLER(sampler_LinearRepeat);

        struct PolyBrushAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct PolyBrushVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float3 tangentWS : TEXCOORD3;
            float3 bitangentWS : TEXCOORD4;
            half4 color : TEXCOORD5;
            half fogFactor : TEXCOORD6;
            float3 viewWS : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct TerrainLayerData
        {
            half4 albedo1;
            half4 albedo2;
            half4 albedo3;
            half4 albedo4;
            half3 normalTS1;
            half3 normalTS2;
            half3 normalTS3;
            half3 normalTS4;
        };

        half3 UnpackNormalScale(half4 packedNormal, half scale)
        {
            half3 normal;
            normal.xy = (packedNormal.ag * 2.0h - 1.0h) * scale;
            normal.z = sqrt(max(1.0h - saturate(dot(normal.xy, normal.xy)), 0.0h));
            return normal;
        }

        float2 GetLayerBaseUV(float3 positionWS, float2 uv)
        {
            half useUv = step(0.5h, _Use_UV);
            return lerp(positionWS.xz, uv, useUv);
        }

        TerrainLayerData SampleTerrainLayers(float2 baseUV)
        {
            TerrainLayerData data;
            data.albedo1 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture1, sampler_LinearRepeat, baseUV * _Tilling1);
            data.albedo2 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture2, sampler_LinearRepeat, baseUV * _Tilling2);
            data.albedo3 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture3, sampler_LinearRepeat, baseUV * _Tilling3);
            data.albedo4 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture4, sampler_LinearRepeat, baseUV * _Tilling4);

            data.normalTS1 = UnpackNormalScale((half4)SAMPLE_TEXTURE2D(_NormalMap1, sampler_LinearRepeat, baseUV * _Tilling1), 1.0h);
            data.normalTS2 = UnpackNormalScale((half4)SAMPLE_TEXTURE2D(_NormalMap2, sampler_LinearRepeat, baseUV * _Tilling2), 1.0h);
            data.normalTS3 = UnpackNormalScale((half4)SAMPLE_TEXTURE2D(_NormalMap3, sampler_LinearRepeat, baseUV * _Tilling3), 1.0h);
            data.normalTS4 = UnpackNormalScale((half4)SAMPLE_TEXTURE2D(_NormalMap4, sampler_LinearRepeat, baseUV * _Tilling4), 1.0h);
            return data;
        }

        half3 EvaluateDirectTerrainPBR(
            Light light,
            half3 normalWS,
            half3 viewWS,
            half3 albedo,
            half perceptualRoughness)
        {
            half3 debugColor;
            if (TryGetMainLightShadowDebugOverride(light, debugColor))
            {
                return debugColor;
            }

            half roughness = max(PerceptualRoughnessToRoughness(perceptualRoughness), HALF_MIN_SQRT);
            half3 halfVector = SafeNormalize(float3(light.direction) + float3(viewWS));

            half nDotL = saturate(dot(normalWS, light.direction));
            half nDotH = saturate(dot(normalWS, halfVector));
            half nDotV = saturate(dot(normalWS, viewWS));
            half lDotH = saturate(dot(light.direction, halfVector));

            half d = D_GGX(nDotH, roughness);
            half v = V_SmithJointApprox(nDotL, nDotV, roughness);
            half3 f = F_Schlick(kDielectricF0, lDotH);
            half3 brdf = albedo * INV_PI + d * v * f;

            half3 radiance = light.color
                * light.distanceAttenuation
                * light.shadowAttenuation
                * nDotL;
            return brdf * radiance;
        }

        half3 EvaluateTerrainPBR(
            half3 normalWS,
            half3 viewWS,
            float3 positionWS,
            half3 albedo,
            half smoothness)
        {
            half perceptualRoughness = SmoothnessToPerceptualRoughness(saturate(smoothness));
            half3 color = 0.0h.xxx;

            Light mainLight = GetMainLight(positionWS, normalWS);
            half3 debugColor;
            if (TryGetMainLightShadowDebugOverride(mainLight, debugColor))
            {
                return debugColor;
            }

            color += EvaluateDirectTerrainPBR(mainLight, normalWS, viewWS, albedo, perceptualRoughness);

            int additionalLightCount = GetAdditionalLightsCount();
            for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS, normalWS);
                color += EvaluateDirectTerrainPBR(light, normalWS, viewWS, albedo, perceptualRoughness);
            }

            half nDotV = saturate(dot(normalWS, viewWS));
            half3 indirectDiffuse = SampleSH(normalWS) * albedo;
            half3 envBRDF = F_SchlickRoughness(kDielectricF0, nDotV, perceptualRoughness);
            half3 indirectSpecular = SampleEnvironmentReflection(normalWS, viewWS, perceptualRoughness)
                * envBRDF;

            return color + indirectDiffuse + indirectSpecular;
        }

        PolyBrushVaryings PolyBrushVert(PolyBrushAttributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            PolyBrushVaryings output = (PolyBrushVaryings)0;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.uv;
            output.color = (half4)input.color;

            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.normalWS = normalInputs.normalWS;
            output.tangentWS = normalInputs.tangentWS;
            output.bitangentWS = normalInputs.bitangentWS;
            output.viewWS = GetWorldSpaceViewDirRaw(output.positionWS);
            output.fogFactor = (half)ComputeNWRPFogFactorFromPositionWS(output.positionWS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "NewWorldForward"
            Tags { "LightMode" = "NewWorldForward" }

            Cull Back
            Blend Off
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma vertex PolyBrushVert
            #pragma fragment PolyBrushFrag
            #pragma multi_compile_instancing

            half4 PolyBrushFrag(PolyBrushVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUV = GetLayerBaseUV(input.positionWS, input.uv);
                TerrainLayerData layers = SampleTerrainLayers(baseUV);

                // Graph-compatible PolyBrush order: layer1 -> G/layer2 -> B/layer3 -> A/layer4.
                half t2 = saturate(layers.albedo2.a * input.color.g * _HeightIntensity2);
                half t3 = saturate(layers.albedo3.a * input.color.b * _HeightIntensity3);
                half t4 = saturate(layers.albedo4.a * input.color.a * _HeightIntensity4);

                half3 albedo = lerp(layers.albedo1.rgb, layers.albedo2.rgb, t2);
                albedo = lerp(albedo, layers.albedo3.rgb, t3);
                albedo = lerp(albedo, layers.albedo4.rgb, t4);

                half3 normalTS = normalize(lerp(layers.normalTS1, layers.normalTS2, t2));
                normalTS = normalize(lerp(normalTS, layers.normalTS3, t3));
                normalTS = normalize(lerp(normalTS, layers.normalTS4, t4));

                float3x3 tbn = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS));
                half3 normalWS = normalize(TransformTangentToWorldDir(normalTS, tbn));
                half3 viewWS = SafeNormalize(input.viewWS);

                half3 color = EvaluateTerrainPBR(
                    normalWS,
                    viewWS,
                    input.positionWS,
                    albedo,
                    _Smoothness);

                color = MixNWRPFog(color, input.fogFactor);
                return half4(color, 1.0h);
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

            #define NWRP_MATERIAL_CAST_SHADOWS _CastShadows
            #include "../../../../NWRP/ShaderLibrary/Passes/ShadowCasterPass.hlsl"
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

            #include "../../../../NWRP/ShaderLibrary/Passes/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
    Fallback Off
}
