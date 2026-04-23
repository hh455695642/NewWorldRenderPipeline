using NWRP.Runtime.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class AdditionalLightShadowCasterPass : NWRPPass
    {
        private const float kRasterDepthBias = 1.0f;
        private const float kRasterSlopeBias = 2.5f;

        private readonly struct ShadowedAdditionalLight
        {
            public readonly int additionalLightIndex;
            public readonly int firstSliceIndex;
            public readonly int sliceCount;

            public ShadowedAdditionalLight(int additionalLightIndex, int firstSliceIndex, int sliceCount)
            {
                this.additionalLightIndex = additionalLightIndex;
                this.firstSliceIndex = firstSliceIndex;
                this.sliceCount = sliceCount;
            }
        }

        private readonly AdditionalLightData[] _additionalLights =
            new AdditionalLightData[AdditionalLightUtils.MaxAdditionalLights];
        private readonly ShadowedAdditionalLight[] _shadowedLights =
            new ShadowedAdditionalLight[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Matrix4x4[] _worldToShadow =
            CreateWorldToShadowBuffer();
        private readonly Vector4[] _lightShadowParams =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Vector4[] _atlasRects =
            new Vector4[AdditionalLightUtils.MaxAdditionalShadowSlices];

        private RenderTexture _shadowmapTexture;
        private int _shadowmapWidth;
        private int _shadowmapHeight;

        public AdditionalLightShadowCasterPass()
            : base(
                NWRPPassEvent.ShadowMap,
                "Render Additional Spot / Point Light Realtime Atlas",
                NWRPProfiling.AdditionalLightShadow,
                usePassProfilingScope: false)
        {
        }

        public void Dispose()
        {
            ReleaseShadowmap();
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (asset == null
                || !asset.EnableAdditionalLightShadows
                || (asset.MaxShadowedAdditionalSpotLights <= 0
                    && asset.MaxShadowedAdditionalPointLights <= 0))
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int additionalCount = AdditionalLightUtils.CollectAdditionalLights(
                ref frameData,
                _additionalLights,
                out _);
            int shadowedLightCount = CollectShadowedLights(
                ref frameData,
                additionalCount,
                asset.MaxShadowedAdditionalSpotLights,
                asset.MaxShadowedAdditionalPointLights,
                out int totalSliceCount);
            if (shadowedLightCount <= 0 || totalSliceCount <= 0)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int tileResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(asset.AdditionalLightShadowResolution, 128, 1024));
            GetAtlasLayout(
                totalSliceCount,
                tileResolution,
                out int atlasWidth,
                out int atlasHeight,
                out int tileColumns);

            if (!EnsureShadowmap(atlasWidth, atlasHeight))
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            ResetShadowMetadata();

            int renderedShadowSliceCount = 0;
            using (new ProfilingScope(
                       frameData.cmd,
                       AdditionalLightShadowPassUtils.RenderRealtimeShadowAtlasSampler))
            {
                MainLightShadowPassUtils.ClearShadowAtlas(ref frameData, _shadowmapTexture);

                CommandBuffer cmd = frameData.cmd;
                cmd.SetGlobalFloat(
                    NWRPShaderIds.MainLightShadowCasterCull,
                    (float)asset.AdditionalLightShadowCasterCullModeSetting);
                cmd.SetGlobalDepthBias(kRasterDepthBias, kRasterSlopeBias);
                MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

                for (int shadowedLightIndex = 0;
                    shadowedLightIndex < shadowedLightCount;
                    shadowedLightIndex++)
                {
                    ShadowedAdditionalLight shadowedLight = _shadowedLights[shadowedLightIndex];
                    AdditionalLightData lightData = _additionalLights[shadowedLight.additionalLightIndex];

                    if (lightData.visibleLight.lightType == LightType.Spot)
                    {
                        if (RenderSpotLightShadow(
                                ref frameData,
                                lightData,
                                shadowedLight.firstSliceIndex,
                                tileColumns,
                                tileResolution,
                                atlasWidth,
                                atlasHeight))
                        {
                            _lightShadowParams[lightData.compactIndex] = new Vector4(
                                1f,
                                GetShadowStrength(lightData),
                                shadowedLight.firstSliceIndex,
                                1f);
                            renderedShadowSliceCount++;
                        }

                        continue;
                    }

                    if (lightData.visibleLight.lightType == LightType.Point)
                    {
                        int renderedFaceCount = RenderPointLightShadow(
                            ref frameData,
                            lightData,
                            shadowedLight.firstSliceIndex,
                            tileColumns,
                            tileResolution,
                            atlasWidth,
                            atlasHeight);
                        if (renderedFaceCount == AdditionalLightUtils.PointLightFaceCount)
                        {
                            _lightShadowParams[lightData.compactIndex] = new Vector4(
                                1f,
                                GetShadowStrength(lightData),
                                shadowedLight.firstSliceIndex,
                                AdditionalLightUtils.PointLightFaceCount);
                            renderedShadowSliceCount += renderedFaceCount;
                        }
                    }
                }

                cmd.SetGlobalDepthBias(0f, 0f);
                cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, (float)CullMode.Back);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightParams, Vector4.zero);
                MainLightShadowPassUtils.ExecuteBuffer(ref frameData);
            }

            if (renderedShadowSliceCount <= 0)
            {
                UploadDisabledGlobals(ref frameData);
                frameData.context.SetupCameraProperties(frameData.camera);
                return;
            }

            frameData.context.SetupCameraProperties(frameData.camera);
            AdditionalLightShadowPassUtils.UploadReceiverGlobals(
                ref frameData,
                _shadowmapTexture,
                _worldToShadow,
                _lightShadowParams,
                _atlasRects,
                atlasWidth,
                atlasHeight);
        }

        private static Matrix4x4[] CreateWorldToShadowBuffer()
        {
            Matrix4x4[] matrices = new Matrix4x4[AdditionalLightUtils.MaxAdditionalShadowSlices];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.identity;
            }

            return matrices;
        }

        private int CollectShadowedLights(
            ref NWRPFrameData frameData,
            int additionalCount,
            int spotBudget,
            int pointBudget,
            out int totalSliceCount)
        {
            Camera camera = frameData.camera;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float maxReceiverDistance = Mathf.Max(frameData.asset.AdditionalLightShadowDistance, 0f);
            int remainingSpotBudget = Mathf.Max(spotBudget, 0);
            int remainingPointBudget = Mathf.Max(pointBudget, 0);
            int shadowedCount = 0;
            totalSliceCount = 0;

            for (int i = 0;
                i < additionalCount && shadowedCount < AdditionalLightUtils.MaxAdditionalLights;
                i++)
            {
                AdditionalLightData lightData = _additionalLights[i];
                if (!IsShadowedLightCandidate(
                        ref frameData,
                        lightData,
                        cameraPosition,
                        maxReceiverDistance))
                {
                    continue;
                }

                int sliceCount;
                switch (lightData.visibleLight.lightType)
                {
                    case LightType.Spot:
                        if (remainingSpotBudget <= 0)
                        {
                            continue;
                        }

                        remainingSpotBudget--;
                        sliceCount = 1;
                        break;

                    case LightType.Point:
                        if (remainingPointBudget <= 0)
                        {
                            continue;
                        }

                        remainingPointBudget--;
                        sliceCount = AdditionalLightUtils.PointLightFaceCount;
                        break;

                    default:
                        continue;
                }

                _shadowedLights[shadowedCount] = new ShadowedAdditionalLight(
                    i,
                    totalSliceCount,
                    sliceCount);
                totalSliceCount += sliceCount;
                shadowedCount++;
            }

            return shadowedCount;
        }

        private static bool IsShadowedLightCandidate(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            Vector3 cameraPosition,
            float maxReceiverDistance)
        {
            if (lightData.visibleLight.lightType != LightType.Spot
                && lightData.visibleLight.lightType != LightType.Point)
            {
                return false;
            }

            if (lightData.light == null
                || lightData.light.shadows == LightShadows.None
                || lightData.light.shadowStrength <= 0f)
            {
                return false;
            }

            if (!frameData.cullingResults.GetShadowCasterBounds(
                    lightData.visibleLightIndex,
                    out Bounds _))
            {
                return false;
            }

            float range = Mathf.Max(lightData.visibleLight.range, 0f);
            float maxLightDistance = maxReceiverDistance + range;
            Vector3 lightPosition = new Vector3(
                lightData.position.x,
                lightData.position.y,
                lightData.position.z);
            if (maxLightDistance > 0f
                && (lightPosition - cameraPosition).sqrMagnitude
                > maxLightDistance * maxLightDistance)
            {
                return false;
            }

            return true;
        }

        private bool RenderSpotLightShadow(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int sliceIndex,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            if (!frameData.cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
                    lightData.visibleLightIndex,
                    out Matrix4x4 viewMatrix,
                    out Matrix4x4 projectionMatrix,
                    out ShadowSplitData splitData))
            {
                return false;
            }

            splitData.shadowCascadeBlendCullingFactor = 1.0f;
            ShadowDrawingSettings shadowDrawingSettings =
                new ShadowDrawingSettings(
                    frameData.cullingResults,
                    lightData.visibleLightIndex,
                    BatchCullingProjectionType.Perspective)
                {
                    useRenderingLayerMaskTest = true,
                    splitData = splitData
                };

            RenderShadowSlice(
                ref frameData,
                shadowDrawingSettings,
                viewMatrix,
                projectionMatrix,
                sliceIndex,
                tileColumns,
                tileResolution,
                atlasWidth,
                atlasHeight,
                lightData.spotDirection,
                Vector4.zero,
                Vector4.zero);
            return true;
        }

        private int RenderPointLightShadow(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int firstSliceIndex,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            int renderedFaceCount = 0;
            for (int faceIndex = 0; faceIndex < AdditionalLightUtils.PointLightFaceCount; faceIndex++)
            {
                CubemapFace cubemapFace = GetCubemapFace(faceIndex);
                if (!frameData.cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                        lightData.visibleLightIndex,
                        cubemapFace,
                        0f,
                        out Matrix4x4 viewMatrix,
                        out Matrix4x4 projectionMatrix,
                        out ShadowSplitData splitData))
                {
                    continue;
                }

                splitData.shadowCascadeBlendCullingFactor = 1.0f;
                ShadowDrawingSettings shadowDrawingSettings =
                    new ShadowDrawingSettings(
                        frameData.cullingResults,
                        lightData.visibleLightIndex,
                        BatchCullingProjectionType.Perspective)
                    {
                        useRenderingLayerMaskTest = true,
                        splitData = splitData
                    };

                RenderShadowSlice(
                    ref frameData,
                    shadowDrawingSettings,
                    viewMatrix,
                    projectionMatrix,
                    firstSliceIndex + faceIndex,
                    tileColumns,
                    tileResolution,
                    atlasWidth,
                    atlasHeight,
                    Vector4.zero,
                    lightData.position,
                    new Vector4(1f, 0f, 0f, 0f));
                renderedFaceCount++;
            }

            return renderedFaceCount;
        }

        private void RenderShadowSlice(
            ref NWRPFrameData frameData,
            ShadowDrawingSettings shadowDrawingSettings,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            int sliceIndex,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight,
            Vector4 shadowLightDirection,
            Vector4 shadowLightPosition,
            Vector4 shadowLightParams)
        {
            GetTileOffset(
                sliceIndex,
                tileColumns,
                tileResolution,
                out int offsetX,
                out int offsetY);

            CommandBuffer cmd = frameData.cmd;
            cmd.SetViewport(new Rect(offsetX, offsetY, tileResolution, tileResolution));
            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, shadowLightDirection);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, shadowLightPosition);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightParams, shadowLightParams);
            cmd.SetGlobalVector(
                NWRPShaderIds.ShadowBias,
                MainLightShadowPassUtils.CalculateShadowBias(
                    frameData.asset.AdditionalLightShadowBias,
                    frameData.asset.AdditionalLightShadowNormalBias,
                    projectionMatrix,
                    tileResolution));
            MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

            frameData.context.DrawShadows(ref shadowDrawingSettings);

            _worldToShadow[sliceIndex] =
                MainLightShadowPassUtils.BuildWorldToShadowMatrix(
                    projectionMatrix,
                    viewMatrix,
                    offsetX,
                    offsetY,
                    tileResolution,
                    atlasWidth,
                    atlasHeight);
            _atlasRects[sliceIndex] = new Vector4(
                (float)offsetX / atlasWidth,
                (float)offsetY / atlasHeight,
                (float)(offsetX + tileResolution) / atlasWidth,
                (float)(offsetY + tileResolution) / atlasHeight);
        }

        private void ResetShadowMetadata()
        {
            for (int i = 0; i < AdditionalLightUtils.MaxAdditionalLights; i++)
            {
                _lightShadowParams[i] = Vector4.zero;
            }

            for (int i = 0; i < AdditionalLightUtils.MaxAdditionalShadowSlices; i++)
            {
                _worldToShadow[i] = Matrix4x4.identity;
                _atlasRects[i] = Vector4.zero;
            }
        }

        private void UploadDisabledGlobals(ref NWRPFrameData frameData)
        {
            AdditionalLightShadowPassUtils.UploadDisabledGlobals(ref frameData);
        }

        private bool EnsureShadowmap(int width, int height)
        {
            if (_shadowmapTexture != null && _shadowmapWidth == width && _shadowmapHeight == height)
            {
                return true;
            }

            ReleaseShadowmap();

            _shadowmapTexture = new RenderTexture(width, height, 32, RenderTextureFormat.Shadowmap)
            {
                name = "NWRP_AdditionalLightShadows_Shadowmap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                anisoLevel = 0
            };
            _shadowmapTexture.Create();

            _shadowmapWidth = width;
            _shadowmapHeight = height;
            return _shadowmapTexture.IsCreated();
        }

        private void ReleaseShadowmap()
        {
            if (_shadowmapTexture == null)
            {
                return;
            }

            _shadowmapTexture.Release();
            if (Application.isPlaying)
            {
                Object.Destroy(_shadowmapTexture);
            }
            else
            {
                Object.DestroyImmediate(_shadowmapTexture);
            }

            _shadowmapTexture = null;
            _shadowmapWidth = 0;
            _shadowmapHeight = 0;
        }

        private static void GetAtlasLayout(
            int totalSliceCount,
            int tileResolution,
            out int atlasWidth,
            out int atlasHeight,
            out int tileColumns)
        {
            tileColumns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(totalSliceCount)));
            int tileRows = Mathf.Max(1, Mathf.CeilToInt((float)totalSliceCount / tileColumns));
            atlasWidth = tileResolution * tileColumns;
            atlasHeight = tileResolution * tileRows;
        }

        private static void GetTileOffset(
            int sliceIndex,
            int tileColumns,
            int tileResolution,
            out int offsetX,
            out int offsetY)
        {
            offsetX = (sliceIndex % tileColumns) * tileResolution;
            offsetY = (sliceIndex / tileColumns) * tileResolution;
        }

        private static CubemapFace GetCubemapFace(int faceIndex)
        {
            return (CubemapFace)faceIndex;
        }

        private static float GetShadowStrength(AdditionalLightData lightData)
        {
            return lightData.light != null ? lightData.light.shadowStrength : 1f;
        }
    }
}
