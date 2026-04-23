using NWRP.Runtime.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class AdditionalLightShadowCasterPass : NWRPPass
    {
        private const float kRasterDepthBias = 1.0f;
        private const float kRasterSlopeBias = 2.5f;

        private readonly AdditionalLightData[] _additionalLights =
            new AdditionalLightData[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Matrix4x4[] _worldToShadow =
            CreateWorldToShadowBuffer();
        private readonly Vector4[] _shadowParams =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Vector4[] _atlasRects =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private readonly int[] _shadowedLightIndices =
            new int[AdditionalLightUtils.MaxAdditionalLights];

        private RenderTexture _shadowmapTexture;
        private int _shadowmapWidth;
        private int _shadowmapHeight;

        public AdditionalLightShadowCasterPass()
            : base(
                NWRPPassEvent.ShadowMap,
                "Render Additional Spot Light Realtime Atlas",
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
            int shadowedLightCount = CollectShadowedSpotLights(
                ref frameData,
                additionalCount,
                asset.MaxShadowedAdditionalLights);
            if (shadowedLightCount <= 0)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int tileResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(asset.AdditionalLightShadowResolution, 128, 1024));
            GetAtlasLayout(
                shadowedLightCount,
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

            int renderedShadowCount = 0;
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

                for (int shadowedIndex = 0; shadowedIndex < shadowedLightCount; shadowedIndex++)
                {
                    AdditionalLightData lightData = _additionalLights[_shadowedLightIndices[shadowedIndex]];
                    if (!frameData.cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
                            lightData.visibleLightIndex,
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

                    GetTileOffset(
                        shadowedIndex,
                        tileColumns,
                        tileResolution,
                        out int offsetX,
                        out int offsetY);
                    cmd.SetViewport(new Rect(offsetX, offsetY, tileResolution, tileResolution));
                    cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                    cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, lightData.spotDirection);
                    cmd.SetGlobalVector(
                        NWRPShaderIds.ShadowBias,
                        MainLightShadowPassUtils.CalculateShadowBias(
                            asset.AdditionalLightShadowBias,
                            asset.AdditionalLightShadowNormalBias,
                            projectionMatrix,
                            tileResolution));
                    MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

                    frameData.context.DrawShadows(ref shadowDrawingSettings);

                    _worldToShadow[lightData.compactIndex] =
                        MainLightShadowPassUtils.BuildWorldToShadowMatrix(
                            projectionMatrix,
                            viewMatrix,
                            offsetX,
                            offsetY,
                            tileResolution,
                            atlasWidth,
                            atlasHeight);
                    _shadowParams[lightData.compactIndex] = new Vector4(
                        1f,
                        lightData.light != null ? lightData.light.shadowStrength : 1f,
                        0f,
                        0f);
                    _atlasRects[lightData.compactIndex] = new Vector4(
                        (float)offsetX / atlasWidth,
                        (float)offsetY / atlasHeight,
                        (float)(offsetX + tileResolution) / atlasWidth,
                        (float)(offsetY + tileResolution) / atlasHeight);
                    renderedShadowCount++;
                }

                cmd.SetGlobalDepthBias(0f, 0f);
                cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, (float)CullMode.Back);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
                MainLightShadowPassUtils.ExecuteBuffer(ref frameData);
            }

            if (renderedShadowCount <= 0)
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
            Matrix4x4[] matrices = new Matrix4x4[AdditionalLightUtils.MaxAdditionalLights];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.identity;
            }

            return matrices;
        }

        private int CollectShadowedSpotLights(
            ref NWRPFrameData frameData,
            int additionalCount,
            int lightBudget)
        {
            Camera camera = frameData.camera;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float maxReceiverDistance = Mathf.Max(frameData.asset.AdditionalLightShadowDistance, 0f);
            int shadowedCount = 0;

            for (int i = 0; i < additionalCount && shadowedCount < lightBudget; i++)
            {
                AdditionalLightData lightData = _additionalLights[i];
                if (lightData.visibleLight.lightType != LightType.Spot)
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
                if (maxLightDistance > 0f
                    && (lightPosition - cameraPosition).sqrMagnitude
                    > maxLightDistance * maxLightDistance)
                {
                    continue;
                }

                _shadowedLightIndices[shadowedCount] = i;
                shadowedCount++;
            }

            return shadowedCount;
        }

        private void ResetShadowMetadata()
        {
            for (int i = 0; i < AdditionalLightUtils.MaxAdditionalLights; i++)
            {
                _worldToShadow[i] = Matrix4x4.identity;
                _shadowParams[i] = Vector4.zero;
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
            int shadowedLightCount,
            int tileResolution,
            out int atlasWidth,
            out int atlasHeight,
            out int tileColumns)
        {
            tileColumns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(shadowedLightCount)));
            int tileRows = Mathf.Max(1, Mathf.CeilToInt((float)shadowedLightCount / tileColumns));
            atlasWidth = tileResolution * tileColumns;
            atlasHeight = tileResolution * tileRows;
        }

        private static void GetTileOffset(
            int shadowedIndex,
            int tileColumns,
            int tileResolution,
            out int offsetX,
            out int offsetY)
        {
            offsetX = (shadowedIndex % tileColumns) * tileResolution;
            offsetY = (shadowedIndex / tileColumns) * tileResolution;
        }
    }
}
