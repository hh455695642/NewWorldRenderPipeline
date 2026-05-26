using System.Collections.Generic;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Renderer Data")]
    public sealed class NWRPRendererData : ScriptableObject
    {
        public NewWorldRenderPipelineAsset.FeatureSettings featureSettings =
            new NewWorldRenderPipelineAsset.FeatureSettings();

        [System.NonSerialized]
        private OutlineFeature _runtimeOutlineFeature;

        [System.NonSerialized]
        private OpaqueTextureFeature _runtimeOpaqueTextureFeature;

        [System.NonSerialized]
        private DepthTextureFeature _runtimeDepthTextureFeature;

        [System.NonSerialized]
        private NWRPFogFeature _runtimeFogFeature;

        [System.NonSerialized]
        private PostProcessFeature _runtimePostProcessFeature;

        [System.NonSerialized]
        private VegetationIndirectShadowFeature _runtimeVegetationIndirectShadowFeature;

        private NewWorldRenderPipelineAsset.FeatureSettings FeatureSettingsData
        {
            get
            {
                EnsureFeatureSettings();
                return featureSettings;
            }
        }

        public List<NWRPFeature> Features => FeatureSettingsData.features;

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
                featureSettings.features.Add(source.features[i]);
            }
        }

        internal OutlineFeature GetOrCreateOutlineFeature()
        {
            if (_runtimeOutlineFeature != null)
            {
                return _runtimeOutlineFeature;
            }

            _runtimeOutlineFeature = ScriptableObject.CreateInstance<OutlineFeature>();
            _runtimeOutlineFeature.hideFlags = HideFlags.HideAndDontSave;
            _runtimeOutlineFeature.name = $"{name} Runtime OutlineFeature";
            return _runtimeOutlineFeature;
        }

        internal OpaqueTextureFeature GetOrCreateOpaqueTextureFeature()
        {
            if (_runtimeOpaqueTextureFeature != null)
            {
                return _runtimeOpaqueTextureFeature;
            }

            _runtimeOpaqueTextureFeature =
                ScriptableObject.CreateInstance<OpaqueTextureFeature>();
            _runtimeOpaqueTextureFeature.hideFlags = HideFlags.HideAndDontSave;
            _runtimeOpaqueTextureFeature.name = $"{name} Runtime OpaqueTextureFeature";
            return _runtimeOpaqueTextureFeature;
        }

        internal DepthTextureFeature GetOrCreateDepthTextureFeature()
        {
            if (_runtimeDepthTextureFeature != null)
            {
                return _runtimeDepthTextureFeature;
            }

            _runtimeDepthTextureFeature =
                ScriptableObject.CreateInstance<DepthTextureFeature>();
            _runtimeDepthTextureFeature.hideFlags = HideFlags.HideAndDontSave;
            _runtimeDepthTextureFeature.name = $"{name} Runtime DepthTextureFeature";
            return _runtimeDepthTextureFeature;
        }

        internal NWRPFogFeature GetOrCreateFogFeature()
        {
            if (_runtimeFogFeature != null)
            {
                return _runtimeFogFeature;
            }

            _runtimeFogFeature = ScriptableObject.CreateInstance<NWRPFogFeature>();
            _runtimeFogFeature.hideFlags = HideFlags.HideAndDontSave;
            _runtimeFogFeature.name = $"{name} Runtime FogFeature";
            return _runtimeFogFeature;
        }

        internal PostProcessFeature GetOrCreatePostProcessFeature()
        {
            if (_runtimePostProcessFeature != null)
            {
                return _runtimePostProcessFeature;
            }

            _runtimePostProcessFeature =
                ScriptableObject.CreateInstance<PostProcessFeature>();
            _runtimePostProcessFeature.hideFlags = HideFlags.HideAndDontSave;
            _runtimePostProcessFeature.name = $"{name} Runtime PostProcessFeature";
            return _runtimePostProcessFeature;
        }

        internal VegetationIndirectShadowFeature GetOrCreateVegetationIndirectShadowFeature()
        {
            if (_runtimeVegetationIndirectShadowFeature != null)
            {
                return _runtimeVegetationIndirectShadowFeature;
            }

            _runtimeVegetationIndirectShadowFeature =
                ScriptableObject.CreateInstance<VegetationIndirectShadowFeature>();
            _runtimeVegetationIndirectShadowFeature.hideFlags = HideFlags.HideAndDontSave;
            _runtimeVegetationIndirectShadowFeature.name =
                $"{name} Runtime VegetationIndirectShadowFeature";
            return _runtimeVegetationIndirectShadowFeature;
        }

        internal void DisposeRuntimeFeatures()
        {
            DisposeRuntimeFeature(ref _runtimeOutlineFeature);
            DisposeRuntimeFeature(ref _runtimeOpaqueTextureFeature);
            DisposeRuntimeFeature(ref _runtimeDepthTextureFeature);
            DisposeRuntimeFeature(ref _runtimeFogFeature);
            DisposeRuntimeFeature(ref _runtimePostProcessFeature);
            DisposeRuntimeFeature(ref _runtimeVegetationIndirectShadowFeature);
        }

        private void EnsureFeatureSettings()
        {
            if (featureSettings == null)
            {
                featureSettings = new NewWorldRenderPipelineAsset.FeatureSettings();
            }

            featureSettings.EnsureInitialized();
        }

        private static void DisposeRuntimeFeature<T>(ref T feature)
            where T : ScriptableObject
        {
            if (feature == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(feature);
            }
            else
            {
                DestroyImmediate(feature);
            }

            feature = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureFeatureSettings();
        }
#endif
    }
}
