using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Outline",
        MenuPath = "Rendering/Outline",
        ShowInAddMenu = false,
        SortOrder = 70)]
    public sealed class OutlineFeature : NWRPFeature, INWRPSerializedFeatureStateProvider
    {
        private DrawOutlinePass _outlinePass;

        bool INWRPSerializedFeatureStateProvider.DeferSerializedPasses => false;

        protected override void Create()
        {
            _outlinePass = new DrawOutlinePass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            renderer.EnqueuePass(_outlinePass);
        }

        void INWRPSerializedFeatureStateProvider.RecordSerializedFeatureState(
            ref NWRPSerializedFeatureState state)
        {
            state.hasOutline = true;
        }
    }
}
