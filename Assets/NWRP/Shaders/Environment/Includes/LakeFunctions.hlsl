#ifndef LAKE_FUNCTIONS_INCLUDED
#define LAKE_FUNCTIONS_INCLUDED

half3 UnpackLakeNormal(half4 packedNormal)
{
    half3 normalTS;
    normalTS.xy = packedNormal.ag * 2.0h - 1.0h;
    normalTS.z = sqrt(max(1.0h - saturate(dot(normalTS.xy, normalTS.xy)), 0.0h));
    return normalTS;
}

half SmoothMask(half edge, half smooth, half value)
{
    return smoothstep(edge, edge + max(smooth, 1.0e-4h), value);
}

half DistanceFromCamera(float3 positionWS, half start, half fade)
{
    float dist = distance(_WorldSpaceCameraPos, positionWS);
    return saturate((half)((dist - start) / max(fade, 1.0e-3h)));
}

float2 LakeWorldProjectedUV(float3 positionWS)
{
    return positionWS.xz * 0.1;
}

float2 LakePanner(float2 uv, half2 speed, half2 tile, half scale)
{
    half safeScale = max(scale, 1.0e-3h);
    return uv * (float2)(tile * safeScale) + (float2)(speed * 0.1h) * _Time.y;
}

void GerstnerWave(
    half wavelength,
    half sharpness,
    half height,
    half speed,
    half3 direction,
    float3 positionWS,
    out float3 displacement,
    out half3 normalWS)
{
    half safeLength = max(wavelength, 1.0e-3h);
    half safeHeight = max(abs(height), 1.0e-4h);
    half3 dir = normalize(direction);

    half k = (half)(TWO_PI / safeLength);
    half w = sqrt(9.8h * k);
    half phase = dot((half3)positionWS, dir * k) - w * _Time.y * speed;
    half s;
    half c;
    sincos(phase, s, c);

    half q = sharpness / max(k * safeHeight, 1.0e-3h);
    displacement = float3(dir * (s * q * height) * -1.0h);
    displacement.y = height * c;

    half wa = k * height;
    normalWS = normalize(half3(dir.x * wa * s, 1.0h - sharpness * wa * c, dir.z * wa * s));
}

void TwoWayNormalBlend(
    float3 positionWS,
    half distanceMask,
    half normalStrength,
    half distanceStrength,
    half normalPan,
    half normalScale,
    out half3 unscaledNormalTS,
    out half3 normalTS)
{
    float2 baseUV = LakeWorldProjectedUV(positionWS);
    float2 uvA = LakePanner(baseUV, -normalPan.xx * 0.5h, normalScale.xx * 0.5h, 1.0h);
    float2 uvB = LakePanner(baseUV, normalPan.xx, normalScale.xx, 1.0h);

    half3 normalA = UnpackLakeNormal(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, uvA));
    half3 normalB = UnpackLakeNormal(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, uvB));
    half3 blendedNormal = normalize(lerp(normalA, normalB, 0.5h));
    half strength = lerp(normalStrength, distanceStrength, distanceMask);

    unscaledNormalTS = blendedNormal;
    normalTS = normalize(half3(blendedNormal.xy * strength, lerp(1.0h, blendedNormal.z, saturate(strength))));
}

half DepthFadeWorldPosition(
    half depthFadeDistance,
    float2 screenUV,
    float rawDepth,
    float4 waterPositionCS,
    float3 waterPositionWS)
{
    float3 sceneWS = ComputeSceneWorldSpacePosition(screenUV, rawDepth);
    half verticalDepth = (half)max(waterPositionWS.y - sceneWS.y, 0.0);
    half sceneEyeDepth = (half)LinearEyeDepth(rawDepth);
    half waterEyeDepth = (half)LinearEyeDepth(waterPositionCS.z);
    half viewDepth = max(sceneEyeDepth - waterEyeDepth, 0.0h);
    half depth = max(verticalDepth, viewDepth);
    return saturate(exp(-depth / max(depthFadeDistance, 1.0e-3h)));
}

half EncodeLakeLineBand(half wave, half thickness)
{
    half centered = abs(frac(wave) * 2.0h - 1.0h);
    half width = lerp(0.02h, 0.45h, saturate(thickness));
    half softEdge = min(width + 0.18h, 1.0h);
    return saturate(1.0h - smoothstep(width, softEdge, centered));
}

