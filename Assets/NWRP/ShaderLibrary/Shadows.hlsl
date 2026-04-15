#ifndef NEWWORLD_SHADOWS_INCLUDED
#define NEWWORLD_SHADOWS_INCLUDED

#define NWRP_MAIN_LIGHT_SHADOW_FILTER_HARD 0
#define NWRP_MAIN_LIGHT_SHADOW_FILTER_MEDIUM_PCF 1

TEXTURE2D_SHADOW(_MainLightShadowmapTexture);
TEXTURE2D_SHADOW(_MainLightDynamicShadowmapTexture);
SAMPLER_CMP(sampler_LinearClampCompare);

float4x4 _MainLightWorldToShadow[2];
int _MainLightShadowCascadeCount;
int _MainLightShadowFilterMode;
float _MainLightShadowFilterRadius;
half4 _MainLightShadowParams;
half4 _MainLightDynamicShadowParams;
float4 _MainLightShadowmapSize;
float4 _MainLightShadowReceiverBiasParams;
float4 _MainLightShadowAtlasTexelSize;
float4 _CascadeShadowSplitSpheres0;
float4 _CascadeShadowSplitSpheres1;
float4 _CascadeShadowSplitSphereRadii;

int GetMainLightShadowCascadeIndex(float3 positionWS)
{
    if (_MainLightShadowCascadeCount <= 0)
    {
        return -1;
    }

    float3 toCascade0 = positionWS - _CascadeShadowSplitSpheres0.xyz;
    if (dot(toCascade0, toCascade0) <= _CascadeShadowSplitSphereRadii.x)
    {
        return 0;
    }

    if (_MainLightShadowCascadeCount == 1)
    {
        return -1;
    }

    float3 toCascade1 = positionWS - _CascadeShadowSplitSpheres1.xyz;
    if (dot(toCascade1, toCascade1) <= _CascadeShadowSplitSphereRadii.y)
    {
        return 1;
    }

    return -1;
}

float4 GetMainLightShadowCoord(float3 positionWS, int cascadeIndex)
{
    return mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));
}

float4 GetMainLightShadowTileBounds(int cascadeIndex)
{
    if (_MainLightShadowCascadeCount <= 1)
    {
        return float4(0.0, 0.0, 1.0, 1.0);
    }

    float tileScaleX = 0.5;
    float tileMinX = cascadeIndex * tileScaleX;
    return float4(tileMinX, 0.0, tileMinX + tileScaleX, 1.0);
}

float2 ClampMainLightShadowSampleUV(float2 uv, int cascadeIndex, float paddingScale)
{
    float4 tileBounds = GetMainLightShadowTileBounds(cascadeIndex);
    float2 padding = _MainLightShadowAtlasTexelSize.xy * paddingScale;
    return clamp(uv, tileBounds.xy + padding, tileBounds.zw - padding);
}

float GetMainLightShadowTileResolution()
{
    return max(_MainLightShadowAtlasTexelSize.w, 1.0);
}

float GetMainLightShadowReceiverWorldTexelSize(int cascadeIndex)
{
    float cascadeRadius = cascadeIndex == 0 ? _CascadeShadowSplitSpheres0.w : _CascadeShadowSplitSpheres1.w;
    return (2.0 * cascadeRadius) / GetMainLightShadowTileResolution();
}

half GetMainLightReceiverShadowFade(float3 positionWS)
{
    float distanceToCamera = distance(positionWS, _WorldSpaceCameraPos.xyz);
    float maxDistance = _MainLightShadowParams.y;
    float invFadeRange = _MainLightShadowParams.z;
    return saturate((maxDistance - distanceToCamera) * invFadeRange);
}

bool IsMainLightShadowCoordOutOfBounds(float3 shadowCoordUVW)
{
    return shadowCoordUVW.x < 0.0 || shadowCoordUVW.x > 1.0
        || shadowCoordUVW.y < 0.0 || shadowCoordUVW.y > 1.0
        || shadowCoordUVW.z < 0.0 || shadowCoordUVW.z > 1.0;
}

half SampleMainLightShadowTextureHard(float3 shadowCoordUVW, int cascadeIndex)
{
    shadowCoordUVW.xy = ClampMainLightShadowSampleUV(shadowCoordUVW.xy, cascadeIndex, 0.5);
    return SAMPLE_TEXTURE2D_SHADOW(
        _MainLightShadowmapTexture,
        sampler_LinearClampCompare,
        shadowCoordUVW
    );
}

half SampleMainLightDynamicShadowTextureHard(float3 shadowCoordUVW, int cascadeIndex)
{
    shadowCoordUVW.xy = ClampMainLightShadowSampleUV(shadowCoordUVW.xy, cascadeIndex, 0.5);
    return SAMPLE_TEXTURE2D_SHADOW(
        _MainLightDynamicShadowmapTexture,
        sampler_LinearClampCompare,
        shadowCoordUVW
    );
}

