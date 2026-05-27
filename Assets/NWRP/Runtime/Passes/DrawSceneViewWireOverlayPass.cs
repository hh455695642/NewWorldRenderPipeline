#if UNITY_EDITOR
namespace NWRP.Runtime.Passes
{
    internal sealed class DrawSceneViewWireOverlayPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public DrawSceneViewWireOverlayPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.DebugOverlay, "Draw Scene View Wire Overlay")
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteDrawSceneViewWireOverlay(ref frameData);
        }
    }
}
#endif
