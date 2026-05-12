using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Post Process Feature")]
    public sealed class PostProcessFeature : NWRPFeature
    {
        private PostProcessPass _postProcessPass;

        protected override void Create()
        {
            _postProcessPass = new PostProcessPass();
        }

        public override bool TryGetFrameTargetRequirements(
            ref NWRPFrameData frameData,
            out NWRPFrameTargetRequirements requirements)
        {
            requirements = default;
            if (!HasAnyActivePostProcess(ref frameData))
            {
                return false;
            }

            // PostProcess v1 only has final tonemapping, but the framework always renders
            // scene color into an intermediate HDR target first. The final PostProcess pass
            // writes directly back to the camera/backbuffer target and marks it presented.
            requirements.requiresIntermediateColor = true;
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (!HasAnyActivePostProcess(ref frameData))
            {
                return;
            }

            if (_postProcessPass == null)
            {
                _postProcessPass = new PostProcessPass();
            }

            renderer.EnqueuePass(_postProcessPass);
        }

        internal static bool HasAnyActivePostProcess(ref NWRPFrameData frameData)
        {
            // Future effects should be OR'ed here, for example Bloom/LUT/Sharpen.
            // NWRPRenderer only sees one PostProcess pass regardless of how many
            // internal effects are active.
            return IsTonemappingActive(ref frameData);
        }

        internal static bool IsTonemappingActive(ref NWRPFrameData frameData)
        {
            return IsPostProcessingEnabled(ref frameData) && frameData.tonemappingActive;
        }

        internal static bool IsPostProcessingEnabled(ref NWRPFrameData frameData)
        {
            return frameData.asset != null
                && frameData.asset.SupportsPostProcessing
                && frameData.postProcessingEnabled;
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
            _postProcessPass?.Dispose();
            _postProcessPass = null;
        }
    }
}
