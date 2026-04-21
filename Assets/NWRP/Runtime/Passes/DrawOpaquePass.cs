namespace NWRP.Runtime.Passes
{
    public sealed class DrawOpaquePass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public DrawOpaquePass(NWRPRenderer renderer)
            : base(NWRPPassEvent.Opaque, "Draw Opaque Objects")
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteDrawOpaque(ref frameData);
        }
    }
}
