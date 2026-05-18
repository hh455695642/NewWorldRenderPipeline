using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Vegetation Indirect Shadow Feature")]
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
                || !frameData.asset.EnableMainLightShadows
                || !frameData.asset.EnableVegetationIndirectTreeShadows)
            {
                return;
            }

            renderer.EnqueuePass(_shadowPass);
        }
    }
}
