using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class DepthPrepass : NWRPPass
    {
        private static readonly ShaderTagId s_DepthOnlyTagId = new ShaderTagId("DepthOnly");

        public DepthPrepass()
            : base(NWRPPassEvent.DepthPrepass, "Depth Prepass")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!frameData.targets.hasCameraDepthTexture
                || frameData.targets.cameraDepthTextureHandle == null
                || !frameData.targets.cameraDepthTextureIsDepthTarget)
            {
                return;
            }

            CommandBuffer cmd = frameData.cmd;
            cmd.SetRenderTarget(
                new RenderTargetIdentifier(BuiltinRenderTextureType.None),
                frameData.targets.cameraDepthTexture);
            cmd.ClearRenderTarget(true, false, Color.clear);
            cmd.SetViewport(GetCameraViewport(frameData.camera));
            ExecuteBuffer(ref frameData);

            SortingSettings sortingSettings = new SortingSettings(frameData.camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            DrawingSettings drawingSettings = new DrawingSettings(s_DepthOnlyTagId, sortingSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = frameData.asset.useGPUInstancing
            };

            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            frameData.context.DrawRenderers(
                frameData.cullingResults,
                ref drawingSettings,
                ref filteringSettings);

            cmd.SetGlobalTexture(
                NWRPShaderIds.CameraDepthTexture,
                frameData.targets.cameraDepthTextureHandle);
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

        private static void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            frameData.context.ExecuteCommandBuffer(frameData.cmd);
            frameData.cmd.Clear();
        }
    }
}
