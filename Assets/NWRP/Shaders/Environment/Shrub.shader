Shader "NewWorld/Env/Shrub"
{
    Properties
    {
        [Header(Base)]
        [NoScaleOffset] _Albedo("Base Texture", 2D) = "white" {}
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0.35

        [Header(Colors)]
        _MainColor("Main Color", Color) = (0.5, 0.7, 0.3, 1)
        _SecondColor("Second Color", Color) = (0.3, 0.5, 0.2, 1)
        _WorldNoiseScale("World Noise Scale", Float) = 0.5
        _SecondColorBlend("Second Color Blend", Range(0, 1)) = 0.5

        [Header(Shadow)]
        _ShadowColor("Shadow Color", Color) = (0.2, 0.3, 0.1, 1)
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 0.5
        [ToggleUI]_ReceiveShadows("Receive Realtime Shadows", Float) = 1.0

        [Header(Idle Motion)]
        _IdleSwayStrength("Idle Sway Strength", Range(0, 0.5)) = 0.08
        _IdleSwaySpeed("Idle Sway Speed", Range(0.1, 5)) = 1.5

        [Header(Distance Fade)]
        _DitherFadeStart("Fade Start", Range(5, 500)) = 30
        _DitherFadeEnd("Fade End", Range(30, 2000)) = 50
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE
        #pragma target 4.5

        #include "../../ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _MainColor;
            half4 _SecondColor;
            half4 _ShadowColor;
            half _SecondColorBlend;
            half _ShadowStrength;
            half _ReceiveShadows;
            float _WorldNoiseScale;
            float _AlphaCutoff;
            float _IdleSwayStrength;
            float _IdleSwaySpeed;
            float _DitherFadeStart;
            float _DitherFadeEnd;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../ShaderLibrary/GlobalIllumination.hlsl"
        #include "./Includes/VegetationIndirectInstancing.hlsl"

        TEXTURE2D(_Albedo);
        SAMPLER(sampler_Albedo);

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
            half3 normalWS : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 mod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
        float4 mod289(float4 x) { return x - floor(x / 289.0) * 289.0; }
        float4 permute(float4 x) { return mod289((x * 34.0 + 1.0) * x); }
        float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - r * 0.85373472095314; }

        float snoise3D(float3 v)
        {
            const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
            float3 i = floor(v + dot(v, C.yyy));
            float3 x0 = v - i + dot(i, C.xxx);
            float3 g = step(x0.yzx, x0.xyz);
            float3 l = 1.0 - g;
            float3 i1 = min(g.xyz, l.zxy);
            float3 i2 = max(g.xyz, l.zxy);
            float3 x1 = x0 - i1 + C.xxx;
            float3 x2 = x0 - i2 + C.yyy;
            float3 x3 = x0 - 0.5;
            i = mod289(i);
            float4 p = permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0)) + i.y + float4(0.0, i1.y, i2.y, 1.0)) + i.x + float4(0.0, i1.x, i2.x, 1.0));
            float4 j = p - 49.0 * floor(p / 49.0);
            float4 x_ = floor(j / 7.0);
            float4 y_ = floor(j - 7.0 * x_);
            float4 x = (x_ * 2.0 + 0.5) / 7.0 - 1.0;
            float4 y = (y_ * 2.0 + 0.5) / 7.0 - 1.0;
            float4 h = 1.0 - abs(x) - abs(y);
            float4 b0 = float4(x.xy, y.xy);
            float4 b1 = float4(x.zw, y.zw);
            float4 s0 = floor(b0) * 2.0 + 1.0;
            float4 s1 = floor(b1) * 2.0 + 1.0;
            float4 sh = -step(h, 0.0);
            float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
            float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
            float3 g0 = float3(a0.xy, h.x);
            float3 g1 = float3(a0.zw, h.y);
            float3 g2 = float3(a1.xy, h.z);
            float3 g3 = float3(a1.zw, h.w);
            float4 norm = taylorInvSqrt(float4(dot(g0, g0), dot(g1, g1), dot(g2, g2), dot(g3, g3)));
            g0 *= norm.x;
            g1 *= norm.y;
            g2 *= norm.z;
            g3 *= norm.w;
            float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
            m = m * m;
            m = m * m;
            float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));
            return 42.0 * dot(m, px);
        }

        float hash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float3 ApplyIdleMotion(float3 positionOS, float3 positionWS, float heightMask)
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
            float heightFactor = heightMask * heightMask;
            float3 offset = float3(idleOffset.x, 0.0, idleOffset.y) * heightFactor;
            offset.y = -length(offset.xz) * 0.3 * heightMask;
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

        void ApplyDistanceDitherFade(float4 positionCS, float3 positionWS)
        {
            float dist = distance(_WorldSpaceCameraPos, positionWS);
            float fade = saturate((dist - _DitherFadeStart) / max(_DitherFadeEnd - _DitherFadeStart, 0.001));
            clip(1.0 - fade - GetDitherThreshold(positionCS.xy));
        }

        half3 ApplyWorldSpaceNoiseColor(float3 positionWS, half3 albedo)
        {
            half noiseValue = snoise3D(positionWS * _WorldNoiseScale) * 0.5h + 0.5h;
            half3 mainColor = _MainColor.rgb * albedo;
            half3 secondColor = _SecondColor.rgb * albedo;
            return lerp(mainColor, secondColor, noiseValue * _SecondColorBlend);
        }

        half3 ComputeStylizedLighting(half3 albedo, half3 normalWS, Light mainLight)
        {
            half nDotL = dot(normalWS, mainLight.direction);
            half halfLambert = nDotL * 0.5h + 0.5h;
            half shadowTerm = smoothstep(0.0h, 0.5h, halfLambert) * mainLight.shadowAttenuation;
            half3 shadowedColor = lerp(albedo, albedo * _ShadowColor.rgb, _ShadowStrength);
            half3 litColor = albedo * mainLight.color;
            return lerp(shadowedColor, litColor, shadowTerm) + SampleSH(normalWS) * albedo * 0.3h;
        }

        Varyings ShrubVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float heightMask = input.color.r > 0.01 ? input.color.r : input.texcoord.y;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 animatedPositionOS = ApplyIdleMotion(input.positionOS.xyz, positionWS, heightMask);

            output.positionWS = TransformObjectToWorld(animatedPositionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.texcoord;
            output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
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
            #pragma vertex ShrubVert
            #pragma fragment ForwardFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing
            #pragma multi_compile_fog

            half4 ForwardFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                clip(albedoSample.a - _AlphaCutoff);
                ApplyDistanceDitherFade(input.positionCS, input.positionWS);

                half3 normalWS = normalize(input.normalWS);
                half3 albedo = ApplyWorldSpaceNoiseColor(input.positionWS, albedoSample.rgb);
                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half3 finalColor = ComputeStylizedLighting(albedo, normalWS, mainLight);

                int additionalLightCount = GetAdditionalLightsCount();
                for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, normalWS);
                    half addNdotL = saturate(dot(normalWS, light.direction) * 0.5h + 0.5h);
                    finalColor += albedo * addNdotL * light.color * light.distanceAttenuation * light.shadowAttenuation * 0.5h;
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
            #pragma vertex ShrubVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                clip(albedoSample.a - _AlphaCutoff);
                ApplyDistanceDitherFade(input.positionCS, input.positionWS);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
