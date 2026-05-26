using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    public sealed class ValleyHeightFogOverlayFeature : NWRPFeature
    {
        private ValleyHeightFogOverlayPass _overlayPass;

        protected override void Create()
        {
            _overlayPass = new ValleyHeightFogOverlayPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (renderer == null
                || frameData.camera == null
                || frameData.camera.cameraType == CameraType.Preview)
            {
                return;
            }

            _overlayPass ??= new ValleyHeightFogOverlayPass();
            renderer.EnqueuePass(_overlayPass);
        }
    }
}
