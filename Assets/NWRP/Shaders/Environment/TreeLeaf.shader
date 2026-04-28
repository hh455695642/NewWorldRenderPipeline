Shader "NewWorld/Env/TreeLeaf"
{
    Properties
    {
        [Header(Maps)]
        [NoScaleOffset] _Albedo("Base", 2D) = "white" {}
        _AlphaCutoff("Opacity Cutoff", Range(0, 1)) = 0.35

        [Header(Settings)]
        _MainColor("Main Color", Color) = (1, 1, 1, 1)
        [ToggleUI]_ReceiveShadows("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI]_CastShadows("Cast Realtime Shadows", Float) = 1.0

        [Header(Second Color)]
        _SecondColor("Second Color", Color) = (0, 0, 0, 0)
        [KeywordEnum(World_Noise_3D, UV_Gradient)] _SecondColorOverlayType("Overlay Method", Float) = 0
        _SecondColorOffset("Offset", Float) = 1
        _SecondColorFade("Balance", Float) = 1
        _WorldNoiseScale("World Noise Scale", Float) = 1

        [Header(Distance Fade)]
        _FadeDistance("Distance", Float) = 30
        _FadeFalloff("Falloff", Range(0, 1)) = 0.7

        [Header(FakeSSS)]
        _TranslucencyInt("Translucency", Range(0, 10)) = 1
        _TranslucencyColor("Translucency Color", Color) = (1, 1, 1, 0)

        [HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1
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
            half4 _TranslucencyColor;
            half4 _SecondColor;
            half4 _MainColor;
            half _TranslucencyInt;
            half _SecondColorFade;
            half _SecondColorOffset;
            half _ReceiveShadows;
            half _CastShadows;
            float _WorldNoiseScale;
            float _FadeFalloff;
            float _FadeDistance;
            float _AlphaCutoff;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../ShaderLibrary/GlobalIllumination.hlsl"
        #include "./Includes/VegetationIndirectInstancing.hlsl"

        TEXTURE2D(_Albedo);
        SAMPLER(sampler_Albedo);

        float3 mod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
        float4 mod289(float4 x) { return x - floor(x / 289.0) * 289.0; }
        float4 permute(float4 x) { return mod289((x * 34.0 + 1.0) * x); }
        float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - r * 0.85373472095314; }

        float snoise(float3 v)
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

        half CalculateSecondColorMask(float3 worldPos, float2 uv)
        {
            half noiseValue;
            #if defined(_SECONDCOLOROVERLAYTYPE_UV_GRADIENT)
                noiseValue = uv.y;
            #else
                noiseValue = snoise(worldPos * _WorldNoiseScale) * 0.5h + 0.5h;
            #endif

            half offset = noiseValue + (1.0h - _SecondColorOffset);
            half invOffset = 1.0h - offset;
            half fadeAdjust = _SecondColorFade + 0.5h;
            return saturate(lerp(offset, invOffset, fadeAdjust));
        }

        half CalculateDistanceFade(float3 positionWS)
        {
            float fadeRange = _FadeDistance * (1.0 - _FadeFalloff + 0.001);
            float fade = saturate((distance(_WorldSpaceCameraPos, positionWS) - _FadeDistance) / max(fadeRange, 0.001));
            return 1.0h - fade;
        }

        half3 CalculateTranslucency(half3 viewDir, half3 lightDir, half shadowAtten, half3 lightColor)
        {
            half vDotL = saturate(dot(-viewDir, lightDir));
            return vDotL * shadowAtten * lightColor * _TranslucencyColor.rgb * _TranslucencyInt;
        }

        struct LeafAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct LeafVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half3 viewDirWS : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        LeafVaryings LeafVert(LeafAttributes input)
        {
            LeafVaryings output = (LeafVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.texcoord;
            output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
            output.viewDirWS = normalize(GetWorldSpaceViewDir(output.positionWS));
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
            #pragma vertex LeafVert
            #pragma fragment ForwardFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _SECONDCOLOROVERLAYTYPE_WORLD_NOISE_3D _SECONDCOLOROVERLAYTYPE_UV_GRADIENT

            half4 ForwardFrag(LeafVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                half distanceFade = CalculateDistanceFade(input.positionWS);
                clip(albedoSample.a * distanceFade - _AlphaCutoff);

                half3 baseColor = _MainColor.rgb * albedoSample.rgb;
                half secondColorMask = CalculateSecondColorMask(input.positionWS, input.uv);
                half3 secondColor = _SecondColor.rgb * albedoSample.rgb;
                baseColor = lerp(baseColor, secondColor, secondColorMask);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half nDotL = dot(normalWS, mainLight.direction);
                half halfLambert = nDotL * 0.5h + 0.5h;
                half shadowAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 finalColor = baseColor * mainLight.color * halfLambert * shadowAtten;
                finalColor += CalculateTranslucency(viewDirWS, mainLight.direction, shadowAtten, mainLight.color) * baseColor;
                finalColor += SampleSH(normalWS) * baseColor;

                int additionalLightCount = GetAdditionalLightsCount();
                for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, normalWS);
                    half addNdotL = dot(normalWS, light.direction);
                    half addHalfLambert = addNdotL * 0.5h + 0.5h;
                    finalColor += baseColor * light.color * addHalfLambert * light.distanceAttenuation * light.shadowAttenuation;
                }

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0h);
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
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing

            #include "../../ShaderLibrary/Passes/ShadowCasterPass.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lightDirectionWS = _ShadowLightPosition.w > 0.5
                    ? normalize(_ShadowLightPosition.xyz - positionWS)
                    : normalize(_ShadowLightDirection.xyz);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = input.texcoord;
                output.positionWS = positionWS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_CastShadows - 0.5h);
                half4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                half distanceFade = CalculateDistanceFade(input.positionWS);
                clip(albedoSample.a * distanceFade - _AlphaCutoff);
                return 0.0h;
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
            #pragma vertex LeafVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing

            half4 DepthFrag(LeafVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                half distanceFade = CalculateDistanceFade(input.positionWS);
                clip(albedoSample.a * distanceFade - _AlphaCutoff);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
