using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [CreateAssetMenu(menuName = "Rendering/NWRP Features/Outline Feature")]
    public sealed class OutlineFeature : NWRPFeature
    {
        private DrawOutlinePass _outlinePass;

        protected override void Create()
        {
            _outlinePass = new DrawOutlinePass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            renderer.EnqueuePass(_outlinePass);
        }
    }
}
