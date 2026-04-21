using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// Pipeline asset for NWRP runtime settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Rendering/New World Render Pipeline Asset")]
    public class NewWorldRenderPipelineAsset : RenderPipelineAsset, ISerializationCallbackReceiver
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
        public sealed class MainLightShadowToggleSettings
        {
            [Tooltip("Enable all main light shadow rendering. When disabled, both realtime and cached main light shadows are skipped.")]
            public bool enableMainLightShadows = true;
        }

        [System.Serializable]
        public sealed class MainLightShadowDistanceSettings
        {
            [Range(10f, 300f)]
            public float mainLightShadowDistance = 80f;

            [Range(1, 2)]
            public int mainLightShadowCascadeCount = 2;

            [Range(0.05f, 0.95f)]
            public float mainLightShadowCascadeSplit = 0.35f;
        }

        [System.Serializable]
        public sealed class MainLightShadowAtlasSettings
        {
            [Range(256, 4096)]
            public int mainLightShadowResolution = 2048;

            [Tooltip("Receiver filter mode for main light shadows. Hard uses a single comparison; Medium PCF uses a 3x3 tent kernel.")]
            public MainLightShadowFilterMode mainLightShadowFilterMode = MainLightShadowFilterMode.Hard;

            [InspectorName("Main Light Shadow Filter Radius")]
            [Tooltip("Receiver-side Medium PCF filter radius in shadow texels. 1.0 matches the current 3x3 tent kernel footprint.")]
            [Range(0.5f, 2.0f)]
            public float mainLightShadowFilterRadius = 1.0f;
        }

        [System.Serializable]
        public sealed class MainLightShadowBiasSettings
        {
            [InspectorName("Main Light Shadow Depth Bias")]
            [Tooltip("Depth bias applied along the main light direction. Use it to reduce shallow-angle acne and coplanar shadow artifacts.")]
            [Range(0f, 5f)]
            public float mainLightShadowBias = 0.7f;

            [InspectorName("Main Light Shadow Normal Bias")]
            [Tooltip("Normal bias applied along the caster normal. Use it carefully on thin or cutout-like geometry because large values can erode shadow silhouettes.")]
            [Range(0f, 3f)]
            public float mainLightShadowNormalBias = 1.0f;

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
        }

        [System.Serializable]
        public sealed class MainLightShadowCachedSettings
        {
            [Tooltip("Use cached static main light shadows for Game Cameras. SceneView and Preview cameras fall back to realtime main light shadows.")]
            public bool enableCachedMainLightShadows = false;

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

        [System.Serializable]
        public sealed class MainLightShadowSettings
        {
            public const int CurrentSerializedVersion = 1;

            public MainLightShadowToggleSettings toggles = new MainLightShadowToggleSettings();
            public MainLightShadowDistanceSettings distance = new MainLightShadowDistanceSettings();
            public MainLightShadowAtlasSettings atlas = new MainLightShadowAtlasSettings();
            public MainLightShadowBiasSettings bias = new MainLightShadowBiasSettings();
            public MainLightShadowCachedSettings cached = new MainLightShadowCachedSettings();

            [HideInInspector]
            public int serializedVersion = 0;

            [HideInInspector]
            public bool enableMainLightShadows = true;

            [HideInInspector]
            public bool enableCachedMainLightShadows = false;

            [HideInInspector]
            public MainLightShadowMode shadowMode = MainLightShadowMode.Realtime;

            [HideInInspector]
            public bool cachedShadowSettingsMigrated = false;

            [HideInInspector]
            public int mainLightShadowResolution = 2048;

            [HideInInspector]
            public float mainLightShadowDistance = 80f;

            [HideInInspector]
            public int mainLightShadowCascadeCount = 2;

            [HideInInspector]
            public float mainLightShadowCascadeSplit = 0.35f;

            [HideInInspector]
            public float mainLightShadowBias = 0.7f;

            [HideInInspector]
            public float mainLightShadowNormalBias = 1.0f;

            [HideInInspector]
            public MainLightShadowFilterMode mainLightShadowFilterMode = MainLightShadowFilterMode.Hard;

            [HideInInspector]
            public float mainLightShadowFilterRadius = 1.0f;

            [HideInInspector]
            public float mainLightShadowReceiverDepthBias = 0f;

            [HideInInspector]
            public float mainLightShadowReceiverNormalBias = 0f;

            [HideInInspector]
            public MainLightShadowCasterCullMode mainLightShadowCasterCullMode = MainLightShadowCasterCullMode.Back;

            [HideInInspector]
            public bool enableDynamicShadowOverlay = true;

            [HideInInspector]
            public LayerMask staticCasterLayerMask = ~0;

            [HideInInspector]
            public LayerMask dynamicCasterLayerMask = 0;

            [HideInInspector]
            public float cameraPositionInvalidationThreshold = 0.25f;

            [HideInInspector]
            public float cameraRotationInvalidationThreshold = 0.5f;

            [HideInInspector]
            public float lightDirectionInvalidationThreshold = 0.5f;

            public void EnsureInitialized()
            {
                if (toggles == null)
                {
                    toggles = new MainLightShadowToggleSettings();
                }

                if (distance == null)
                {
                    distance = new MainLightShadowDistanceSettings();
                }

                if (atlas == null)
                {
                    atlas = new MainLightShadowAtlasSettings();
                }

                if (bias == null)
                {
                    bias = new MainLightShadowBiasSettings();
                }

                if (cached == null)
                {
                    cached = new MainLightShadowCachedSettings();
                }
            }
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
                EnsureMainLightShadowSettings(allowAssetFileMigration: true);
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

        public bool EnableMainLightShadows => MainLightShadowSettingsData.toggles.enableMainLightShadows;
        public bool EnableCachedMainLightShadows => MainLightShadowSettingsData.cached.enableCachedMainLightShadows;
        internal MainLightShadowMode MainLightShadowModeSetting =>
            EnableCachedMainLightShadows ? MainLightShadowMode.CachedStaticDynamic : MainLightShadowMode.Realtime;
        public int MainLightShadowResolution => MainLightShadowSettingsData.atlas.mainLightShadowResolution;
        public float MainLightShadowDistance => MainLightShadowSettingsData.distance.mainLightShadowDistance;
        public int MainLightShadowCascadeCount => MainLightShadowSettingsData.distance.mainLightShadowCascadeCount;
        public float MainLightShadowCascadeSplit => MainLightShadowSettingsData.distance.mainLightShadowCascadeSplit;
        public float MainLightShadowBias => MainLightShadowSettingsData.bias.mainLightShadowBias;
        public float MainLightShadowNormalBias => MainLightShadowSettingsData.bias.mainLightShadowNormalBias;
        public MainLightShadowFilterMode MainLightShadowFilterModeSetting => MainLightShadowSettingsData.atlas.mainLightShadowFilterMode;
        public float MainLightShadowFilterRadius => MainLightShadowSettingsData.atlas.mainLightShadowFilterRadius;
        public float MainLightShadowReceiverDepthBias => MainLightShadowSettingsData.bias.mainLightShadowReceiverDepthBias;
        public float MainLightShadowReceiverNormalBias => MainLightShadowSettingsData.bias.mainLightShadowReceiverNormalBias;
        public MainLightShadowCasterCullMode MainLightShadowCasterCullModeSetting => MainLightShadowSettingsData.bias.mainLightShadowCasterCullMode;
        public bool EnableDynamicShadowOverlay =>
            MainLightShadowSettingsData.cached.enableCachedMainLightShadows
            && MainLightShadowSettingsData.cached.enableDynamicShadowOverlay;
        public LayerMask StaticCasterLayerMask => MainLightShadowSettingsData.cached.staticCasterLayerMask;
        public LayerMask DynamicCasterLayerMask => MainLightShadowSettingsData.cached.dynamicCasterLayerMask;
        public float CameraPositionInvalidationThreshold => MainLightShadowSettingsData.cached.cameraPositionInvalidationThreshold;
        public float CameraRotationInvalidationThreshold => MainLightShadowSettingsData.cached.cameraRotationInvalidationThreshold;
        public float LightDirectionInvalidationThreshold => MainLightShadowSettingsData.cached.lightDirectionInvalidationThreshold;

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

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (mainLightShadows == null)
            {
                return;
            }

            mainLightShadows.EnsureInitialized();
            if (mainLightShadows.serializedVersion < MainLightShadowSettings.CurrentSerializedVersion)
            {
                return;
            }

            SyncMainLightShadowLegacyBridge(mainLightShadows);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            EnsureMainLightShadowSettings(allowAssetFileMigration: false);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            EnsureMainLightShadowSettings(allowAssetFileMigration: true);

            MainLightShadowSettings settings = mainLightShadows;
            settings.atlas.mainLightShadowResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(settings.atlas.mainLightShadowResolution, 256, 4096)
            );
            settings.distance.mainLightShadowCascadeCount = Mathf.Clamp(settings.distance.mainLightShadowCascadeCount, 1, 2);
            settings.distance.mainLightShadowCascadeSplit = Mathf.Clamp(settings.distance.mainLightShadowCascadeSplit, 0.05f, 0.95f);
            settings.distance.mainLightShadowDistance = Mathf.Max(0f, settings.distance.mainLightShadowDistance);
            settings.bias.mainLightShadowBias = Mathf.Max(0f, settings.bias.mainLightShadowBias);
            settings.bias.mainLightShadowNormalBias = Mathf.Max(0f, settings.bias.mainLightShadowNormalBias);
            settings.atlas.mainLightShadowFilterRadius = Mathf.Clamp(settings.atlas.mainLightShadowFilterRadius, 0.5f, 2.0f);
            settings.bias.mainLightShadowReceiverDepthBias = Mathf.Max(0f, settings.bias.mainLightShadowReceiverDepthBias);
            settings.bias.mainLightShadowReceiverNormalBias = Mathf.Max(0f, settings.bias.mainLightShadowReceiverNormalBias);
            settings.cached.cameraPositionInvalidationThreshold = Mathf.Max(0f, settings.cached.cameraPositionInvalidationThreshold);
            settings.cached.cameraRotationInvalidationThreshold = Mathf.Max(0f, settings.cached.cameraRotationInvalidationThreshold);
            settings.cached.lightDirectionInvalidationThreshold = Mathf.Max(0f, settings.cached.lightDirectionInvalidationThreshold);

            SyncMainLightShadowLegacyBridge(settings);
        }
