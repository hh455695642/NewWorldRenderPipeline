#ifndef NEWWORLD_PARTICLES_INSTANCING_INCLUDED
#define NEWWORLD_PARTICLES_INSTANCING_INCLUDED

#include "Core.hlsl"

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(SHADER_TARGET_SURFACE_ANALYSIS)
    #define UNITY_PARTICLE_INSTANCING_ENABLED
#endif

#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)

#ifndef UNITY_PARTICLE_INSTANCE_DATA
    #define UNITY_PARTICLE_INSTANCE_DATA NWRPDefaultParticleInstanceData
#endif

struct NWRPDefaultParticleInstanceData
{
    float3x4 transform;
    uint color;
    float animFrame;
};

StructuredBuffer<UNITY_PARTICLE_INSTANCE_DATA> unity_ParticleInstanceData;
float4 unity_ParticleUVShiftData;
half unity_ParticleUseMeshColors;

half4 NWRPUnpackParticleColor(uint rgba)
{
    return half4(
        rgba & 255,
        (rgba >> 8) & 255,
        (rgba >> 16) & 255,
        (rgba >> 24) & 255) * (1.0h / 255.0h);
}

void ParticleInstancingMatrices(out float4x4 objectToWorld, out float4x4 worldToObject)
{
    UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];

    objectToWorld._11_21_31_41 = float4(data.transform._11_21_31, 0.0);
    objectToWorld._12_22_32_42 = float4(data.transform._12_22_32, 0.0);
    objectToWorld._13_23_33_43 = float4(data.transform._13_23_33, 0.0);
    objectToWorld._14_24_34_44 = float4(data.transform._14_24_34, 1.0);

    float3x3 worldToObject3x3;
    worldToObject3x3[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
    worldToObject3x3[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
    worldToObject3x3[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;

    float det = dot(objectToWorld[0].xyz, worldToObject3x3[0]);
    worldToObject3x3 = transpose(worldToObject3x3) * rcp(det);

    float3 worldToObjectPosition = mul(worldToObject3x3, -objectToWorld._14_24_34);
    worldToObject._11_21_31_41 = float4(worldToObject3x3._11_21_31, 0.0);
    worldToObject._12_22_32_42 = float4(worldToObject3x3._12_22_32, 0.0);
    worldToObject._13_23_33_43 = float4(worldToObject3x3._13_23_33, 0.0);
    worldToObject._14_24_34_44 = float4(worldToObjectPosition, 1.0);
}

void ParticleInstancingSetup()
{
    ParticleInstancingMatrices(UNITY_MATRIX_M, unity_WorldToObject);
}

half4 GetNWRPParticleVertexColor(half4 color)
{
#if !defined(UNITY_PARTICLE_INSTANCE_DATA_NO_COLOR)
    UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
    color = lerp(half4(1.0h, 1.0h, 1.0h, 1.0h), color, unity_ParticleUseMeshColors);
    color *= NWRPUnpackParticleColor(data.color);
#endif
    return color;
}

void GetNWRPParticleUVs(out float2 uv, out float3 blendUV, float4 inputTexcoord, float inputBlend)
{
    uv = inputTexcoord.xy;
    blendUV = float3(inputTexcoord.xy, 0.0);

    if (unity_ParticleUVShiftData.x != 0.0)
    {
        UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
        float numTilesX = unity_ParticleUVShiftData.y;
        float2 animScale = unity_ParticleUVShiftData.zw;

#if defined(UNITY_PARTICLE_INSTANCE_DATA_NO_ANIM_FRAME)
        float sheetIndex = 0.0;
#else
        float sheetIndex = data.animFrame;
#endif

        float index0 = floor(sheetIndex);
        float vIdx0 = floor(index0 / numTilesX);
        float uIdx0 = floor(index0 - vIdx0 * numTilesX);
        float2 offset0 = float2(uIdx0 * animScale.x, (1.0 - animScale.y) - vIdx0 * animScale.y);
        uv = inputTexcoord.xy * animScale + offset0;

#if defined(_FLIPBOOKBLENDING_ON)
        float index1 = floor(sheetIndex + 1.0);
        float vIdx1 = floor(index1 / numTilesX);
        float uIdx1 = floor(index1 - vIdx1 * numTilesX);
        float2 offset1 = float2(uIdx1 * animScale.x, (1.0 - animScale.y) - vIdx1 * animScale.y);
        blendUV.xy = inputTexcoord.xy * animScale + offset1;
        blendUV.z = frac(sheetIndex);
#else
        blendUV.xy = uv;
#endif
        return;
    }

#if defined(_FLIPBOOKBLENDING_ON)
    blendUV.xy = inputTexcoord.zw;
    blendUV.z = inputBlend;
#else
    blendUV.xy = uv;
#endif
}

#else

void ParticleInstancingSetup() {}

half4 GetNWRPParticleVertexColor(half4 color)
{
    return color;
}

void GetNWRPParticleUVs(out float2 uv, out float3 blendUV, float4 inputTexcoord, float inputBlend)
{
    uv = inputTexcoord.xy;

#if defined(_FLIPBOOKBLENDING_ON)
    blendUV = float3(inputTexcoord.zw, inputBlend);
#else
    blendUV = float3(inputTexcoord.xy, 0.0);
#endif
}

#endif

#endif // NEWWORLD_PARTICLES_INSTANCING_INCLUDED
