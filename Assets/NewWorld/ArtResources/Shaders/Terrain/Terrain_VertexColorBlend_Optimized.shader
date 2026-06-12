Shader "NewWorld/Terrain/Terrain_VertexColorBlend_Optimized"
{
    Properties
    {
        [NoScaleOffset]_AlbedoTexture1("AlbedoMap1", 2D) = "white" {}
        _Tilling1("Tilling1", Float) = 120
        [NoScaleOffset]_AlbedoTexture2("AlbedoMap2", 2D) = "white" {}
        _Tilling2("Tilling2", Float) = 120
        [NoScaleOffset]_AlbedoTexture3("AlbedoMap3", 2D) = "white" {}
        _Tilling3("Tilling3", Float) = 120
        [NoScaleOffset]_AlbedoTexture4("AlbedoMap4", 2D) = "white" {}
        _Tilling4("Tilling4", Float) = 120
        _HeightIntensity2("HeightIntensity2", Range(0, 10)) = 2
        _HeightIntensity3("HeightIntensity3", Range(0, 10)) = 1.5
        _HeightIntensity4("HeightIntensity4", Range(0, 10)) = 2

        [ToggleUI]_ReceiveShadows("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI]_CastShadows("Cast Realtime Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #pragma target 3.0

        #include "../../../../NWRP/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Tilling1;
            float _Tilling2;
            float _Tilling3;
            float _Tilling4;
            half _HeightIntensity2;
            half _HeightIntensity3;
            half _HeightIntensity4;
            half _ReceiveShadows;
            half _CastShadows;
        CBUFFER_END

        #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
        #include "../../../../NWRP/ShaderLibrary/Lighting.hlsl"
        #undef NWRP_MATERIAL_RECEIVE_SHADOWS
        #include "../../../../NWRP/ShaderLibrary/GlobalIllumination.hlsl"

        TEXTURE2D(_AlbedoTexture1);
        TEXTURE2D(_AlbedoTexture2);
        TEXTURE2D(_AlbedoTexture3);
        TEXTURE2D(_AlbedoTexture4);
        SAMPLER(sampler_LinearRepeat);

        struct TerrainAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct TerrainVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 color : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        half3 ComputeTerrainBaseColor(float3 positionWS, half4 vertexColor)
        {
            float2 worldUV = positionWS.xz;

            // Fixed four texture samples. Keep layer count explicit for mobile bandwidth control.
            half4 albedo1 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture1, sampler_LinearRepeat, worldUV * _Tilling1);
            half4 albedo2 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture2, sampler_LinearRepeat, worldUV * _Tilling2);
            half4 albedo3 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture3, sampler_LinearRepeat, worldUV * _Tilling3);
            half4 albedo4 = (half4)SAMPLE_TEXTURE2D(_AlbedoTexture4, sampler_LinearRepeat, worldUV * _Tilling4);

            half t2 = saturate(albedo2.a * vertexColor.g * _HeightIntensity2);
            half t3 = saturate(albedo3.a * vertexColor.b * _HeightIntensity3);
            half t4 = saturate(albedo4.a * vertexColor.a * _HeightIntensity4);

            half4 blend12 = lerp(albedo1, albedo2, t2);
            half4 blend123 = lerp(blend12, albedo3, t3);
            half4 blend1234 = lerp(blend123, albedo4, t4);
            return blend1234.rgb;
        }

        TerrainVaryings TerrainVert(TerrainAttributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            TerrainVaryings output = (TerrainVaryings)0;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
            output.color = (half4)input.color;
            output.fogFactor = (half)ComputeNWRPFogFactorFromPositionWS(output.positionWS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "NewWorldForward"
            Tags { "LightMode" = "NewWorldForward" }

            Cull Back
            Blend Off
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma vertex TerrainVert
            #pragma fragment TerrainFrag
            #pragma multi_compile_instancing

            half4 TerrainFrag(TerrainVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 albedo = ComputeTerrainBaseColor(input.positionWS, input.color);

                Light mainLight = GetMainLight(input.positionWS, normalWS);
                half3 debugColor;
                if (TryGetMainLightShadowDebugOverride(mainLight, debugColor))
                {
                    return half4(debugColor, 1.0h);
                }

                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 direct = albedo
                    * mainLight.color
                    * mainLight.distanceAttenuation
                    * mainLight.shadowAttenuation
                    * nDotL;
                half3 indirect = SampleSH(normalWS) * albedo;

                half3 finalColor = direct + indirect;
                finalColor = MixNWRPFog(finalColor, input.fogFactor);
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
    Fallback Off
}
