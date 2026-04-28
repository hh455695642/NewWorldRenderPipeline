Shader "NewWorld/Env/WorldGrass"
{
    Properties
    {
        [Header(Shadow Color)]
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.3, 0.1, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.5
        [ToggleUI]_ReceiveShadows ("Receive Realtime Shadows", Float) = 1.0

        [Header(World Space Noise Color)]
        [NoScaleOffset]_NoiseColorTex ("Noise Color Texture", 2D) = "gray" {}
        _NoiseColorScale ("Noise Color Scale", Float) = 0.1
        _NoiseColorIntensity ("Noise Color Intensity", Range(0, 1)) = 0.3
        _NoiseColor1 ("Noise Color 1", Color) = (0.5, 0.7, 0.3, 1)
        _NoiseColor2 ("Noise Color 2", Color) = (0.3, 0.5, 0.2, 1)
        _TipColor ("Tip Color", Color) = (0.98, 0.98, 0.98, 1)
        _HeightGradientThreshold ("Height Gradient Threshold", Range(0, 1)) = 0.7

        [Header(Ramp Color)]
        [NoScaleOffset]_RampTex ("Ramp Texture", 2D) = "white" {}
        _RampIntensity ("Ramp Intensity", Range(0, 1)) = 0.5

        [Header(Idle Motion)]
        _IdleSwayStrength ("Idle Sway Strength", Range(0, 0.5)) = 0.08
        _IdleSwaySpeed ("Idle Sway Speed", Range(0.1, 5)) = 1.5

        [Header(Gust Wind)]
        _GustStrength ("Gust Strength", Range(0, 2)) = 0.3
        _GustSpeed ("Gust Speed", Range(0.1, 5)) = 0.8
        _GustFrequency ("Gust Frequency", Range(0.01, 1)) = 0.15

        [Header(Distance Fade)]
        _DitherFadeStart ("Fade Start", Range(5, 30)) = 30
        _DitherFadeEnd ("Fade End", Range(30, 100)) = 50
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE
        #include "../../ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _TipColor;
            half _HeightGradientThreshold;
            half4 _ShadowColor;
            half _ShadowStrength;
            half _ReceiveShadows;

            float _NoiseColorScale;
            half _NoiseColorIntensity;
            half4 _NoiseColor1;
            half4 _NoiseColor2;

            half _RampIntensity;

            float _IdleSwayStrength;
            float _IdleSwaySpeed;
            float _GustStrength;
            float _GustSpeed;
            float _GustFrequency;

            float _DitherFadeStart;
            float _DitherFadeEnd;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../ShaderLibrary/GlobalIllumination.hlsl"
        #include "./Includes/VegetationIndirectInstancing.hlsl"

        TEXTURE2D(_NoiseColorTex);
        SAMPLER(sampler_NoiseColorTex);
        TEXTURE2D(_RampTex);
        SAMPLER(sampler_RampTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 texcoord : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float4 color : TEXCOORD2;
            float fogFactor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float3 permute(float3 x) { return mod289(((x * 34.0) + 1.0) * x); }

        float snoise(float2 v)
        {
            const float4 C = float4(0.211324865405187, 0.366025403784439,
                                   -0.577350269189626, 0.024390243902439);
            float2 i = floor(v + dot(v, C.yy));
            float2 x0 = v - i + dot(i, C.xx);
            float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
            float4 x12 = x0.xyxy + C.xxzz;
            x12.xy -= i1;
            i = mod289(i);
            float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));
            float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
            m = m * m;
            m = m * m;
            float3 x = 2.0 * frac(p * C.www) - 1.0;
            float3 h = abs(x) - 0.5;
            float3 ox = floor(x + 0.5);
            float3 a0 = x - ox;
            m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);
            float3 g;
            g.x = a0.x * x0.x + h.x * x0.y;
            g.yz = a0.yz * x12.xz + h.yz * x12.yw;
            return 130.0 * dot(m, g);
        }

        float hash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float smoothPulse(float x, float duration, float interval)
        {
            float cycle = fmod(x, interval);
            float normalizedCycle = cycle / duration;
            float fadeIn = smoothstep(0.0, 0.2, normalizedCycle);
            float fadeOut = 1.0 - smoothstep(0.8, 1.0, normalizedCycle);
            return saturate(fadeIn * fadeOut * step(cycle, duration));
        }

        float3 ApplyWindAnimation(float3 positionOS, float3 positionWS, float heightMask)
        {
            float time = _Time.y;
            float2 bladeID = floor(positionWS.xz * 2.0);
            float randomOffset1 = hash(bladeID) * TWO_PI;
            float randomOffset2 = hash(bladeID + 100.0) * TWO_PI;
            float randomSpeed = 0.8 + hash(bladeID + 200.0) * 0.4;

            float idleTime = time * _IdleSwaySpeed * randomSpeed;
            float idleSwayX = sin(idleTime + randomOffset1) * 0.6
                           + sin(idleTime * 1.7 + randomOffset1 * 1.3) * 0.3
                           + sin(idleTime * 2.9 + randomOffset1 * 0.7) * 0.1;
            float idleSwayZ = sin(idleTime * 1.2 + randomOffset2) * 0.6
                           + sin(idleTime * 1.9 + randomOffset2 * 1.1) * 0.3
                           + sin(idleTime * 3.1 + randomOffset2 * 0.9) * 0.1;
            float2 idleOffset = float2(idleSwayX, idleSwayZ) * _IdleSwayStrength;

            float gustTime = time * _GustFrequency;
            float gustIntensity = smoothPulse(gustTime, 9.0 * _GustFrequency, 9.0 * _GustFrequency);
            float2 gustNoiseUV = positionWS.xz * 0.05 + float2(time * _GustSpeed * 0.1, 0.0);
            float gustNoise = SAMPLE_TEXTURE2D_LOD(_NoiseColorTex, sampler_NoiseColorTex, gustNoiseUV, 0).r;

            float2 gustWaveUV = positionWS.xz * 0.08;
            float gustWavePhase = time * _GustSpeed;
            float gustWave = sin(gustWaveUV.x * PI + gustWaveUV.y * 1.5 - gustWavePhase * 2.0);
            float gustWave2 = sin(gustWaveUV.x * 2.0 - gustWaveUV.y * 2.5 - gustWavePhase * 1.5) * 0.3;
            float combinedGust = (gustWave + gustWave2) * (0.7 + gustNoise * 0.3);

            float gustDirAngle = time * 0.1 + snoise(float2(time * 0.05, 0.0)) * 0.5;
            float2 gustDir = normalize(float2(cos(gustDirAngle), sin(gustDirAngle) * 0.5 + 0.5));
            float2 gustOffset = gustDir * combinedGust * _GustStrength * gustIntensity;

            float idleBlend = lerp(1.0, 0.5, gustIntensity);
            float2 totalOffset = idleOffset * idleBlend + gustOffset;
            float heightFactor = heightMask * heightMask;

            float3 offset = float3(totalOffset.x, 0.0, totalOffset.y) * heightFactor;
            float horizontalDisplacement = length(offset.xz);
            offset.y = -horizontalDisplacement * 0.3 * heightMask;
            return positionOS + offset;
        }

        static const float DITHER_THRESHOLDS[16] =
        {
            1.0/17.0,  9.0/17.0,  3.0/17.0, 11.0/17.0,
            13.0/17.0, 5.0/17.0, 15.0/17.0,  7.0/17.0,
            4.0/17.0, 12.0/17.0,  2.0/17.0, 10.0/17.0,
            16.0/17.0, 8.0/17.0, 14.0/17.0,  6.0/17.0
        };

        float GetDitherThreshold(float2 screenPos)
        {
            uint2 index = (uint2)fmod(screenPos, 4.0);
            return DITHER_THRESHOLDS[index.x * 4 + index.y];
        }

        void ApplyDistanceDitherFade(float4 positionCS, float3 positionWS, float heightMask)
        {
            float dist = distance(_WorldSpaceCameraPos, positionWS);
            float distanceFade = saturate(
                (dist - _DitherFadeStart) / max(_DitherFadeEnd - _DitherFadeStart, 0.001));
            float bottomFade = saturate(heightMask * 8.0);
            float visibility = bottomFade * (1.0 - distanceFade);
            clip(visibility - GetDitherThreshold(positionCS.xy));
        }

        half3 ApplyWorldSpaceNoiseColor(float3 positionWS)
        {
            float2 noiseUV = positionWS.xz * _NoiseColorScale;
            half noiseValue = SAMPLE_TEXTURE2D(_NoiseColorTex, sampler_NoiseColorTex, noiseUV).r;
            return lerp(_NoiseColor1.rgb, _NoiseColor2.rgb, noiseValue * _NoiseColorIntensity);
        }

        half3 ApplyRampColor(half3 baseColor, half nDotL)
        {
            half2 rampUV = half2(saturate(nDotL), 0.5h);
            half3 rampColor = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, rampUV).rgb;
            return lerp(baseColor, rampColor, _RampIntensity);
        }

        half3 ApplyHeightGradient(float3 positionWS, half3 baseColor, half heightMask)
        {
            half remappedMask = saturate((heightMask - _HeightGradientThreshold) / max(1.0h - _HeightGradientThreshold, 0.001h));
            float2 noiseUV = positionWS.xz * _NoiseColorScale;
            half noiseValue = SAMPLE_TEXTURE2D(_NoiseColorTex, sampler_NoiseColorTex, noiseUV).r;
            remappedMask *= saturate(noiseValue);
            return lerp(baseColor, _TipColor.rgb, remappedMask);
        }

        half3 ComputeStylizedLighting(half3 albedo, Light mainLight)
        {
            half3 upNormal = half3(0.0h, 1.0h, 0.0h);
            half nDotL = dot(upNormal, mainLight.direction);
            half halfLambert = nDotL * 0.5h + 0.5h;
            half shadowTerm = smoothstep(0.0h, 0.5h, halfLambert) * mainLight.shadowAttenuation;

            albedo = ApplyRampColor(albedo, halfLambert);
            half3 shadowedColor = lerp(albedo, albedo * _ShadowColor.rgb, _ShadowStrength);
            half3 litColor = albedo * mainLight.color;
            half3 finalColor = lerp(shadowedColor, litColor, shadowTerm);
            finalColor += SampleSH(upNormal) * albedo * 0.3h;
            return finalColor;
        }

        Varyings VegetationVert(Attributes input)
        {
            Varyings output = (Varyings)0;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float heightMask = input.color.r > 0.01 ? input.color.r : input.texcoord.y;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 animatedPositionOS = ApplyWindAnimation(input.positionOS.xyz, positionWS, heightMask);

            output.positionWS = TransformObjectToWorld(animatedPositionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.texcoord;
            output.color = input.color;
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "NewWorldForward"
            Tags { "LightMode" = "NewWorldForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VegetationVert
            #pragma fragment ForwardFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing
            #pragma multi_compile_fog

            half4 ForwardFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half heightMask = input.uv.y;
                ApplyDistanceDitherFade(input.positionCS, input.positionWS, heightMask);

                half3 albedo = ApplyWorldSpaceNoiseColor(input.positionWS);
                albedo = ApplyHeightGradient(input.positionWS, albedo, heightMask);

                half3 upNormal = half3(0.0h, 1.0h, 0.0h);
                Light mainLight = GetMainLight(input.positionWS, upNormal);
                half3 finalColor = ComputeStylizedLighting(albedo, mainLight);
                
                int additionalLightCount = GetAdditionalLightsCount();
                for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, upNormal);
                    half addNdotL = saturate(dot(upNormal, light.direction) * 0.5h + 0.5h);
                    finalColor += albedo
                        * addNdotL
                        * light.color
                        * light.distanceAttenuation
                        * light.shadowAttenuation
                        * 0.5h;
                }

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VegetationVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half heightMask = input.uv.y;
                ApplyDistanceDitherFade(input.positionCS, input.positionWS, heightMask);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
