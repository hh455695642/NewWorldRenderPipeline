namespace NWRP.Runtime.Passes
{
    public sealed class SubmitPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public SubmitPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.DebugOverlay, "Submit Context")
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteSubmit(ref frameData);
        }
    }
}
