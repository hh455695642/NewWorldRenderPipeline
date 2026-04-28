Shader "NewWorld/Env/Tree"
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
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #pragma target 4.5
        #include "../../ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _ReceiveShadows;
            half _CastShadows;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../ShaderLibrary/GlobalIllumination.hlsl"
        #include "./Includes/VegetationIndirectInstancing.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        struct TreeAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct TreeVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TreeVaryings TreeVert(TreeAttributes input)
        {
            TreeVaryings output = (TreeVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
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

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex TreeVert
            #pragma fragment ForwardFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing
            #pragma multi_compile_fog

            half4 ForwardFrag(TreeVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 finalColor = albedo * mainLight.color * nDotL * mainLight.shadowAttenuation;
                finalColor += SampleSH(normalWS) * albedo;

                int additionalLightCount = GetAdditionalLightsCount();
                for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, normalWS);
                    half addNdotL = saturate(dot(normalWS, light.direction));
                    finalColor += albedo * light.color * addNdotL * light.distanceAttenuation * light.shadowAttenuation;
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
            #pragma vertex ShadowCasterVert
            #pragma fragment ShadowCasterFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing

            #define NWRP_MATERIAL_CAST_SHADOWS _CastShadows
            #include "../../ShaderLibrary/Passes/ShadowCasterPass.hlsl"
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
            #pragma instancing_options procedural:SetupInstancing

            #include "../../ShaderLibrary/Passes/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
