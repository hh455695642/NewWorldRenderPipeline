using NWRP.Runtime.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class AdditionalLightShadowCasterPass : NWRPPass
    {
        private const float kRasterDepthBias = 1.0f;
        private const float kRasterSlopeBias = 2.5f;

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

            NWRPShadowCullingContext shadowCullingContext = frameData.shadowCullingContext;
            if (shadowCullingContext == null || !shadowCullingContext.HasAdditionalShadows)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (!EnsureShadowmap(
                    shadowCullingContext.AdditionalAtlasWidth,
                    shadowCullingContext.AdditionalAtlasHeight))
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int renderedLightCount = 0;
            using (new ProfilingScope(
                       frameData.cmd,
                       AdditionalLightShadowPassUtils.RenderRealtimeShadowAtlasSampler))
            {
                MainLightShadowPassUtils.ClearShadowAtlas(ref frameData, _shadowmapTexture);
                if (!shadowCullingContext.Apply(ref frameData))
                {
                    UploadDisabledGlobals(ref frameData);
                    return;
                }

                CommandBuffer cmd = frameData.cmd;
                cmd.SetGlobalFloat(
                    NWRPShaderIds.MainLightShadowCasterCull,
                    (float)asset.AdditionalLightShadowCasterCullModeSetting);
                cmd.SetGlobalDepthBias(kRasterDepthBias, kRasterSlopeBias);
                MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

                for (int lightIndex = 0;
                     lightIndex < shadowCullingContext.AdditionalLightCount;
                     lightIndex++)
                {
                    NWRPAdditionalShadowLightEntry lightEntry =
                        shadowCullingContext.AdditionalLights[lightIndex];
                    if (!lightEntry.hasValidSlices)
                    {
                        continue;
                    }

                    bool renderedAnySliceForLight = false;
                    Vector4 pointShadowBias = lightEntry.lightType == LightType.Point
                        ? AdditionalLightShadowPassUtils.CalculatePointShadowBias(
                            asset.AdditionalLightShadowBias,
                            lightEntry.visibleLight.range,
                            shadowCullingContext.AdditionalTileResolution)
                        : Vector4.zero;
                    for (int sliceOffset = 0; sliceOffset < lightEntry.sliceCount; sliceOffset++)
                    {
                        int shadowSliceIndex = lightEntry.firstSliceIndex + sliceOffset;
                        NWRPAdditionalShadowSlice shadowSlice =
                            shadowCullingContext.AdditionalSlices[shadowSliceIndex];
                        if (!shadowSlice.isValid)
                        {
                            continue;
                        }

                        cmd.SetViewport(new Rect(
                            shadowSlice.offsetX,
                            shadowSlice.offsetY,
                            shadowSlice.resolution,
                            shadowSlice.resolution));
                        cmd.SetViewProjectionMatrices(
                            shadowSlice.viewMatrix,
                            shadowSlice.projectionMatrix);

                        if (lightEntry.lightType == LightType.Point)
                        {
                            cmd.SetGlobalVector(
                                NWRPShaderIds.ShadowLightDirection,
                                AdditionalLightShadowPassUtils.GetPointLightFaceDirection(
                                    (CubemapFace)shadowSlice.localSliceIndex));
                            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, lightEntry.position);
                            cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, pointShadowBias);
                        }
                        else
                        {
                            cmd.SetGlobalVector(
                                NWRPShaderIds.ShadowLightDirection,
                                lightEntry.spotDirection);
                            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
                            cmd.SetGlobalVector(
                                NWRPShaderIds.ShadowBias,
                                MainLightShadowPassUtils.CalculateShadowBias(
                                    asset.AdditionalLightShadowBias,
                                    asset.AdditionalLightShadowNormalBias,
                                    shadowSlice.projectionMatrix,
                                    shadowSlice.resolution));
                        }

                        MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

                        ShadowDrawingSettings shadowDrawingSettings =
                            MainLightShadowPassUtils.CreateShadowDrawingSettings(
                                frameData.cullingResults,
                                lightEntry.visibleLightIndex,
                                useRenderingLayerMaskTest: true,
                                splitIndex: shadowSlice.localSliceIndex);
                        MainLightShadowPassUtils.DrawShadowRendererList(
                            ref frameData,
                            ref shadowDrawingSettings);
                        renderedAnySliceForLight = true;
                    }

                    if (renderedAnySliceForLight)
                    {
                        renderedLightCount++;
                    }
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
                shadowCullingContext.AdditionalWorldToShadowMatrices,
                shadowCullingContext.AdditionalShadowParams,
                shadowCullingContext.AdditionalAtlasRects,
                shadowCullingContext.AdditionalAtlasWidth,
                shadowCullingContext.AdditionalAtlasHeight);
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
