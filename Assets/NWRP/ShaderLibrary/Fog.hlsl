#ifndef NEWWORLD_FOG_INCLUDED
#define NEWWORLD_FOG_INCLUDED

// ============================================================
// NewWorld Render Pipeline - Fog.hlsl
//
// Uniform fog path. No shader keywords or variants.
// _NWRPFogMode: 0 Off, 1 Linear, 2 Exp, 3 Exp2.
// _NWRPFogParams: x=start, y=end, z=1/(end-start), w=density.
// ============================================================

float _NWRPFogMode;
float4 _NWRPFogParams;
half4 _NWRPFogColor;

float ComputeNWRPFogFactorFromEyeDepth(float eyeDepth)
{
    eyeDepth = max(eyeDepth, 0.0);

    if (_NWRPFogMode < 0.5)
    {
        return 1.0;
    }

    if (_NWRPFogMode < 1.5)
    {
        return saturate((_NWRPFogParams.y - eyeDepth) * _NWRPFogParams.z);
    }

    if (_NWRPFogMode < 2.5)
    {
        float f = _NWRPFogParams.w * eyeDepth;
        return saturate(exp2(-f));
    }

    if (_NWRPFogMode < 3.5)
    {
        float f = _NWRPFogParams.w * eyeDepth;
        return saturate(exp2(-f * f));
    }

    return 1.0;
}

float ComputeNWRPFogFactorFromPositionWS(float3 positionWS)
{
    float eyeDepth = -TransformWorldToView(positionWS).z;
    return ComputeNWRPFogFactorFromEyeDepth(eyeDepth);
}

half3 MixNWRPFog(half3 fragColor, float fogFactor)
{
    return lerp(_NWRPFogColor.rgb, fragColor, fogFactor);
}

half4 MixNWRPFogColor(half4 fragColor, float fogFactor)
{
    fragColor.rgb = MixNWRPFog(fragColor.rgb, fogFactor);
    return fragColor;
}

// Compatibility wrappers for shaders outside the current cleanup scope.
float ComputeFogFactor(float eyeDepth)
{
    return ComputeNWRPFogFactorFromEyeDepth(eyeDepth);
}

half3 MixFog(half3 fragColor, float fogFactor)
{
    return MixNWRPFog(fragColor, fogFactor);
}

half4 MixFogColor(half4 fragColor, float fogFactor)
{
    return MixNWRPFogColor(fragColor, fogFactor);
}

#endif // NEWWORLD_FOG_INCLUDED
