Shader "NewWorld/Env/WorldGrass"
{
    Properties
    {
        [Header(Shadow Color)]
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.3, 0.1, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.5
        
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
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _TipColor;
            half _HeightGradientThreshold;
            half4 _ShadowColor;
            half _ShadowStrength;
            
            float _NoiseColorScale;
            half _NoiseColorIntensity;
            half4 _NoiseColor1;
            half4 _NoiseColor2;
            
            half _RampIntensity;
            
            // Idle sway parameters
            float _IdleSwayStrength;
            float _IdleSwaySpeed;
            
            // Wind gust parameters
            float _GustStrength;
            float _GustSpeed;
            float _GustFrequency;
            
            float _DitherFadeStart;
            float _DitherFadeEnd;
            
            float _DepthFadeDistance;
        
        CBUFFER_END
        

        #pragma multi_compile_instancing
        
        
// 实例化缓冲区（可用于 GPU Instancing）
UNITY_INSTANCING_BUFFER_START(PerInstance)
    //UNITY_DEFINE_INSTANCED_PROP(float, _TestValue)
    //UNITY_DEFINE_INSTANCED_PROP(half4, _NoiseColor1) 
UNITY_INSTANCING_BUFFER_END(PerInstance)
        

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
            float4 screenPos : TEXCOORD4;
            float eyeDepth : TEXCOORD5;
            float4 shadowCoord : TEXCOORD6;  // Added for shadow receiving
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        // ==================== Utility Functions ====================
        
        // Simplex noise for wind
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

        // Hash function for pseudo-random values
        float hash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }
        
        // Smooth interpolation for gust timing
        float smoothPulse(float x, float duration, float interval)
        {
            float cycle = fmod(x, interval);
            float normalizedCycle = cycle / duration;
            
            // Smooth in and out using smoothstep
            float fadeIn = smoothstep(0.0, 0.2, normalizedCycle);
            float fadeOut = 1.0 - smoothstep(0.8, 1.0, normalizedCycle);
            
            return saturate(fadeIn * fadeOut * step(cycle, duration));
        }

        // ==================== Wind Animation ====================
        
        float3 ApplyWindAnimation(float3 positionOS, float3 positionWS, float heightMask)
        {
            float time = _Time.y;
            
            // ============ IDLE SWAY (Always active, random direction per grass blade) ============
            // Use world position to create unique random offset per blade
            float2 bladeID = floor(positionWS.xz * 2.0); // Unique ID based on position
            float randomOffset1 = hash(bladeID) * 6.28318; // Random phase offset
            float randomOffset2 = hash(bladeID + 100.0) * 6.28318;
            float randomSpeed = 0.8 + hash(bladeID + 200.0) * 0.4; // Speed variation 0.8-1.2
            
            // Multi-frequency idle sway for organic feel
            float idleTime = time * _IdleSwaySpeed * randomSpeed;
            
            // Primary idle sway
            float idleSwayX = sin(idleTime * 1.0 + randomOffset1) * 0.6 
                           + sin(idleTime * 1.7 + randomOffset1 * 1.3) * 0.3
                           + sin(idleTime * 2.9 + randomOffset1 * 0.7) * 0.1;
                           
            float idleSwayZ = sin(idleTime * 1.2 + randomOffset2) * 0.6 
                           + sin(idleTime * 1.9 + randomOffset2 * 1.1) * 0.3
                           + sin(idleTime * 3.1 + randomOffset2 * 0.9) * 0.1;
            
            float2 idleOffset = float2(idleSwayX, idleSwayZ) * _IdleSwayStrength;
            
            // ============ WIND GUST (Intermittent, wave-like motion across the field) ============
            // Calculate gust intensity using smooth pulse
            float gustTime = time * _GustFrequency;
            float gustIntensity = smoothPulse(gustTime, 9.0 * _GustFrequency, 9.0 * _GustFrequency);
            
            // Sample noise texture for gust direction and intensity variation
            float2 gustNoiseUV = positionWS.xz * 0.05 + float2(time * _GustSpeed * 0.1, 0);
            float gustNoise = SAMPLE_TEXTURE2D_LOD(_NoiseColorTex, sampler_NoiseColorTex, gustNoiseUV, 0).r;
            
            // Create wave-like motion across the field
            float2 gustWaveUV = positionWS.xz * 0.08;
            float gustWavePhase = time * _GustSpeed;
            
            // Main directional wave (like wind blowing across wheat field)
            float gustWave = sin(gustWaveUV.x * 3.14159 + gustWaveUV.y * 1.5 - gustWavePhase * 2.0);
            
            // Secondary cross wave for more natural look
            float gustWave2 = sin(gustWaveUV.x * 2.0 - gustWaveUV.y * 2.5 - gustWavePhase * 1.5) * 0.3;
            
            // Combine waves with noise
            float combinedGust = (gustWave + gustWave2) * (0.7 + gustNoise * 0.3);
            
            // Gust direction - changes slowly over time for variety
            float gustDirAngle = time * 0.1 + snoise(float2(time * 0.05, 0)) * 0.5;
            float2 gustDir = float2(cos(gustDirAngle), sin(gustDirAngle) * 0.5 + 0.5);
            gustDir = normalize(gustDir);
            
            float2 gustOffset = gustDir * combinedGust * _GustStrength * gustIntensity;
            
            // ============ COMBINE IDLE AND GUST ============
            // Idle is always present, gust adds on top
            // When gust is strong, we can optionally reduce idle slightly for cleaner motion
            float idleBlend = lerp(1.0, 0.5, gustIntensity);
            float2 totalOffset = idleOffset * idleBlend + gustOffset;
            
            // Apply height-based falloff (grass bends more at top)
            float heightFactor = heightMask * heightMask; // Quadratic for natural bending
            
            // Build final offset
            float3 offset = float3(totalOffset.x, 0, totalOffset.y) * heightFactor;
            
            // Preserve grass length by adjusting Y position
            float horizontalDisplacement = length(offset.xz);
            offset.y = -horizontalDisplacement * 0.3 * heightMask;
            
            return positionOS + offset;
        }

        // ==================== Dither Fade (Distance) ====================
        
        static const float DITHER_THRESHOLDS[16] = {
            1.0/17.0,  9.0/17.0,  3.0/17.0, 11.0/17.0,
            13.0/17.0, 5.0/17.0, 15.0/17.0,  7.0/17.0,
            4.0/17.0, 12.0/17.0,  2.0/17.0, 10.0/17.0,
            16.0/17.0, 8.0/17.0, 14.0/17.0,  6.0/17.0
        };

        float GetDitherThreshold(float2 screenPos)
        {
            uint2 index = uint2(fmod(screenPos, 4.0));
            return DITHER_THRESHOLDS[index.x * 4 + index.y];
        }
        
        void ApplyDistanceDitherFade(float4 positionCS, float3 positionWS,float HeightMask)
        {
            // -------- Distance Fade --------
            float dist = distance(_WorldSpaceCameraPos, positionWS);

            float distanceFade = saturate(
                (dist - _DitherFadeStart) /
                max(_DitherFadeEnd - _DitherFadeStart, 0.001)
            );

            // -------- Bottom Fade --------
            float bottomFade = saturate(HeightMask * 8.0);

            // -------- Combine --------
            float visibility = bottomFade * (1.0 - distanceFade);

            // -------- Dither --------
            float2 screenPos = positionCS.xy;
            float dither = GetDitherThreshold(screenPos);

            // Final clip
            clip(visibility - dither);
        }
        

        // ==================== Color Functions ====================
        
        half3 ApplyWorldSpaceNoiseColor(float3 positionWS)
        {
            float2 noiseUV = positionWS.xz * _NoiseColorScale;
            half noiseValue = SAMPLE_TEXTURE2D(_NoiseColorTex, sampler_NoiseColorTex, noiseUV).r;
            //half3 noiseColor = lerp( UNITY_ACCESS_INSTANCED_PROP(PerInstance,_NoiseColor1.rgb), _NoiseColor2.rgb, noiseValue * _NoiseColorIntensity);
            half3 noiseColor = lerp( _NoiseColor1.rgb, _NoiseColor2.rgb, noiseValue * _NoiseColorIntensity);
            return noiseColor;
        }

        half3 ApplyRampColor(half3 baseColor, float NdotL)
        {
            half2 rampUV = half2(saturate(NdotL), 0.5);
            half3 rampColor = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, rampUV).rgb;
            return lerp(baseColor, rampColor, _RampIntensity);
        }

        half3 ApplyHeightGradient(float3 positionWS, half3 baseColor, float heightMask)
        {
            float remappedMask = saturate((heightMask - _HeightGradientThreshold) / max(1.0 - _HeightGradientThreshold, 0.001));
            float2 noiseUV = positionWS.xz * _NoiseColorScale;
            half noiseValue = SAMPLE_TEXTURE2D(_NoiseColorTex, sampler_NoiseColorTex, noiseUV).r;
            remappedMask *= saturate(noiseValue);
            return lerp(baseColor, _TipColor.rgb, remappedMask);
        }

        // ==================== Lighting ====================
        
        half3 ComputeStylizedLighting(half3 albedo, float3 positionWS, Light mainLight, float shadowAttenuation)
        {
            // Use fixed up normal for stylized grass look
            float3 upNormal = float3(0, 1, 0);
            
            // Half Lambert for soft lighting
            float NdotL = dot(upNormal, mainLight.direction);
            float halfLambert = NdotL * 0.5 + 0.5;
            
            // Stylized shadow transition
            float shadowTerm = smoothstep(0.0, 0.5, halfLambert);
            
            // Apply shadow attenuation from shadow map
            shadowTerm *= shadowAttenuation;
            
            // Apply ramp if available
            albedo = ApplyRampColor(albedo, halfLambert);
            
            // Blend between shadow color and lit color
            half3 shadowedColor = lerp(albedo, albedo * _ShadowColor.rgb, _ShadowStrength);
            half3 litColor = albedo * mainLight.color;
            
            half3 finalColor = lerp(shadowedColor, litColor, shadowTerm);
            
            // Add ambient
            half3 ambient = SampleSH(upNormal) * albedo * 0.3;
            finalColor += ambient;
            
            return finalColor;
        }

        ENDHLSL

        // ==================== Forward Pass ====================
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            
            #pragma vertex ForwardVert
            #pragma fragment ForwardFrag

            #pragma multi_compile_instancing
            //#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            
            // Shadow keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            Varyings ForwardVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Height mask from vertex color R channel or UV.y
                float heightMask = input.color.r > 0.01 ? input.color.r : input.texcoord.y;
                
                // Apply wind animation
                float3 positionWS_temp = TransformObjectToWorld(input.positionOS.xyz);
                float3 animatedPositionOS = ApplyWindAnimation(input.positionOS.xyz, positionWS_temp, heightMask);
                
                output.positionWS = TransformObjectToWorld(animatedPositionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.texcoord;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                // For depth fade
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                
                // Shadow coordinates for receiving shadows
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    output.shadowCoord = ComputeScreenPos(output.positionCS);
                #else
                    output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                #endif

                return output;
            }

            half4 ForwardFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // Height mask
                //float heightMask = input.color.r > 0.01 ? input.color.r : input.uv.y;
                float heightMask = input.uv.y;

                
                // // Apply distance dither fade
                ApplyDistanceDitherFade(input.positionCS, input.positionWS,heightMask);
                
                
                // Base color
                half3 albedo = half3(1.0,1.0,1.0); //use white as noise color base
                
                // Apply color effects
                albedo = ApplyWorldSpaceNoiseColor(input.positionWS); 
                albedo = ApplyHeightGradient(input.positionWS, albedo, heightMask);

                // Get main light with shadow attenuation
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = input.shadowCoord;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                
                Light mainLight = GetMainLight(shadowCoord);
                float shadowAttenuation = mainLight.shadowAttenuation;
                
                // Lighting with shadow
                half3 finalColor = ComputeStylizedLighting(albedo, input.positionWS, mainLight, shadowAttenuation);
                
                // Additional lights
                #ifdef _ADDITIONAL_LIGHTS
                    float3 upNormal = float3(0, 1, 0);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        half addNdotL = saturate(dot(upNormal, light.direction) * 0.5 + 0.5);
                        finalColor += albedo * addNdotL * light.color * light.distanceAttenuation * light.shadowAttenuation * 0.5;
                    }
                #endif
                
                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);

            }

            ENDHLSL
        }

        // ==================== Depth Only Pass ====================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float heightMask = input.color.r > 0.01 ? input.color.r : input.texcoord.y;
                float3 positionWS_temp = TransformObjectToWorld(input.positionOS.xyz);
                float3 animatedPositionOS = ApplyWindAnimation(input.positionOS.xyz, positionWS_temp, heightMask);
                
                output.positionWS = TransformObjectToWorld(animatedPositionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.texcoord;
                output.color = input.color;

                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float heightMask = input.uv.y;
                ApplyDistanceDitherFade(input.positionCS, input.positionWS,heightMask);

                return input.positionCS.z;
            }

            ENDHLSL
        }

        // ==================== Depth Normals Pass ====================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            struct VaryingsDepthNormals
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepthNormals DepthNormalsVert(Attributes input)
            {
                VaryingsDepthNormals output = (VaryingsDepthNormals)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float heightMask = input.color.r > 0.01 ? input.color.r : input.texcoord.y;
                float3 positionWS_temp = TransformObjectToWorld(input.positionOS.xyz);
                float3 animatedPositionOS = ApplyWindAnimation(input.positionOS.xyz, positionWS_temp, heightMask);
                
                output.positionWS = TransformObjectToWorld(animatedPositionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.texcoord;
                output.color = input.color;

                return output;
            }

            half4 DepthNormalsFrag(VaryingsDepthNormals input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float heightMask = input.uv.y;
                ApplyDistanceDitherFade(input.positionCS, input.positionWS,heightMask);

                // Output up-facing normal for stylized grass
                float3 upNormal = float3(0, 1, 0);
                return half4(upNormal * 0.5 + 0.5, 0);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
