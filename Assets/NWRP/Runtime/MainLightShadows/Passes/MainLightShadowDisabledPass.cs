using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class MainLightShadowDisabledPass : NWRPPass
    {
        public MainLightShadowDisabledPass()
            : base(
                NWRPPassEvent.ShadowMap,
                "Upload Main Light Disabled Globals",
                NWRPProfiling.MainLightShadow,
                usePassProfilingScope: false)
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            MainLightShadowPassUtils.UploadDisabledGlobals(ref frameData, null);
        }
    }
}
