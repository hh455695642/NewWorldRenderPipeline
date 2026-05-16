Shader "NewWorld/Env/MobileFallback/WorldGrass"
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
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #pragma target 3.0
        #include "../../../ShaderLibrary/Core.hlsl"

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
        #include "../../../ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../../ShaderLibrary/GlobalIllumination.hlsl"

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
            output.color = input.color;
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

                half heightMask = input.color.r > 0.01h ? input.color.r : (half)input.uv.y;
                half3 albedo = lerp(_NoiseColor1.rgb, _NoiseColor2.rgb, heightMask);
                albedo = lerp(albedo, _TipColor.rgb, saturate((heightMask - _HeightGradientThreshold) * 3.0h));
                half3 normalWS = half3(0.0h, 1.0h, 0.0h);
                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half halfLambert = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half3 lit = albedo * mainLight.color * halfLambert * mainLight.shadowAttenuation;
                half3 shade = lerp(lit, _ShadowColor.rgb * albedo, _ShadowStrength * (1.0h - mainLight.shadowAttenuation));
                shade += SampleSH(normalWS) * albedo;
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
                return 0.0h;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
