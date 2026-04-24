using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Additional Punctual Light Shadow Feature")]
    public sealed class AdditionalLightShadowFeature : NWRPFeature
    {
        private AdditionalLightShadowDisabledPass _disabledPass;
        private AdditionalLightShadowCasterPass _shadowPass;

        protected override void Create()
        {
            _disabledPass = new AdditionalLightShadowDisabledPass();
            _shadowPass = new AdditionalLightShadowCasterPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (_disabledPass == null || _shadowPass == null)
            {
                return;
            }

            if (frameData.asset == null || !frameData.asset.EnableAdditionalLightShadows)
            {
                renderer.EnqueuePass(_disabledPass);
                return;
            }

            renderer.EnqueuePass(_shadowPass);
        }

        private void OnDisable()
        {
            _shadowPass?.Dispose();
            _disabledPass = null;
            _shadowPass = null;
        }
    }
}
