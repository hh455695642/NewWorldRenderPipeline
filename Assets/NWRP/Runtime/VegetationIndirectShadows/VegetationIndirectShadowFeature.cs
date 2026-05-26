using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    public sealed class VegetationIndirectShadowFeature : NWRPFeature
    {
        private VegetationIndirectShadowPass _shadowPass;

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
    }
}
