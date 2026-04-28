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

            if (!MainLightShadowPassUtils.TryGetMainLight(ref frameData, out _, out _, out Light mainLight)
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
                using (new ProfilingScope(frameData.cmd, MainLightShadowPassUtils.RenderDynamicOverlaySampler))
                {
                    bool copiedStaticAtlas = MainLightShadowPassUtils.CopyShadowAtlas(
                        ref frameData,
                        _cacheState.StaticShadowmapTexture,
                        _cacheState.CombinedShadowmapTexture);

                    if (copiedStaticAtlas)
                    {
                        receiverShadowmap = _cacheState.CombinedShadowmapTexture;
                        CullingResults dynamicCullResults = frameData.cullingResults;
                        int dynamicCasterLayerMask = asset.DynamicCasterLayerMask.value;

                        if ((MainLightShadowPassUtils.IsEverythingLayerMask(dynamicCasterLayerMask)
                                || MainLightShadowPassUtils.TryCull(
                                    ref frameData,
                                    dynamicCasterLayerMask,
                                    out dynamicCullResults))
                            && MainLightShadowPassUtils.TryGetMainLightIndex(
                                dynamicCullResults,
                                mainLight,
                                out int dynamicLightIndex,
                                out VisibleLight dynamicVisibleLight))
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

                            if (renderedDynamicAtlas)
                            {
                                executionPath = NewWorldRenderPipelineAsset
                                    .MainLightShadowExecutionPath
                                    .CachedStaticPlusDynamicOverlay;
                            }
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

        private void UploadDisabledGlobals(ref NWRPFrameData frameData)
        {
            MainLightShadowPassUtils.UploadDisabledGlobals(
                ref frameData,
                _cacheState.EmptyShadowmapTexture
            );
        }
    }
}
