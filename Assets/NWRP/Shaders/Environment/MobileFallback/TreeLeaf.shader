Shader "NewWorld/Env/MobileFallback/TreeLeaf"
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
        _SecondColorOverlayType("Overlay Method", Float) = 0
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
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #pragma target 3.0
        #include "../../../ShaderLibrary/Core.hlsl"

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
        #include "../../../ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../../ShaderLibrary/GlobalIllumination.hlsl"

        TEXTURE2D(_Albedo);
        SAMPLER(sampler_Albedo);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half3 viewDirWS : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        half DistanceFade(float3 positionWS)
        {
            float fadeRange = _FadeDistance * (1.0 - _FadeFalloff + 0.001);
            float fade = saturate((distance(_WorldSpaceCameraPos, positionWS) - _FadeDistance) / max(fadeRange, 0.001));
            return 1.0h - fade;
        }

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.texcoord;
            output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
            output.viewDirWS = SafeNormalize(_WorldSpaceCameraPos - output.positionWS);
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                half fade = DistanceFade(input.positionWS);
                clip(albedoSample.a * fade - _AlphaCutoff);

                half secondColorMask = saturate(input.uv.y + (1.0h - _SecondColorOffset));
                secondColorMask = saturate(lerp(secondColorMask, 1.0h - secondColorMask, _SecondColorFade + 0.5h));
                half3 albedo = lerp(_MainColor.rgb, _SecondColor.rgb, secondColorMask) * albedoSample.rgb;
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half halfLambert = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;
                half3 color = albedo * mainLight.color * halfLambert * mainLight.shadowAttenuation;
                half backLight = saturate(dot(-input.viewDirWS, mainLight.direction));
                color += albedo * _TranslucencyColor.rgb * backLight * _TranslucencyInt * mainLight.shadowAttenuation;
                color += SampleSH(normalWS) * albedo;
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
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #include "../../../ShaderLibrary/Passes/ShadowCasterPass.hlsl"

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
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
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
                clip(albedoSample.a * DistanceFade(input.positionWS) - _AlphaCutoff);
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
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                clip(albedoSample.a * DistanceFade(input.positionWS) - _AlphaCutoff);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
