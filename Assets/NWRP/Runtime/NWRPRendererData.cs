using System.Collections.Generic;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Renderer Data")]
    public sealed class NWRPRendererData : ScriptableObject
    {
        [System.Serializable]
        public sealed class RendererFilteringSettings
        {
            [InspectorName("Opaque Layer Mask")]
            [Tooltip("Controls which opaque layers this renderer draws.")]
            public LayerMask opaqueLayerMask = ~0;

            [InspectorName("Transparent Layer Mask")]
            [Tooltip("Controls which transparent layers this renderer draws.")]
            public LayerMask transparentLayerMask = ~0;
        }

        public RendererFilteringSettings filtering =
            new RendererFilteringSettings();

        public NewWorldRenderPipelineAsset.FeatureSettings featureSettings =
            new NewWorldRenderPipelineAsset.FeatureSettings();

        [System.NonSerialized]
        private NWRPRuntimeFeatureStore _runtimeFeatures;

        private NewWorldRenderPipelineAsset.FeatureSettings FeatureSettingsData
        {
            get
            {
                EnsureFeatureSettings();
                return featureSettings;
            }
        }

        private RendererFilteringSettings FilteringData
        {
            get
            {
                EnsureFilteringSettings();
                return filtering;
            }
        }

        public List<NWRPFeature> Features => FeatureSettingsData.features;

        public LayerMask OpaqueLayerMask => FilteringData.opaqueLayerMask;

        public LayerMask TransparentLayerMask => FilteringData.transparentLayerMask;

        public bool EnableOutline => FeatureSettingsData.outline.enableOutline;

        public bool EnableOpaqueTexture => FeatureSettingsData.opaqueTexture.enableOpaqueTexture;

        public bool EnableDepthTexture => FeatureSettingsData.depthTexture.enableDepthTexture;

        public NewWorldRenderPipelineAsset.DepthTextureCopyMode DepthTextureCopyModeSetting =>
            FeatureSettingsData.depthTexture.copyDepthMode;

        public bool EnableVegetationIndirectTreeShadows =>
            FeatureSettingsData.vegetationIndirectShadows.enableVegetationIndirectTreeShadows;

        public void CopyFeatureSettingsFrom(
            NewWorldRenderPipelineAsset.FeatureSettings source)
        {
            EnsureFeatureSettings();
            if (source == null)
            {
                return;
            }

            source.EnsureInitialized();
            featureSettings.outline.enableOutline =
                source.outline.enableOutline;
            featureSettings.opaqueTexture.enableOpaqueTexture =
                source.opaqueTexture.enableOpaqueTexture;
            featureSettings.depthTexture.enableDepthTexture =
                source.depthTexture.enableDepthTexture;
            featureSettings.depthTexture.copyDepthMode =
                source.depthTexture.copyDepthMode;
            featureSettings.vegetationIndirectShadows
                .enableVegetationIndirectTreeShadows =
                source.vegetationIndirectShadows.enableVegetationIndirectTreeShadows;

            featureSettings.features.Clear();
            for (int i = 0; i < source.features.Count; i++)
            {
                NWRPFeature feature = source.features[i];
                if (feature != null)
                {
                    featureSettings.features.Add(feature);
                }
            }
        }

        internal T GetOrCreateRuntimeFeature<T>()
            where T : NWRPFeature
        {
            _runtimeFeatures ??= new NWRPRuntimeFeatureStore(name);
            return _runtimeFeatures.GetOrCreate<T>();
        }

        internal void DisposeRuntimeFeatures()
        {
            _runtimeFeatures?.DisposeAll();
            _runtimeFeatures = null;
        }

        private void EnsureFeatureSettings()
        {
            if (featureSettings == null)
            {
                featureSettings = new NewWorldRenderPipelineAsset.FeatureSettings();
            }

            featureSettings.EnsureInitialized();
        }

        private void EnsureFilteringSettings()
        {
            if (filtering == null)
            {
                filtering = new RendererFilteringSettings();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureFilteringSettings();
            EnsureFeatureSettings();
            FeatureSettingsData.RemoveNullFeatures();
        }
#endif
    }
}
