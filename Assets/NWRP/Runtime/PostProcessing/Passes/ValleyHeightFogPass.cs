using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class ValleyHeightFogPass : NWRPPass
    {
        private const string k_ShaderName = "Hidden/NWRP/PostProcess/ValleyHeightFog";

        public const int SingleLayerShaderPass = 0;
        public const int ThreeLayerShaderPass = 1;

        private static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private readonly int _tempColorId = Shader.PropertyToID("_NWRPValleyHeightFogTempColor");

        private Material _copyMaterial;
        private Material _fogMaterial;

        public ValleyHeightFogPass()
            : base(NWRPPassEvent.AfterTransparent, "Valley Height Fog")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!ValleyHeightFogFeature.IsActive(ref frameData)
                || frameData.targets.cameraColorHandle == null
                || frameData.targets.cameraColorHandle.rt == null
                || !frameData.targets.hasCameraDepthTexture
                || frameData.targets.cameraDepthTextureHandle == null)
            {
                return;
            }

            if (!EnsureMaterials())
            {
                return;
            }

            RTHandle source = frameData.targets.cameraColorHandle;
            RenderTextureDescriptor descriptor = CreateTempDescriptor(source.rt);
            CommandBuffer cmd = frameData.cmd;
            Rect viewport = NWRPRenderer.GetCameraRenderViewport(ref frameData);

            UploadConstants(cmd, frameData.valleyHeightFog);
            int shaderPassIndex = GetShaderPassIndex(frameData.valleyHeightFog);

            cmd.GetTemporaryRT(_tempColorId, descriptor, FilterMode.Bilinear);
            try
            {
                RenderTargetIdentifier tempColor = _tempColorId;

                BlitToTarget(cmd, source, tempColor, viewport, _fogMaterial, shaderPassIndex);
                BlitToTarget(
                    cmd,
                    tempColor,
                    frameData.targets.cameraColor,
                    viewport,
                    _copyMaterial,
                    0);
            }
            finally
            {
                cmd.ReleaseTemporaryRT(_tempColorId);
                cmd.SetRenderTarget(frameData.targets.cameraColor, frameData.targets.cameraDepth);
                cmd.SetViewport(viewport);
            }
        }

        public static int GetShaderPassIndex(NWRPValleyHeightFog fog)
        {
            return fog != null
                && fog.mode.value == NWRPValleyHeightFogMode.ThreeLayer
                    ? ThreeLayerShaderPass
                    : SingleLayerShaderPass;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_copyMaterial);
            CoreUtils.Destroy(_fogMaterial);
            _copyMaterial = null;
            _fogMaterial = null;
        }

        private bool EnsureMaterials()
        {
            if (_copyMaterial == null)
            {
                _copyMaterial = NWRPBlitterResources.CreateCoreBlitMaterial();
            }

            if (_fogMaterial == null)
            {
                Shader shader = Shader.Find(k_ShaderName);
                if (shader == null)
                {
                    Debug.LogError("NWRP Valley Height Fog requires Hidden/NWRP/PostProcess/ValleyHeightFog.");
                    return false;
                }

                _fogMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            return _copyMaterial != null && _fogMaterial != null;
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

        private static void UploadConstants(CommandBuffer cmd, NWRPValleyHeightFog fog)
        {
            if (fog == null)
            {
                cmd.SetGlobalColor(NWRPShaderIds.ValleyHeightFogColor, Color.clear);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogHeightParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogDistanceParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogNoiseParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogNoiseParams2, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogBottomParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogBottomNoiseParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogMidParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogMidNoiseParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogTopParams, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ValleyHeightFogThreeLayerNoiseParams, Vector4.zero);
                return;
            }

            Color fogColor = fog.fogColor.value.linear;
            float fogLength = Mathf.Max(0.001f, fog.fogLength.value);
            float heightDensity = Mathf.Max(0.01f, fog.heightDensity.value);
            float noiseRoughness = Mathf.Max(0.001f, fog.noiseRoughness.value);
            float noisePersistance = Mathf.Clamp01(fog.noisePersistance.value);

            cmd.SetGlobalColor(NWRPShaderIds.ValleyHeightFogColor, fogColor);
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogHeightParams,
                new Vector4(
                    fog.fogBaseHeight.value,
                    heightDensity,
                    0f,
                    0f));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogDistanceParams,
                new Vector4(
                    Mathf.Max(0f, fog.fogStart.value),
                    fogLength,
                    1f / fogLength,
                    0f));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogNoiseParams,
                new Vector4(
                    Mathf.Max(0f, fog.noiseScale.value),
                    Mathf.Max(0f, fog.noiseIntensity.value),
                    fog.noiseSpeed.value,
                    noiseRoughness));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogNoiseParams2,
                new Vector4(noisePersistance, 0f, 0f, 0f));

            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogBottomParams,
                new Vector4(
                    fog.bottomHeight.value,
                    Mathf.Max(0.001f, fog.bottomFade.value),
                    Mathf.Max(0.001f, fog.bottomDensity.value),
                    Mathf.Max(0f, fog.bottomIntensity.value)));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogBottomNoiseParams,
                new Vector4(
                    Mathf.Clamp(fog.bottomNoiseScale.value, 0f, 0.5f),
                    Mathf.Clamp(fog.bottomNoiseIntensity.value, 0f, 3f),
                    0f,
                    0f));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogMidParams,
                new Vector4(
                    fog.midHeight.value,
                    Mathf.Max(0.001f, fog.midFade.value),
                    Mathf.Max(0.001f, fog.midDensity.value),
                    Mathf.Max(0f, fog.midIntensity.value)));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogMidNoiseParams,
                new Vector4(
                    Mathf.Clamp(fog.midNoiseScale.value, 0f, 0.02f),
                    Mathf.Clamp(fog.midNoiseIntensity.value, 0f, 2f),
                    0f,
                    0f));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogTopParams,
                new Vector4(
                    Mathf.Clamp(fog.topIntensity.value, 0f, 0.5f),
                    Mathf.Clamp(fog.topDensity.value, 0.0001f, 0.01f),
                    Mathf.Clamp(fog.topNoiseScale.value, 0f, 0.01f),
                    Mathf.Clamp(fog.topNoiseIntensity.value, 0f, 2f)));
            cmd.SetGlobalVector(
                NWRPShaderIds.ValleyHeightFogThreeLayerNoiseParams,
                new Vector4(
                    fog.threeLayerNoiseSpeed.value,
                    Mathf.Max(0.001f, fog.threeLayerNoiseRoughness.value),
                    Mathf.Clamp01(fog.threeLayerNoisePersistance.value),
                    0f));
        }
    }
}
