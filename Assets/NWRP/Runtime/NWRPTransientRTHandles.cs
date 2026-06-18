using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    internal static class NWRPTransientRTHandles
    {
        public static void ReAllocateIfNeeded(
            ref RTHandle handle,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            TextureWrapMode wrapMode,
            string name)
        {
            if (IsCompatible(handle, descriptor, filterMode, wrapMode))
            {
                return;
            }

            Release(ref handle);
            handle = RTHandles.Alloc(
                descriptor,
                filterMode,
                wrapMode,
                name: name);
        }

        public static void Release(ref RTHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            RTHandles.Release(handle);
            handle = null;
        }

        private static bool IsCompatible(
            RTHandle handle,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            TextureWrapMode wrapMode)
        {
            RenderTexture renderTexture = handle != null ? handle.rt : null;
            if (renderTexture == null)
            {
                return false;
            }

            RenderTextureDescriptor current = renderTexture.descriptor;
            return current.width == descriptor.width
                && current.height == descriptor.height
                && current.volumeDepth == descriptor.volumeDepth
                && current.graphicsFormat == descriptor.graphicsFormat
                && current.depthStencilFormat == descriptor.depthStencilFormat
                && current.depthBufferBits == descriptor.depthBufferBits
                && current.msaaSamples == descriptor.msaaSamples
                && current.dimension == descriptor.dimension
                && current.useMipMap == descriptor.useMipMap
                && current.autoGenerateMips == descriptor.autoGenerateMips
                && current.bindMS == descriptor.bindMS
                && current.memoryless == descriptor.memoryless
                && current.vrUsage == descriptor.vrUsage
                && renderTexture.filterMode == filterMode
                && renderTexture.wrapMode == wrapMode;
        }
    }
}
