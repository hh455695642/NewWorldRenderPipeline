#ifndef NEWWORLD_SHADOWS_INCLUDED
#define NEWWORLD_SHADOWS_INCLUDED

#define NWRP_MAIN_LIGHT_SHADOW_FILTER_HARD 0
#define NWRP_MAIN_LIGHT_SHADOW_FILTER_MEDIUM_PCF 1

#define NWRP_MAIN_LIGHT_SHADOW_DEBUG_OFF 0
#define NWRP_MAIN_LIGHT_SHADOW_DEBUG_SOURCE_TINT 1

#define NWRP_MAIN_LIGHT_SHADOW_PATH_UNKNOWN 0
#define NWRP_MAIN_LIGHT_SHADOW_PATH_DISABLED 1
#define NWRP_MAIN_LIGHT_SHADOW_PATH_REALTIME 2
#define NWRP_MAIN_LIGHT_SHADOW_PATH_CACHED_STATIC 3
#define NWRP_MAIN_LIGHT_SHADOW_PATH_CACHED_STATIC_DYNAMIC 4

#ifndef MAX_ADDITIONAL_LIGHTS
#define MAX_ADDITIONAL_LIGHTS 8
#endif

#ifndef MAX_ADDITIONAL_LIGHT_SHADOW_SLICES
#define MAX_ADDITIONAL_LIGHT_SHADOW_SLICES 48
#endif

TEXTURE2D_SHADOW(_MainLightShadowmapTexture);
TEXTURE2D_SHADOW(_MainLightDynamicShadowmapTexture);
TEXTURE2D_SHADOW(_AdditionalLightsShadowmapTexture);
SAMPLER_CMP(sampler_LinearClampCompare);

float4x4 _MainLightWorldToShadow[2];
int _MainLightShadowCascadeCount;
int _MainLightShadowFilterMode;
int _MainLightShadowDebugViewMode;
int _MainLightShadowDebugExecutionPath;
float _MainLightShadowFilterRadius;
half4 _MainLightShadowParams;
half4 _MainLightDynamicShadowParams;
float4 _MainLightShadowmapSize;
float4 _MainLightShadowReceiverBiasParams;
float4 _MainLightShadowAtlasTexelSize;
float4 _CascadeShadowSplitSpheres0;
float4 _CascadeShadowSplitSpheres1;
float4 _CascadeShadowSplitSphereRadii;
float4 _AdditionalLightsPosition[MAX_ADDITIONAL_LIGHTS];
half4 _AdditionalLightsColor[MAX_ADDITIONAL_LIGHTS];
half4 _AdditionalLightsAttenuation[MAX_ADDITIONAL_LIGHTS];
half4 _AdditionalLightsSpotDir[MAX_ADDITIONAL_LIGHTS];
int _AdditionalLightsCount;
float4x4 _AdditionalLightsWorldToShadow[MAX_ADDITIONAL_LIGHT_SHADOW_SLICES];
float4 _AdditionalLightsShadowParams[MAX_ADDITIONAL_LIGHTS];
float4 _AdditionalLightsShadowAtlasRects[MAX_ADDITIONAL_LIGHT_SHADOW_SLICES];
float4 _AdditionalLightsShadowAtlasSize;
float4 _AdditionalLightsShadowGlobalParams;

struct MainLightShadowResult
{
    half finalVisibility;
    half staticVisibility;
    half dynamicVisibility;
};

#ifndef NWRP_MATERIAL_RECEIVE_SHADOWS
#define NWRP_MATERIAL_RECEIVE_SHADOWS 1.0h
#endif

half GetMaterialRealtimeShadowReceiverState()
{
    return saturate((half)NWRP_MATERIAL_RECEIVE_SHADOWS);
}

bool IsMainLightShadowSourceTintDebugEnabled()
{
    return _MainLightShadowDebugViewMode == NWRP_MAIN_LIGHT_SHADOW_DEBUG_SOURCE_TINT;
}

MainLightShadowResult CreateDefaultMainLightShadowResult()
{
    MainLightShadowResult result;
    result.finalVisibility = 1.0h;
    result.staticVisibility = 1.0h;
    result.dynamicVisibility = 1.0h;
    return result;
}

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

bool AreAdditionalLightShadowsEnabled()
{
    return _AdditionalLightsShadowGlobalParams.x > 0.5h;
}

half GetAdditionalLightReceiverShadowFade(float3 positionWS)
{
    float maxDistance = _AdditionalLightsShadowGlobalParams.y;
    float invFadeRange = _AdditionalLightsShadowGlobalParams.z;
    if (maxDistance <= 0.0)
    {
        return 0.0h;
    }

    float distanceToCamera = distance(positionWS, _WorldSpaceCameraPos.xyz);
    return saturate((maxDistance - distanceToCamera) * invFadeRange);
}

