namespace NWRP.Runtime.Passes
{
    using UnityEngine.Rendering;

    public sealed class DrawOutlinePass : NWRPPass
    {
        private static readonly ShaderTagId s_NewWorldOutlineTagId = new ShaderTagId("NewWorldOutline");

        public DrawOutlinePass()
            : base(NWRPPassEvent.Opaque, "Draw Outline Objects")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            SortingSettings sortingSettings = new SortingSettings(frameData.camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            DrawingSettings drawingSettings = new DrawingSettings(s_NewWorldOutlineTagId, sortingSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = frameData.asset.useGPUInstancing
            };

            FilteringSettings filteringSettings = new FilteringSettings(
                RenderQueueRange.opaque,
                NWRPRenderer.GetOpaqueLayerMaskValue(ref frameData));
            NWRPRenderer.DrawRendererList(ref frameData, ref drawingSettings, ref filteringSettings);
        }

        public override NWRPFramePassResourceUsage GetFrameResourceUsage(
            ref NWRPFrameData frameData)
        {
            return new NWRPFramePassResourceUsage
            {
                cameraColor = NWRPFrameResourceAccess.Write,
                cameraDepth = NWRPFrameResourceAccess.Write
            };
        }
    }
}
