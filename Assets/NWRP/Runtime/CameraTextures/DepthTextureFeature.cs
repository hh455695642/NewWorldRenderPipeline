using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Depth Texture Feature")]
    public sealed class DepthTextureFeature : NWRPFeature
    {
        private CopyDepthPass _copyDepthPass;
        private DepthPrepass _depthPrepass;

        protected override void Create()
        {
            _copyDepthPass = new CopyDepthPass();
            _depthPrepass = new DepthPrepass();
        }

        public override bool TryGetFrameTargetRequirements(
            ref NWRPFrameData frameData,
            out NWRPFrameTargetRequirements requirements)
        {
            requirements = default;
            if (frameData.asset == null || !frameData.asset.EnableDepthTexture)
            {
                return false;
            }

            requirements = GetFrameTargetRequirements(
                frameData.asset.DepthTextureCopyModeSetting,
                frameData.camera);
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (frameData.asset == null || !frameData.asset.EnableDepthTexture)
            {
                return;
            }

            NewWorldRenderPipelineAsset.DepthTextureCopyMode copyMode =
                frameData.asset.DepthTextureCopyModeSetting;
            if (ShouldUseDepthPrepass(copyMode, frameData.camera))
            {
                _depthPrepass ??= new DepthPrepass();
                renderer.EnqueuePass(_depthPrepass);
                return;
            }

            _copyDepthPass ??= new CopyDepthPass();
            _copyDepthPass.Setup(GetCopyDepthPassEvent(copyMode));
            renderer.EnqueuePass(_copyDepthPass);
        }

        internal static NWRPFrameTargetRequirements GetFrameTargetRequirements(
            NewWorldRenderPipelineAsset.DepthTextureCopyMode copyMode,
            Camera camera)
        {
            bool useDepthPrepass = ShouldUseDepthPrepass(copyMode, camera);
            return new NWRPFrameTargetRequirements
            {
                requiresDepthTexture = true,
                requiresDepthTextureCopy = !useDepthPrepass,
                requiresDepthTexturePrepass = useDepthPrepass,
                requiresIntermediateDepth = !useDepthPrepass
            };
        }

        internal static bool ShouldUseDepthPrepass(
            NewWorldRenderPipelineAsset.DepthTextureCopyMode copyMode,
            Camera camera)
        {
            return copyMode == NewWorldRenderPipelineAsset.DepthTextureCopyMode.ForcePrepass
                || !CopyDepthPass.CanCopyDepth(camera);
        }

        private static NWRPPassEvent GetCopyDepthPassEvent(
            NewWorldRenderPipelineAsset.DepthTextureCopyMode copyMode)
        {
            return copyMode == NewWorldRenderPipelineAsset.DepthTextureCopyMode.AfterTransparents
                ? NWRPPassEvent.AfterTransparent
                : NWRPPassEvent.BeforeTransparent;
        }

        private void OnDisable()
        {
            _copyDepthPass?.Dispose();
            _copyDepthPass = null;
            _depthPrepass = null;
        }
    }
}
