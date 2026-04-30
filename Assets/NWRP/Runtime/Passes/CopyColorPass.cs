using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class CopyColorPass : NWRPPass
    {
        private Material _copyColorMaterial;

        public CopyColorPass()
            : base(NWRPPassEvent.BeforeTransparent, "CopyColor")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!frameData.targets.hasOpaqueTexture
                || frameData.targets.cameraColorHandle == null
                || frameData.targets.opaqueTextureHandle == null)
            {
                return;
            }

            if (!EnsureMaterial())
            {
                return;
            }

            CommandBuffer cmd = frameData.cmd;
            Blitter.BlitCameraTexture(
                cmd,
                frameData.targets.cameraColorHandle,
                frameData.targets.opaqueTextureHandle,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                _copyColorMaterial,
                0);

            cmd.SetGlobalTexture(
                NWRPShaderIds.CameraOpaqueTexture,
                frameData.targets.opaqueTextureHandle);

            cmd.SetRenderTarget(frameData.targets.cameraColor, frameData.targets.cameraDepth);
            cmd.SetViewport(GetCameraViewport(frameData.camera));
        }

        private static Rect GetCameraViewport(Camera camera)
        {
            if (camera == null)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            Rect cameraViewport = camera.pixelRect;
            if (cameraViewport.width <= 0f || cameraViewport.height <= 0f)
            {
                cameraViewport = new Rect(
                    0f,
                    0f,
                    Mathf.Max(camera.pixelWidth, 1),
                    Mathf.Max(camera.pixelHeight, 1));
            }

            return cameraViewport;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_copyColorMaterial);
            _copyColorMaterial = null;
        }

        private bool EnsureMaterial()
        {
            if (_copyColorMaterial != null)
            {
                return true;
            }

            _copyColorMaterial = NWRPBlitterResources.CreateCoreBlitMaterial();
            return _copyColorMaterial != null;
        }
    }
}
