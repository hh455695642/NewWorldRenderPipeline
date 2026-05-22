using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Opaque Texture Feature")]
    public sealed class OpaqueTextureFeature : NWRPFeature
    {
        private CopyColorPass _copyColorPass;

        protected override void Create()
        {
            _copyColorPass = new CopyColorPass();
        }

        public override bool TryGetFrameTargetRequirements(
            ref NWRPFrameData frameData,
            out NWRPFrameTargetRequirements requirements)
        {
            requirements = default;
            if (frameData.asset == null || !frameData.asset.EnableOpaqueTexture)
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
            if (frameData.asset == null
                || !frameData.asset.EnableOpaqueTexture)
            {
                return;
            }

            if (_copyColorPass == null)
            {
                _copyColorPass = new CopyColorPass();
            }

            renderer.EnqueuePass(_copyColorPass);
        }

        private void OnDisable()
        {
            _copyColorPass?.Dispose();
            _copyColorPass = null;
        }
    }
}