#endif

        private void EnsureMainLightShadowSettings(bool allowAssetFileMigration)
        {
            if (mainLightShadows == null)
            {
                mainLightShadows = new MainLightShadowSettings();
            }

            mainLightShadows.EnsureInitialized();
#if UNITY_EDITOR
            if (allowAssetFileMigration)
            {
                TryLoadLegacyMainLightShadowSettingsFromAssetFile();
            }
#endif
            MigrateMainLightShadowLegacyData(mainLightShadows);
            SyncMainLightShadowLegacyBridge(mainLightShadows);
        }

        private static void MigrateMainLightShadowLegacyData(MainLightShadowSettings settings)
        {
            if (!settings.cachedShadowSettingsMigrated)
            {
                settings.enableCachedMainLightShadows =
                    settings.shadowMode == MainLightShadowMode.CachedStaticDynamic;
                settings.cachedShadowSettingsMigrated = true;
            }

            if (settings.serializedVersion >= MainLightShadowSettings.CurrentSerializedVersion)
            {
                return;
            }

            settings.toggles.enableMainLightShadows = settings.enableMainLightShadows;
            settings.cached.enableCachedMainLightShadows = settings.enableCachedMainLightShadows;
            settings.cached.enableDynamicShadowOverlay = settings.enableDynamicShadowOverlay;

            settings.distance.mainLightShadowDistance = settings.mainLightShadowDistance;
            settings.distance.mainLightShadowCascadeCount = settings.mainLightShadowCascadeCount;
            settings.distance.mainLightShadowCascadeSplit = settings.mainLightShadowCascadeSplit;

            settings.atlas.mainLightShadowResolution = settings.mainLightShadowResolution;
            settings.atlas.mainLightShadowFilterMode = settings.mainLightShadowFilterMode;
            settings.atlas.mainLightShadowFilterRadius = settings.mainLightShadowFilterRadius;

            settings.bias.mainLightShadowBias = settings.mainLightShadowBias;
            settings.bias.mainLightShadowNormalBias = settings.mainLightShadowNormalBias;
            settings.bias.mainLightShadowReceiverDepthBias = settings.mainLightShadowReceiverDepthBias;
            settings.bias.mainLightShadowReceiverNormalBias = settings.mainLightShadowReceiverNormalBias;
            settings.bias.mainLightShadowCasterCullMode = settings.mainLightShadowCasterCullMode;

            settings.cached.staticCasterLayerMask = settings.staticCasterLayerMask;
            settings.cached.dynamicCasterLayerMask = settings.dynamicCasterLayerMask;
            settings.cached.cameraPositionInvalidationThreshold = settings.cameraPositionInvalidationThreshold;
            settings.cached.cameraRotationInvalidationThreshold = settings.cameraRotationInvalidationThreshold;
            settings.cached.lightDirectionInvalidationThreshold = settings.lightDirectionInvalidationThreshold;

            settings.serializedVersion = MainLightShadowSettings.CurrentSerializedVersion;
        }

        private static void SyncMainLightShadowLegacyBridge(MainLightShadowSettings settings)
        {
            settings.enableMainLightShadows = settings.toggles.enableMainLightShadows;
            settings.enableCachedMainLightShadows = settings.cached.enableCachedMainLightShadows;
            settings.shadowMode = settings.cached.enableCachedMainLightShadows
                ? MainLightShadowMode.CachedStaticDynamic
                : MainLightShadowMode.Realtime;
            settings.cachedShadowSettingsMigrated = true;

            settings.mainLightShadowDistance = settings.distance.mainLightShadowDistance;
            settings.mainLightShadowCascadeCount = settings.distance.mainLightShadowCascadeCount;
            settings.mainLightShadowCascadeSplit = settings.distance.mainLightShadowCascadeSplit;

            settings.mainLightShadowResolution = settings.atlas.mainLightShadowResolution;
            settings.mainLightShadowFilterMode = settings.atlas.mainLightShadowFilterMode;
            settings.mainLightShadowFilterRadius = settings.atlas.mainLightShadowFilterRadius;

            settings.mainLightShadowBias = settings.bias.mainLightShadowBias;
            settings.mainLightShadowNormalBias = settings.bias.mainLightShadowNormalBias;
            settings.mainLightShadowReceiverDepthBias = settings.bias.mainLightShadowReceiverDepthBias;
            settings.mainLightShadowReceiverNormalBias = settings.bias.mainLightShadowReceiverNormalBias;
            settings.mainLightShadowCasterCullMode = settings.bias.mainLightShadowCasterCullMode;

            settings.enableDynamicShadowOverlay = settings.cached.enableDynamicShadowOverlay;
            settings.staticCasterLayerMask = settings.cached.staticCasterLayerMask;
            settings.dynamicCasterLayerMask = settings.cached.dynamicCasterLayerMask;
            settings.cameraPositionInvalidationThreshold = settings.cached.cameraPositionInvalidationThreshold;
            settings.cameraRotationInvalidationThreshold = settings.cached.cameraRotationInvalidationThreshold;
            settings.lightDirectionInvalidationThreshold = settings.cached.lightDirectionInvalidationThreshold;

            settings.serializedVersion = MainLightShadowSettings.CurrentSerializedVersion;
        }

