using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class ValleyHeightFogPass : NWRPPass
        , INWRPFullscreenEffectNode
    {
        private const string k_ShaderName = "Hidden/NWRP/PostProcess/ValleyHeightFog";

        public const int SingleLayerShaderPass = 0;
        public const int ThreeLayerShaderPass = 1;

        private readonly NWRPFullscreenChain _fullscreenChain =
            new NWRPFullscreenChain();
        private Material _fogMaterial;

        public ValleyHeightFogPass()
            : base(NWRPPassEvent.AfterTransparent, "Valley Height Fog")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _fullscreenChain.Execute(ref frameData, this, this);
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
            _fullscreenChain.Dispose();
            CoreUtils.Destroy(_fogMaterial);
            _fogMaterial = null;
        }

        public override bool CanPresentCameraColorToBackBuffer(ref NWRPFrameData frameData)
        {
            return CanPresentFinalFog(ref frameData);
        }

        public override NWRPFramePassResourceUsage GetFrameResourceUsage(
            ref NWRPFrameData frameData)
        {
            NWRPFramePassResourceUsage usage =
                NWRPFramePassResourceUsage.CameraColorReadWrite(
                    CanPresentFinalFog(ref frameData));
            usage.cameraDepthTexture = NWRPFrameResourceAccess.Read;
            return usage;
        }

        private static bool CanPresentFinalFog(ref NWRPFrameData frameData)
        {
            return ValleyHeightFogFeature.IsActive(ref frameData)
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
            return ValleyHeightFogFeature.IsActive(ref frameData);
        }

        bool INWRPFullscreenEffectNode.CanPresentToBackBuffer(
            ref NWRPFrameData frameData)
        {
            return CanPresentFinalFog(ref frameData);
        }

        bool INWRPFullscreenEffectNode.Prepare(ref NWRPFrameData frameData)
        {
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

            if (_fogMaterial == null)
            {
                return false;
            }

            UploadConstants(frameData.cmd, frameData.valleyHeightFog);
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

            fullscreenPass = new NWRPFullscreenEffectPass(
                _fogMaterial,
                GetShaderPassIndex(frameData.valleyHeightFog));
            return true;
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
