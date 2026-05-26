using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class ValleyHeightFogOverlayPass : NWRPPass
    {
        private static readonly ShaderTagId s_AfterFogTagId = new ShaderTagId("AfterFog");
        private static readonly ShaderTagId s_NwrpAfterFogTagId = new ShaderTagId("NWRPAfterFog");

        private FilteringSettings _filteringSettings;
        private RenderStateBlock _stateBlock;

        public ValleyHeightFogOverlayPass()
            : base(NWRPPassEvent.AfterValleyHeightFog, "Valley Height Fog Overlay")
        {
            _filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            _stateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (frameData.camera == null
                || frameData.camera.cameraType == CameraType.Preview
                || !frameData.targets.hasCameraTargets)
            {
                return;
            }

            CommandBuffer cmd = frameData.cmd;
            cmd.SetRenderTarget(frameData.targets.cameraColor, frameData.targets.cameraDepth);
            cmd.SetViewport(NWRPRenderer.GetCameraRenderViewport(ref frameData));
            ExecuteBuffer(ref frameData);

            SortingSettings sortingSettings = new SortingSettings(frameData.camera)
            {
                criteria = SortingCriteria.CommonTransparent
            };

            DrawingSettings drawingSettings = new DrawingSettings(
                s_AfterFogTagId,
                sortingSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = frameData.asset != null && frameData.asset.useGPUInstancing
            };

            // Extension point: new NWRP shaders can opt into the same layer without
            // depending on the legacy AfterFog tag name.
            drawingSettings.SetShaderPassName(1, s_NwrpAfterFogTagId);

            frameData.context.DrawRenderers(
                frameData.cullingResults,
                ref drawingSettings,
                ref _filteringSettings,
                ref _stateBlock);
        }

        private static void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            frameData.context.ExecuteCommandBuffer(frameData.cmd);
            frameData.cmd.Clear();
        }
    }
}
