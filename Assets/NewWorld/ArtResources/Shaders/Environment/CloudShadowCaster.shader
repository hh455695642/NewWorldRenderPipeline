Shader "NewWorld/Env/CloudShadowCaster"
{
    Properties
    {
        [Header(Cloud Noise)]
        _CloudTex ("Cloud Noise Texture", 2D) = "white" {}
        _CloudTiling ("Cloud Tiling", Vector) = (1, 1, 0, 0)
        _CloudSpeed ("Cloud Scroll Speed (XY)", Vector) = (0.02, 0.01, 0, 0)
        _CloudContrast ("Cloud Contrast", Range(0.1, 10)) = 2.0

        [Header(Distortion)]
        _DistortTex ("Distortion Texture", 2D) = "gray" {}
        _DistortTiling ("Distort Tiling", Vector) = (1, 1, 0, 0)
        _DistortSpeed ("Distort Scroll Speed (XY)", Vector) = (0.03, -0.015, 0, 0)
        _DistortStrength ("Distort Strength", Range(0, 0.5)) = 0.1

        [Header(Shadow Control)]
        _AlphaCutoff ("Alpha Cutoff (Shadow Threshold)", Range(0, 1)) = 0.4
        _ShadowSoftness ("Shadow Softness", Range(0.01, 0.5)) = 0.1
        _CloudDensity ("Cloud Density", Range(0, 2)) = 1.0

        [Header(Debug Visual)]
        [Toggle] _DebugVisible ("Show Cloud Plane (Debug)", Float) = 0
        _DebugOpacity ("Debug Opacity", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "NewWorldRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "../../../../NWRP/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_CloudTex);
        SAMPLER(sampler_CloudTex);
        TEXTURE2D(_DistortTex);
        SAMPLER(sampler_DistortTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _CloudTex_ST;
            float4 _DistortTex_ST;
            float4 _CloudTiling;
            float4 _CloudSpeed;
            float4 _DistortTiling;
            float4 _DistortSpeed;
            half _DistortStrength;
            half _AlphaCutoff;
            half _ShadowSoftness;
            half _CloudDensity;
            half _CloudContrast;
            half _DebugVisible;
            half _DebugOpacity;
        CBUFFER_END

        float4 _ShadowBias;
        float4 _ShadowLightDirection;
        float4 _ShadowLightPosition;

        struct CloudAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct CloudVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        half ComputeCloudAlpha(float2 uv)
        {
            float2 distortUV = uv * _DistortTiling.xy + _Time.y * _DistortSpeed.xy;
            half2 distortion = SAMPLE_TEXTURE2D(_DistortTex, sampler_DistortTex, distortUV).rg;
            distortion = (distortion - 0.5h) * 2.0h;

            float2 cloudUV = uv * _CloudTiling.xy + _Time.y * _CloudSpeed.xy + distortion * _DistortStrength;
            half cloud = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cloudUV).r;
            cloud = saturate(pow(abs(cloud * _CloudDensity), max(_CloudContrast, 0.0001h)));
            return cloud;
        }

        float3 ApplyCloudShadowBias(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
        {
            float invNdotL = 1.0 - saturate(dot(lightDirectionWS, normalWS));
            positionWS += lightDirectionWS * _ShadowBias.x;
            positionWS += normalWS * (invNdotL * _ShadowBias.y);
            return positionWS;
        }

        CloudVaryings CloudShadowVert(CloudAttributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            CloudVaryings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float3 lightDirectionWS = _ShadowLightPosition.w > 0.5
                ? normalize(_ShadowLightPosition.xyz - positionWS)
                : normalize(_ShadowLightDirection.xyz);
            float3 biasedPositionWS = ApplyCloudShadowBias(positionWS, normalWS, lightDirectionWS);

            output.positionCS = TransformWorldToHClip(biasedPositionWS);
        #if UNITY_REVERSED_Z
            output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
            output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif
            output.uv = input.texcoord;
            return output;
        }

        ENDHLSL

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex CloudShadowVert
            #pragma fragment CloudShadowFrag
            #pragma multi_compile_instancing

            half4 CloudShadowFrag(CloudVaryings input) : SV_Target
            {
                half cloud = ComputeCloudAlpha(input.uv);
                half softness = max(_ShadowSoftness, 0.0001h);
                half coverage = smoothstep(_AlphaCutoff - softness, _AlphaCutoff + softness, cloud);
                clip(coverage - 0.5h);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
