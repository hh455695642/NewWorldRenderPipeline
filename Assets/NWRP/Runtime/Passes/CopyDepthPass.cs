using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class CopyDepthPass : NWRPPass
    {
        private const string kDepthMsaa2Keyword = "_DEPTH_MSAA_2";
        private const string kDepthMsaa4Keyword = "_DEPTH_MSAA_4";
        private const string kDepthMsaa8Keyword = "_DEPTH_MSAA_8";
        private const string kOutputDepthKeyword = "_OUTPUT_DEPTH";

        private Material _copyDepthMaterial;

        public CopyDepthPass()
            : base(NWRPPassEvent.BeforeTransparent, "CopyDepth")
        {
        }

        public void Setup(NWRPPassEvent copyEvent)
        {
            passEvent = copyEvent;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!frameData.targets.hasCameraDepthTexture
                || frameData.targets.cameraDepthHandle == null
                || frameData.targets.cameraDepthTextureHandle == null)
            {
                return;
            }

            if (!EnsureMaterial())
            {
                return;
            }

            CommandBuffer cmd = frameData.cmd;
            RTHandle source = frameData.targets.cameraDepthHandle;
            RTHandle destination = frameData.targets.cameraDepthTextureHandle;
            bool copyToDepth = frameData.targets.cameraDepthTextureIsDepthTarget;

            cmd.SetGlobalTexture(NWRPShaderIds.CameraDepthAttachment, source.nameID);
            cmd.SetGlobalVector(
                NWRPShaderIds.CameraDepthAttachmentTexelSize,
                GetDepthAttachmentTexelSize(source));
            ConfigureKeywords(source, copyToDepth);
            if (copyToDepth)
            {
                SetDepthCopyTarget(cmd, frameData.targets.cameraDepthTexture);
                Blitter.BlitTexture(
                    cmd,
                    source,
                    GetBlitScaleBias(source),
                    _copyDepthMaterial,
                    0);
            }
            else
            {
                Blitter.BlitCameraTexture(
                    cmd,
                    source,
                    destination,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    _copyDepthMaterial,
                    0);
            }

            cmd.SetGlobalTexture(NWRPShaderIds.CameraDepthTexture, destination.nameID);
            cmd.SetRenderTarget(frameData.targets.cameraColor, frameData.targets.cameraDepth);
            cmd.SetViewport(GetCameraViewport(frameData.camera));
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_copyDepthMaterial);
            _copyDepthMaterial = null;
        }

        public static bool CanCopyDepth(Camera camera)
        {
            int msaaSamples = 1;
            if (camera != null && camera.targetTexture != null)
            {
                msaaSamples = Mathf.Max(1, camera.targetTexture.antiAliasing);
            }

            bool msaaDepth = msaaSamples > 1 && SystemInfo.supportsMultisampledTextures != 0;
            if (msaaDepth && IsGlesDevice())
            {
                return false;
            }

            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
        }

        private bool EnsureMaterial()
        {
            if (_copyDepthMaterial != null)
            {
                return true;
            }

            _copyDepthMaterial = NWRPBlitterResources.CreateCopyDepthMaterial();
            return _copyDepthMaterial != null;
        }

        private void ConfigureKeywords(RTHandle source, bool copyToDepth)
        {
            int msaaSamples = source != null && source.rt != null
                ? source.rt.descriptor.msaaSamples
                : 1;

            _copyDepthMaterial.DisableKeyword(kDepthMsaa2Keyword);
            _copyDepthMaterial.DisableKeyword(kDepthMsaa4Keyword);
            _copyDepthMaterial.DisableKeyword(kDepthMsaa8Keyword);

            if (msaaSamples == 2)
            {
                _copyDepthMaterial.EnableKeyword(kDepthMsaa2Keyword);
            }
            else if (msaaSamples == 4)
            {
                _copyDepthMaterial.EnableKeyword(kDepthMsaa4Keyword);
            }
            else if (msaaSamples == 8)
            {
                _copyDepthMaterial.EnableKeyword(kDepthMsaa8Keyword);
            }

            if (copyToDepth)
            {
                _copyDepthMaterial.EnableKeyword(kOutputDepthKeyword);
            }
            else
            {
                _copyDepthMaterial.DisableKeyword(kOutputDepthKeyword);
            }
        }

        private static void SetDepthCopyTarget(
            CommandBuffer cmd,
            RenderTargetIdentifier destination)
        {
            cmd.SetRenderTarget(
                new RenderTargetIdentifier(BuiltinRenderTextureType.None),
                destination);
            cmd.ClearRenderTarget(true, false, Color.clear);
        }

        private static Vector4 GetBlitScaleBias(RTHandle source)
        {
            if (source == null || !source.useScaling)
            {
                return new Vector4(1f, 1f, 0f, 0f);
            }

            return new Vector4(
                source.rtHandleProperties.rtHandleScale.x,
                source.rtHandleProperties.rtHandleScale.y,
                0f,
                0f);
        }

        private static Vector4 GetDepthAttachmentTexelSize(RTHandle source)
        {
            RenderTexture renderTexture = source != null ? source.rt : null;
            if (renderTexture == null)
            {
                return Vector4.zero;
            }

            float width = Mathf.Max(renderTexture.width, 1);
            float height = Mathf.Max(renderTexture.height, 1);
            return new Vector4(1f / width, 1f / height, width, height);
        }

        private static Rect GetCameraViewport(Camera camera)
        {
            if (camera == null)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            Rect cameraViewport = camera.pixelRect;
            if (cameraViewport.width <= 0f || cameraViewport.height <= 0f)
            {
                cameraViewport = new Rect(
                    0f,
                    0f,
                    Mathf.Max(camera.pixelWidth, 1),
                    Mathf.Max(camera.pixelHeight, 1));
            }

            return cameraViewport;
        }

        private static bool IsGlesDevice()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2
                || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
        }
    }
}
