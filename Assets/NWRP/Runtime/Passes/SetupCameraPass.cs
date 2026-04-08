namespace NWRP.Runtime.Passes
{
    public sealed class SetupCameraPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public SetupCameraPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.BeforeShadowMap)
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteSetupCamera(ref frameData);
        }
    }
}
