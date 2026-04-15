using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// Pipeline asset for NWRP runtime settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Rendering/New World Render Pipeline Asset")]
    public class NewWorldRenderPipelineAsset : RenderPipelineAsset
    {
        public enum MainLightShadowMode
        {
            Realtime = 0,
            CachedStaticDynamic = 1
        }

        public enum MainLightShadowFilterMode
        {
            Hard = 0,
            MediumPCF = 1
        }

        public enum MainLightShadowCasterCullMode
        {
            Front = (int)CullMode.Front,
            Back = (int)CullMode.Back
        }

        [System.Serializable]
        public sealed class FeatureSettings
        {
            public List<NWRPFeature> features = new List<NWRPFeature>();
        }

        [System.Serializable]
        public sealed class MainLightShadowSettings
        {
            [Tooltip("Enable all main light shadow rendering. When disabled, both realtime and cached main light shadows are skipped.")]
            public bool enableMainLightShadows = true;

            [Tooltip("Use cached static main light shadows for Game Cameras. SceneView and Preview cameras fall back to realtime main light shadows.")]
            public bool enableCachedMainLightShadows = false;

            [HideInInspector]
            public MainLightShadowMode shadowMode = MainLightShadowMode.Realtime;

            [HideInInspector]
            public bool cachedShadowSettingsMigrated = false;

            [Range(256, 4096)]
            public int mainLightShadowResolution = 2048;

            [Range(10f, 300f)]
            public float mainLightShadowDistance = 80f;

            [Range(1, 2)]
            public int mainLightShadowCascadeCount = 2;

            [Range(0.05f, 0.95f)]
            public float mainLightShadowCascadeSplit = 0.35f;

            [InspectorName("Main Light Shadow Depth Bias")]
            [Tooltip("Depth bias applied along the main light direction. Use it to reduce shallow-angle acne and coplanar shadow artifacts.")]
            [Range(0f, 5f)]
            public float mainLightShadowBias = 0.7f;

            [InspectorName("Main Light Shadow Normal Bias")]
            [Tooltip("Normal bias applied along the caster normal. Use it carefully on thin or cutout-like geometry because large values can erode shadow silhouettes.")]
            [Range(0f, 3f)]
            public float mainLightShadowNormalBias = 1.0f;

            [Tooltip("Receiver filter mode for main light shadows. Hard uses a single comparison; Medium PCF uses a 3x3 tent kernel.")]
            public MainLightShadowFilterMode mainLightShadowFilterMode = MainLightShadowFilterMode.Hard;

            [InspectorName("Main Light Shadow Filter Radius")]
            [Tooltip("Receiver-side Medium PCF filter radius in shadow texels. 1.0 matches the current 3x3 tent kernel footprint.")]
            [Range(0.5f, 2.0f)]
            public float mainLightShadowFilterRadius = 1.0f;

            [InspectorName("Main Light Shadow Receiver Depth Bias")]
            [Tooltip("Receiver-side depth bias applied dynamically based on the surface angle to the main light. Increase it to reduce acne on grazing angles.")]
            [Range(0f, 5f)]
            public float mainLightShadowReceiverDepthBias = 0f;

            [InspectorName("Main Light Shadow Receiver Normal Bias")]
            [Tooltip("Receiver-side normal offset applied dynamically on grazing angles. Increase it carefully to reduce acne without over-expanding contact gaps.")]
            [Range(0f, 3f)]
            public float mainLightShadowReceiverNormalBias = 0f;

            [Tooltip("Caster cull mode used by project shadow caster passes that support the main light shadow cull override.")]
            public MainLightShadowCasterCullMode mainLightShadowCasterCullMode = MainLightShadowCasterCullMode.Back;

            [Header("Cached Main Light Shadows")]
            [Tooltip("Enable a per-frame dynamic shadow overlay for Game Cameras when cached main light shadows are active.")]
            public bool enableDynamicShadowOverlay = true;

            [Tooltip("Layer mask rendered into the cached static main light shadow atlas for Game Cameras. Moving these casters does not refresh the cache until it is dirtied or invalidated.")]
            public LayerMask staticCasterLayerMask = ~0;

            [Tooltip("Layer mask rendered into the per-frame dynamic shadow overlay atlas for Game Cameras. Only used when Enable Dynamic Shadow is enabled.")]
            public LayerMask dynamicCasterLayerMask = 0;

            [Tooltip("Game Camera position delta in world units required to invalidate the cached static shadow atlas when using the OnDirty cached update path.")]
            [Min(0f)]
            public float cameraPositionInvalidationThreshold = 0.25f;

            [Tooltip("Game Camera rotation delta in degrees required to invalidate the cached static shadow atlas when using the OnDirty cached update path.")]
            [Min(0f)]
            public float cameraRotationInvalidationThreshold = 0.5f;

            [Tooltip("Main light direction delta in degrees required to invalidate the cached static shadow atlas when using the OnDirty cached update path.")]
            [Min(0f)]
            public float lightDirectionInvalidationThreshold = 0.5f;
        }

        [Header("General")]
        [Tooltip("Enable SRP Batcher")]
        public bool useSRPBatcher = true;

        [Tooltip("Enable GPU instancing")]
        public bool useGPUInstancing = true;

        [Header("Main Light Shadows")]
        public MainLightShadowSettings mainLightShadows = new MainLightShadowSettings();

        [Header("Feature Settings")]
        public FeatureSettings featureSettings = new FeatureSettings();

        [System.NonSerialized]
        private MainLightShadowFeature _runtimeMainLightShadowFeature;

        private MainLightShadowSettings MainLightShadowSettingsData
        {
            get
            {
                if (mainLightShadows == null)
                {
                    mainLightShadows = new MainLightShadowSettings();
                }

                if (!mainLightShadows.cachedShadowSettingsMigrated)
                {
                    mainLightShadows.enableCachedMainLightShadows =
                        mainLightShadows.shadowMode == MainLightShadowMode.CachedStaticDynamic;
                    mainLightShadows.cachedShadowSettingsMigrated = true;
                }

                return mainLightShadows;
            }
        }

        public List<NWRPFeature> Features
        {
            get
            {
                if (featureSettings == null)
                {
                    featureSettings = new FeatureSettings();
                }

                if (featureSettings.features == null)
                {
                    featureSettings.features = new List<NWRPFeature>();
                }

                return featureSettings.features;
            }
        }

        public bool EnableMainLightShadows => MainLightShadowSettingsData.enableMainLightShadows;
        public bool EnableCachedMainLightShadows => MainLightShadowSettingsData.enableCachedMainLightShadows;
        internal MainLightShadowMode MainLightShadowModeSetting =>
            EnableCachedMainLightShadows ? MainLightShadowMode.CachedStaticDynamic : MainLightShadowMode.Realtime;
        public int MainLightShadowResolution => MainLightShadowSettingsData.mainLightShadowResolution;
        public float MainLightShadowDistance => MainLightShadowSettingsData.mainLightShadowDistance;
        public int MainLightShadowCascadeCount => MainLightShadowSettingsData.mainLightShadowCascadeCount;
        public float MainLightShadowCascadeSplit => MainLightShadowSettingsData.mainLightShadowCascadeSplit;
        public float MainLightShadowBias => MainLightShadowSettingsData.mainLightShadowBias;
        public float MainLightShadowNormalBias => MainLightShadowSettingsData.mainLightShadowNormalBias;
        public MainLightShadowFilterMode MainLightShadowFilterModeSetting => MainLightShadowSettingsData.mainLightShadowFilterMode;
        public float MainLightShadowFilterRadius => MainLightShadowSettingsData.mainLightShadowFilterRadius;
        public float MainLightShadowReceiverDepthBias => MainLightShadowSettingsData.mainLightShadowReceiverDepthBias;
        public float MainLightShadowReceiverNormalBias => MainLightShadowSettingsData.mainLightShadowReceiverNormalBias;
        public MainLightShadowCasterCullMode MainLightShadowCasterCullModeSetting => MainLightShadowSettingsData.mainLightShadowCasterCullMode;
        public bool EnableDynamicShadowOverlay =>
            MainLightShadowSettingsData.enableCachedMainLightShadows && MainLightShadowSettingsData.enableDynamicShadowOverlay;
        public LayerMask StaticCasterLayerMask => MainLightShadowSettingsData.staticCasterLayerMask;
        public LayerMask DynamicCasterLayerMask => MainLightShadowSettingsData.dynamicCasterLayerMask;
        public float CameraPositionInvalidationThreshold => MainLightShadowSettingsData.cameraPositionInvalidationThreshold;
        public float CameraRotationInvalidationThreshold => MainLightShadowSettingsData.cameraRotationInvalidationThreshold;
        public float LightDirectionInvalidationThreshold => MainLightShadowSettingsData.lightDirectionInvalidationThreshold;

        /// <summary>
        /// Marks the cached main light shadow atlas dirty. If the pipeline asset has no serialized feature instance,
        /// the runtime fallback main light shadow feature is used instead.
        /// </summary>
        public void MarkMainLightShadowCacheDirty()
        {
            bool handled = false;
            List<NWRPFeature> features = Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] is not MainLightShadowFeature feature)
                {
                    continue;
                }

                feature.EnsureCreated();
                feature.MarkCacheDirty();
                handled = true;
            }

            if (handled)
            {
                return;
            }

            MainLightShadowFeature runtimeFeature = GetOrCreateMainLightShadowFeature();
            runtimeFeature.EnsureCreated();
            runtimeFeature.MarkCacheDirty();
        }

        /// <summary>
        /// Clears the cached main light shadow atlas state. If the pipeline asset has no serialized feature instance,
        /// the runtime fallback main light shadow feature is used instead.
        /// </summary>
        public void ClearMainLightShadowCache()
        {
            bool handled = false;
            List<NWRPFeature> features = Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] is not MainLightShadowFeature feature)
                {
                    continue;
                }

                feature.EnsureCreated();
                feature.ClearCache();
                handled = true;
            }

            if (handled)
            {
                return;
            }

            MainLightShadowFeature runtimeFeature = GetOrCreateMainLightShadowFeature();
            runtimeFeature.EnsureCreated();
            runtimeFeature.ClearCache();
        }

        internal MainLightShadowFeature GetOrCreateMainLightShadowFeature()
        {
            if (_runtimeMainLightShadowFeature != null)
            {
                return _runtimeMainLightShadowFeature;
            }

            _runtimeMainLightShadowFeature = ScriptableObject.CreateInstance<MainLightShadowFeature>();
            _runtimeMainLightShadowFeature.hideFlags = HideFlags.HideAndDontSave;
            _runtimeMainLightShadowFeature.name = "NWRP Runtime MainLightShadowFeature";
            return _runtimeMainLightShadowFeature;
        }

        internal void DisposeRuntimeFeatures()
        {
            if (_runtimeMainLightShadowFeature == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeMainLightShadowFeature);
            }
            else
            {
                DestroyImmediate(_runtimeMainLightShadowFeature);
            }
            _runtimeMainLightShadowFeature = null;
        }

        protected override RenderPipeline CreatePipeline()
        {
            return new NewWorldRenderPipeline(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            MainLightShadowSettings settings = MainLightShadowSettingsData;

            settings.mainLightShadowResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(settings.mainLightShadowResolution, 256, 4096)
            );
            settings.mainLightShadowCascadeCount = Mathf.Clamp(settings.mainLightShadowCascadeCount, 1, 2);
            settings.mainLightShadowCascadeSplit = Mathf.Clamp(settings.mainLightShadowCascadeSplit, 0.05f, 0.95f);
            settings.mainLightShadowDistance = Mathf.Max(0f, settings.mainLightShadowDistance);
            settings.mainLightShadowBias = Mathf.Max(0f, settings.mainLightShadowBias);
            settings.mainLightShadowNormalBias = Mathf.Max(0f, settings.mainLightShadowNormalBias);
            settings.mainLightShadowFilterRadius = Mathf.Clamp(settings.mainLightShadowFilterRadius, 0.5f, 2.0f);
            settings.mainLightShadowReceiverDepthBias = Mathf.Max(0f, settings.mainLightShadowReceiverDepthBias);
            settings.mainLightShadowReceiverNormalBias = Mathf.Max(0f, settings.mainLightShadowReceiverNormalBias);
            settings.cameraPositionInvalidationThreshold = Mathf.Max(0f, settings.cameraPositionInvalidationThreshold);
            settings.cameraRotationInvalidationThreshold = Mathf.Max(0f, settings.cameraRotationInvalidationThreshold);
            settings.lightDirectionInvalidationThreshold = Mathf.Max(0f, settings.lightDirectionInvalidationThreshold);
        }
#endif
    }
}
