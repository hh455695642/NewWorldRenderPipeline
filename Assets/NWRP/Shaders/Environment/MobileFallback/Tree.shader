Shader "NewWorld/Env/MobileFallback/Tree"
{
    Properties
    {
        [Header(Maps)]
        [NoScaleOffset] [MainTexture]_BaseMap("Base", 2D) = "white" {}
        [MainColor]_BaseColor("Main Color", Color) = (1, 1, 1, 1)
        [ToggleUI]_ReceiveShadows("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI]_CastShadows("Cast Realtime Shadows", Float) = 1.0
        [HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #pragma target 3.0
        #include "../../../ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _ReceiveShadows;
            half _CastShadows;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../../ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../../ShaderLibrary/GlobalIllumination.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 color = albedo * mainLight.color * nDotL * mainLight.shadowAttenuation;
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
            #pragma vertex ShadowCasterVert
            #pragma fragment ShadowCasterFrag
            #pragma multi_compile_instancing
            #define NWRP_MATERIAL_CAST_SHADOWS _CastShadows
            #include "../../../ShaderLibrary/Passes/ShadowCasterPass.hlsl"
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
            #include "../../../ShaderLibrary/Passes/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
