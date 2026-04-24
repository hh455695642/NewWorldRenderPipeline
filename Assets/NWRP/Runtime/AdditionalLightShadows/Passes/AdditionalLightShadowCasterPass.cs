using NWRP.Runtime.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class AdditionalLightShadowCasterPass : NWRPPass
    {
        private const float kRasterDepthBias = 1.0f;
        private const float kRasterSlopeBias = 2.5f;
        private const int kMinimumShadowTileResolution = 128;

        private readonly AdditionalLightData[] _additionalLights =
            new AdditionalLightData[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Matrix4x4[] _worldToShadow =
            CreateWorldToShadowBuffer();
        private readonly Vector4[] _shadowParams =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Vector4[] _atlasRects =
            new Vector4[AdditionalLightUtils.MaxAdditionalLightShadowSlices];
        private readonly int[] _shadowCandidateIndices =
            new int[AdditionalLightUtils.MaxAdditionalLights];
        private readonly float[] _shadowCandidateDistances =
            new float[AdditionalLightUtils.MaxAdditionalLights];

        private RenderTexture _shadowmapTexture;
        private int _shadowmapWidth;
        private int _shadowmapHeight;

        public AdditionalLightShadowCasterPass()
            : base(
                NWRPPassEvent.ShadowMap,
                "Render Additional Punctual Light Realtime Atlas",
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
            if (asset == null || !asset.EnableAdditionalLightShadows || asset.MaxShadowedAdditionalLights <= 0)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int additionalCount = AdditionalLightUtils.CollectAdditionalLights(
                ref frameData,
                _additionalLights,
                out _);
            int candidateCount = CollectShadowCandidates(ref frameData, additionalCount);
            if (candidateCount <= 0)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int selectedLightBudget = GetSelectedShadowLightBudget(asset, candidateCount);
            if (!TryGetShadowAtlasLayout(
                    asset,
                    selectedLightBudget,
                    out int selectedLightCount,
                    out int tileResolution,
                    out int atlasWidth,
                    out int atlasHeight,
                    out int tileColumns))
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (!EnsureShadowmap(atlasWidth, atlasHeight))
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            ResetShadowMetadata();

            int renderedLightCount = 0;
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

                int shadowSliceIndex = 0;
                for (int candidateIndex = 0; candidateIndex < selectedLightCount; candidateIndex++)
                {
                    AdditionalLightData lightData = _additionalLights[_shadowCandidateIndices[candidateIndex]];
                    int sliceCount = AdditionalLightUtils.GetShadowSliceCount(lightData.visibleLight.lightType);
                    int firstSliceIndex = shadowSliceIndex;

                    if (!RenderShadowedLight(
                            ref frameData,
                            lightData,
                            firstSliceIndex,
                            tileColumns,
                            tileResolution,
                            atlasWidth,
                            atlasHeight))
                    {
                        continue;
                    }

                    // Commit receiver metadata only after all slices for this light rendered.
                    _shadowParams[lightData.compactIndex] = new Vector4(
                        1f,
                        lightData.light != null ? lightData.light.shadowStrength : 1f,
                        lightData.visibleLight.lightType == LightType.Point
                            ? AdditionalLightUtils.PointLightShadowTypeId
                            : AdditionalLightUtils.SpotLightShadowTypeId,
                        firstSliceIndex);
                    shadowSliceIndex += sliceCount;
                    renderedLightCount++;
                }

                cmd.SetGlobalDepthBias(0f, 0f);
                cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, (float)CullMode.Back);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
                MainLightShadowPassUtils.ExecuteBuffer(ref frameData);
            }

            if (renderedLightCount <= 0)
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
                _shadowParams,
                _atlasRects,
                atlasWidth,
                atlasHeight);
        }

        private static Matrix4x4[] CreateWorldToShadowBuffer()
        {
            Matrix4x4[] matrices = new Matrix4x4[AdditionalLightUtils.MaxAdditionalLightShadowSlices];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.identity;
            }

            return matrices;
        }

        private int CollectShadowCandidates(
            ref NWRPFrameData frameData,
            int additionalCount)
        {
            Camera camera = frameData.camera;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float maxReceiverDistance = Mathf.Max(frameData.asset.AdditionalLightShadowDistance, 0f);
            int candidateCount = 0;

            for (int i = 0; i < additionalCount; i++)
            {
                AdditionalLightData lightData = _additionalLights[i];
                int sliceCount = AdditionalLightUtils.GetShadowSliceCount(lightData.visibleLight.lightType);
                if (sliceCount <= 0)
                {
                    continue;
                }

                if (lightData.light == null
                    || lightData.light.shadows == LightShadows.None
                    || lightData.light.shadowStrength <= 0f)
                {
                    continue;
                }

                if (!frameData.cullingResults.GetShadowCasterBounds(
                        lightData.visibleLightIndex,
                        out Bounds _))
                {
                    continue;
                }

                float range = Mathf.Max(lightData.visibleLight.range, 0f);
                float maxLightDistance = maxReceiverDistance + range;
                Vector3 lightPosition = new Vector3(
                    lightData.position.x,
                    lightData.position.y,
                    lightData.position.z);
                float cameraDistanceSqr = (lightPosition - cameraPosition).sqrMagnitude;
                if (maxLightDistance > 0f
                    && cameraDistanceSqr > maxLightDistance * maxLightDistance)
                {
                    continue;
                }

                _shadowCandidateIndices[candidateCount] = i;
                _shadowCandidateDistances[i] = cameraDistanceSqr;
                candidateCount++;
            }

            SortShadowCandidates(candidateCount);
            return candidateCount;
        }

        private static int GetSelectedShadowLightBudget(
            NewWorldRenderPipelineAsset asset,
            int candidateCount)
        {
            return Mathf.Min(
                candidateCount,
                Mathf.Clamp(
                    asset.MaxShadowedAdditionalLights,
                    0,
                    AdditionalLightUtils.MaxShadowedAdditionalLights));
        }

        private void SortShadowCandidates(int candidateCount)
        {
            for (int i = 1; i < candidateCount; i++)
            {
                int candidateLightIndex = _shadowCandidateIndices[i];
                int insertionIndex = i - 1;
                while (insertionIndex >= 0
                    && CompareShadowCandidates(
                        candidateLightIndex,
                        _shadowCandidateIndices[insertionIndex]) < 0)
                {
                    _shadowCandidateIndices[insertionIndex + 1] = _shadowCandidateIndices[insertionIndex];
                    insertionIndex--;
                }

                _shadowCandidateIndices[insertionIndex + 1] = candidateLightIndex;
            }
        }

        private int CompareShadowCandidates(int lhsLightIndex, int rhsLightIndex)
        {
            float lhsDistance = _shadowCandidateDistances[lhsLightIndex];
            float rhsDistance = _shadowCandidateDistances[rhsLightIndex];
            if (!Mathf.Approximately(lhsDistance, rhsDistance))
            {
                return lhsDistance < rhsDistance ? -1 : 1;
            }

            bool lhsIsSpot = _additionalLights[lhsLightIndex].visibleLight.lightType == LightType.Spot;
            bool rhsIsSpot = _additionalLights[rhsLightIndex].visibleLight.lightType == LightType.Spot;
            if (lhsIsSpot != rhsIsSpot)
            {
                return lhsIsSpot ? -1 : 1;
            }

            return _additionalLights[lhsLightIndex].visibleLightIndex.CompareTo(
                _additionalLights[rhsLightIndex].visibleLightIndex);
        }

        private bool TryGetShadowAtlasLayout(
            NewWorldRenderPipelineAsset asset,
            int selectedLightBudget,
            out int selectedLightCount,
            out int tileResolution,
            out int atlasWidth,
            out int atlasHeight,
            out int tileColumns)
        {
            selectedLightCount = selectedLightBudget;
            int requestedTileResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(asset.AdditionalLightShadowResolution, 128, 1024));
            int atlasMaxSize = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(asset.AdditionalLightShadowAtlasMaxSize, 512, 2048));

            while (selectedLightCount > 0)
            {
                int totalShadowSlices = GetSelectedShadowSliceCount(selectedLightCount);
                GetAtlasLayout(
                    totalShadowSlices,
                    requestedTileResolution,
                    atlasMaxSize,
                    out tileResolution,
                    out atlasWidth,
                    out atlasHeight,
                    out tileColumns);
                if (tileResolution >= kMinimumShadowTileResolution)
                {
                    return true;
                }

                selectedLightCount--;
            }

            tileResolution = 0;
            atlasWidth = 0;
            atlasHeight = 0;
            tileColumns = 0;
            return false;
        }

        private int GetSelectedShadowSliceCount(int selectedLightCount)
        {
            int totalShadowSlices = 0;
            for (int i = 0; i < selectedLightCount; i++)
            {
                AdditionalLightData lightData = _additionalLights[_shadowCandidateIndices[i]];
                totalShadowSlices += AdditionalLightUtils.GetShadowSliceCount(lightData.visibleLight.lightType);
            }

            return totalShadowSlices;
        }

        private bool RenderShadowedLight(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int firstSliceIndex,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            return lightData.visibleLight.lightType switch
            {
                LightType.Spot => RenderSpotLightShadow(
                    ref frameData,
                    lightData,
                    firstSliceIndex,
                    tileColumns,
                    tileResolution,
                    atlasWidth,
                    atlasHeight),
                LightType.Point => RenderPointLightShadow(
                    ref frameData,
                    lightData,
                    firstSliceIndex,
                    tileColumns,
                    tileResolution,
                    atlasWidth,
                    atlasHeight),
                _ => false
            };
        }

        private bool RenderSpotLightShadow(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int shadowSliceIndex,
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

            GetTileOffset(
                shadowSliceIndex,
                tileColumns,
                tileResolution,
                out int offsetX,
                out int offsetY);

            CommandBuffer cmd = frameData.cmd;
            cmd.SetViewport(new Rect(offsetX, offsetY, tileResolution, tileResolution));
            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, lightData.spotDirection);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
            cmd.SetGlobalVector(
                NWRPShaderIds.ShadowBias,
                MainLightShadowPassUtils.CalculateShadowBias(
                    frameData.asset.AdditionalLightShadowBias,
                    frameData.asset.AdditionalLightShadowNormalBias,
                    projectionMatrix,
                    tileResolution));
            MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

            frameData.context.DrawShadows(ref shadowDrawingSettings);
            RecordShadowSliceData(
                shadowSliceIndex,
                projectionMatrix,
                viewMatrix,
                offsetX,
                offsetY,
                tileResolution,
                atlasWidth,
                atlasHeight);
            return true;
        }

        private bool RenderPointLightShadow(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int firstSliceIndex,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            ShadowDrawingSettings shadowDrawingSettings =
                new ShadowDrawingSettings(
                    frameData.cullingResults,
                    lightData.visibleLightIndex,
                    BatchCullingProjectionType.Perspective)
                {
                    useRenderingLayerMaskTest = true
                };

            Vector4 shadowBias = AdditionalLightShadowPassUtils.CalculatePointShadowBias(
                frameData.asset.AdditionalLightShadowBias,
                lightData.visibleLight.range,
                tileResolution);
            float fovBias = AdditionalLightShadowPassUtils.GetPointLightShadowFrustumFovBiasInDegrees(tileResolution);
            CommandBuffer cmd = frameData.cmd;

            for (int faceIndex = 0; faceIndex < AdditionalLightUtils.PointLightShadowFaceCount; faceIndex++)
            {
                CubemapFace cubemapFace = (CubemapFace)faceIndex;
                if (!frameData.cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                        lightData.visibleLightIndex,
                        cubemapFace,
                        fovBias,
                        out Matrix4x4 viewMatrix,
                        out Matrix4x4 projectionMatrix,
                        out ShadowSplitData splitData))
                {
                    ResetShadowSliceData(firstSliceIndex, faceIndex);
                    return false;
                }

                AdditionalLightShadowPassUtils.FixupPointShadowViewMatrix(ref viewMatrix);
                splitData.shadowCascadeBlendCullingFactor = 1.0f;
                shadowDrawingSettings.splitData = splitData;

                int shadowSliceIndex = firstSliceIndex + faceIndex;
                GetTileOffset(
                    shadowSliceIndex,
                    tileColumns,
                    tileResolution,
                    out int offsetX,
                    out int offsetY);

                cmd.SetViewport(new Rect(offsetX, offsetY, tileResolution, tileResolution));
                cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                cmd.SetGlobalVector(
                    NWRPShaderIds.ShadowLightDirection,
                    AdditionalLightShadowPassUtils.GetPointLightFaceDirection(cubemapFace));
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, lightData.position);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, shadowBias);
                MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

                frameData.context.DrawShadows(ref shadowDrawingSettings);
                RecordShadowSliceData(
                    shadowSliceIndex,
                    projectionMatrix,
                    viewMatrix,
                    offsetX,
                    offsetY,
                    tileResolution,
                    atlasWidth,
                    atlasHeight);
            }

            return true;
        }

        private void RecordShadowSliceData(
            int shadowSliceIndex,
            Matrix4x4 projectionMatrix,
            Matrix4x4 viewMatrix,
            int offsetX,
            int offsetY,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            _worldToShadow[shadowSliceIndex] =
                MainLightShadowPassUtils.BuildWorldToShadowMatrix(
                    projectionMatrix,
                    viewMatrix,
                    offsetX,
                    offsetY,
                    tileResolution,
                    atlasWidth,
                    atlasHeight);
            _atlasRects[shadowSliceIndex] = new Vector4(
                (float)offsetX / atlasWidth,
                (float)offsetY / atlasHeight,
                (float)(offsetX + tileResolution) / atlasWidth,
                (float)(offsetY + tileResolution) / atlasHeight);
        }

        private void ResetShadowMetadata()
        {
            for (int i = 0; i < _worldToShadow.Length; i++)
            {
                _worldToShadow[i] = Matrix4x4.identity;
            }

            for (int i = 0; i < _shadowParams.Length; i++)
            {
                _shadowParams[i] = Vector4.zero;
            }

            for (int i = 0; i < _atlasRects.Length; i++)
            {
                _atlasRects[i] = Vector4.zero;
            }
        }

        private void ResetShadowSliceData(int firstSliceIndex, int renderedSliceCount)
        {
            for (int i = 0; i < renderedSliceCount; i++)
            {
                int shadowSliceIndex = firstSliceIndex + i;
                _worldToShadow[shadowSliceIndex] = Matrix4x4.identity;
                _atlasRects[shadowSliceIndex] = Vector4.zero;
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
            int totalShadowSlices,
            int requestedTileResolution,
            int atlasMaxSize,
            out int tileResolution,
            out int atlasWidth,
            out int atlasHeight,
            out int tileColumns)
        {
            if (totalShadowSlices <= 0)
            {
                tileResolution = 0;
                atlasWidth = 0;
                atlasHeight = 0;
                tileColumns = 0;
                return;
            }

            tileColumns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(totalShadowSlices)));
            int tileRows = Mathf.Max(1, Mathf.CeilToInt((float)totalShadowSlices / tileColumns));
            int maxTileResolution = Mathf.Min(
                Mathf.Max(1, atlasMaxSize / tileColumns),
                Mathf.Max(1, atlasMaxSize / tileRows));
            tileResolution = Mathf.Min(requestedTileResolution, FloorToPowerOfTwo(maxTileResolution));
            atlasWidth = tileResolution * tileColumns;
            atlasHeight = tileResolution * tileRows;
        }

        private static int FloorToPowerOfTwo(int value)
        {
            if (value < 1)
            {
                return 0;
            }

            int power = Mathf.ClosestPowerOfTwo(value);
            return power > value ? power >> 1 : power;
        }

        private static void GetTileOffset(
            int shadowSliceIndex,
            int tileColumns,
            int tileResolution,
            out int offsetX,
            out int offsetY)
        {
            offsetX = (shadowSliceIndex % tileColumns) * tileResolution;
            offsetY = (shadowSliceIndex / tileColumns) * tileResolution;
        }
    }
}
