using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Main Light Shadow Feature")]
    public sealed class MainLightShadowFeature : NWRPFeature
    {
        private MainLightShadowDisabledPass _mainLightShadowDisabledPass;
        private MainLightShadowCasterPass _mainLightShadowPass;
        private MainLightShadowStaticCachePass _staticCachePass;
        private MainLightShadowDynamicOverlayPass _dynamicOverlayPass;
        private MainLightShadowCasterDebugOverlayPass _debugOverlayPass;
        private MainLightShadowCacheState _cacheState;
        private NewWorldRenderPipelineAsset.MainLightShadowExecutionPath _lastExecutionPath
            = NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.Unknown;

        internal NewWorldRenderPipelineAsset.MainLightShadowExecutionPath LastExecutionPath => _lastExecutionPath;

        protected override void Create()
        {
            _cacheState = new MainLightShadowCacheState();
            _mainLightShadowDisabledPass = new MainLightShadowDisabledPass();
            _mainLightShadowPass = new MainLightShadowCasterPass();
            _staticCachePass = new MainLightShadowStaticCachePass(_cacheState);
            _dynamicOverlayPass = new MainLightShadowDynamicOverlayPass(_cacheState);
            _debugOverlayPass = new MainLightShadowCasterDebugOverlayPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (_mainLightShadowDisabledPass == null
                || _mainLightShadowPass == null
                || _staticCachePass == null
                || _dynamicOverlayPass == null
                || _debugOverlayPass == null)
            {
                return;
            }

            NewWorldRenderPipelineAsset asset = frameData.asset;
            bool isGameCamera = MainLightShadowPassUtils.ShouldUseCachedMainLightShadow(frameData.camera);
            if (asset == null || !asset.EnableMainLightShadows)
            {
                RecordDebugState(
                    NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.Disabled
                );
                if (isGameCamera)
                {
                    _cacheState?.Clear();
                }
                renderer.EnqueuePass(_mainLightShadowDisabledPass);
                EnqueueDebugOverlayPass(renderer, ref frameData);
                return;
            }

            if (!asset.EnableCachedMainLightShadows)
            {
                RecordDebugState(
                    NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.RealtimeAtlas
                );
                if (isGameCamera)
                {
                    _cacheState?.Clear();
                }
                renderer.EnqueuePass(_mainLightShadowPass);
                EnqueueDebugOverlayPass(renderer, ref frameData);
                return;
            }

            if (!isGameCamera)
            {
                RecordDebugState(
                    NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.RealtimeAtlas
                );
                renderer.EnqueuePass(_mainLightShadowPass);
                return;
            }

            RecordDebugState(
                MainLightShadowPassUtils.ShouldRenderDynamicOverlay(asset)
                    ? NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.CachedStaticPlusDynamicOverlay
                    : NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.CachedStatic
            );

            renderer.EnqueuePass(_staticCachePass);

            if (MainLightShadowPassUtils.ShouldRenderDynamicOverlay(asset))
            {
                renderer.EnqueuePass(_dynamicOverlayPass);
            }

            EnqueueDebugOverlayPass(renderer, ref frameData);
        }

        public void MarkCacheDirty()
        {
            _cacheState?.MarkDirty();
        }

        public void ClearCache()
        {
            _cacheState?.Clear();
        }

        private void OnDisable()
        {
            if (_mainLightShadowPass != null)
            {
                _mainLightShadowPass.Dispose();
            }

            _cacheState?.Dispose();
            _cacheState = null;
            _mainLightShadowDisabledPass = null;
            _staticCachePass = null;
            _dynamicOverlayPass = null;
            _debugOverlayPass?.Dispose();
            _debugOverlayPass = null;
            _lastExecutionPath = NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.Unknown;
        }

        private void EnqueueDebugOverlayPass(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (renderer == null || _debugOverlayPass == null)
            {
                return;
            }

            if (!MainLightShadowPassUtils.ShouldRenderShadowDebugView(frameData.asset, frameData.camera))
            {
                return;
            }

            renderer.EnqueuePass(_debugOverlayPass);
        }

        private void RecordDebugState(NewWorldRenderPipelineAsset.MainLightShadowExecutionPath executionPath)
        {
            _lastExecutionPath = executionPath;
        }
    }
}
