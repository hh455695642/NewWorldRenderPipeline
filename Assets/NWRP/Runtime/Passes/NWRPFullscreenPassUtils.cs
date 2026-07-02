using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal enum NWRPFullscreenTempSlot
    {
        A,
        B
    }

    internal static class NWRPFullscreenPassUtils
    {
        internal static readonly int FullscreenTempA =
            Shader.PropertyToID("_NWRPFullscreenTempColorA");
        internal static readonly int FullscreenTempB =
            Shader.PropertyToID("_NWRPFullscreenTempColorB");

        private static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

        internal static int GetTempColorId(NWRPFullscreenTempSlot slot)
        {
            return slot == NWRPFullscreenTempSlot.B ? FullscreenTempB : FullscreenTempA;
        }

        internal static RenderTextureDescriptor CreateColorDescriptor(
            RenderTexture sourceTexture)
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

        internal static void AllocateTempColor(
            ref NWRPFrameData frameData,
            NWRPFullscreenTempSlot slot,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode)
        {
            AllocateTempColor(
                ref frameData,
                GetTempColorId(slot),
                descriptor,
                filterMode);
        }

        internal static void AllocateTempColor(
            ref NWRPFrameData frameData,
            int textureId,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode)
        {
            if (frameData.transientResources != null)
            {
                NWRPFrameResourceDesc resourceDesc = NWRPFrameResourceDesc.Color(
                    descriptor.width,
                    descriptor.height,
                    descriptor.graphicsFormat,
                    filterMode);
                frameData.transientResources.Allocate(
                    resourceDesc,
                    frameData.currentPassIndex,
                    frameData.currentPassIndex);
            }

            frameData.cmd.GetTemporaryRT(textureId, descriptor, filterMode);
            frameData.debugStats.RecordTemporaryRT(NWRPFrameTemporaryRTKind.Color);
        }

        internal static void ReleaseTempColor(
            CommandBuffer cmd,
            NWRPFullscreenTempSlot slot)
        {
            ReleaseTempColor(cmd, GetTempColorId(slot));
        }

        internal static void ReleaseTempColor(CommandBuffer cmd, int textureId)
        {
            cmd.ReleaseTemporaryRT(textureId);
        }

        internal static void BlitToTarget(
            ref NWRPFrameData frameData,
            RTHandle source,
            RenderTargetIdentifier destination,
            Rect viewport,
            Material material,
            int passIndex)
        {
            SetFullscreenTarget(ref frameData, destination, viewport);
            Blitter.BlitTexture(
                frameData.cmd,
                source,
                s_FullScaleBias,
                material,
                passIndex);
        }

        internal static void BlitToTarget(
            ref NWRPFrameData frameData,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            Rect viewport,
            Material material,
            int passIndex)
        {
            SetFullscreenTarget(ref frameData, destination, viewport);
            Blitter.BlitTexture(
                frameData.cmd,
                source,
                s_FullScaleBias,
                material,
                passIndex);
        }

        internal static void BlitToTarget(
            ref NWRPFrameData frameData,
            RTHandle source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            Material material,
            int passIndex)
        {
            BlitToTarget(
                ref frameData,
                source,
                destination,
                MakeViewport(width, height),
                material,
                passIndex);
        }

        internal static void BlitToTarget(
            ref NWRPFrameData frameData,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            Material material,
            int passIndex)
        {
            BlitToTarget(
                ref frameData,
                source,
                destination,
                MakeViewport(width, height),
                material,
                passIndex);
        }

        internal static void BlitToBackBuffer(
            ref NWRPFrameData frameData,
            RTHandle source,
            Material material,
            int passIndex)
        {
            BlitToBackBuffer(ref frameData, source, material, passIndex, true);
        }

        internal static void BlitToBackBuffer(
            ref NWRPFrameData frameData,
            RTHandle source,
            Material material,
            int passIndex,
            bool recordFinalFusion)
        {
            SetBackBufferTarget(ref frameData, recordFinalFusion);
            Blitter.BlitTexture(
                frameData.cmd,
                source,
                NWRPRenderer.GetFinalBlitScaleBias(frameData.camera, source),
                material,
                passIndex);
            frameData.targets.cameraColorPresented = true;
        }

        internal static void BlitToBackBuffer(
            ref NWRPFrameData frameData,
            RenderTargetIdentifier source,
            Material material,
            int passIndex)
        {
            BlitToBackBuffer(ref frameData, source, material, passIndex, true);
        }

        internal static void BlitToBackBuffer(
            ref NWRPFrameData frameData,
            RenderTargetIdentifier source,
            Material material,
            int passIndex,
            bool recordFinalFusion)
        {
            SetBackBufferTarget(ref frameData, recordFinalFusion);
            Blitter.BlitTexture(
                frameData.cmd,
                source,
                NWRPRenderer.GetFinalBlitScaleBias(frameData.camera),
                material,
                passIndex);
            frameData.targets.cameraColorPresented = true;
        }

        private static Rect MakeViewport(int width, int height)
        {
            return new Rect(0f, 0f, Mathf.Max(width, 1), Mathf.Max(height, 1));
        }

        private static void SetFullscreenTarget(
            ref NWRPFrameData frameData,
            RenderTargetIdentifier destination,
            Rect viewport)
        {
            NWRPRenderer.InvalidateCameraRenderTarget(ref frameData);
            frameData.debugStats.RecordFullscreenBlit();
            NWRPRenderer.SetFullscreenScaleBiasRt(
                ref frameData,
                isGameBackBufferTarget: false);
            CoreUtils.SetRenderTarget(
                frameData.cmd,
                destination,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            frameData.cmd.SetViewport(viewport);
        }

        private static void SetBackBufferTarget(
            ref NWRPFrameData frameData,
            bool recordFinalFusion)
        {
            Rect cameraViewport = NWRPRenderer.GetCameraViewport(frameData.camera);
            RenderBufferLoadAction loadAction =
                NWRPRenderer.IsDefaultViewport(frameData.camera, cameraViewport)
                    ? RenderBufferLoadAction.DontCare
                    : RenderBufferLoadAction.Load;

            NWRPRenderer.InvalidateCameraRenderTarget(ref frameData);
            frameData.debugStats.RecordFinalBlit();
            if (recordFinalFusion)
            {
                frameData.debugStats.RecordCameraColorFinalPassFusion();
            }

            bool isGameBackBufferTarget =
                frameData.camera != null
                && frameData.camera.cameraType == CameraType.Game
                && frameData.camera.targetTexture == null;
            NWRPRenderer.SetFullscreenScaleBiasRt(
                ref frameData,
                isGameBackBufferTarget);

            CoreUtils.SetRenderTarget(
                frameData.cmd,
                frameData.targets.backBufferColor,
                loadAction,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            frameData.cmd.SetViewport(cameraViewport);
        }
    }
}
