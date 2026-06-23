using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class MainLightShadowDynamicOverlayPass : NWRPPass
    {
        private readonly MainLightShadowCacheState _cacheState;

        public MainLightShadowDynamicOverlayPass(MainLightShadowCacheState cacheState)
            : base(
                NWRPPassEvent.ShadowMap,
                "Render Main Light Dynamic Overlay",
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
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (!MainLightShadowPassUtils.ShouldUseCachedMainLightShadow(frameData.camera))
            {
                return;
            }

            if (!MainLightShadowPassUtils.TryGetMainLight(
                    ref frameData,
                    out _,
                    out VisibleLight mainVisibleLight,
                    out Light mainLight)
                || mainLight == null
                || mainLight.shadows == LightShadows.None
                || mainLight.shadowStrength <= 0f
                || !_cacheState.HasValidCache
                || _cacheState.StaticShadowmapTexture == null)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            bool dynamicOverlayEnabled = MainLightShadowPassUtils.ShouldRenderDynamicOverlay(asset);
            Texture receiverShadowmap = _cacheState.StaticShadowmapTexture;
            NewWorldRenderPipelineAsset.MainLightShadowExecutionPath executionPath =
                NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.CachedStatic;

            if (dynamicOverlayEnabled && _cacheState.CombinedShadowmapTexture == null)
            {
                _cacheState.EnsureCombinedShadowmap(_cacheState.AtlasWidth, _cacheState.AtlasHeight);
            }

            if (dynamicOverlayEnabled && _cacheState.CombinedShadowmapTexture != null)
            {
                bool hasRegularDynamicCasters = TryGetDynamicOverlayCulling(
                    ref frameData,
                    asset,
                    mainLight,
                    out CullingResults dynamicCullResults,
                    out int dynamicLightIndex,
                    out VisibleLight dynamicVisibleLight);
                bool hasIndirectDynamicCasters =
                    MainLightShadowIndirectCasterContext.HasPendingDynamicOverlayDraw;

                using (new ProfilingScope(frameData.cmd, MainLightShadowPassUtils.RenderDynamicOverlaySampler))
                {
                    bool copiedStaticAtlas = (hasRegularDynamicCasters || hasIndirectDynamicCasters)
                        && MainLightShadowPassUtils.CopyShadowAtlas(
                            ref frameData,
                            _cacheState.StaticShadowmapTexture,
                            _cacheState.CombinedShadowmapTexture);

                    if (copiedStaticAtlas)
                    {
                        receiverShadowmap = _cacheState.CombinedShadowmapTexture;
                        executionPath = NewWorldRenderPipelineAsset
                            .MainLightShadowExecutionPath
                            .CachedStaticPlusDynamicOverlay;

                        bool includeStaticIndirectCasters =
                            MainLightShadowIndirectCasterContext.HasPendingStaticCacheDraw;
                        if (includeStaticIndirectCasters || hasIndirectDynamicCasters)
                        {
                            MainLightShadowIndirectCasterContext.AddTarget(
                                _cacheState.CombinedShadowmapTexture,
                                _cacheState.CascadeData,
                                _cacheState.CascadeCount,
                                MainLightShadowPassUtils.GetShadowLightDirection(mainVisibleLight),
                                includeStaticCasters: includeStaticIndirectCasters,
                                includeDynamicCasters: hasIndirectDynamicCasters);
                        }

                        if (hasRegularDynamicCasters)
                        {
                            MainLightShadowPassUtils.BindShadowAtlas(
                                ref frameData,
                                _cacheState.CombinedShadowmapTexture);
                            bool renderedDynamicAtlas = MainLightShadowPassUtils.RenderMainLightShadowAtlas(
                                ref frameData,
                                dynamicCullResults,
                                dynamicLightIndex,
                                dynamicVisibleLight,
                                _cacheState.CascadeCount,
                                _cacheState
                            );

                            _ = renderedDynamicAtlas;
                        }
                    }
                }
            }

            MainLightShadowPassUtils.UploadCachedReceiverGlobals(
                ref frameData,
                receiverShadowmap,
                _cacheState,
                mainLight.shadowStrength,
                MainLightShadowPassUtils.GetEffectiveShadowDistance(asset, frameData.camera),
                executionPath
            );

            frameData.context.SetupCameraProperties(frameData.camera);
        }

        private static bool TryGetDynamicOverlayCulling(
            ref NWRPFrameData frameData,
            NewWorldRenderPipelineAsset asset,
            Light mainLight,
            out CullingResults dynamicCullResults,
            out int dynamicLightIndex,
            out VisibleLight dynamicVisibleLight)
        {
            dynamicCullResults = frameData.cullingResults;
            dynamicLightIndex = -1;
            dynamicVisibleLight = default;

            int dynamicCasterLayerMask = asset.DynamicCasterLayerMask.value;
            if (!MainLightShadowPassUtils.IsEverythingLayerMask(dynamicCasterLayerMask)
                && !MainLightShadowPassUtils.TryCull(
                    ref frameData,
                    dynamicCasterLayerMask,
                    out dynamicCullResults))
            {
                return false;
            }

            if (!MainLightShadowPassUtils.TryGetMainLightIndex(
                    dynamicCullResults,
                    mainLight,
                    out dynamicLightIndex,
                    out dynamicVisibleLight))
            {
                return false;
            }

            return dynamicCullResults.GetShadowCasterBounds(
                dynamicLightIndex,
                out Bounds _);
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
