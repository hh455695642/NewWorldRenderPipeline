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
            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (asset == null || asset.FogModeSetting == NewWorldRenderPipelineAsset.FogMode.Off)
            {
                SetDisabledFog(ref frameData);
                return;
            }

            float start = asset.FogStartDistance;
            float end = asset.FogEndDistance;
            float invRange = 1.0f / Mathf.Max(end - start, 0.01f);
            float density = asset.FogDensity;
            Color fogColor = asset.FogColor.linear;

            frameData.cmd.SetGlobalFloat(NWRPShaderIds.FogMode, (float)asset.FogModeSetting);
            frameData.cmd.SetGlobalVector(
                NWRPShaderIds.FogParams,
                new Vector4(start, end, invRange, density));
            frameData.cmd.SetGlobalColor(NWRPShaderIds.FogColor, fogColor);
        }

        private static void SetDisabledFog(ref NWRPFrameData frameData)
        {
            frameData.cmd.SetGlobalFloat(
                NWRPShaderIds.FogMode,
                (float)NewWorldRenderPipelineAsset.FogMode.Off);
            frameData.cmd.SetGlobalVector(NWRPShaderIds.FogParams, Vector4.zero);
            frameData.cmd.SetGlobalColor(NWRPShaderIds.FogColor, Color.clear);
        }
    }
}
