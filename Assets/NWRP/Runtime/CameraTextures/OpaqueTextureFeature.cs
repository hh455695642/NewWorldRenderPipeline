using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Opaque Texture",
        MenuPath = "Camera/Opaque Texture",
        ShowInAddMenu = false,
        SortOrder = 50)]
    public sealed class OpaqueTextureFeature : NWRPFeature, INWRPSerializedFeatureStateProvider
    {
        private CopyColorPass _copyColorPass;

        bool INWRPSerializedFeatureStateProvider.DeferSerializedPasses => false;

        protected override void Create()
        {
            _copyColorPass = new CopyColorPass();
        }

        public override bool TryGetFrameTargetRequirements(
            ref NWRPFrameData frameData,
            out NWRPFrameTargetRequirements requirements)
        {
            requirements = default;
            if (frameData.rendererData == null || !frameData.rendererData.EnableOpaqueTexture)
            {
                return false;
            }

            requirements.requiresIntermediateColor = true;
            requirements.requiresIntermediateDepth = true;
            requirements.requiresOpaqueTexture = true;
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (frameData.rendererData == null
                || !frameData.rendererData.EnableOpaqueTexture)
            {
                return;
            }

            if (_copyColorPass == null)
            {
                _copyColorPass = new CopyColorPass();
            }

            renderer.EnqueuePass(_copyColorPass);
        }

        void INWRPSerializedFeatureStateProvider.RecordSerializedFeatureState(
            ref NWRPSerializedFeatureState state)
        {
            state.hasOpaqueTexture = true;
        }

        private void OnDisable()
        {
            _copyColorPass?.Dispose();
            _copyColorPass = null;
        }
    }
}
