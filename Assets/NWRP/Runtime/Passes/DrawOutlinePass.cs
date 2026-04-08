namespace NWRP.Runtime.Passes
{
    public sealed class DrawOutlinePass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public DrawOutlinePass(NWRPRenderer renderer)
            : base(NWRPPassEvent.Opaque)
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteDrawOutline(ref frameData);
        }
    }
}
