using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class MainLightShadowStaticCachePass : NWRPPass
    {
        private readonly MainLightShadowCacheState _cacheState;

        public MainLightShadowStaticCachePass(MainLightShadowCacheState cacheState)
            : base(
                NWRPPassEvent.ShadowMap,
                "Render Main Light Cached Shadow",
                NWRPProfiling.MainLightShadow,
                usePassProfilingScope: false)
        {
            _cacheState = cacheState;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (asset == null
                || !asset.EnableMainLightShadows
                || !asset.EnableCachedMainLightShadows)
            {
                _cacheState.Invalidate();
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (!MainLightShadowPassUtils.ShouldUseCachedMainLightShadow(frameData.camera))
            {
                return;
            }

            bool dynamicOverlayEnabled = MainLightShadowPassUtils.ShouldRenderDynamicOverlay(asset);

            if (!MainLightShadowPassUtils.TryGetMainLight(ref frameData, out int mainLightIndex, out _, out Light mainLight))
            {
                _cacheState.Invalidate();
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (mainLight == null || mainLight.shadows == LightShadows.None || mainLight.shadowStrength <= 0f)
            {
                _cacheState.Invalidate();
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int cascadeCount = Mathf.Clamp(asset.MainLightShadowCascadeCount, 1, 2);
            int requestedResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(asset.MainLightShadowResolution, 256, 4096));
            MainLightShadowPassUtils.GetAtlasSize(requestedResolution, cascadeCount, out int atlasWidth, out int atlasHeight, out int tileResolution);

            float effectiveShadowDistance = MainLightShadowPassUtils.GetEffectiveShadowDistance(asset, frameData.camera);
            int staticCasterLayerMask = MainLightShadowPassUtils.GetStaticCasterLayerMaskValue(asset);
            int dynamicCasterLayerMask = asset.DynamicCasterLayerMask.value;
            if (!dynamicOverlayEnabled)
            {
                _cacheState.ReleaseCombinedShadowmap();
            }

            bool staticReallocated = _cacheState.EnsureStaticShadowmap(atlasWidth, atlasHeight);
            bool emptyReallocated = _cacheState.EnsureEmptyShadowmap();
            bool combinedReallocated = dynamicOverlayEnabled
                && _cacheState.EnsureCombinedShadowmap(atlasWidth, atlasHeight);

            bool needsRebuild = staticReallocated
                || emptyReallocated
                || combinedReallocated
                || _cacheState.NeedsStaticCacheRebuild(
                    asset,
                    frameData.camera,
                    mainLight,
                    atlasWidth,
                    atlasHeight,
                    tileResolution,
                    cascadeCount,
                    effectiveShadowDistance,
                    staticCasterLayerMask,
                    dynamicCasterLayerMask,
                    dynamicOverlayEnabled
                );

            if (!needsRebuild)
            {
                if (!dynamicOverlayEnabled && _cacheState.HasValidCache && _cacheState.StaticShadowmapTexture != null)
                {
                    MainLightShadowPassUtils.UploadCachedReceiverGlobals(
                        ref frameData,
                        _cacheState.StaticShadowmapTexture,
                        _cacheState,
                        mainLight.shadowStrength,
                        effectiveShadowDistance,
                        NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.CachedStatic
                    );
                }

                return;
            }

            bool renderedStaticAtlas;
            using (new ProfilingScope(frameData.cmd, MainLightShadowPassUtils.RenderCachedShadowSampler))
            {
                if (!MainLightShadowPassUtils.ComputeCascadeData(
                        ref frameData,
                        mainLightIndex,
                        mainLight,
                        cascadeCount,
                        atlasWidth,
                        atlasHeight,
                        tileResolution,
                        _cacheState))
                {
                    _cacheState.Invalidate();
                    UploadDisabledGlobals(ref frameData);
                    return;
                }

                CullingResults staticCullResults = frameData.cullingResults;
                if (!MainLightShadowPassUtils.IsEverythingLayerMask(staticCasterLayerMask)
                    && !MainLightShadowPassUtils.TryCull(ref frameData, staticCasterLayerMask, out staticCullResults))
                {
                    _cacheState.Invalidate();
                    UploadDisabledGlobals(ref frameData);
                    return;
                }

                if (!MainLightShadowPassUtils.TryGetMainLightIndex(
                        staticCullResults,
                        mainLight,
                        out int staticLightIndex,
                        out VisibleLight staticVisibleLight))
                {
                    _cacheState.Invalidate();
                    UploadDisabledGlobals(ref frameData);
                    return;
                }

                MainLightShadowPassUtils.ClearShadowAtlas(ref frameData, _cacheState.StaticShadowmapTexture);
                renderedStaticAtlas = MainLightShadowPassUtils.RenderMainLightShadowAtlas(
                    ref frameData,
                    staticCullResults,
                    staticLightIndex,
                    staticVisibleLight,
                    cascadeCount,
                    _cacheState
                );
            }

            if (!renderedStaticAtlas)
            {
                _cacheState.Invalidate();
                UploadDisabledGlobals(ref frameData);
                frameData.context.SetupCameraProperties(frameData.camera);
                return;
            }

            _cacheState.CommitStaticCache(
                asset,
                frameData.camera,
                mainLight,
                atlasWidth,
                atlasHeight,
                tileResolution,
                cascadeCount,
                effectiveShadowDistance,
                staticCasterLayerMask,
                dynamicCasterLayerMask,
                dynamicOverlayEnabled
            );

            if (!_cacheState.HasValidCache || _cacheState.StaticShadowmapTexture == null)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (dynamicOverlayEnabled)
            {
                return;
            }

            MainLightShadowPassUtils.UploadCachedReceiverGlobals(
                ref frameData,
                _cacheState.StaticShadowmapTexture,
                _cacheState,
                mainLight.shadowStrength,
                effectiveShadowDistance,
                NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.CachedStatic
            );

            if (renderedStaticAtlas)
            {
                frameData.context.SetupCameraProperties(frameData.camera);
            }
        }

        private void UploadDisabledGlobals(ref NWRPFrameData frameData)
        {
            MainLightShadowPassUtils.UploadDisabledGlobals(
                ref frameData,
                _cacheState.EmptyShadowmapTexture
            );
        }
    }
}
