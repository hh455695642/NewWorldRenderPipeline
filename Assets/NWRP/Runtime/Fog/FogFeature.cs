using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Fog",
        MenuPath = "Environment/Fog",
        ShowInAddMenu = false,
        SortOrder = 100)]
    public sealed class FogFeature : NWRPFeature, INWRPSerializedFeatureStateProvider
    {
        private SetupFogPass _setupFogPass;

        bool INWRPSerializedFeatureStateProvider.DeferSerializedPasses => false;

        protected override void Create()
        {
            _setupFogPass = new SetupFogPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            _setupFogPass ??= new SetupFogPass();
            renderer.EnqueuePass(_setupFogPass);
        }

        void INWRPSerializedFeatureStateProvider.RecordSerializedFeatureState(
            ref NWRPSerializedFeatureState state)
        {
            state.hasFog = true;
        }

        private void OnDisable()
        {
            _setupFogPass = null;
        }
    }
}