#define NWRP_SAMPLE_MAIN_LIGHT_TENT9(shadowTexture, shadowCoordUVW, cascadeIndex) \
    float radius = _MainLightShadowFilterRadius; \
    float paddingScale = max(radius, 1.0) * 0.5; \
    float2 texelSize = _MainLightShadowAtlasTexelSize.xy * radius; \
    half visibility = 0.0h; \
    float2 uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(-1.0, -1.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 1.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(0.0, -1.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 2.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(1.0, -1.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 1.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(-1.0, 0.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 2.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy, cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 4.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(1.0, 0.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 2.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(-1.0, 1.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 1.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(0.0, 1.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 2.0h; \
    uv = ClampMainLightShadowSampleUV(shadowCoordUVW.xy + texelSize * float2(1.0, 1.0), cascadeIndex, paddingScale); \
    visibility += SAMPLE_TEXTURE2D_SHADOW(shadowTexture, sampler_LinearClampCompare, float3(uv, shadowCoordUVW.z)) * 1.0h;

half SampleMainLightShadowTextureMediumTent(float3 shadowCoordUVW, int cascadeIndex)
{
    NWRP_SAMPLE_MAIN_LIGHT_TENT9(_MainLightShadowmapTexture, shadowCoordUVW, cascadeIndex)
    return visibility * (1.0h / 16.0h);
}

half SampleMainLightDynamicShadowTextureMediumTent(float3 shadowCoordUVW, int cascadeIndex)
{
    NWRP_SAMPLE_MAIN_LIGHT_TENT9(_MainLightDynamicShadowmapTexture, shadowCoordUVW, cascadeIndex)
    return visibility * (1.0h / 16.0h);
}

half SampleMainLightShadowHard(float3 shadowCoordUVW, int cascadeIndex)
{
    half visibility = SampleMainLightShadowTextureHard(shadowCoordUVW, cascadeIndex);
    if (_MainLightDynamicShadowParams.x > 0.5h)
    {
        half dynamicVisibility = SampleMainLightDynamicShadowTextureHard(shadowCoordUVW, cascadeIndex);
        visibility = min(visibility, dynamicVisibility);
    }

    return visibility;
}

half SampleMainLightShadowMediumTent(float3 shadowCoordUVW, int cascadeIndex)
{
    half visibility = SampleMainLightShadowTextureMediumTent(shadowCoordUVW, cascadeIndex);
    if (_MainLightDynamicShadowParams.x > 0.5h)
    {
        half dynamicVisibility = SampleMainLightDynamicShadowTextureMediumTent(shadowCoordUVW, cascadeIndex);
        visibility = min(visibility, dynamicVisibility);
    }

    return visibility;
}

half SampleMainLightShadowAtCoord(float3 shadowCoordUVW, int cascadeIndex)
{
    if (_MainLightShadowFilterMode == NWRP_MAIN_LIGHT_SHADOW_FILTER_MEDIUM_PCF)
    {
        return SampleMainLightShadowMediumTent(shadowCoordUVW, cascadeIndex);
    }

    return SampleMainLightShadowHard(shadowCoordUVW, cascadeIndex);
}

half SampleMainLightShadowInternal(float3 positionWS, float3 normalWS, float3 lightDirectionWS, bool applyReceiverBias)
{
    if (_MainLightShadowParams.x <= 0.0h || _MainLightShadowCascadeCount <= 0)
    {
        return 1.0h;
    }

    int cascadeIndex = GetMainLightShadowCascadeIndex(positionWS);
    if (cascadeIndex < 0)
    {
        return 1.0h;
    }

    float3 samplePositionWS = positionWS;
    float receiverBiasFactor = 0.0;
    if (applyReceiverBias)
    {
        float3 safeNormalWS = normalize(normalWS);
        float3 safeLightDirectionWS = normalize(lightDirectionWS);
        receiverBiasFactor = 1.0 - saturate(dot(safeNormalWS, safeLightDirectionWS));
        float receiverNormalBias = _MainLightShadowReceiverBiasParams.y
            * receiverBiasFactor
            * GetMainLightShadowReceiverWorldTexelSize(cascadeIndex);
        samplePositionWS += safeNormalWS * receiverNormalBias;
    }

    float3 shadowCoordUVW = GetMainLightShadowCoord(samplePositionWS, cascadeIndex).xyz;
    if (applyReceiverBias)
    {
        shadowCoordUVW.z -= _MainLightShadowReceiverBiasParams.x
            * receiverBiasFactor
            * _MainLightShadowAtlasTexelSize.z;
    }

    if (IsMainLightShadowCoordOutOfBounds(shadowCoordUVW))
    {
        return 1.0h;
    }

    half visibility = SampleMainLightShadowAtCoord(shadowCoordUVW, cascadeIndex);
    half fade = GetMainLightReceiverShadowFade(positionWS);
    half fadedVisibility = lerp(1.0h, visibility, fade);
    return lerp(1.0h, fadedVisibility, _MainLightShadowParams.x);
}

half SampleMainLightShadow(float3 positionWS)
{
    return SampleMainLightShadowInternal(positionWS, 0.0.xxx, 0.0.xxx, false);
}

half SampleMainLightShadow(float3 positionWS, float3 lightDirectionWS)
{
    return SampleMainLightShadow(positionWS);
}

half SampleMainLightShadow(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
{
    return SampleMainLightShadowInternal(positionWS, normalWS, lightDirectionWS, true);
}

#undef NWRP_SAMPLE_MAIN_LIGHT_TENT9

#endif // NEWWORLD_SHADOWS_INCLUDED
