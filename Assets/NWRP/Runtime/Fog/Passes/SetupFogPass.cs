using UnityEngine;

namespace NWRP.Runtime.Passes
{
    public sealed class SetupFogPass : NWRPPass
    {
        public SetupFogPass()
            : base(NWRPPassEvent.BeforeOpaque, "Setup Fog")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!frameData.fogActive
                || frameData.fogMode == NWRPFogMode.Off)
            {
                SetDisabledFog(ref frameData);
                return;
            }

            float start = frameData.fogStartDistance;
            float end = frameData.fogEndDistance;
            float invRange = 1.0f / Mathf.Max(end - start, 0.01f);
            float density = frameData.fogDensity;
            Color fogColor = frameData.fogColor.linear;

            frameData.cmd.SetGlobalFloat(NWRPShaderIds.FogMode, (float)frameData.fogMode);
            frameData.cmd.SetGlobalVector(
                NWRPShaderIds.FogParams,
                new Vector4(start, end, invRange, density));
            frameData.cmd.SetGlobalColor(NWRPShaderIds.FogColor, fogColor);
        }

        private static void SetDisabledFog(ref NWRPFrameData frameData)
        {
            frameData.cmd.SetGlobalFloat(
                NWRPShaderIds.FogMode,
                (float)NWRPFogMode.Off);
            frameData.cmd.SetGlobalVector(NWRPShaderIds.FogParams, Vector4.zero);
            frameData.cmd.SetGlobalColor(NWRPShaderIds.FogColor, Color.clear);
        }
    }
}
