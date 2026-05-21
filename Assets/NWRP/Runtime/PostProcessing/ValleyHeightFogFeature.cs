using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Valley Height Fog Feature")]
    public sealed class ValleyHeightFogFeature : NWRPFeature
    {
        private CopyDepthPass _copyDepthPass;
        private DepthPrepass _depthPrepass;
        private ValleyHeightFogPass _valleyHeightFogPass;

        protected override void Create()
        {
            _copyDepthPass = new CopyDepthPass();
            _depthPrepass = new DepthPrepass();
            _valleyHeightFogPass = new ValleyHeightFogPass();
        }

        public override bool TryGetFrameTargetRequirements(
            ref NWRPFrameData frameData,
            out NWRPFrameTargetRequirements requirements)
        {
            requirements = default;
            if (!IsActive(ref frameData))
            {
                return false;
            }

            requirements.requiresIntermediateColor = true;
            if (NeedsOwnEarlyDepthTexture(ref frameData))
            {
                requirements.Merge(DepthTextureFeature.GetFrameTargetRequirements(
                    NewWorldRenderPipelineAsset.DepthTextureCopyMode.AfterOpaques,
                    frameData.camera));
            }

            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (!IsActive(ref frameData))
            {
                return;
            }

            if (NeedsOwnEarlyDepthTexture(ref frameData))
            {
                if (DepthTextureFeature.ShouldUseDepthPrepass(
                        NewWorldRenderPipelineAsset.DepthTextureCopyMode.AfterOpaques,
                        frameData.camera))
                {
                    _depthPrepass ??= new DepthPrepass();
                    renderer.EnqueuePass(_depthPrepass);
                }
                else
                {
                    _copyDepthPass ??= new CopyDepthPass();
                    _copyDepthPass.Setup(NWRPPassEvent.BeforeTransparent);
                    renderer.EnqueuePass(_copyDepthPass);
                }
            }

            _valleyHeightFogPass ??= new ValleyHeightFogPass();
            renderer.EnqueuePass(_valleyHeightFogPass);
        }

        internal static bool IsActive(ref NWRPFrameData frameData)
        {
            return PostProcessFeature.IsPostProcessingEnabled(ref frameData)
                && frameData.valleyHeightFogActive;
        }

        private static bool NeedsOwnEarlyDepthTexture(ref NWRPFrameData frameData)
        {
            if (frameData.asset == null)
            {
                return false;
            }

            return !frameData.asset.EnableDepthTexture
                || frameData.asset.DepthTextureCopyModeSetting
                    == NewWorldRenderPipelineAsset.DepthTextureCopyMode.AfterTransparents;
        }

        private void OnDisable()
        {
            DisposePasses();
        }

        private void OnDestroy()
        {
            DisposePasses();
        }

        private void DisposePasses()
        {
            _copyDepthPass?.Dispose();
            _valleyHeightFogPass?.Dispose();
            _copyDepthPass = null;
            _depthPrepass = null;
            _valleyHeightFogPass = null;
        }
    }
}
