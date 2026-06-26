using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class CloudShadowProjectorPass : NWRPPass
        , INWRPFullscreenEffectNode
    {
        private const string k_ShaderName = "Hidden/NWRP/Environment/CloudShadowProjector";
        private const float k_MinProjectorAxisSize = 0.001f;

        private readonly NWRPFullscreenChain _fullscreenChain =
            new NWRPFullscreenChain();
        private Material _projectorMaterial;

        public CloudShadowProjectorPass()
            : base(NWRPPassEvent.AfterTransparent, "Cloud Shadow Projector")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _fullscreenChain.Execute(ref frameData, this, this);
        }

        public void Dispose()
        {
            _fullscreenChain.Dispose();
            CoreUtils.Destroy(_projectorMaterial);
            _projectorMaterial = null;
        }

        public override bool CanPresentCameraColorToBackBuffer(ref NWRPFrameData frameData)
        {
            return CanPresentFinalCloudShadow(ref frameData);
        }

        public override NWRPFramePassResourceUsage GetFrameResourceUsage(
            ref NWRPFrameData frameData)
        {
            NWRPFramePassResourceUsage usage =
                NWRPFramePassResourceUsage.CameraColorReadWrite(
                    CanPresentFinalCloudShadow(ref frameData));
            usage.cameraDepthTexture = NWRPFrameResourceAccess.Read;
            return usage;
        }

        private static bool CanPresentFinalCloudShadow(ref NWRPFrameData frameData)
        {
            return CloudShadowProjectorFeature.CanRun(ref frameData)
                && frameData.camera != null
                && frameData.camera.cameraType == CameraType.Game
                && frameData.targets.usesIntermediateColor
                && frameData.targets.cameraColorHandle != null
                && frameData.targets.hasCameraDepthTexture;
        }

        NWRPPassEvent INWRPFullscreenEffectNode.PassEvent => passEvent;

        bool INWRPFullscreenEffectNode.RequiresDepthTexture => true;

        bool INWRPFullscreenEffectNode.RequiresOpaqueTexture => false;

        bool INWRPFullscreenEffectNode.IsActive(ref NWRPFrameData frameData)
        {
            return CloudShadowProjectorFeature.CanRun(ref frameData);
        }

        bool INWRPFullscreenEffectNode.CanPresentToBackBuffer(
            ref NWRPFrameData frameData)
        {
            return CanPresentFinalCloudShadow(ref frameData);
        }

        bool INWRPFullscreenEffectNode.Prepare(ref NWRPFrameData frameData)
        {
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

            if (_projectorMaterial == null)
            {
                return false;
            }

            UploadConstants(frameData.cmd, frameData.cloudShadowProjector);
            frameData.cmd.SetGlobalTexture(
                NWRPShaderIds.CameraDepthTexture,
                frameData.targets.cameraDepthTextureHandle);
            return true;
        }

        int INWRPFullscreenEffectNode.GetPassCount(ref NWRPFrameData frameData)
        {
            return 1;
        }

        bool INWRPFullscreenEffectNode.TryGetPass(
            ref NWRPFrameData frameData,
            int passIndex,
            bool isFinalPass,
            out NWRPFullscreenEffectPass fullscreenPass)
        {
            fullscreenPass = default;
            if (passIndex != 0)
            {
                return false;
            }

            fullscreenPass = new NWRPFullscreenEffectPass(_projectorMaterial, 0);
            return true;
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
