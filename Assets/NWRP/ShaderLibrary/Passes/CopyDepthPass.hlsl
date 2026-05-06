#ifndef NEWWORLD_SHADERLIB_PASS_COPY_DEPTH_INCLUDED
#define NEWWORLD_SHADERLIB_PASS_COPY_DEPTH_INCLUDED

#include "../Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

float4 _BlitScaleBias;

#if SHADER_API_GLES
struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};
#else
struct Attributes
{
    uint vertexID : SV_VertexID;
};
#endif

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

float2 GetCopyDepthFullScreenTexCoord(uint vertexID)
{
#if UNITY_UV_STARTS_AT_TOP
    return float2((vertexID << 1) & 2, 1.0 - (vertexID & 2));
#else
    return float2((vertexID << 1) & 2, vertexID & 2);
#endif
}

float4 GetCopyDepthFullScreenPosition(uint vertexID)
{
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    return float4(uv * 2.0 - 1.0, 0.0, 1.0);
}

Varyings Vert(Attributes input)
{
    Varyings output;

#if SHADER_API_GLES
    output.positionCS = input.positionOS;
    output.texcoord = input.uv;
#else
    output.positionCS = GetCopyDepthFullScreenPosition(input.vertexID);
    output.texcoord = GetCopyDepthFullScreenTexCoord(input.vertexID);
#endif

    output.texcoord = output.texcoord * _BlitScaleBias.xy + _BlitScaleBias.zw;
    return output;
}

#if defined(_DEPTH_MSAA_2)
    #define NWRP_DEPTH_MSAA_SAMPLES 2
#elif defined(_DEPTH_MSAA_4)
    #define NWRP_DEPTH_MSAA_SAMPLES 4
#elif defined(_DEPTH_MSAA_8)
    #define NWRP_DEPTH_MSAA_SAMPLES 8
#else
    #define NWRP_DEPTH_MSAA_SAMPLES 1
#endif

#if NWRP_DEPTH_MSAA_SAMPLES == 1
    TEXTURE2D(_CameraDepthAttachment);
#else
    Texture2DMS<float, NWRP_DEPTH_MSAA_SAMPLES> _CameraDepthAttachment;
    float4 _CameraDepthAttachment_TexelSize;
#endif

#if UNITY_REVERSED_Z
    #define NWRP_DEPTH_DEFAULT_VALUE 1.0
    #define NWRP_DEPTH_RESOLVE_OP min
#else
    #define NWRP_DEPTH_DEFAULT_VALUE 0.0
    #define NWRP_DEPTH_RESOLVE_OP max
#endif

float SampleCopyDepth(float2 uv)
{
#if NWRP_DEPTH_MSAA_SAMPLES == 1
    return SAMPLE_TEXTURE2D(_CameraDepthAttachment, sampler_PointClamp, uv).r;
#else
    int2 coord = int2(uv * _CameraDepthAttachment_TexelSize.zw);
    float outDepth = NWRP_DEPTH_DEFAULT_VALUE;

    UNITY_UNROLL
    for (int sampleIndex = 0; sampleIndex < NWRP_DEPTH_MSAA_SAMPLES; ++sampleIndex)
    {
        outDepth = NWRP_DEPTH_RESOLVE_OP(
            _CameraDepthAttachment.Load(coord, sampleIndex),
            outDepth);
    }

    return outDepth;
#endif
}

#if defined(_OUTPUT_DEPTH)
float FragCopyDepth(Varyings input) : SV_Depth
{
    return SampleCopyDepth(input.texcoord);
}
#else
float4 FragCopyDepth(Varyings input) : SV_Target
{
    float depth = SampleCopyDepth(input.texcoord);
    return float4(depth, 0.0, 0.0, 1.0);
}
#endif

#endif // NEWWORLD_SHADERLIB_PASS_COPY_DEPTH_INCLUDED
