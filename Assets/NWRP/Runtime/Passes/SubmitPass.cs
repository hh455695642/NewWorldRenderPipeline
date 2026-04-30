namespace NWRP.Runtime.Passes
{
    public sealed class SubmitPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public SubmitPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.DebugOverlay, "Final Blit", null, false)
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteSubmit(ref frameData);
        }
    }
}
