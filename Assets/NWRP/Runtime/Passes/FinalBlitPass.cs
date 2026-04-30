namespace NWRP.Runtime.Passes
{
    public sealed class FinalBlitPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public FinalBlitPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.DebugOverlay, "Final Blit", null, false)
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteFinalBlit(ref frameData);
        }
    }
}
