Shader "NewWorld/Env/Env_Rocks_TopDownSlope"
{
    Properties
    {
        [Header(Masks)]
        [NoScaleOffset]_MaskDetailMap_01("MaskDetailMap_01", 2D) = "linearGrey" {}
        [NoScaleOffset]_MaskDetailMap_02("MaskDetailMap_02", 2D) = "linearGrey" {}
        [NoScaleOffset]_MAHSMap("MAHSMap", 2D) = "linearGrey" {}
        _MaskMapTile_01("MaskMapTile_01", Range(0.001, 0.1)) = 0.005
        _MaskMapTile_02("MaskMapTile_02", Range(0.001, 0.1)) = 0.005

        [Header(Base Colors)]
        _MainColor("MainColor", Color) = (0.8, 0.8, 0.8, 0)
        _Color_01("Color_01", Color) = (0.8, 0.8, 0.8, 0)
        _Color_02("Color_02", Color) = (0.8, 0.8, 0.8, 0)
        _Color_03("Color_03", Color) = (0.8, 0.8, 0.8, 0)
        _Color_04("Color_04", Color) = (0.8, 0.8, 0.8, 0)
        _Color_05("Color_05", Color) = (0.8, 0.8, 0.8, 0)
        _Color_AO("Color_AO", Color) = (0.8, 0.8, 0.8, 0)
        _Color_Height("Color_Height", Color) = (0.8, 0.8, 0.8, 0)

        [Header(Cover)]
        [NoScaleOffset]_CoverMap("CoverMap", 2D) = "white" {}
        _CoverMapTile("CoverMapTile", Range(0.001, 0.5)) = 0.005
        _CoverAmount("CoverAmount", Range(-1, 1)) = 0
        _CoverBlendIntensity("CoverBlendIntensity", Range(-10, 10)) = 0
        _CoverHeightStart("CoverHeightStart", Range(1, 50)) = 3

        [Header(Normal)]
        [Normal][NoScaleOffset]_NormalMap("NormalMap", 2D) = "bump" {}

        [Header(NWRP)]
        [ToggleUI]_ReceiveShadows("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI]_CastShadows("Cast Realtime Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #pragma target 3.0

        #include "../../../../NWRP/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _MainColor;
            half4 _Color_01;
            half4 _Color_02;
            half4 _Color_03;
            half4 _Color_04;
            half4 _Color_05;
            half4 _Color_AO;
            half4 _Color_Height;
            float _MaskMapTile_01;
            float _MaskMapTile_02;
            float _CoverMapTile;
            half _CoverAmount;
            half _CoverBlendIntensity;
            float _CoverHeightStart;
            half _ReceiveShadows;
            half _CastShadows;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../../../NWRP/ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../../../NWRP/ShaderLibrary/BRDF.hlsl"
        #include "../../../../NWRP/ShaderLibrary/GlobalIllumination.hlsl"

        TEXTURE2D(_MaskDetailMap_01); SAMPLER(sampler_MaskDetailMap_01);
        TEXTURE2D(_MaskDetailMap_02); SAMPLER(sampler_MaskDetailMap_02);
        TEXTURE2D(_MAHSMap);          SAMPLER(sampler_MAHSMap);
        TEXTURE2D(_CoverMap);         SAMPLER(sampler_CoverMap);
        TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);

        struct RockAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct RockVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float3 tangentWS : TEXCOORD3;
            float3 bitangentWS : TEXCOORD4;
            float3 viewWS : TEXCOORD5;
            half fogFactor : TEXCOORD6;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        half SmoothMask(half value)
        {
            return smoothstep(0.5h, 0.6h, value);
        }

        half3 TriplanarWeights(half3 normalWS)
        {
            half3 weights = abs(normalWS);
            weights = max(weights, 0.0001h.xxx);
            return weights / dot(weights, 1.0h.xxx);
        }

        half4 SampleTriplanar(
            TEXTURE2D_PARAM(sourceTexture, sourceSampler),
            float3 positionWS,
            half3 normalWS,
            float tile)
        {
            float3 uvw = positionWS * tile;
            half3 weights = TriplanarWeights(normalWS);
            half4 sampleX = (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uvw.zy);
            half4 sampleY = (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uvw.xz);
            half4 sampleZ = (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uvw.xy);
            return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
        }

        half3 UnpackNormalScale(half4 packedNormal, half scale)
        {
            half3 normal;
            normal.xy = (packedNormal.ag * 2.0h - 1.0h) * scale;
            normal.z = sqrt(max(1.0h - saturate(dot(normal.xy, normal.xy)), 0.0h));
            return normal;
        }

        half3 ComputeRockBaseColor(float3 positionWS, half3 normalWS, half4 mahs)
        {
            half4 mask01 = SampleTriplanar(
                TEXTURE2D_ARGS(_MaskDetailMap_01, sampler_MaskDetailMap_01),
                positionWS,
                normalWS,
                _MaskMapTile_01);
            half4 mask02 = SampleTriplanar(
                TEXTURE2D_ARGS(_MaskDetailMap_02, sampler_MaskDetailMap_02),
                positionWS,
                normalWS,
                _MaskMapTile_02);

            half3 color = _MainColor.rgb;
            color = lerp(color, _Color_01.rgb, SmoothMask(mask01.r));
            color = lerp(color, _Color_02.rgb, SmoothMask(mask01.g));
            color = lerp(color, _Color_02.rgb, SmoothMask(mask01.b) * 0.7h);
            color = lerp(color, _Color_03.rgb, saturate(SmoothMask(mask02.g) * 0.5h));
            color = lerp(color, _Color_Height.rgb, mahs.b);
            color = lerp(color, _Color_04.rgb, saturate(SmoothMask(mask01.b) - SmoothMask(mask02.r)) * 0.25h);
            color = lerp(color, _Color_05.rgb, SmoothMask(mask02.b) * 0.25h);
            color = lerp(color, _Color_AO.rgb, saturate((1.0h - mahs.g) * 0.65h));

            half3 cover = SampleTriplanar(
                TEXTURE2D_ARGS(_CoverMap, sampler_CoverMap),
                positionWS,
                normalWS,
                _CoverMapTile).rgb;

            half slopeMask = saturate(smoothstep(
                0.0h,
                0.2h,
                saturate(dot(normalWS, half3(0.0h, 1.0h, 0.0h)) + _CoverAmount + mahs.b * _CoverBlendIntensity)));
            half heightMask = saturate(1.0h - (positionWS.y - _CoverHeightStart) * 0.2h);
            return lerp(color, cover, slopeMask * heightMask);
        }

        half3 EvaluateDirectRockPBR(
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

            half3 f0 = kDielectricF0;
            half d = D_GGX(nDotH, roughness);
            half v = V_SmithJointApprox(nDotL, nDotV, roughness);
            half3 f = F_Schlick(f0, lDotH);
            half3 brdf = albedo * INV_PI + d * v * f;

            half3 radiance = light.color
                * light.distanceAttenuation
                * light.shadowAttenuation
                * nDotL;
            return brdf * radiance;
        }

        half3 EvaluateRockPBR(
            half3 normalWS,
            half3 viewWS,
            float3 positionWS,
            half3 albedo,
            half smoothness,
            half occlusion)
        {
            half perceptualRoughness = SmoothnessToPerceptualRoughness(saturate(smoothness));
            half3 color = 0.0h.xxx;

            Light mainLight = GetMainLight(positionWS, normalWS);
            half3 debugColor;
            if (TryGetMainLightShadowDebugOverride(mainLight, debugColor))
            {
                return debugColor;
            }

            color += EvaluateDirectRockPBR(mainLight, normalWS, viewWS, albedo, perceptualRoughness);

            int additionalLightCount = GetAdditionalLightsCount();
            for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS, normalWS);
                color += EvaluateDirectRockPBR(light, normalWS, viewWS, albedo, perceptualRoughness);
            }

            half3 indirectDiffuse = SampleSH(normalWS) * albedo * occlusion;
            half3 f0 = kDielectricF0;
            half nDotV = saturate(dot(normalWS, viewWS));
            half3 envBRDF = F_SchlickRoughness(f0, nDotV, perceptualRoughness);
            half3 indirectSpecular = SampleEnvironmentReflection(normalWS, viewWS, perceptualRoughness)
                * envBRDF
                * occlusion;

            return color + indirectDiffuse + indirectSpecular;
        }

        RockVaryings RockVert(RockAttributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            RockVaryings output = (RockVaryings)0;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.uv;

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
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex RockVert
            #pragma fragment RockFrag
            #pragma multi_compile_instancing

            half4 RockFrag(RockVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 baseNormalWS = normalize(input.normalWS);
                half4 mahs = (half4)SAMPLE_TEXTURE2D(_MAHSMap, sampler_MAHSMap, input.uv);
                half3 albedo = ComputeRockBaseColor(input.positionWS, baseNormalWS, mahs);

                half3 normalTS = UnpackNormalScale(
                    (half4)SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                    1.0h);
                float3x3 tbn = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS));
                half3 normalWS = normalize(TransformTangentToWorldDir(normalTS, tbn));
                half3 viewWS = SafeNormalize(input.viewWS);

                half3 color = EvaluateRockPBR(
                    normalWS,
                    viewWS,
                    input.positionWS,
                    albedo,
                    mahs.a,
                    mahs.g);
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
