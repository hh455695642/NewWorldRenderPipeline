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
            Texture dynamicShadowmap = _cacheState.EmptyShadowmapTexture;

            if (dynamicOverlayEnabled && _cacheState.DynamicShadowmapTexture == null)
            {
                _cacheState.EnsureDynamicShadowmap(_cacheState.AtlasWidth, _cacheState.AtlasHeight);
            }

            if (dynamicOverlayEnabled && _cacheState.DynamicShadowmapTexture != null)
            {
                dynamicShadowmap = _cacheState.DynamicShadowmapTexture;
                using (new ProfilingScope(frameData.cmd, MainLightShadowPassUtils.RenderDynamicOverlaySampler))
                {
                    CullingResults dynamicCullResults = frameData.cullingResults;
                    int dynamicCasterLayerMask = asset.DynamicCasterLayerMask.value;

                    if (!MainLightShadowPassUtils.IsEverythingLayerMask(dynamicCasterLayerMask)
                        && !MainLightShadowPassUtils.TryCull(
                            ref frameData,
                            dynamicCasterLayerMask,
                            out dynamicCullResults))
                    {
                        dynamicOverlayEnabled = false;
                        dynamicShadowmap = _cacheState.EmptyShadowmapTexture;
                    }
                    else if (!MainLightShadowPassUtils.TryGetMainLightIndex(
                            dynamicCullResults,
                            mainLight,
                            out int dynamicLightIndex,
                            out VisibleLight dynamicVisibleLight))
                    {
                        dynamicOverlayEnabled = false;
                        dynamicShadowmap = _cacheState.EmptyShadowmapTexture;
                    }
                    else
                    {
                        MainLightShadowPassUtils.ClearShadowAtlas(
                            ref frameData,
                            _cacheState.DynamicShadowmapTexture);
                        bool renderedDynamicAtlas = MainLightShadowPassUtils.RenderMainLightShadowAtlas(
                            ref frameData,
                            dynamicCullResults,
                            dynamicLightIndex,
                            dynamicVisibleLight,
                            _cacheState.CascadeCount,
                            _cacheState
                        );

                        if (!renderedDynamicAtlas)
                        {
                            dynamicOverlayEnabled = false;
                            dynamicShadowmap = _cacheState.EmptyShadowmapTexture;
                        }
                    }
                }
            }

            MainLightShadowPassUtils.UploadCachedReceiverGlobals(
                ref frameData,
                _cacheState.StaticShadowmapTexture,
                dynamicShadowmap,
                _cacheState,
                mainLight.shadowStrength,
                MainLightShadowPassUtils.GetEffectiveShadowDistance(asset, frameData.camera),
                dynamicOverlayEnabled,
                dynamicOverlayEnabled
                    ? NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.CachedStaticPlusDynamicOverlay
                    : NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.CachedStatic
            );

            frameData.context.SetupCameraProperties(frameData.camera);
        }

        private void UploadDisabledGlobals(ref NWRPFrameData frameData)
        {
            MainLightShadowPassUtils.UploadDisabledGlobals(
                ref frameData,
                _cacheState.EmptyShadowmapTexture,
                _cacheState.EmptyShadowmapTexture
            );
        }
    }
}
