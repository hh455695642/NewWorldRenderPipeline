namespace NWRP.Runtime.Passes
{
    public sealed class DrawSkyboxPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;

        public DrawSkyboxPass(NWRPRenderer renderer)
            : base(NWRPPassEvent.Skybox)
        {
            _renderer = renderer;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteDrawSkybox(ref frameData);
        }
    }
}