float2 ClampAdditionalLightShadowSampleUV(float2 uv, int lightIndex, float paddingScale)
{
    float4 tileBounds = _AdditionalLightsShadowAtlasRects[lightIndex];
    float2 padding = _AdditionalLightsShadowAtlasSize.xy * paddingScale;
    return clamp(uv, tileBounds.xy + padding, tileBounds.zw - padding);
}

half SampleAdditionalLightShadowTextureHard(float3 shadowCoordUVW, int sliceIndex)
{
    shadowCoordUVW.xy = ClampAdditionalLightShadowSampleUV(shadowCoordUVW.xy, sliceIndex, 0.5);
    return SAMPLE_TEXTURE2D_SHADOW(
        _AdditionalLightsShadowmapTexture,
        sampler_LinearClampCompare,
        shadowCoordUVW
    );
}

int GetAdditionalLightShadowFirstSliceIndex(int lightIndex)
{
    return (int)(_AdditionalLightsShadowParams[lightIndex].z + 0.5);
}

int GetAdditionalLightShadowSliceCount(int lightIndex)
{
    return (int)(_AdditionalLightsShadowParams[lightIndex].w + 0.5);
}

bool IsAdditionalLightPointShadow(int lightIndex)
{
    return GetAdditionalLightShadowSliceCount(lightIndex) > 1;
}

int GetPointLightShadowFaceIndex(float3 lightToReceiverWS)
{
    float3 absDirection = abs(lightToReceiverWS);

    if (absDirection.x >= absDirection.y && absDirection.x >= absDirection.z)
    {
        return lightToReceiverWS.x >= 0.0 ? 0 : 1;
    }

    if (absDirection.y >= absDirection.z)
    {
        return lightToReceiverWS.y >= 0.0 ? 2 : 3;
    }

    return lightToReceiverWS.z >= 0.0 ? 4 : 5;
}

half SampleAdditionalLightShadowFromSlice(float3 positionWS, int lightIndex, int sliceIndex)
{
    if (!AreAdditionalLightShadowsEnabled())
    {
        return 1.0h;
    }

    float4 shadowParams = _AdditionalLightsShadowParams[lightIndex];
    if (shadowParams.x <= 0.5h)
    {
        return 1.0h;
    }

    if (sliceIndex < 0 || sliceIndex >= MAX_ADDITIONAL_LIGHT_SHADOW_SLICES)
    {
        return 1.0h;
    }

    float4 shadowCoord = mul(_AdditionalLightsWorldToShadow[sliceIndex], float4(positionWS, 1.0));
    if (shadowCoord.w <= 0.0)
    {
        return 1.0h;
    }

    // Additional spot and point lights use perspective shadow matrices, so receiver coordinates must
    // be projected back to normalized atlas UVW before comparison sampling.
    float3 shadowCoordUVW = shadowCoord.xyz / shadowCoord.w;
    if (IsMainLightShadowCoordOutOfBounds(shadowCoordUVW))
    {
        return 1.0h;
    }

    half visibility = SampleAdditionalLightShadowTextureHard(shadowCoordUVW, sliceIndex);
    half fade = GetAdditionalLightReceiverShadowFade(positionWS);
    visibility = lerp(1.0h, visibility, fade);
    return lerp(1.0h, visibility, shadowParams.y);
}

half SampleAdditionalLightShadow(float3 positionWS, int lightIndex)
{
    if (!AreAdditionalLightShadowsEnabled())
    {
        return 1.0h;
    }

    float4 shadowParams = _AdditionalLightsShadowParams[lightIndex];
    if (shadowParams.x <= 0.5h)
    {
        return 1.0h;
    }

    int sliceIndex = GetAdditionalLightShadowFirstSliceIndex(lightIndex);
    if (IsAdditionalLightPointShadow(lightIndex))
    {
        float3 lightToReceiverWS = positionWS - _AdditionalLightsPosition[lightIndex].xyz;
        sliceIndex += GetPointLightShadowFaceIndex(lightToReceiverWS);
    }

    return SampleAdditionalLightShadowFromSlice(positionWS, lightIndex, sliceIndex);
}

