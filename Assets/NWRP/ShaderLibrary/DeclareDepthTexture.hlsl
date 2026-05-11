#ifndef NEWWORLD_DECLARE_DEPTH_TEXTURE_INCLUDED
#define NEWWORLD_DECLARE_DEPTH_TEXTURE_INCLUDED

#include "Core.hlsl"

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

float4 _CameraDepthTextureScaleBias;

float2 TransformSceneDepthUV(float2 screenUV)
{
    return screenUV * _CameraDepthTextureScaleBias.xy + _CameraDepthTextureScaleBias.zw;
}

float SampleSceneDepthRawTextureUV(float2 textureUV)
{
    return SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, textureUV).r;
}

float SampleSceneDepth(float2 screenUV)
{
    return SampleSceneDepthRawTextureUV(TransformSceneDepthUV(screenUV));
}

float LoadSceneDepthRawTextureCoord(uint2 pixelCoord)
{
    return LOAD_TEXTURE2D(_CameraDepthTexture, pixelCoord).r;
}

float LoadSceneDepth(uint2 pixelCoord)
{
    return LoadSceneDepthRawTextureCoord(pixelCoord);
}

float SampleSceneDepthLinear01(float2 uv)
{
    return Linear01Depth(SampleSceneDepth(uv));
}

float SampleSceneDepthLinearEye(float2 uv)
{
    return LinearEyeDepth(SampleSceneDepth(uv));
}

#endif // NEWWORLD_DECLARE_DEPTH_TEXTURE_INCLUDED
