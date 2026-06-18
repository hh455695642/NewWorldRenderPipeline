using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Main Light Shadows",
        MenuPath = "Lighting/Main Light Shadows",
        ShowInAddMenu = false,
        SortOrder = 10)]
    public sealed class MainLightShadowFeature : NWRPFeature, INWRPSerializedFeatureStateProvider
    {
        private MainLightShadowDisabledPass _mainLightShadowDisabledPass;
        private MainLightShadowCasterPass _mainLightShadowPass;
        private MainLightShadowStaticCachePass _gameStaticCachePass;
        private MainLightShadowDynamicOverlayPass _gameDynamicOverlayPass;
#if UNITY_EDITOR
        private MainLightShadowStaticCachePass _sceneViewStaticCachePass;
        private MainLightShadowDynamicOverlayPass _sceneViewDynamicOverlayPass;
#endif
        private MainLightShadowCasterDebugOverlayPass _debugOverlayPass;
        private MainLightShadowCacheState _gameCacheState;
#if UNITY_EDITOR
        private MainLightShadowCacheState _sceneViewCacheState;
#endif
        private NewWorldRenderPipelineAsset.MainLightShadowExecutionPath _lastExecutionPath
            = NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.Unknown;

        internal NewWorldRenderPipelineAsset.MainLightShadowExecutionPath LastExecutionPath => _lastExecutionPath;

        bool INWRPSerializedFeatureStateProvider.DeferSerializedPasses => false;

        protected override void Create()
        {
            _gameCacheState = new MainLightShadowCacheState();
            _mainLightShadowDisabledPass = new MainLightShadowDisabledPass();
            _mainLightShadowPass = new MainLightShadowCasterPass();
            _gameStaticCachePass = new MainLightShadowStaticCachePass(_gameCacheState);
            _gameDynamicOverlayPass = new MainLightShadowDynamicOverlayPass(_gameCacheState);
#if UNITY_EDITOR
            _sceneViewCacheState = new MainLightShadowCacheState();
            _sceneViewStaticCachePass = new MainLightShadowStaticCachePass(_sceneViewCacheState);
            _sceneViewDynamicOverlayPass = new MainLightShadowDynamicOverlayPass(_sceneViewCacheState);
#endif
            _debugOverlayPass = new MainLightShadowCasterDebugOverlayPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            MainLightShadowIndirectCasterContext.Clear();

            if (_mainLightShadowDisabledPass == null
                || _mainLightShadowPass == null
                || _debugOverlayPass == null)
            {
                return;
            }

            NewWorldRenderPipelineAsset asset = frameData.asset;
            bool canUseCachedShadow = TryGetCachedPasses(
                frameData.camera,
                out MainLightShadowStaticCachePass staticCachePass,
                out MainLightShadowDynamicOverlayPass dynamicOverlayPass);
            MainLightShadowCacheState cacheState = GetCacheStateForCamera(frameData.camera);
            if (asset == null || !asset.EnableMainLightShadows)
            {
                RecordDebugState(
                    NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.Disabled
                );
                cacheState?.Clear();
                renderer.EnqueuePass(_mainLightShadowDisabledPass);
                EnqueueDebugOverlayPass(renderer, ref frameData);
                return;
            }

            if (!asset.EnableCachedMainLightShadows)
            {
                RecordDebugState(
                    NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.RealtimeAtlas
                );
                cacheState?.Clear();
                renderer.EnqueuePass(_mainLightShadowPass);
                EnqueueDebugOverlayPass(renderer, ref frameData);
                return;
            }

            if (!canUseCachedShadow)
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

            renderer.EnqueuePass(staticCachePass);

            if (MainLightShadowPassUtils.ShouldRenderDynamicOverlay(asset))
            {
                renderer.EnqueuePass(dynamicOverlayPass);
            }

            EnqueueDebugOverlayPass(renderer, ref frameData);
        }

        public void MarkCacheDirty()
        {
            _gameCacheState?.MarkDirty();
#if UNITY_EDITOR
            _sceneViewCacheState?.MarkDirty();
#endif
        }

        public void ClearCache()
        {
            _gameCacheState?.Clear();
#if UNITY_EDITOR
            _sceneViewCacheState?.Clear();
#endif
        }

        void INWRPSerializedFeatureStateProvider.RecordSerializedFeatureState(
            ref NWRPSerializedFeatureState state)
        {
            state.hasMainLightShadow = true;
        }

        private void OnDisable()
        {
            if (_mainLightShadowPass != null)
            {
                _mainLightShadowPass.Dispose();
            }

            _gameCacheState?.Dispose();
            _gameCacheState = null;
#if UNITY_EDITOR
            _sceneViewCacheState?.Dispose();
            _sceneViewCacheState = null;
#endif
            _mainLightShadowDisabledPass = null;
            _gameStaticCachePass = null;
            _gameDynamicOverlayPass = null;
#if UNITY_EDITOR
            _sceneViewStaticCachePass = null;
            _sceneViewDynamicOverlayPass = null;
#endif
            _debugOverlayPass?.Dispose();
            _debugOverlayPass = null;
            _lastExecutionPath = NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.Unknown;
        }

        internal MainLightShadowCacheState GetCacheStateForCameraType(CameraType cameraType)
        {
            if (!MainLightShadowPassUtils.ShouldUseCachedMainLightShadow(cameraType))
            {
                return null;
            }

#if UNITY_EDITOR
            if (cameraType == CameraType.SceneView)
            {
                return _sceneViewCacheState;
            }
#endif

            return _gameCacheState;
        }

        private MainLightShadowCacheState GetCacheStateForCamera(Camera camera)
        {
            return camera != null
                ? GetCacheStateForCameraType(camera.cameraType)
                : null;
        }

        private bool TryGetCachedPasses(
            Camera camera,
            out MainLightShadowStaticCachePass staticCachePass,
            out MainLightShadowDynamicOverlayPass dynamicOverlayPass)
        {
            staticCachePass = null;
            dynamicOverlayPass = null;

            if (camera == null
                || !MainLightShadowPassUtils.ShouldUseCachedMainLightShadow(camera.cameraType))
            {
                return false;
            }

#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {
                staticCachePass = _sceneViewStaticCachePass;
                dynamicOverlayPass = _sceneViewDynamicOverlayPass;
                return staticCachePass != null && dynamicOverlayPass != null;
            }
#endif

            staticCachePass = _gameStaticCachePass;
            dynamicOverlayPass = _gameDynamicOverlayPass;
            return staticCachePass != null && dynamicOverlayPass != null;
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
