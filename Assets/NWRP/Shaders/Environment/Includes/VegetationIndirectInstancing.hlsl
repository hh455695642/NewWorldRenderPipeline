#ifndef VEGETATION_INDIRECT_INSTANCING_INCLUDED
#define VEGETATION_INDIRECT_INSTANCING_INCLUDED

// Shared procedural-instancing helper for vegetation indirect rendering.
//
// Shader pass usage:
// - Add:
//     pragma multi_compile_instancing
//     pragma instancing_options procedural:SetupInstancing
// - Include this file after Core.hlsl:
//     #include "./Includes/VegetationIndirectInstancing.hlsl"
// - Add instance id support to your structs:
//     UNITY_VERTEX_INPUT_INSTANCE_ID
// - In vertex/fragment functions:
//     UNITY_SETUP_INSTANCE_ID(input);
//     UNITY_TRANSFER_INSTANCE_ID(input, output);   // vertex only
//
// Runtime usage:
// - C# binds a StructuredBuffer<VegetationVisibleInstance> named _VisibleVegetationBuffer.
// - Each element stores local-to-world and world-to-object matrices for stable normals.

float _NWRPVegetationUseCustomSH;
float4 _NWRPVegetationSHAr;
float4 _NWRPVegetationSHAg;
float4 _NWRPVegetationSHAb;
float4 _NWRPVegetationSHBr;
float4 _NWRPVegetationSHBg;
float4 _NWRPVegetationSHBb;
float4 _NWRPVegetationSHC;

inline half3 SampleVegetationPackedSH(half3 normalWS)
{
    half4 n = half4(normalWS, 1.0h);
    half3 res;
    res.r = dot((half4)_NWRPVegetationSHAr, n);
    res.g = dot((half4)_NWRPVegetationSHAg, n);
    res.b = dot((half4)_NWRPVegetationSHAb, n);

    half4 vB = n.xyzz * n.yzzx;
    res.r += dot((half4)_NWRPVegetationSHBr, vB);
    res.g += dot((half4)_NWRPVegetationSHBg, vB);
    res.b += dot((half4)_NWRPVegetationSHBb, vB);

    half vC = n.x * n.x - n.y * n.y;
    res += (half3)_NWRPVegetationSHC.rgb * vC;
    return max(0.0h, res);
}

inline half3 SampleVegetationIndirectSH(half3 normalWS)
{
    half useCustomSH = (half)saturate(_NWRPVegetationUseCustomSH);
    return lerp(SampleSH(normalWS), SampleVegetationPackedSH(normalWS), useCustomSH);
}

struct VegetationVisibleInstance
{
    float4x4 localToWorld;
    float4x4 worldToObject;
};

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
StructuredBuffer<VegetationVisibleInstance> _VisibleVegetationBuffer;
#endif

inline void SetupInstancing()
{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    VegetationVisibleInstance instanceData = _VisibleVegetationBuffer[unity_InstanceID];
    unity_ObjectToWorld = instanceData.localToWorld;
    unity_WorldToObject = instanceData.worldToObject;

#endif
}

#endif