half SampleAdditionalLightShadow(float3 positionWS, float3 normalWS, float3 lightDirectionWS, int lightIndex)
{
    return SampleAdditionalLightShadow(positionWS, lightIndex);
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

half SampleMainLightStaticShadowAtCoord(float3 shadowCoordUVW, int cascadeIndex)
{
    half visibility = SampleMainLightShadowTextureHard(shadowCoordUVW, cascadeIndex);
    if (_MainLightShadowFilterMode == NWRP_MAIN_LIGHT_SHADOW_FILTER_MEDIUM_PCF)
    {
        visibility = SampleMainLightShadowTextureMediumTent(shadowCoordUVW, cascadeIndex);
    }

    return visibility;
}

half SampleMainLightDynamicShadowAtCoord(float3 shadowCoordUVW, int cascadeIndex)
{
    half visibility = SampleMainLightDynamicShadowTextureHard(shadowCoordUVW, cascadeIndex);
    if (_MainLightShadowFilterMode == NWRP_MAIN_LIGHT_SHADOW_FILTER_MEDIUM_PCF)
    {
        visibility = SampleMainLightDynamicShadowTextureMediumTent(shadowCoordUVW, cascadeIndex);
    }

    return visibility;
}

MainLightShadowResult SampleMainLightShadowAtCoord(float3 shadowCoordUVW, int cascadeIndex)
{
    MainLightShadowResult result = CreateDefaultMainLightShadowResult();
    result.staticVisibility = SampleMainLightStaticShadowAtCoord(shadowCoordUVW, cascadeIndex);
    if (_MainLightDynamicShadowParams.x > 0.5h)
    {
        result.dynamicVisibility = SampleMainLightDynamicShadowAtCoord(shadowCoordUVW, cascadeIndex);
    }

    result.finalVisibility = min(result.staticVisibility, result.dynamicVisibility);
    return result;
}

MainLightShadowResult SampleMainLightShadowResultInternal(
    float3 positionWS,
    float3 normalWS,
    float3 lightDirectionWS,
    bool applyReceiverBias)
{
    MainLightShadowResult result = CreateDefaultMainLightShadowResult();

    if (GetMaterialRealtimeShadowReceiverState() <= 0.5h)
    {
        return result;
    }

    if (_MainLightShadowParams.x <= 0.0h || _MainLightShadowCascadeCount <= 0)
    {
        return result;
    }

    int cascadeIndex = GetMainLightShadowCascadeIndex(positionWS);
    if (cascadeIndex < 0)
    {
        return result;
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
        return result;
    }

    result = SampleMainLightShadowAtCoord(shadowCoordUVW, cascadeIndex);
    half fade = GetMainLightReceiverShadowFade(positionWS);
    result.staticVisibility = lerp(1.0h, result.staticVisibility, fade);
    result.dynamicVisibility = lerp(1.0h, result.dynamicVisibility, fade);
    result.finalVisibility = min(result.staticVisibility, result.dynamicVisibility);
    result.finalVisibility = lerp(1.0h, result.finalVisibility, _MainLightShadowParams.x);
    return result;
}

MainLightShadowResult GetMainLightShadowResult(float3 positionWS)
{
    return SampleMainLightShadowResultInternal(positionWS, 0.0.xxx, 0.0.xxx, false);
}

MainLightShadowResult GetMainLightShadowResult(float3 positionWS, float3 lightDirectionWS)
{
    return GetMainLightShadowResult(positionWS);
}

MainLightShadowResult GetMainLightShadowResult(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
{
    return SampleMainLightShadowResultInternal(positionWS, normalWS, lightDirectionWS, true);
}

half3 GetMainLightShadowDebugTint(MainLightShadowResult shadowResult)
{
    half staticOcclusion = 1.0h - shadowResult.staticVisibility;
    half dynamicOcclusion = 1.0h - shadowResult.dynamicVisibility;
    const half epsilon = 0.02h;

    bool hasStaticOcclusion = staticOcclusion > epsilon;
    bool hasDynamicOcclusion = dynamicOcclusion > epsilon;

    if (hasDynamicOcclusion && hasStaticOcclusion)
    {
        return half3(1.00h, 0.90h, 0.20h);
    }

    if (hasDynamicOcclusion)
    {
        return half3(0.20h, 0.45h, 1.00h);
    }

    if (hasStaticOcclusion)
    {
        return half3(0.20h, 1.00h, 0.30h);
    }

    return half3(1.0h, 1.0h, 1.0h);
}

bool HasMainLightShadowDebugOverlap(MainLightShadowResult shadowResult)
{
    half staticOcclusion = 1.0h - shadowResult.staticVisibility;
    half dynamicOcclusion = 1.0h - shadowResult.dynamicVisibility;
    const half epsilon = 0.02h;
    return staticOcclusion > epsilon && dynamicOcclusion > epsilon;
}

half3 GetMainLightShadowDebugColor(MainLightShadowResult shadowResult)
{
    half shadowAmount = saturate(1.0h - shadowResult.finalVisibility);
    return lerp(half3(1.0h, 1.0h, 1.0h), half3(1.00h, 0.90h, 0.20h), shadowAmount);
}

half SampleMainLightShadow(float3 positionWS)
{
    return GetMainLightShadowResult(positionWS).finalVisibility;
}

half SampleMainLightShadow(float3 positionWS, float3 lightDirectionWS)
{
    return GetMainLightShadowResult(positionWS, lightDirectionWS).finalVisibility;
}

half SampleMainLightShadow(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
{
    return GetMainLightShadowResult(positionWS, normalWS, lightDirectionWS).finalVisibility;
}

#undef NWRP_SAMPLE_MAIN_LIGHT_TENT9
#undef MAX_ADDITIONAL_LIGHT_SHADOW_SLICES
#undef MAX_ADDITIONAL_LIGHTS

#endif // NEWWORLD_SHADOWS_INCLUDED
