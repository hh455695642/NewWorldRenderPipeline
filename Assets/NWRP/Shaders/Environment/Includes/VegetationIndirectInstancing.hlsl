#ifndef VEGETATION_INDIRECT_INSTANCING_INCLUDED
#define VEGETATION_INDIRECT_INSTANCING_INCLUDED

// Shared procedural-instancing helper for vegetation indirect rendering.
//
// Shader pass usage:
// - Add:
//     #pragma multi_compile_instancing
//     #pragma instancing_options procedural:SetupInstancing
// - Include this file after Core.hlsl:
//     #include "./Includes/VegetationIndirectInstancing.hlsl"
// - Add instance id support to your structs:
//     UNITY_VERTEX_INPUT_INSTANCE_ID
// - In vertex/fragment functions:
//     UNITY_SETUP_INSTANCE_ID(input);
//     UNITY_TRANSFER_INSTANCE_ID(input, output);   // vertex only
//
// Runtime usage:
// - C# binds a StructuredBuffer<float4x4> named _VisibleVegetationBuffer via MaterialPropertyBlock.
// - Each element stores one instance local-to-world matrix.

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
StructuredBuffer<float4x4> _VisibleVegetationBuffer;
#endif

inline void SetupInstancing()
{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    float4x4 instanceMatrix = _VisibleVegetationBuffer[unity_InstanceID];
    unity_ObjectToWorld = instanceMatrix;
    // Matches the original vegetation path and avoids driver-sensitive matrix inversion on Unity's
    // D3D compiler. This assumes vegetation instances avoid problematic non-uniform scale.
    unity_WorldToObject = transpose(instanceMatrix);

#endif
}

#endif
