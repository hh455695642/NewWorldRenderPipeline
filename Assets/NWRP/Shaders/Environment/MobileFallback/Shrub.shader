Shader "NewWorld/Env/MobileFallback/Shrub"
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
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #pragma target 3.0
        #include "../../../ShaderLibrary/Core.hlsl"

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
            half fogFactor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

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
            output.fogFactor = (half)ComputeNWRPFogFactorFromPositionWS(output.positionWS);
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
                clip(albedoSample.a - _AlphaCutoff);
                half3 albedo = lerp(_MainColor.rgb, _SecondColor.rgb, _SecondColorBlend) * albedoSample.rgb;
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half halfLambert = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half3 lit = albedo * mainLight.color * halfLambert * mainLight.shadowAttenuation;
                half3 shade = lerp(lit, _ShadowColor.rgb * albedo, _ShadowStrength * (1.0h - mainLight.shadowAttenuation));
                shade += SampleSH(normalWS) * albedo;
                shade = MixNWRPFog(shade, input.fogFactor);
                return half4(shade, 1.0h);
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
                clip(albedoSample.a - _AlphaCutoff);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
