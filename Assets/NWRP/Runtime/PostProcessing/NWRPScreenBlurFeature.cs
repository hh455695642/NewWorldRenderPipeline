using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    public sealed class NWRPScreenBlurFeature : NWRPFeature
    {
        private NWRPScreenBlurPass _screenBlurPass;

        protected override void Create()
        {
            _screenBlurPass = new NWRPScreenBlurPass();
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

            requirements.requiresIntermediateColor = true;
            return true;
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (renderer == null
                || frameData.camera == null
                || frameData.camera.cameraType == CameraType.Preview
                || !IsActive(ref frameData))
            {
                return;
            }

            _screenBlurPass ??= new NWRPScreenBlurPass();
            _screenBlurPass.Setup(frameData.screenBlur.GetPassEvent());
            renderer.EnqueuePass(_screenBlurPass);
        }

        internal static bool IsActive(ref NWRPFrameData frameData)
        {
            return PostProcessFeature.IsPostProcessingEnabled(ref frameData)
                && frameData.screenBlurActive
                && frameData.screenBlur != null;
        }

        internal static bool IsAfterPostProcessActive(ref NWRPFrameData frameData)
        {
            return IsActive(ref frameData)
                && frameData.screenBlur.injectionPoint.value
                    == NWRPScreenBlurInjectionPoint.AfterPostProcess;
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
            _screenBlurPass?.Dispose();
            _screenBlurPass = null;
        }
    }
}
