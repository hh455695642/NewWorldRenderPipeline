using UnityEngine;

namespace NWRP.Runtime.Passes
{
    internal readonly struct NWRPFullscreenEffectPass
    {
        public readonly Material material;
        public readonly int passIndex;

        public NWRPFullscreenEffectPass(Material material, int passIndex)
        {
            this.material = material;
            this.passIndex = passIndex;
        }

        public bool IsValid => material != null && passIndex >= 0;
    }

    internal interface INWRPFullscreenEffectNode
    {
        NWRPPassEvent PassEvent { get; }
        bool RequiresDepthTexture { get; }
        bool RequiresOpaqueTexture { get; }

        bool IsActive(ref NWRPFrameData frameData);

        bool CanPresentToBackBuffer(ref NWRPFrameData frameData);

        bool Prepare(ref NWRPFrameData frameData);

        int GetPassCount(ref NWRPFrameData frameData);

        bool TryGetPass(
            ref NWRPFrameData frameData,
            int passIndex,
            bool isFinalPass,
            out NWRPFullscreenEffectPass fullscreenPass);
    }
}
