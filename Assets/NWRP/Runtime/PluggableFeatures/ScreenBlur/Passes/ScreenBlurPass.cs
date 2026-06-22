using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class ScreenBlurPass : NWRPPass
    {
        private const string k_ShaderName = "Hidden/NWRP/PostProcess/ScreenBlur";

        private enum ScreenBlurShaderPass
        {
            Horizontal = 0,
            Vertical = 1
        }

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
            if (!ScreenBlurFeature.IsActive(ref frameData)
                || frameData.targets.cameraColorHandle == null
                || frameData.targets.cameraColorHandle.rt == null
                || frameData.camera == null)
            {
                return;
            }

            if (!EnsureMaterial())
            {
                return;
            }

            NWRPScreenBlur screenBlur = frameData.screenBlur;
            int iterations = Mathf.Clamp(
                screenBlur.iterations.value,
                1,
                NWRPScreenBlur.MaxIterations);
            float radius = Mathf.Clamp(
                screenBlur.radius.value,
                0f,
                NWRPScreenBlur.MaxRadius);
            if (radius <= 0f)
            {
                return;
            }

            RTHandle source = frameData.targets.cameraColorHandle;
            RenderTextureDescriptor descriptor =
                NWRPFullscreenPassUtils.CreateColorDescriptor(source.rt);
            CommandBuffer cmd = frameData.cmd;
            Rect viewport = NWRPRenderer.GetCameraRenderViewport(ref frameData);
            bool presentToBackBuffer =
                frameData.frameGraph.IsCameraColorFinalPresentPass(this);
            bool presentedToBackBuffer = false;

            UploadConstants(cmd, source.rt, radius);
            NWRPFullscreenPassUtils.AllocateTempColor(
                ref frameData,
                NWRPFullscreenTempSlot.A,
                descriptor,
                FilterMode.Bilinear);
            try
            {
                RenderTargetIdentifier tempColor =
                    NWRPFullscreenPassUtils.GetTempColorId(NWRPFullscreenTempSlot.A);
                for (int i = 0; i < iterations; i++)
                {
                    NWRPFullscreenPassUtils.BlitToTarget(
                        ref frameData,
                        source,
                        tempColor,
                        viewport,
                        _blurMaterial,
                        (int)ScreenBlurShaderPass.Horizontal);

                    if (presentToBackBuffer && i == iterations - 1)
                    {
                        NWRPFullscreenPassUtils.BlitToBackBuffer(
                            ref frameData,
                            tempColor,
                            _blurMaterial,
                            (int)ScreenBlurShaderPass.Vertical);
                        presentedToBackBuffer = true;
                    }
                    else
                    {
                        NWRPFullscreenPassUtils.BlitToTarget(
                            ref frameData,
                            tempColor,
                            frameData.targets.cameraColor,
                            viewport,
                            _blurMaterial,
                            (int)ScreenBlurShaderPass.Vertical);
                    }
                }
            }
            finally
            {
                NWRPFullscreenPassUtils.ReleaseTempColor(
                    cmd,
                    NWRPFullscreenTempSlot.A);
                if (!presentedToBackBuffer)
                {
                    NWRPRenderer.RestoreCameraRenderTarget(ref frameData);
                }
            }
        }

        public void Dispose()
        {
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
