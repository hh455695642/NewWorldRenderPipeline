using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// NewWorld 渲染管线资源配置。
    /// 在 Unity 编辑器中通过 Create → Rendering → New World Render Pipeline Asset 创建，
    /// 然后拖入 ProjectSettings → Graphics → Scriptable Render Pipeline Settings 生效。
    /// </summary>
    [CreateAssetMenu(menuName = "Rendering/New World Render Pipeline Asset")]
    public class NewWorldRenderPipelineAsset : RenderPipelineAsset
    {
        [System.Serializable]
        public sealed class FeatureSettings
        {
            public List<NWRPFeature> features = new List<NWRPFeature>();
        }

        [Header("General")]
        [Tooltip("是否启用 SRP Batcher（合批优化）")]
        public bool useSRPBatcher = true;

        [Tooltip("是否启用 GPU Instancing")]
        public bool useGPUInstancing = true;

        [Header("Lighting")]
        [Tooltip("每个物体支持的最大附加光源数量（点光源/聚光灯）")]
        [Range(0, 8)]
        public int maxAdditionalLights = 4;

        [Header("Feature Settings")]
        public FeatureSettings featureSettings = new FeatureSettings();

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

        protected override RenderPipeline CreatePipeline()
        {
            return new NewWorldRenderPipeline(this);
        }
    }
}
