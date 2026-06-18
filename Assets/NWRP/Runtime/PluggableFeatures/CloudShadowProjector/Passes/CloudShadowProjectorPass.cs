using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class CloudShadowProjectorPass : NWRPPass
    {
        private const string k_ShaderName = "Hidden/NWRP/Environment/CloudShadowProjector";
        private const float k_MinProjectorAxisSize = 0.001f;

        private static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private Material _copyMaterial;
        private Material _projectorMaterial;
        private RTHandle _tempColorHandle;

        public CloudShadowProjectorPass()
            : base(NWRPPassEvent.AfterTransparent, "Cloud Shadow Projector")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!CloudShadowProjectorFeature.CanRun(ref frameData)
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

            UploadConstants(cmd, frameData.cloudShadowProjector);
            cmd.SetGlobalTexture(
                NWRPShaderIds.CameraDepthTexture,
                frameData.targets.cameraDepthTextureHandle);

            NWRPTransientRTHandles.ReAllocateIfNeeded(
                ref _tempColorHandle,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                "_NWRPCloudShadowTempColor");

            try
            {
                BlitToTarget(cmd, source, _tempColorHandle, viewport, _projectorMaterial, 0);
                BlitToTarget(
                    cmd,
                    _tempColorHandle,
                    frameData.targets.cameraColor,
                    viewport,
                    _copyMaterial,
                    0);
            }
            finally
            {
                cmd.SetRenderTarget(frameData.targets.cameraColor, frameData.targets.cameraDepth);
                cmd.SetViewport(viewport);
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_copyMaterial);
            CoreUtils.Destroy(_projectorMaterial);
            NWRPTransientRTHandles.Release(ref _tempColorHandle);
            _copyMaterial = null;
            _projectorMaterial = null;
        }

        private bool EnsureMaterials()
        {
            if (_copyMaterial == null)
            {
                _copyMaterial = NWRPBlitterResources.CreateCoreBlitMaterial();
            }

            if (_projectorMaterial == null)
            {
                Shader shader = Shader.Find(k_ShaderName);
                if (shader == null)
                {
                    Debug.LogError("NWRP Cloud Shadow Projector requires Hidden/NWRP/Environment/CloudShadowProjector.");
                    return false;
                }

                _projectorMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            return _copyMaterial != null && _projectorMaterial != null;
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

        private static void UploadConstants(
            CommandBuffer cmd,
            NWRPCloudShadowProjector projector)
        {
            UploadDistortion(cmd, projector);
            UploadLayer(
                cmd,
                projector,
                isPrimaryLayer: true,
                NWRPShaderIds.CloudShadowTexture0,
                NWRPShaderIds.CloudShadowWorldToProjector0,
                NWRPShaderIds.CloudShadowUV0,
                NWRPShaderIds.CloudShadowParams0,
                NWRPShaderIds.CloudShadowColor0);
            UploadLayer(
                cmd,
                projector,
                isPrimaryLayer: false,
                NWRPShaderIds.CloudShadowTexture1,
                NWRPShaderIds.CloudShadowWorldToProjector1,
                NWRPShaderIds.CloudShadowUV1,
                NWRPShaderIds.CloudShadowParams1,
                NWRPShaderIds.CloudShadowColor1);
        }

        private static void UploadDistortion(
            CommandBuffer cmd,
            NWRPCloudShadowProjector projector)
        {
            Texture texture = projector != null
                ? projector.distortionTexture.value
                : null;
            float strength = projector != null
                ? Mathf.Clamp(projector.distortionStrength.value, 0f, 0.25f)
                : 0f;
            bool active = texture != null && strength > 0f;
            Vector2 tiling = active ? projector.distortionTiling.value : Vector2.one;
            Vector2 offset = active ? projector.distortionOffset.value : Vector2.zero;
            Vector2 scroll = active ? projector.distortionScroll.value : Vector2.zero;

            cmd.SetGlobalTexture(
                NWRPShaderIds.CloudShadowDistortionTexture,
                active ? texture : Texture2D.grayTexture);
            cmd.SetGlobalVector(
                NWRPShaderIds.CloudShadowDistortionUV,
                new Vector4(tiling.x, tiling.y, offset.x, offset.y));
            cmd.SetGlobalVector(
                NWRPShaderIds.CloudShadowDistortionParams,
                new Vector4(scroll.x, scroll.y, active ? strength : 0f, 0f));
        }

        private static void UploadLayer(
            CommandBuffer cmd,
            NWRPCloudShadowProjector projector,
            bool isPrimaryLayer,
            int textureId,
            int matrixId,
            int uvId,
            int paramsId,
            int colorId)
        {
            bool active = projector != null
                && (isPrimaryLayer
                    ? NWRPCloudShadowProjector.IsLayerActive(
                        projector.primaryEnabled,
                        projector.primaryTexture,
                        projector.primaryIntensity)
                    : NWRPCloudShadowProjector.IsLayerActive(
                        projector.secondaryEnabled,
                        projector.secondaryTexture,
                        projector.secondaryIntensity));

            Texture texture = active
                ? GetTexture(projector, isPrimaryLayer)
                : Texture2D.blackTexture;
            Vector3 center = active
                ? GetCenter(projector, isPrimaryLayer)
                : Vector3.zero;
            Vector3 rotation = active
                ? GetRotation(projector, isPrimaryLayer)
                : Vector3.zero;
            Vector3 size = active
                ? GetSafeSize(GetSize(projector, isPrimaryLayer))
                : Vector3.one;
            Vector2 tiling = active
                ? GetTiling(projector, isPrimaryLayer)
                : Vector2.one;
            Vector2 offset = active
                ? GetOffset(projector, isPrimaryLayer)
                : Vector2.zero;
            Vector2 scroll = active
                ? GetScroll(projector, isPrimaryLayer)
                : Vector2.zero;
            float intensity = active
                ? Mathf.Max(0f, GetIntensity(projector, isPrimaryLayer))
                : 0f;
            float edgeSoftness = active
                ? Mathf.Clamp(GetEdgeSoftness(projector, isPrimaryLayer), 0f, 0.5f)
                : 0f;
            Color shadowColor = active
                ? GetShadowColor(projector, isPrimaryLayer).linear
                : Color.white;

            Matrix4x4 worldToProjector =
                Matrix4x4.TRS(center, Quaternion.Euler(rotation), size).inverse;

            cmd.SetGlobalTexture(textureId, texture);
            cmd.SetGlobalMatrix(matrixId, worldToProjector);
            cmd.SetGlobalVector(uvId, new Vector4(tiling.x, tiling.y, offset.x, offset.y));
            cmd.SetGlobalVector(paramsId, new Vector4(scroll.x, scroll.y, intensity, edgeSoftness));
            cmd.SetGlobalColor(colorId, shadowColor);
        }

        private static Texture GetTexture(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryTexture.value
                : projector.secondaryTexture.value;
        }

        private static Vector3 GetCenter(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryCenter.value
                : projector.secondaryCenter.value;
        }

        private static Vector3 GetRotation(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryRotation.value
                : projector.secondaryRotation.value;
        }

        private static Vector3 GetSize(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primarySize.value
                : projector.secondarySize.value;
        }

        private static Vector2 GetTiling(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryTiling.value
                : projector.secondaryTiling.value;
        }

        private static Vector2 GetOffset(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryOffset.value
                : projector.secondaryOffset.value;
        }

        private static Vector2 GetScroll(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryScroll.value
                : projector.secondaryScroll.value;
        }

        private static float GetIntensity(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryIntensity.value
                : projector.secondaryIntensity.value;
        }

        private static float GetEdgeSoftness(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryEdgeSoftness.value
                : projector.secondaryEdgeSoftness.value;
        }

        private static Color GetShadowColor(NWRPCloudShadowProjector projector, bool primary)
        {
            return primary
                ? projector.primaryShadowColor.value
                : projector.secondaryShadowColor.value;
        }

        private static Vector3 GetSafeSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(Mathf.Abs(size.x), k_MinProjectorAxisSize),
                Mathf.Max(Mathf.Abs(size.y), k_MinProjectorAxisSize),
                Mathf.Max(Mathf.Abs(size.z), k_MinProjectorAxisSize));
        }
    }
}
