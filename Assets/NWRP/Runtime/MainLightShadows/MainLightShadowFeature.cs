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
        private MainLightShadowCacheState _cacheState;

        public bool HasValidCache => _cacheState != null && _cacheState.HasValidCache;
        public bool IsCacheDirty => _cacheState == null || _cacheState.IsDirty;
        public CameraType LastCacheCameraType => _cacheState != null ? _cacheState.LastCacheCameraType : CameraType.Game;
        public int LastCacheCameraInstanceId => _cacheState != null ? _cacheState.LastCacheCameraInstanceId : 0;

        protected override void Create()
        {
            _cacheState = new MainLightShadowCacheState();
            _mainLightShadowDisabledPass = new MainLightShadowDisabledPass();
            _mainLightShadowPass = new MainLightShadowCasterPass();
            _staticCachePass = new MainLightShadowStaticCachePass(_cacheState);
            _dynamicOverlayPass = new MainLightShadowDynamicOverlayPass(_cacheState);
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (_mainLightShadowDisabledPass == null
                || _mainLightShadowPass == null
                || _staticCachePass == null
                || _dynamicOverlayPass == null)
            {
                return;
            }

            NewWorldRenderPipelineAsset asset = frameData.asset;
            bool isGameCamera = MainLightShadowPassUtils.ShouldUseCachedMainLightShadow(frameData.camera);
            if (asset == null || !asset.EnableMainLightShadows)
            {
                if (isGameCamera)
                {
                    _cacheState?.Clear();
                }
                renderer.EnqueuePass(_mainLightShadowDisabledPass);
                return;
            }

            if (!asset.EnableCachedMainLightShadows)
            {
                if (isGameCamera)
                {
                    _cacheState?.Clear();
                }
                renderer.EnqueuePass(_mainLightShadowPass);
                return;
            }

            if (!isGameCamera)
            {
                renderer.EnqueuePass(_mainLightShadowPass);
                return;
            }

            renderer.EnqueuePass(_staticCachePass);

            if (MainLightShadowPassUtils.ShouldRenderDynamicOverlay(asset))
            {
                renderer.EnqueuePass(_dynamicOverlayPass);
            }
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
        }
    }
}
