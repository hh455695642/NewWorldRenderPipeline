using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Cloud Shadow Projector",
        MenuPath = "Environment/Cloud Shadow Projector",
        VolumeDriven = true,
        SortOrder = 150)]
    public sealed class CloudShadowProjectorFeature : NWRPFeature
    {
        private CloudShadowProjectorPass _cloudShadowProjectorPass;

        protected override void Create()
        {
            _cloudShadowProjectorPass = new CloudShadowProjectorPass();
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

            if (!DepthTextureFeature.AllowsFeatureDepthTextureRequest(ref frameData))
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
            if (!CanSchedule(ref frameData))
            {
                return;
            }

            _cloudShadowProjectorPass ??= new CloudShadowProjectorPass();
            renderer.EnqueuePass(_cloudShadowProjectorPass);
        }

        internal static bool IsActive(ref NWRPFrameData frameData)
        {
            return frameData.cloudShadowProjectorActive
                && frameData.cloudShadowProjector != null;
        }

        internal static bool CanRun(ref NWRPFrameData frameData)
        {
            return IsActive(ref frameData)
                && frameData.targets.hasCameraDepthTexture
                && frameData.targets.cameraDepthTextureHandle != null;
        }

        internal static bool CanSchedule(ref NWRPFrameData frameData)
        {
            return IsActive(ref frameData)
                && frameData.targets.hasCameraDepthTexture;
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
            _cloudShadowProjectorPass?.Dispose();
            _cloudShadowProjectorPass = null;
        }
    }
}
