using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Depth Texture",
        MenuPath = "Camera/Depth Texture",
        ShowInAddMenu = false,
        SortOrder = 60)]
    public sealed class DepthTextureFeature : NWRPFeature, INWRPSerializedFeatureStateProvider
    {
        private CopyDepthPass _copyDepthPass;
        private DepthPrepass _depthPrepass;

        bool INWRPSerializedFeatureStateProvider.DeferSerializedPasses => false;

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
            if (frameData.rendererData == null || !frameData.rendererData.EnableDepthTexture)
            {
                return false;
            }

            requirements = GetFrameTargetRequirements(
                frameData.rendererData.DepthTextureCopyModeSetting,
                frameData.camera);
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (!ShouldEnqueueDepthTexturePass(ref frameData))
            {
                return;
            }

            NewWorldRenderPipelineAsset.DepthTextureCopyMode copyMode =
                GetCopyMode(ref frameData);
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

        void INWRPSerializedFeatureStateProvider.RecordSerializedFeatureState(
            ref NWRPSerializedFeatureState state)
        {
            state.hasDepthTexture = true;
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

        internal static NewWorldRenderPipelineAsset.DepthTextureCopyMode GetCopyMode(
            ref NWRPFrameData frameData)
        {
            return frameData.rendererData != null
                ? frameData.rendererData.DepthTextureCopyModeSetting
                : NewWorldRenderPipelineAsset.DepthTextureCopyMode.AfterOpaques;
        }

        private static bool ShouldEnqueueDepthTexturePass(ref NWRPFrameData frameData)
        {
            if (frameData.rendererData != null
                && frameData.rendererData.EnableDepthTexture)
            {
                return true;
            }

            return frameData.targets.hasCameraDepthTexture;
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
