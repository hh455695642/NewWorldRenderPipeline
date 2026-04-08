namespace NWRP.Runtime.Passes
{
    public sealed class SetupLightsPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public SetupLightsPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.BeforeShadowMap)
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteSetupLights(ref frameData);
        }
    }
}
