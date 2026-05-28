using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Valley Height Fog Overlay",
        MenuPath = "Post Processing/Valley Height Fog Overlay",
        VolumeDriven = true,
        SortOrder = 230)]
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
