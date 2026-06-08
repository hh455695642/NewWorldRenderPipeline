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
    // Y flip is selected by C# via _BlitScaleBias to match source/destination RT orientation.
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

TEXTURE2D(_CameraDepthAttachment);

float SampleCopyDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraDepthAttachment, sampler_PointClamp, uv).r;
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