#if UNITY_EDITOR
        private void TryLoadLegacyMainLightShadowSettingsFromAssetFile()
        {
            if (mainLightShadows == null)
            {
                return;
            }

            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            string fullPath = Path.Combine(projectRoot, assetPath);
            if (!File.Exists(fullPath))
            {
                return;
            }

            string fileText = File.ReadAllText(fullPath);
            bool hasNewStructuredData = fileText.Contains("    toggles:") || fileText.Contains("    cached:");
            bool hasLegacyMainLightShadowData = fileText.Contains("    mainLightShadowDistance:")
                || fileText.Contains("    enableCachedMainLightShadows:")
                || fileText.Contains("    enableDynamicShadowOverlay:");
            if (hasNewStructuredData || !hasLegacyMainLightShadowData)
            {
                return;
            }

            mainLightShadows.serializedVersion = 0;
            string[] lines = fileText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            bool inMainLightShadows = false;
            bool pendingStaticMask = false;
            bool pendingDynamicMask = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (!inMainLightShadows)
                {
                    if (trimmed == "mainLightShadows:")
                    {
                        inMainLightShadows = true;
                    }

                    continue;
                }

                if (!line.StartsWith("    "))
                {
                    break;
                }

                if (TryReadBool(trimmed, "enableMainLightShadows:", out bool enableMainLightShadows))
                {
                    mainLightShadows.enableMainLightShadows = enableMainLightShadows;
                    continue;
                }

                if (TryReadBool(trimmed, "enableCachedMainLightShadows:", out bool enableCachedMainLightShadows))
                {
                    mainLightShadows.enableCachedMainLightShadows = enableCachedMainLightShadows;
                    continue;
                }

                if (TryReadInt(trimmed, "shadowMode:", out int shadowMode))
                {
                    mainLightShadows.shadowMode = (MainLightShadowMode)shadowMode;
                    continue;
                }

                if (TryReadBool(trimmed, "cachedShadowSettingsMigrated:", out bool cachedShadowSettingsMigrated))
                {
                    mainLightShadows.cachedShadowSettingsMigrated = cachedShadowSettingsMigrated;
                    continue;
                }

                if (TryReadInt(trimmed, "mainLightShadowResolution:", out int mainLightShadowResolution))
                {
                    mainLightShadows.mainLightShadowResolution = mainLightShadowResolution;
                    continue;
                }

                if (TryReadFloat(trimmed, "mainLightShadowDistance:", out float mainLightShadowDistance))
                {
                    mainLightShadows.mainLightShadowDistance = mainLightShadowDistance;
                    continue;
                }

                if (TryReadInt(trimmed, "mainLightShadowCascadeCount:", out int mainLightShadowCascadeCount))
                {
                    mainLightShadows.mainLightShadowCascadeCount = mainLightShadowCascadeCount;
                    continue;
                }

                if (TryReadFloat(trimmed, "mainLightShadowCascadeSplit:", out float mainLightShadowCascadeSplit))
                {
                    mainLightShadows.mainLightShadowCascadeSplit = mainLightShadowCascadeSplit;
                    continue;
                }

                if (TryReadFloat(trimmed, "mainLightShadowBias:", out float mainLightShadowBias))
                {
                    mainLightShadows.mainLightShadowBias = mainLightShadowBias;
                    continue;
                }

                if (TryReadFloat(trimmed, "mainLightShadowNormalBias:", out float mainLightShadowNormalBias))
                {
                    mainLightShadows.mainLightShadowNormalBias = mainLightShadowNormalBias;
                    continue;
                }

                if (TryReadInt(trimmed, "mainLightShadowFilterMode:", out int mainLightShadowFilterMode))
                {
                    mainLightShadows.mainLightShadowFilterMode = (MainLightShadowFilterMode)mainLightShadowFilterMode;
                    continue;
                }

                if (TryReadFloat(trimmed, "mainLightShadowFilterRadius:", out float mainLightShadowFilterRadius))
                {
                    mainLightShadows.mainLightShadowFilterRadius = mainLightShadowFilterRadius;
                    continue;
                }

                if (TryReadFloat(trimmed, "mainLightShadowReceiverDepthBias:", out float mainLightShadowReceiverDepthBias))
                {
                    mainLightShadows.mainLightShadowReceiverDepthBias = mainLightShadowReceiverDepthBias;
                    continue;
                }

                if (TryReadFloat(trimmed, "mainLightShadowReceiverNormalBias:", out float mainLightShadowReceiverNormalBias))
                {
                    mainLightShadows.mainLightShadowReceiverNormalBias = mainLightShadowReceiverNormalBias;
                    continue;
                }

                if (TryReadInt(trimmed, "mainLightShadowCasterCullMode:", out int mainLightShadowCasterCullMode))
                {
                    mainLightShadows.mainLightShadowCasterCullMode = (MainLightShadowCasterCullMode)mainLightShadowCasterCullMode;
                    continue;
                }

                if (TryReadBool(trimmed, "enableDynamicShadowOverlay:", out bool enableDynamicShadowOverlay))
                {
                    mainLightShadows.enableDynamicShadowOverlay = enableDynamicShadowOverlay;
                    continue;
                }

                if (trimmed.StartsWith("staticCasterLayerMask:"))
                {
                    pendingStaticMask = true;
                    pendingDynamicMask = false;
                    continue;
                }

                if (trimmed.StartsWith("dynamicCasterLayerMask:"))
                {
                    pendingStaticMask = false;
                    pendingDynamicMask = true;
                    continue;
                }

                if (TryReadFloat(trimmed, "cameraPositionInvalidationThreshold:", out float cameraPositionInvalidationThreshold))
                {
                    mainLightShadows.cameraPositionInvalidationThreshold = cameraPositionInvalidationThreshold;
                    continue;
                }

                if (TryReadFloat(trimmed, "cameraRotationInvalidationThreshold:", out float cameraRotationInvalidationThreshold))
                {
                    mainLightShadows.cameraRotationInvalidationThreshold = cameraRotationInvalidationThreshold;
                    continue;
                }

                if (TryReadFloat(trimmed, "lightDirectionInvalidationThreshold:", out float lightDirectionInvalidationThreshold))
                {
                    mainLightShadows.lightDirectionInvalidationThreshold = lightDirectionInvalidationThreshold;
                    continue;
                }

                if ((pendingStaticMask || pendingDynamicMask)
                    && TryReadInt(trimmed, "m_Bits:", out int layerMaskValue))
                {
                    if (pendingStaticMask)
                    {
                        mainLightShadows.staticCasterLayerMask = layerMaskValue;
                        pendingStaticMask = false;
                    }
                    else
                    {
                        mainLightShadows.dynamicCasterLayerMask = layerMaskValue;
                        pendingDynamicMask = false;
                    }
                }
            }
        }

        private static bool TryReadBool(string line, string prefix, out bool value)
        {
            if (TryReadValue(line, prefix, out string rawValue))
            {
                value = rawValue == "1" || rawValue.Equals("true", System.StringComparison.OrdinalIgnoreCase);
                return true;
            }

            value = false;
            return false;
        }

        private static bool TryReadInt(string line, string prefix, out int value)
        {
            if (TryReadValue(line, prefix, out string rawValue)
                && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        private static bool TryReadFloat(string line, string prefix, out float value)
        {
            if (TryReadValue(line, prefix, out string rawValue)
                && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = 0f;
            return false;
        }

        private static bool TryReadValue(string line, string prefix, out string value)
        {
            if (line.StartsWith(prefix))
            {
                value = line.Substring(prefix.Length).Trim();
                return true;
            }

            value = null;
            return false;
        }
#endif
    }
}
