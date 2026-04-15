namespace NWRP.Runtime.Passes
{
    internal sealed class MainLightShadowDisabledPass : NWRPPass
    {
        public MainLightShadowDisabledPass()
            : base(NWRPPassEvent.ShadowMap)
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            MainLightShadowPassUtils.UploadDisabledGlobals(ref frameData, null, null);
        }
    }
}
