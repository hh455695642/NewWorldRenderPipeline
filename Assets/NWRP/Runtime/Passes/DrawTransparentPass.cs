namespace NWRP.Runtime.Passes
{
    public sealed class DrawTransparentPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public DrawTransparentPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.Transparent, "Draw Transparent Objects")
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteDrawTransparent(ref frameData);
        }

        public override NWRPFramePassResourceUsage GetFrameResourceUsage(
            ref NWRPFrameData frameData)
        {
            return new NWRPFramePassResourceUsage
            {
                cameraColor = NWRPFrameResourceAccess.ReadWrite,
                cameraDepth = NWRPFrameResourceAccess.Read
            };
        }
    }
}
