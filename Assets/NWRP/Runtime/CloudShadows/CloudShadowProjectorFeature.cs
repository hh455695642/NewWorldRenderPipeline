using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
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
            if (!CanRun(ref frameData))
            {
                return false;
            }

            requirements.requiresIntermediateColor = true;
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (!CanRun(ref frameData))
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
                && frameData.rendererData != null
                && frameData.rendererData.EnableDepthTexture;
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
