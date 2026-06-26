using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class ScreenBlurPass : NWRPPass
        , INWRPFullscreenEffectNode
    {
        private const string k_ShaderName = "Hidden/NWRP/PostProcess/ScreenBlur";

        private enum ScreenBlurShaderPass
        {
            Horizontal = 0,
            Vertical = 1
        }

        private readonly NWRPFullscreenChain _fullscreenChain =
            new NWRPFullscreenChain();
        private Material _blurMaterial;

        public ScreenBlurPass()
            : base(NWRPPassEvent.BeforePostProcess, "NWRP Screen Blur")
        {
        }

        public void Setup(NWRPPassEvent injectionPoint)
        {
            passEvent = injectionPoint;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _fullscreenChain.Execute(ref frameData, this, this);
        }

        public void Dispose()
        {
            _fullscreenChain.Dispose();
            CoreUtils.Destroy(_blurMaterial);
            _blurMaterial = null;
        }

        public override bool CanPresentCameraColorToBackBuffer(ref NWRPFrameData frameData)
        {
            return CanPresentFinalBlur(ref frameData);
        }

        public override NWRPFramePassResourceUsage GetFrameResourceUsage(
            ref NWRPFrameData frameData)
        {
            return NWRPFramePassResourceUsage.CameraColorReadWrite(
                CanPresentFinalBlur(ref frameData));
        }

        private bool CanPresentFinalBlur(ref NWRPFrameData frameData)
        {
            return passEvent == NWRPPassEvent.AfterPostProcess
                && ScreenBlurFeature.IsActive(ref frameData)
                && frameData.camera != null
                && frameData.camera.cameraType == CameraType.Game
                && frameData.targets.usesIntermediateColor
                && frameData.targets.cameraColorHandle != null;
        }

        NWRPPassEvent INWRPFullscreenEffectNode.PassEvent => passEvent;

        bool INWRPFullscreenEffectNode.RequiresDepthTexture => false;

        bool INWRPFullscreenEffectNode.RequiresOpaqueTexture => false;

        bool INWRPFullscreenEffectNode.IsActive(ref NWRPFrameData frameData)
        {
            return ScreenBlurFeature.IsActive(ref frameData)
                && GetRadius(frameData.screenBlur) > 0f;
        }

        bool INWRPFullscreenEffectNode.CanPresentToBackBuffer(
            ref NWRPFrameData frameData)
        {
            return CanPresentFinalBlur(ref frameData);
        }

        bool INWRPFullscreenEffectNode.Prepare(ref NWRPFrameData frameData)
        {
            if (!EnsureMaterial())
            {
                return false;
            }

            RenderTexture sourceTexture =
                frameData.targets.cameraColorHandle != null
                    ? frameData.targets.cameraColorHandle.rt
                    : null;
            if (sourceTexture == null)
            {
                return false;
            }

            float radius = GetRadius(frameData.screenBlur);
            if (radius <= 0f)
            {
                return false;
            }

            UploadConstants(frameData.cmd, sourceTexture, radius);
            return true;
        }

        int INWRPFullscreenEffectNode.GetPassCount(ref NWRPFrameData frameData)
        {
            if (!ScreenBlurFeature.IsActive(ref frameData)
                || GetRadius(frameData.screenBlur) <= 0f)
            {
                return 0;
            }

            return GetIterations(frameData.screenBlur) * 2;
        }

        bool INWRPFullscreenEffectNode.TryGetPass(
            ref NWRPFrameData frameData,
            int passIndex,
            bool isFinalPass,
            out NWRPFullscreenEffectPass fullscreenPass)
        {
            fullscreenPass = default;
            bool horizontal = (passIndex & 1) == 0;
            fullscreenPass = new NWRPFullscreenEffectPass(
                _blurMaterial,
                horizontal
                    ? (int)ScreenBlurShaderPass.Horizontal
                    : (int)ScreenBlurShaderPass.Vertical);
            return true;
        }

        private bool EnsureMaterial()
        {
            if (_blurMaterial != null)
            {
                return true;
            }

            Shader shader = Shader.Find(k_ShaderName);
            if (shader == null)
            {
                Debug.LogError("NWRP Screen Blur requires Hidden/NWRP/PostProcess/ScreenBlur.");
                return false;
            }

            _blurMaterial = CoreUtils.CreateEngineMaterial(shader);
            return _blurMaterial != null;
        }

        private static int GetIterations(NWRPScreenBlur screenBlur)
        {
            return Mathf.Clamp(
                screenBlur != null ? screenBlur.iterations.value : 0,
                1,
                NWRPScreenBlur.MaxIterations);
        }

        private static float GetRadius(NWRPScreenBlur screenBlur)
        {
            return Mathf.Clamp(
                screenBlur != null ? screenBlur.radius.value : 0f,
                0f,
                NWRPScreenBlur.MaxRadius);
        }

        private static void UploadConstants(
            CommandBuffer cmd,
            RenderTexture sourceTexture,
            float radius)
        {
            cmd.SetGlobalFloat(NWRPShaderIds.ScreenBlurRadius, radius);
            cmd.SetGlobalVector(
                NWRPShaderIds.ScreenBlurTexelSize,
                new Vector4(
                    1f / Mathf.Max(sourceTexture.width, 1),
                    1f / Mathf.Max(sourceTexture.height, 1),
                    sourceTexture.width,
                    sourceTexture.height));
        }
    }
}
