using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    /// <summary>
    /// Unified post-processing pass for NWRP.
    ///
    /// V1 only executes final tonemapping, but Bloom/LUT/Sharpen/DOF should be added
    /// inside this pass instead of enqueueing one external NWRPPass per effect. This keeps
    /// the frame graph simple: Transparent -> NWRP PostProcess -> DebugOverlay/FinalBlit.
    /// </summary>
    public sealed class PostProcessPass : NWRPPass
    {
        private const string k_TonemappingShaderName = "Hidden/NWRP/PostProcess/Tonemapping";

        private Material _tonemappingMaterial;

        public PostProcessPass()
            : base(NWRPPassEvent.PostProcess, "NWRP PostProcess")
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!PostProcessFeature.HasAnyActivePostProcess(ref frameData)
                || frameData.targets.cameraColorHandle == null
                || frameData.camera == null)
            {
                return;
            }

            // V1 has no intermediate post-process chain. Tonemapping is the final operator
            // and writes directly to the camera/backbuffer target to avoid an extra FinalBlit.
            if (PostProcessFeature.IsTonemappingActive(ref frameData))
            {
                ExecuteTonemappingFinal(ref frameData);
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_tonemappingMaterial);
            _tonemappingMaterial = null;
        }

        private void ExecuteTonemappingFinal(ref NWRPFrameData frameData)
        {
            NWRPTonemapping tonemapping = frameData.tonemapping;
            if (tonemapping == null || !EnsureTonemappingMaterial())
            {
                return;
            }

            int passIndex = GetTonemappingPassIndex(tonemapping.mode.value);
            if (passIndex < 0)
            {
                return;
            }

            CommandBuffer cmd = frameData.cmd;
            RTHandle source = frameData.targets.cameraColorHandle;
            Rect cameraViewport = NWRPRenderer.GetCameraViewport(frameData.camera);
            RenderBufferLoadAction loadAction =
                NWRPRenderer.IsDefaultViewport(frameData.camera, cameraViewport)
                    ? RenderBufferLoadAction.DontCare
                    : RenderBufferLoadAction.Load;

            _tonemappingMaterial.SetVector(
                NWRPShaderIds.TonemapParams,
                new Vector4(
                    Mathf.Max(0f, tonemapping.preExposure.value),
                    Mathf.Max(0f, tonemapping.postBrightness.value),
                    Mathf.Max(0f, tonemapping.maxInputBrightness.value),
                    Mathf.Max(0f, tonemapping.agxGamma.value)));

            CoreUtils.SetRenderTarget(
                cmd,
                frameData.targets.backBufferColor,
                loadAction,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            cmd.SetViewport(cameraViewport);

            Blitter.BlitTexture(
                cmd,
                source,
                NWRPRenderer.GetFinalBlitScaleBias(frameData.camera, source),
                _tonemappingMaterial,
                passIndex);

            frameData.targets.cameraColorPresented = true;
        }

        private bool EnsureTonemappingMaterial()
        {
            if (_tonemappingMaterial != null)
            {
                return true;
            }

            Shader shader = Shader.Find(k_TonemappingShaderName);
            if (shader == null)
            {
                Debug.LogError("NWRP tonemapping requires Hidden/NWRP/PostProcess/Tonemapping.");
                return false;
            }

            _tonemappingMaterial = CoreUtils.CreateEngineMaterial(shader);
            return _tonemappingMaterial != null;
        }

        private static int GetTonemappingPassIndex(NWRPTonemappingMode mode)
        {
            switch (mode)
            {
                case NWRPTonemappingMode.Linear:
                    return 0;
                case NWRPTonemappingMode.ACES:
                    return 1;
                case NWRPTonemappingMode.ACESFitted:
                    return 2;
                case NWRPTonemappingMode.AGX:
                    return 3;
                default:
                    return -1;
            }
        }
    }
}
