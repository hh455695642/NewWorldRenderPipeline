using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Valley Height Fog",
        MenuPath = "Post Processing/Valley Height Fog",
        VolumeDriven = true,
        SortOrder = 220)]
    public sealed class ValleyHeightFogFeature : NWRPFeature
    {
        private static bool s_MissingDepthTextureWarningLogged;

        private ValleyHeightFogPass _valleyHeightFogPass;

        protected override void Create()
        {
            _valleyHeightFogPass = new ValleyHeightFogPass();
        }

        public override bool TryGetFrameTargetRequirements(
            ref NWRPFrameData frameData,
            out NWRPFrameTargetRequirements requirements)
        {
            requirements = default;
            if (!IsActive(ref frameData))
            {
                return false;
            }

            if (!CanUseRendererDepthTexture(ref frameData))
            {
                return false;
            }

            requirements.requiresIntermediateColor = true;
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (!IsActive(ref frameData))
            {
                return;
            }

            if (!CanUseRendererDepthTexture(ref frameData))
            {
                WarnMissingDepthTexture(ref frameData);
                return;
            }

            _valleyHeightFogPass ??= new ValleyHeightFogPass();
            renderer.EnqueuePass(_valleyHeightFogPass);
        }

        internal static bool IsActive(ref NWRPFrameData frameData)
        {
            return PostProcessFeature.IsPostProcessingEnabled(ref frameData)
                && frameData.valleyHeightFogActive;
        }

        private static bool CanUseRendererDepthTexture(ref NWRPFrameData frameData)
        {
            return frameData.rendererData != null
                && frameData.rendererData.EnableDepthTexture;
        }

        private static void WarnMissingDepthTexture(ref NWRPFrameData frameData)
        {
            if (s_MissingDepthTextureWarningLogged)
            {
                return;
            }

            s_MissingDepthTextureWarningLogged = true;
            string cameraName = frameData.camera != null ? frameData.camera.name : "NULL";
            Debug.LogWarning(
                "NWRP Valley Height Fog is active but Renderer Data has "
                + "Enable Camera Depth Texture disabled. Valley Height Fog was skipped "
                + $"for camera '{cameraName}'. Enable Camera Depth Texture on the active "
                + "NWRP Renderer Data to provide _CameraDepthTexture.");
        }

        private void OnDisable()
        {
            DisposePasses();
        }

        private void OnDestroy()
        {
            DisposePasses();
        }

        private void DisposePasses()
        {
            _valleyHeightFogPass?.Dispose();
            _valleyHeightFogPass = null;
        }
    }
}
