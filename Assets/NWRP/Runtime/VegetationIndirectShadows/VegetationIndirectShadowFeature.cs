using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Vegetation Indirect Shadows",
        MenuPath = "Vegetation/Indirect Shadows",
        ShowInAddMenu = false,
        SortOrder = 80)]
    public sealed class VegetationIndirectShadowFeature : NWRPFeature, INWRPSerializedFeatureStateProvider
    {
        private VegetationIndirectShadowPass _shadowPass;

        bool INWRPSerializedFeatureStateProvider.DeferSerializedPasses => true;

        protected override void Create()
        {
            _shadowPass = new VegetationIndirectShadowPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (renderer == null
                || _shadowPass == null
                || frameData.asset == null
                || frameData.rendererData == null
                || !frameData.asset.EnableMainLightShadows
                || !frameData.rendererData.EnableVegetationIndirectTreeShadows)
            {
                return;
            }

            renderer.EnqueuePass(_shadowPass);
        }

        void INWRPSerializedFeatureStateProvider.RecordSerializedFeatureState(
            ref NWRPSerializedFeatureState state)
        {
            state.hasVegetationIndirectShadow = true;
        }
    }
}
