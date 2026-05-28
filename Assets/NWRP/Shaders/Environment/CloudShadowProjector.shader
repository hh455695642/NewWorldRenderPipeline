Shader "Hidden/NWRP/Environment/CloudShadowProjector"
{
    SubShader
    {
        Tags { "RenderPipeline"="NewWorldRenderPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudShadowProjector"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma editor_sync_compilation
            #pragma vertex Vert
            #pragma fragment Frag

            #include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../../ShaderLibrary/DepthWorldReconstructionBlit.hlsl"

            TEXTURE2D(_NWRPCloudShadowTexture0);
            TEXTURE2D(_NWRPCloudShadowTexture1);
            TEXTURE2D(_NWRPCloudShadowDistortionTexture);

            float4x4 _NWRPCloudShadowWorldToProjector0;
            float4x4 _NWRPCloudShadowWorldToProjector1;
            float4 _NWRPCloudShadowUV0;
            float4 _NWRPCloudShadowUV1;
            float4 _NWRPCloudShadowParams0;
            float4 _NWRPCloudShadowParams1;
            float4 _NWRPCloudShadowColor0;
            float4 _NWRPCloudShadowColor1;
            float4 _NWRPCloudShadowDistortionUV;
            float4 _NWRPCloudShadowDistortionParams;

            #define NWRP_CLOUD_UV_TILING(uvParams) (uvParams).xy
            #define NWRP_CLOUD_UV_OFFSET(uvParams) (uvParams).zw
            #define NWRP_CLOUD_SCROLL(layerParams) (layerParams).xy
            #define NWRP_CLOUD_INTENSITY(layerParams) (layerParams).z
            #define NWRP_CLOUD_EDGE_SOFTNESS(layerParams) (layerParams).w
            #define NWRP_CLOUD_DISTORTION_TILING(distortionUV) (distortionUV).xy
            #define NWRP_CLOUD_DISTORTION_OFFSET(distortionUV) (distortionUV).zw
            #define NWRP_CLOUD_DISTORTION_SCROLL(distortionParams) (distortionParams).xy
            #define NWRP_CLOUD_DISTORTION_STRENGTH(distortionParams) (distortionParams).z

            half4 SampleCloudShadowSource(float2 uv)
            {
                return (half4)SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv,
                    _BlitMipLevel);
            }

            half ComputeProjectorBoxMask(float3 projectorPosition, float edgeSoftness)
            {
                float3 edgeDistance = 0.5 - abs(projectorPosition);
                float minEdgeDistance = min(
                    edgeDistance.x,
                    min(edgeDistance.y, edgeDistance.z));
                float derivativeWidth = max(fwidth(minEdgeDistance), 1.0e-5);
                float softWidth = max(edgeSoftness, derivativeWidth);
                return (half)smoothstep(-derivativeWidth, softWidth, minEdgeDistance);
            }

            float2 ComputeCloudShadowDistortion(float3 positionWS)
            {
                float strength =
                    NWRP_CLOUD_DISTORTION_STRENGTH(_NWRPCloudShadowDistortionParams);
                UNITY_BRANCH
                if (strength <= 0.0)
                {
                    return float2(0.0, 0.0);
                }

                float2 distortionUV =
                    positionWS.xz * NWRP_CLOUD_DISTORTION_TILING(_NWRPCloudShadowDistortionUV)
                    + NWRP_CLOUD_DISTORTION_OFFSET(_NWRPCloudShadowDistortionUV)
                    + _Time.y * NWRP_CLOUD_DISTORTION_SCROLL(_NWRPCloudShadowDistortionParams);
                half2 distortion =
                    (half2)(SAMPLE_TEXTURE2D(
                        _NWRPCloudShadowDistortionTexture,
                        sampler_LinearRepeat,
                        distortionUV).rg * 2.0 - 1.0);
                return (float2)distortion * strength;
            }

            half ComputeLayerAlpha0(float3 positionWS, float2 uvDistortion)
            {
                float3 projectorPosition =
                    mul(_NWRPCloudShadowWorldToProjector0, float4(positionWS, 1.0)).xyz;
                half boxMask = ComputeProjectorBoxMask(
                    projectorPosition,
                    NWRP_CLOUD_EDGE_SOFTNESS(_NWRPCloudShadowParams0));
                float2 cloudUV = projectorPosition.xz + 0.5;
                cloudUV = cloudUV * NWRP_CLOUD_UV_TILING(_NWRPCloudShadowUV0)
                    + NWRP_CLOUD_UV_OFFSET(_NWRPCloudShadowUV0)
                    + _Time.y * NWRP_CLOUD_SCROLL(_NWRPCloudShadowParams0);
                cloudUV += uvDistortion;
                half textureAlpha =
                    (half)SAMPLE_TEXTURE2D(_NWRPCloudShadowTexture0, sampler_LinearRepeat, cloudUV).a;
                return saturate(
                    textureAlpha
                    * boxMask
                    * (half)NWRP_CLOUD_INTENSITY(_NWRPCloudShadowParams0));
            }

            half ComputeLayerAlpha1(float3 positionWS, float2 uvDistortion)
            {
                float3 projectorPosition =
                    mul(_NWRPCloudShadowWorldToProjector1, float4(positionWS, 1.0)).xyz;
                half boxMask = ComputeProjectorBoxMask(
                    projectorPosition,
                    NWRP_CLOUD_EDGE_SOFTNESS(_NWRPCloudShadowParams1));
                float2 cloudUV = projectorPosition.xz + 0.5;
                cloudUV = cloudUV * NWRP_CLOUD_UV_TILING(_NWRPCloudShadowUV1)
                    + NWRP_CLOUD_UV_OFFSET(_NWRPCloudShadowUV1)
                    + _Time.y * NWRP_CLOUD_SCROLL(_NWRPCloudShadowParams1);
                cloudUV += uvDistortion;
                half textureAlpha =
                    (half)SAMPLE_TEXTURE2D(_NWRPCloudShadowTexture1, sampler_LinearRepeat, cloudUV).a;
                return saturate(
                    textureAlpha
                    * boxMask
                    * (half)NWRP_CLOUD_INTENSITY(_NWRPCloudShadowParams1));
            }

            void ApplyCloudShadowLayer(inout half4 sceneColor, half alpha, half3 shadowColor)
            {
                sceneColor.rgb *= lerp(half3(1.0h, 1.0h, 1.0h), shadowColor, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 sceneColor = SampleCloudShadowSource(uv);

                float rawDepth = SampleSceneDepth(uv);
                if (!IsSceneDepthValid(rawDepth))
                {
                    return sceneColor;
                }

                float3 positionWS = ComputeSceneWorldSpacePosition(uv, rawDepth);
                float2 uvDistortion = ComputeCloudShadowDistortion(positionWS);
                ApplyCloudShadowLayer(
                    sceneColor,
                    ComputeLayerAlpha0(positionWS, uvDistortion),
                    (half3)_NWRPCloudShadowColor0.rgb);
                ApplyCloudShadowLayer(
                    sceneColor,
                    ComputeLayerAlpha1(positionWS, uvDistortion),
                    (half3)_NWRPCloudShadowColor1.rgb);
                return sceneColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