half ShoreLineGenerator(
    half depthFadeDistance,
    half speed,
    half lines,
    half thickness,
    half centerMask,
    half centerMaskFade,
    half trailFade,
    half nearShoreExpand,
    half dissolve,
    half2 maskSpeed,
    half2 maskTile,
    half maskScale,
    half lineDirectionality,
    float2 screenUV,
    float rawDepth,
    float4 waterPositionCS,
    float3 waterPositionWS)
{
    half depthFade = DepthFadeWorldPosition(
        depthFadeDistance,
        screenUV,
        rawDepth,
        waterPositionCS,
        waterPositionWS);

    half center = SmoothMask(centerMask, centerMaskFade, depthFade);
    half wavePhase = (depthFade - speed * 0.1h * _Time.y) * max(lines, 1.0e-3h);
    half forwardWave = frac(wavePhase);
    half reverseWave = frac(-wavePhase);
    half forwardLine = EncodeLakeLineBand(forwardWave, thickness);
    half reverseLine = EncodeLakeLineBand(reverseWave, thickness);

    half directionSign = step(0.0h, speed);
    half directionalLine = lerp(reverseLine, forwardLine, directionSign);
    half symmetricLine = max(forwardLine, reverseLine);
    half shoreline = lerp(symmetricLine, directionalLine, saturate(lineDirectionality));

    float2 maskUV = LakePanner(LakeWorldProjectedUV(waterPositionWS), maskSpeed, maskTile, maskScale);
    half dissolveSample = SAMPLE_TEXTURE2D(_SL_Dissolve_Mask, sampler_SL_Dissolve_Mask, maskUV).r;
    half dissolveKeep = saturate(1.0h - dissolveSample * max(dissolve, 0.0h));
    shoreline = saturate(shoreline * dissolveKeep);

    half expand = saturate(depthFade * max(nearShoreExpand, 0.0h));
    shoreline = saturate(lerp(shoreline, max(shoreline, symmetricLine), expand));

    half stepMask = step(0.5h, shoreline);
    half lineWave = lerp(reverseWave, forwardWave, directionSign);
    half trail = saturate((1.0h - lineWave) * stepMask);
    half trailMask = saturate(lerp(stepMask, trail, saturate(trailFade * 0.5h)));

    return saturate(center * trailMask);
}

void WaterRefractionScreenSpace(
    half2 normalTS,
    half farRefractionStrength,
    half refractionDistanceFade,
    half mask,
    half refractionStrength,
    float4 waterPositionCS,
    float2 screenUV,
    float3 positionWS,
    out half3 refractedColor,
    out float2 refractedUV)
{
    half distMask = DistanceFromCamera(positionWS, refractionDistanceFade, 5.0h);
    half baseStrength = mask * refractionStrength;
    half finalStrength = lerp(baseStrength, farRefractionStrength, distMask);
    float2 offsetUV = (float2)(normalTS * finalStrength * 0.05h);
    float2 candidateUV = saturate(screenUV + offsetUV);

    half sceneEyeDepth = (half)LinearEyeDepth(SampleSceneDepth(candidateUV));
    half waterEyeDepth = (half)LinearEyeDepth(waterPositionCS.z);
    half keepOffset = step(waterEyeDepth - sceneEyeDepth, 0.0h);

    refractedUV = lerp(screenUV, candidateUV, keepOffset);
    refractedColor = SampleSceneColor(refractedUV);
}

half4 ColorLayerAlpha(half4 baseColor, half4 layerColor, half layerMask)
{
    half mask = saturate(layerMask * layerColor.a);
    return lerp(baseColor, layerColor, mask);
}

half LayerAlpha(half4 layerColor, half baseAlpha, half layerMask)
{
    half mask = saturate(layerMask);
    half screenAlpha = 1.0h - (1.0h - layerColor.a) * (1.0h - baseAlpha);
    return lerp(baseAlpha, screenAlpha, mask);
}

half3 StylizedSpecular(
    half specularSize,
    half specularHardness,
    half specularSpread,
    half3 specularColor,
    half3 normalWS,
    half3 viewDirWS,
    Light mainLight)
{
    half3 lightDir = normalize(-mainLight.direction);
    half3 reflectionDir = reflect(lightDir, normalWS);
    half rdotv = saturate(dot(reflectionDir, viewDirWS));
    half specPower = exp2((1.0h - specularSpread) * 10.0h + 1.0h);
    half spec = pow(rdotv, specPower);
    half edge = 1.0h - specularSize;
    half smoothSpec = smoothstep(edge, edge + 0.15h, spec);
    spec = lerp(spec, smoothSpec, specularHardness);
    return spec * specularColor * mainLight.color;
}

#endif // LAKE_FUNCTIONS_INCLUDED
