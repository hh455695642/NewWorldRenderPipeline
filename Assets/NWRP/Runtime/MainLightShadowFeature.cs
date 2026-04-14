using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Main Light Shadow Feature")]
    public sealed class MainLightShadowFeature : NWRPFeature
    {
        private MainLightShadowCasterPass _mainLightShadowPass;

        protected override void Create()
        {
            _mainLightShadowPass = new MainLightShadowCasterPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (_mainLightShadowPass == null)
            {
                return;
            }

            renderer.EnqueuePass(_mainLightShadowPass);
        }

        private void OnDisable()
        {
            if (_mainLightShadowPass != null)
            {
                _mainLightShadowPass.Dispose();
            }
        }
    }
}
