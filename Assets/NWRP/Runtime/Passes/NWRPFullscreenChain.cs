using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal readonly struct NWRPFullscreenSource
    {
        private readonly RTHandle _handle;
        private readonly RenderTargetIdentifier _identifier;

        private NWRPFullscreenSource(
            RTHandle handle,
            RenderTargetIdentifier identifier,
            bool isHandle)
        {
            _handle = handle;
            _identifier = identifier;
            IsHandle = isHandle;
        }

        public bool IsHandle { get; }
        public RTHandle Handle => _handle;
        public RenderTargetIdentifier Identifier => _identifier;

        public static NWRPFullscreenSource FromHandle(RTHandle handle)
        {
            return new NWRPFullscreenSource(handle, default, true);
        }

        public static NWRPFullscreenSource FromIdentifier(
            RenderTargetIdentifier identifier)
        {
            return new NWRPFullscreenSource(null, identifier, false);
        }
    }

    internal sealed class NWRPFullscreenChain
    {
        private const int k_InitialPassCapacity = 4;

        private Material _copyMaterial;
        private NWRPFullscreenEffectPass[] _passBuffer =
            new NWRPFullscreenEffectPass[k_InitialPassCapacity];

        public bool Execute(
            ref NWRPFrameData frameData,
            NWRPPass ownerPass,
            INWRPFullscreenEffectNode node)
        {
            if (ownerPass == null
                || node == null
                || !node.IsActive(ref frameData)
                || !HasRequiredTargets(ref frameData, node)
                || frameData.targets.cameraColorHandle == null
                || frameData.targets.cameraColorHandle.rt == null
                || frameData.camera == null)
            {
                return false;
            }

            if (!node.Prepare(ref frameData))
            {
                return false;
            }

            int passCount = Mathf.Max(0, node.GetPassCount(ref frameData));
            if (passCount <= 0 || !CachePasses(ref frameData, node, passCount))
            {
                return false;
            }

            bool presentToBackBuffer =
                node.CanPresentToBackBuffer(ref frameData)
                && frameData.frameGraph.IsCameraColorFinalPresentPass(ownerPass);
            bool needsCopyBackToCameraColor = !presentToBackBuffer && passCount == 1;
            if (needsCopyBackToCameraColor && !EnsureCopyMaterial())
            {
                return false;
            }

            RTHandle cameraColor = frameData.targets.cameraColorHandle;
            RenderTextureDescriptor descriptor =
                NWRPFullscreenPassUtils.CreateColorDescriptor(cameraColor.rt);
            Rect viewport = NWRPRenderer.GetCameraRenderViewport(ref frameData);
            int tempCount = ResolveTempCount(passCount, presentToBackBuffer);

            AllocateTemps(ref frameData, descriptor, tempCount);
            frameData.debugStats.RecordFullscreenChainNode();

            bool presentedToBackBuffer = false;
            try
            {
                NWRPFullscreenSource source =
                    NWRPFullscreenSource.FromHandle(cameraColor);
                NWRPFullscreenSource finalSource = source;

                for (int i = 0; i < passCount; i++)
                {
                    bool isFinalPass = i == passCount - 1;
                    NWRPFullscreenEffectPass fullscreenPass = _passBuffer[i];

                    if (isFinalPass && presentToBackBuffer)
                    {
                        BlitToBackBuffer(
                            ref frameData,
                            source,
                            fullscreenPass.material,
                            fullscreenPass.passIndex);
                        presentedToBackBuffer = true;
                        continue;
                    }

                    RenderTargetIdentifier destination =
                        ResolveDestination(
                            ref frameData,
                            i,
                            passCount,
                            tempCount);
                    BlitToTarget(
                        ref frameData,
                        source,
                        destination,
                        viewport,
                        fullscreenPass.material,
                        fullscreenPass.passIndex);
                    finalSource = NWRPFullscreenSource.FromIdentifier(destination);
                    source = finalSource;
                }

                if (needsCopyBackToCameraColor)
                {
                    BlitToTarget(
                        ref frameData,
                        finalSource,
                        frameData.targets.cameraColor,
                        viewport,
                        _copyMaterial,
                        0);
                }

                return true;
            }
            finally
            {
                ReleaseTemps(frameData.cmd, tempCount);
                if (!presentedToBackBuffer)
                {
                    NWRPRenderer.RestoreCameraRenderTarget(ref frameData);
                }
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_copyMaterial);
            _copyMaterial = null;
        }

        private bool CachePasses(
            ref NWRPFrameData frameData,
            INWRPFullscreenEffectNode node,
            int passCount)
        {
            EnsurePassBuffer(passCount);
            for (int i = 0; i < passCount; i++)
            {
                bool isFinalPass = i == passCount - 1;
                if (!node.TryGetPass(
                        ref frameData,
                        i,
                        isFinalPass,
                        out NWRPFullscreenEffectPass fullscreenPass)
                    || !fullscreenPass.IsValid)
                {
                    return false;
                }

                _passBuffer[i] = fullscreenPass;
            }

            return true;
        }

        private void EnsurePassBuffer(int passCount)
        {
            if (_passBuffer.Length >= passCount)
            {
                return;
            }

            int capacity = _passBuffer.Length;
            while (capacity < passCount)
            {
                capacity *= 2;
            }

            Array.Resize(ref _passBuffer, capacity);
        }

        private static bool HasRequiredTargets(
            ref NWRPFrameData frameData,
            INWRPFullscreenEffectNode node)
        {
            if (node.RequiresDepthTexture
                && (!frameData.targets.hasCameraDepthTexture
                    || frameData.targets.cameraDepthTextureHandle == null))
            {
                return false;
            }

            if (node.RequiresOpaqueTexture
                && (!frameData.targets.hasOpaqueTexture
                    || frameData.targets.opaqueTextureHandle == null))
            {
                return false;
            }

            return true;
        }

        private static int ResolveTempCount(int passCount, bool presentToBackBuffer)
        {
            if (passCount <= 1)
            {
                return presentToBackBuffer ? 0 : 1;
            }

            return passCount > 2 ? 2 : 1;
        }

        private static RenderTargetIdentifier ResolveDestination(
            ref NWRPFrameData frameData,
            int passIndex,
            int passCount,
            int tempCount)
        {
            bool isFinalPass = passIndex == passCount - 1;
            if (isFinalPass && passCount > 1)
            {
                return frameData.targets.cameraColor;
            }

            if (tempCount <= 1 || (passIndex & 1) == 0)
            {
                return NWRPFullscreenPassUtils.GetTempColorId(
                    NWRPFullscreenTempSlot.A);
            }

            return NWRPFullscreenPassUtils.GetTempColorId(
                NWRPFullscreenTempSlot.B);
        }

        private static void AllocateTemps(
            ref NWRPFrameData frameData,
            in RenderTextureDescriptor descriptor,
            int tempCount)
        {
            if (tempCount <= 0)
            {
                return;
            }

            NWRPFullscreenPassUtils.AllocateTempColor(
                ref frameData,
                NWRPFullscreenTempSlot.A,
                descriptor,
                FilterMode.Bilinear);
            frameData.debugStats.RecordFullscreenChainTempRT();

            if (tempCount <= 1)
            {
                return;
            }

            NWRPFullscreenPassUtils.AllocateTempColor(
                ref frameData,
                NWRPFullscreenTempSlot.B,
                descriptor,
                FilterMode.Bilinear);
            frameData.debugStats.RecordFullscreenChainTempRT();
        }

        private static void ReleaseTemps(CommandBuffer cmd, int tempCount)
        {
            if (tempCount <= 0 || cmd == null)
            {
                return;
            }

            NWRPFullscreenPassUtils.ReleaseTempColor(
                cmd,
                NWRPFullscreenTempSlot.A);

            if (tempCount > 1)
            {
                NWRPFullscreenPassUtils.ReleaseTempColor(
                    cmd,
                    NWRPFullscreenTempSlot.B);
            }
        }

        private static void BlitToTarget(
            ref NWRPFrameData frameData,
            NWRPFullscreenSource source,
            RenderTargetIdentifier destination,
            Rect viewport,
            Material material,
            int passIndex)
        {
            if (source.IsHandle)
            {
                NWRPFullscreenPassUtils.BlitToTarget(
                    ref frameData,
                    source.Handle,
                    destination,
                    viewport,
                    material,
                    passIndex);
                return;
            }

            NWRPFullscreenPassUtils.BlitToTarget(
                ref frameData,
                source.Identifier,
                destination,
                viewport,
                material,
                passIndex);
        }

        private static void BlitToBackBuffer(
            ref NWRPFrameData frameData,
            NWRPFullscreenSource source,
            Material material,
            int passIndex)
        {
            if (source.IsHandle)
            {
                NWRPFullscreenPassUtils.BlitToBackBuffer(
                    ref frameData,
                    source.Handle,
                    material,
                    passIndex);
                return;
            }

            NWRPFullscreenPassUtils.BlitToBackBuffer(
                ref frameData,
                source.Identifier,
                material,
                passIndex);
        }

        private bool EnsureCopyMaterial()
        {
            if (_copyMaterial != null)
            {
                return true;
            }

            _copyMaterial = NWRPBlitterResources.CreateCoreBlitMaterial();
            return _copyMaterial != null;
        }
    }
}
