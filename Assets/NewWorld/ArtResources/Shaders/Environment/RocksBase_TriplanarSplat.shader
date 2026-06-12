Shader "NewWorld/Env/RocksBase_TriplanarSplat"
{
    Properties
    {
        [Header(Rock Base)]
        _BaseMap("Rock BaseMap", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _NormalMap("Rock Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 2)) = 1.0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5

        [Header(Triplanar Settings)]
        _TriplanarTiling("Triplanar Tiling", Float) = 1.0
        _TriplanarBlendSharpness("Blend Sharpness", Range(1, 8)) = 2.0

        [Header(Noise Color Variation)]
        _NoiseMap("Noise Map", 2D) = "gray" {}
        _NoiseTiling("Noise Tiling", Float) = 0.5
        _NoiseBlendSharpness("Noise Blend Sharpness", Range(1, 8)) = 2.0
        [HDR]_ColorA("Color A (Dark)", Color) = (0.6, 0.4, 0.35, 1)
        [HDR]_ColorB("Color B (Mid)", Color) = (1.0, 1.0, 1.0, 1)
        [HDR]_ColorC("Color C (Bright)", Color) = (1.2, 1.1, 0.95, 1)
        _NoiseColorIntensity("Noise Color Intensity", Range(0, 1)) = 0.5
        _NoiseContrast("Noise Contrast", Range(0.5, 3)) = 1.0

        [Header(Dust Top Layer)]
        _DustMap("Dust BaseMap", 2D) = "white" {}
        _DustColor("Dust Color", Color) = (0.9, 0.85, 0.7, 1)
        _DustNormalMap("Dust Normal Map", 2D) = "bump" {}
        _DustNormalScale("Dust Normal Scale", Range(0, 2)) = 1.0
        _DustTiling("Dust Tiling", Float) = 2.0
        _DustSmoothness("Dust Smoothness", Range(0, 1)) = 0.3

        [Header(Dust Blend Settings)]
        _DustBlendStart("Dust Blend Start (Normal Y)", Range(0, 1)) = 0.5
        _DustBlendEnd("Dust Blend End (Normal Y)", Range(0, 1)) = 0.9
        _DustBlendContrast("Dust Blend Contrast", Range(1, 10)) = 3.0

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

        TEXTURE2D(_BaseMap);       SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);     SAMPLER(sampler_NormalMap);
        TEXTURE2D(_NoiseMap);      SAMPLER(sampler_NoiseMap);
        TEXTURE2D(_DustMap);       SAMPLER(sampler_DustMap);
        TEXTURE2D(_DustNormalMap); SAMPLER(sampler_DustNormalMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _NormalScale;
            half _Smoothness;

            float _TriplanarTiling;
            float _TriplanarBlendSharpness;

            float4 _NoiseMap_ST;
            float _NoiseTiling;
            float _NoiseBlendSharpness;
            half4 _ColorA;
            half4 _ColorB;
            half4 _ColorC;
            half _NoiseColorIntensity;
            half _NoiseContrast;

            float4 _DustMap_ST;
            half4 _DustColor;
            half _DustNormalScale;
            float _DustTiling;
            half _DustSmoothness;

            half _DustBlendStart;
            half _DustBlendEnd;
            half _DustBlendContrast;

            half _ReceiveShadows;
            half _CastShadows;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../../../NWRP/ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../../../NWRP/ShaderLibrary/BRDF.hlsl"
        #include "../../../../NWRP/ShaderLibrary/GlobalIllumination.hlsl"

        struct RockSplatAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct RockSplatVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 tangentWS : TEXCOORD2;
            float3 bitangentWS : TEXCOORD3;
            float3 viewWS : TEXCOORD4;
            half fogFactor : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        half3 UnpackNormalScale(half4 packedNormal, half scale)
        {
            half3 normal;
            normal.xy = (packedNormal.ag * 2.0h - 1.0h) * scale;
            normal.z = sqrt(max(1.0h - saturate(dot(normal.xy, normal.xy)), 0.0h));
            return normal;
        }

        half3 GetTriplanarWeights(half3 normalWS, half sharpness)
        {
            half3 weights = pow(max(abs(normalWS), 0.0001h.xxx), max(sharpness, 0.0001h));
            return weights / max(dot(weights, 1.0h.xxx), 0.0001h);
        }

        half4 SampleTriplanar(
            TEXTURE2D_PARAM(sourceTexture, sourceSampler),
            float3 positionWS,
            half3 normalWS,
            float tiling,
            half sharpness)
        {
            float3 scaledPosition = positionWS * tiling;
            half3 weights = GetTriplanarWeights(normalWS, sharpness);
            half4 sampleX = (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, scaledPosition.zy);
            half4 sampleY = (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, scaledPosition.xz);
            half4 sampleZ = (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, scaledPosition.xy);
            return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
        }

        half3 SampleTriplanarNormal(
            TEXTURE2D_PARAM(sourceTexture, sourceSampler),
            float3 positionWS,
            half3 normalWS,
            float tiling,
            half sharpness,
            half normalScale)
        {
            float3 scaledPosition = positionWS * tiling;
            half3 weights = GetTriplanarWeights(normalWS, sharpness);

            half3 normalX = UnpackNormalScale(
                (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, scaledPosition.zy),
                normalScale);
            half3 normalY = UnpackNormalScale(
                (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, scaledPosition.xz),
                normalScale);
            half3 normalZ = UnpackNormalScale(
                (half4)SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, scaledPosition.xy),
                normalScale);

            // UDN-style blend keeps the rock normal world-aligned without building three TBN matrices.
            normalX = half3(normalX.xy + normalWS.zy, abs(normalX.z) * normalWS.x);
            normalY = half3(normalY.xy + normalWS.xz, abs(normalY.z) * normalWS.y);
            normalZ = half3(normalZ.xy + normalWS.xy, abs(normalZ.z) * normalWS.z);

            return normalize(
                normalX.zyx * weights.x +
                normalY.xzy * weights.y +
                normalZ.xyz * weights.z);
        }

        half3 SampleNoiseTriplanarColor(float3 positionWS, half3 normalWS)
        {
            half noiseValue = SampleTriplanar(
                TEXTURE2D_ARGS(_NoiseMap, sampler_NoiseMap),
                positionWS,
                normalWS,
                _NoiseTiling,
                (half)_NoiseBlendSharpness).r;

            noiseValue = saturate((noiseValue - 0.5h) * _NoiseContrast + 0.5h);
            half lowerBlend = saturate(noiseValue * 2.0h);
            half upperBlend = saturate((noiseValue - 0.5h) * 2.0h);
            half3 lowerColor = lerp(_ColorA.rgb, _ColorB.rgb, lowerBlend);
            half3 upperColor = lerp(_ColorB.rgb, _ColorC.rgb, upperBlend);
            return lerp(lowerColor, upperColor, step(0.5h, noiseValue));
        }

        half CalculateDustMask(half3 normalWS)
        {
            half slopeFactor = saturate(normalWS.y);
            half dustMask = smoothstep(_DustBlendStart, _DustBlendEnd, slopeFactor);
            return pow(dustMask, _DustBlendContrast);
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

        half3 EvaluateRockPBR(
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

            color += EvaluateDirectRockPBR(mainLight, normalWS, viewWS, albedo, perceptualRoughness);

            int additionalLightCount = GetAdditionalLightsCount();
            for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS, normalWS);
                color += EvaluateDirectRockPBR(light, normalWS, viewWS, albedo, perceptualRoughness);
            }

            half nDotV = saturate(dot(normalWS, viewWS));
            half3 indirectDiffuse = SampleSH(normalWS) * albedo;
            half3 envBRDF = F_SchlickRoughness(kDielectricF0, nDotV, perceptualRoughness);
            half3 indirectSpecular = SampleEnvironmentReflection(normalWS, viewWS, perceptualRoughness)
                * envBRDF;

            return color + indirectDiffuse + indirectSpecular;
        }

        RockSplatVaryings RockSplatVert(RockSplatAttributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            RockSplatVaryings output = (RockSplatVaryings)0;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);

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
            #pragma vertex RockSplatVert
            #pragma fragment RockSplatFrag
            #pragma multi_compile_instancing

            half4 RockSplatFrag(RockSplatVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                half3 baseNormalWS = normalize((half3)input.normalWS);

                half4 rockBase = SampleTriplanar(
                    TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap),
                    positionWS,
                    baseNormalWS,
                    _TriplanarTiling,
                    (half)_TriplanarBlendSharpness) * _BaseColor;

                half3 rockNormalWS = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap),
                    positionWS,
                    baseNormalWS,
                    _TriplanarTiling,
                    (half)_TriplanarBlendSharpness,
                    _NormalScale);

                half3 noiseColor = SampleNoiseTriplanarColor(positionWS, baseNormalWS);
                half3 rockAlbedo = lerp(rockBase.rgb, noiseColor, _NoiseColorIntensity);

                float2 dustUV = positionWS.xz * _DustTiling;
                half4 dustBase = (half4)SAMPLE_TEXTURE2D(_DustMap, sampler_DustMap, dustUV) * _DustColor;
                half3 dustNormalTS = UnpackNormalScale(
                    (half4)SAMPLE_TEXTURE2D(_DustNormalMap, sampler_DustNormalMap, dustUV),
                    _DustNormalScale);

                float3x3 tbn = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS));
                half3 dustNormalWS = normalize(TransformTangentToWorldDir(dustNormalTS, tbn));

                half dustMask = CalculateDustMask(baseNormalWS);
                half3 albedo = lerp(rockAlbedo, dustBase.rgb, dustMask);
                half3 normalWS = normalize(lerp(rockNormalWS, dustNormalWS, dustMask));
                half smoothness = lerp(_Smoothness, _DustSmoothness, dustMask);
                half3 viewWS = SafeNormalize(input.viewWS);

                half3 color = EvaluateRockPBR(
                    normalWS,
                    viewWS,
                    positionWS,
                    albedo,
                    smoothness);

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
    FallBack Off
}
