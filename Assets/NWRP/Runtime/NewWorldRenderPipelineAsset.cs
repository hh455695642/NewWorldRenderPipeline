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
        [System.Serializable]
        public sealed class FeatureSettings
        {
            public List<NWRPFeature> features = new List<NWRPFeature>();
        }

        [System.Serializable]
        public sealed class MainLightShadowSettings
        {
            public bool enableMainLightShadows = true;

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

        public bool EnableMainLightShadows => mainLightShadows != null && mainLightShadows.enableMainLightShadows;
        public int MainLightShadowResolution => mainLightShadows != null ? mainLightShadows.mainLightShadowResolution : 2048;
        public float MainLightShadowDistance => mainLightShadows != null ? mainLightShadows.mainLightShadowDistance : 80f;
        public int MainLightShadowCascadeCount => mainLightShadows != null ? mainLightShadows.mainLightShadowCascadeCount : 2;
        public float MainLightShadowCascadeSplit => mainLightShadows != null ? mainLightShadows.mainLightShadowCascadeSplit : 0.35f;
        public float MainLightShadowBias => mainLightShadows != null ? mainLightShadows.mainLightShadowBias : 0.7f;
        public float MainLightShadowNormalBias => mainLightShadows != null ? mainLightShadows.mainLightShadowNormalBias : 1.0f;

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
            if (mainLightShadows == null)
            {
                mainLightShadows = new MainLightShadowSettings();
            }

            mainLightShadows.mainLightShadowResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(mainLightShadows.mainLightShadowResolution, 256, 4096)
            );
            mainLightShadows.mainLightShadowCascadeCount = Mathf.Clamp(mainLightShadows.mainLightShadowCascadeCount, 1, 2);
            mainLightShadows.mainLightShadowCascadeSplit = Mathf.Clamp(mainLightShadows.mainLightShadowCascadeSplit, 0.05f, 0.95f);
            mainLightShadows.mainLightShadowDistance = Mathf.Max(0f, mainLightShadows.mainLightShadowDistance);
            mainLightShadows.mainLightShadowBias = Mathf.Max(0f, mainLightShadows.mainLightShadowBias);
            mainLightShadows.mainLightShadowNormalBias = Mathf.Max(0f, mainLightShadows.mainLightShadowNormalBias);
        }
#endif
    }
}
