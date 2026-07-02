using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Lighting
{
    internal struct AdditionalLightData
    {
        public int visibleLightIndex;
        public int compactIndex;
        public VisibleLight visibleLight;
        public Light light;
        public Vector4 position;
        public Vector4 color;
        public Vector4 attenuation;
        public Vector4 spotDirection;
        public float sortScore;
    }

    internal static class AdditionalLightUtils
    {
        public const int MaxAdditionalLights = 8;
        public const int MaxShadowedAdditionalLights = 4;
        public const int PointLightShadowFaceCount = 6;
        public const int MaxAdditionalLightShadowSlices = MaxShadowedAdditionalLights * PointLightShadowFaceCount;
        public const float SpotLightShadowTypeId = 0f;
        public const float PointLightShadowTypeId = 1f;

        public static int GetShadowSliceCount(LightType lightType)
        {
            return lightType switch
            {
                LightType.Spot => 1,
                LightType.Point => PointLightShadowFaceCount,
                _ => 0
            };
        }

        public static int CollectAdditionalLights(
            ref NWRPFrameData frameData,
            AdditionalLightData[] additionalLights,
            out int mainLightIndex)
        {
            NativeArray<VisibleLight> visibleLights = frameData.cullingResults.visibleLights;
            mainLightIndex = -1;

            for (int i = 0; i < visibleLights.Length; i++)
            {
                if (visibleLights[i].lightType != LightType.Directional)
                {
                    continue;
                }

                mainLightIndex = i;
                break;
            }

            int additionalCount = 0;
            int limit = GetUploadLimit(ref frameData, additionalLights);
            for (int i = 0; i < visibleLights.Length; i++)
            {
                if (i == mainLightIndex)
                {
                    continue;
                }

                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType != LightType.Point && visibleLight.lightType != LightType.Spot)
                {
                    continue;
                }

                Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
                position.w = 1f;

                float range = visibleLight.range;
                float invRangeSqr = 1f / Mathf.Max(range * range, 0.00001f);

                float spotScale = 0f;
                float spotOffset = 1f;
                if (visibleLight.lightType == LightType.Spot)
                {
                    float outerRad = Mathf.Deg2Rad * visibleLight.spotAngle * 0.5f;
                    float outerCos = Mathf.Cos(outerRad);
                    float innerSpotAngle = visibleLight.light != null
                        ? Mathf.Clamp(visibleLight.light.innerSpotAngle, 0f, visibleLight.spotAngle)
                        : visibleLight.spotAngle;
                    float innerRad = Mathf.Deg2Rad * innerSpotAngle * 0.5f;
                    float innerCos = Mathf.Cos(innerRad);
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    spotScale = 1f / angleRange;
                    spotOffset = -outerCos * spotScale;
                }

                Vector4 spotDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                spotDirection = spotDirection.normalized;
                spotDirection.w = 0f;

                AdditionalLightData data = new AdditionalLightData
                {
                    visibleLightIndex = i,
                    visibleLight = visibleLight,
                    light = visibleLight.light,
                    position = position,
                    color = visibleLight.finalColor,
                    attenuation = new Vector4(invRangeSqr, 0f, spotScale, spotOffset),
                    spotDirection = spotDirection
                };

                float sortScore = GetAdditionalLightSortScore(
                    frameData.camera,
                    visibleLight,
                    position);
                InsertAdditionalLight(
                    additionalLights,
                    limit,
                    ref additionalCount,
                    data,
                    sortScore);
            }

            return additionalCount;
        }

        private static int GetUploadLimit(
            ref NWRPFrameData frameData,
            AdditionalLightData[] additionalLights)
        {
            int arrayLimit = additionalLights != null ? additionalLights.Length : 0;
            return Mathf.Clamp(MaxAdditionalLights, 0, Mathf.Min(MaxAdditionalLights, arrayLimit));
        }

        private static float GetAdditionalLightSortScore(
            Camera camera,
            VisibleLight visibleLight,
            Vector4 lightPosition)
        {
            if (camera == null)
            {
                return visibleLight.light != null ? -visibleLight.light.intensity : 0f;
            }

            Vector3 toLight = new Vector3(
                lightPosition.x,
                lightPosition.y,
                lightPosition.z) - camera.transform.position;
            Vector4 color = visibleLight.finalColor;
            float luminance = Mathf.Max(
                color.x * 0.2126f + color.y * 0.7152f + color.z * 0.0722f,
                0.001f);
            return toLight.sqrMagnitude / luminance;
        }

        private static void InsertAdditionalLight(
            AdditionalLightData[] additionalLights,
            int limit,
            ref int additionalCount,
            AdditionalLightData data,
            float sortScore)
        {
            if (limit <= 0 || additionalLights == null)
            {
                return;
            }

            int insertIndex = additionalCount;
            int compareCount = Mathf.Min(additionalCount, limit);
            for (int i = 0; i < compareCount; i++)
            {
                if (sortScore < additionalLights[i].sortScore)
                {
                    insertIndex = i;
                    break;
                }
            }

            if (additionalCount >= limit && insertIndex >= limit)
            {
                return;
            }

            int lastIndex = Mathf.Min(additionalCount, limit - 1);
            for (int i = lastIndex; i > insertIndex; i--)
            {
                additionalLights[i] = additionalLights[i - 1];
                additionalLights[i].compactIndex = i;
            }

            data.compactIndex = insertIndex;
            data.sortScore = sortScore;
            additionalLights[insertIndex] = data;
            additionalCount = Mathf.Min(additionalCount + 1, limit);
        }
    }
}
