namespace NWRP.Runtime.Passes
{
    internal sealed class AdditionalLightShadowDisabledPass : NWRPPass
    {
        public AdditionalLightShadowDisabledPass()
            : base(
                NWRPPassEvent.ShadowMap,
                "Upload Additional Punctual Light Shadow Disabled Globals",
                NWRPProfiling.AdditionalLightShadow,
                usePassProfilingScope: false)
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            AdditionalLightShadowPassUtils.UploadDisabledGlobals(ref frameData);
        }
    }
}
