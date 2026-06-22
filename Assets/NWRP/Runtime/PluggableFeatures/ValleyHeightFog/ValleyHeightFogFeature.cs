using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Valley Height Fog",
        MenuPath = "Post Processing/Valley Height Fog",
        VolumeDriven = true,
        SortOrder = 220)]
    public sealed class ValleyHeightFogFeature : NWRPFeature
    {
        private ValleyHeightFogPass _valleyHeightFogPass;

        protected override void Create()
        {
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
            requirements.Merge(DepthTextureFeature.GetFrameTargetRequirements(
                DepthTextureFeature.GetCopyMode(ref frameData),
                frameData.camera));
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (!IsActive(ref frameData))
            {
                return;
            }

            _valleyHeightFogPass ??= new ValleyHeightFogPass();
            renderer.EnqueuePass(_valleyHeightFogPass);
        }

        internal static bool IsActive(ref NWRPFrameData frameData)
        {
            return PostProcessFeature.IsPostProcessingEnabled(ref frameData)
                && frameData.valleyHeightFogActive;
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
            _valleyHeightFogPass?.Dispose();
            _valleyHeightFogPass = null;
        }
    }
}
