using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Fog Feature")]
    public sealed class NWRPFogFeature : NWRPFeature
    {
        private SetupFogPass _setupFogPass;

        protected override void Create()
        {
            _setupFogPass = new SetupFogPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            _setupFogPass ??= new SetupFogPass();
            renderer.EnqueuePass(_setupFogPass);
        }

        private void OnDisable()
        {
            _setupFogPass = null;
        }
    }
}
