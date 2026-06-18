using UnityEngine;
using UnityEngine.Experimental.Rendering;
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

        private static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private Material _blurMaterial;
        private RTHandle _tempColorHandle;

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
            RenderTextureDescriptor descriptor = CreateTempDescriptor(source.rt);
            CommandBuffer cmd = frameData.cmd;
            Rect viewport = NWRPRenderer.GetCameraRenderViewport(ref frameData);

            UploadConstants(cmd, source.rt, radius);
            NWRPTransientRTHandles.ReAllocateIfNeeded(
                ref _tempColorHandle,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                "_NWRPScreenBlurTempColor");

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    BlitToTarget(
                        cmd,
                        source,
                        _tempColorHandle,
                        viewport,
                        _blurMaterial,
                        (int)ScreenBlurShaderPass.Horizontal);
                    BlitToTarget(
                        cmd,
                        _tempColorHandle,
                        frameData.targets.cameraColor,
                        viewport,
                        _blurMaterial,
                        (int)ScreenBlurShaderPass.Vertical);
                }
            }
            finally
            {
                cmd.SetRenderTarget(frameData.targets.cameraColor, frameData.targets.cameraDepth);
                cmd.SetViewport(viewport);
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_blurMaterial);
            NWRPTransientRTHandles.Release(ref _tempColorHandle);
            _blurMaterial = null;
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

        private static RenderTextureDescriptor CreateTempDescriptor(RenderTexture sourceTexture)
        {
            RenderTextureDescriptor descriptor = sourceTexture.descriptor;
            descriptor.depthBufferBits = 0;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = false;
            return descriptor;
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

        private static void BlitToTarget(
            CommandBuffer cmd,
            RTHandle source,
            RenderTargetIdentifier destination,
            Rect viewport,
            Material material,
            int passIndex)
        {
            CoreUtils.SetRenderTarget(
                cmd,
                destination,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            cmd.SetViewport(viewport);
            Blitter.BlitTexture(cmd, source, s_FullScaleBias, material, passIndex);
        }

        private static void BlitToTarget(
            CommandBuffer cmd,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            Rect viewport,
            Material material,
            int passIndex)
        {
            CoreUtils.SetRenderTarget(
                cmd,
                destination,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            cmd.SetViewport(viewport);
            Blitter.BlitTexture(cmd, source, s_FullScaleBias, material, passIndex);
        }
    }
}
