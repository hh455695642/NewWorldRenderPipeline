using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class MainLightShadowCasterDebugOverlayPass : NWRPPass
    {
        private static readonly ShaderTagId s_ShadowCasterTagId = new ShaderTagId("ShadowCaster");
        private static readonly Color s_StaticCasterColor = new Color(0.20f, 1.00f, 0.30f, 0.90f);
        private static readonly Color s_DynamicCasterColor = new Color(0.20f, 0.45f, 1.00f, 0.90f);

        private readonly Shader _overlayShader;

        private Material _staticCasterMaterial;
        private Material _dynamicCasterMaterial;

        public MainLightShadowCasterDebugOverlayPass()
            : base(NWRPPassEvent.DebugOverlay, "Overlay Main Light Shadow Caster Tint")
        {
            _overlayShader = Shader.Find("Hidden/NWRP/MainLightShadowCasterTint");
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!MainLightShadowPassUtils.ShouldRenderShadowDebugView(frameData.asset, frameData.camera))
            {
                return;
            }

            if (!EnsureMaterials())
            {
                return;
            }

            int staticCasterLayerMask = MainLightShadowPassUtils.GetStaticCasterLayerMaskValue(frameData.asset);
            int dynamicCasterLayerMask = frameData.asset != null
                ? frameData.asset.DynamicCasterLayerMask.value
                : 0;

            DrawCasters(
                ref frameData,
                staticCasterLayerMask,
                _staticCasterMaterial,
                MainLightShadowPassUtils.DebugStaticCasterTintSampler);
            DrawCasters(
                ref frameData,
                dynamicCasterLayerMask,
                _dynamicCasterMaterial,
                MainLightShadowPassUtils.DebugDynamicCasterTintSampler);
        }

        public void Dispose()
        {
            DestroyMaterial(ref _staticCasterMaterial);
            DestroyMaterial(ref _dynamicCasterMaterial);
        }

        private bool EnsureMaterials()
        {
            if (_overlayShader == null)
            {
                return false;
            }

            if (_staticCasterMaterial == null)
            {
                _staticCasterMaterial = CreateMaterial("NWRP_MainLightShadowStaticCasterDebug", s_StaticCasterColor);
            }

            if (_dynamicCasterMaterial == null)
            {
                _dynamicCasterMaterial = CreateMaterial("NWRP_MainLightShadowDynamicCasterDebug", s_DynamicCasterColor);
            }

            return _staticCasterMaterial != null && _dynamicCasterMaterial != null;
        }

        private void DrawCasters(
            ref NWRPFrameData frameData,
            int layerMask,
            Material material,
            ProfilingSampler sampler)
        {
            if (layerMask == 0 || material == null)
            {
                return;
            }

            using (new ProfilingScope(frameData.cmd, sampler))
            {
                MainLightShadowPassUtils.ExecuteBuffer(ref frameData);

                SortingSettings sortingSettings = new SortingSettings(frameData.camera)
                {
                    criteria = SortingCriteria.None
                };

                DrawingSettings drawingSettings = new DrawingSettings(s_ShadowCasterTagId, sortingSettings)
                {
                    enableDynamicBatching = false,
                    enableInstancing = frameData.asset != null && frameData.asset.useGPUInstancing,
                    overrideMaterial = material,
                    overrideMaterialPassIndex = 0
                };

                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMask);
                frameData.context.DrawRenderers(
                    frameData.cullingResults,
                    ref drawingSettings,
                    ref filteringSettings
                );
            }

            MainLightShadowPassUtils.ExecuteBuffer(ref frameData);
        }

        private Material CreateMaterial(string name, Color baseColor)
        {
            Material material = new Material(_overlayShader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_BaseColor", baseColor);
            return material;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }

            material = null;
        }
    }
}
