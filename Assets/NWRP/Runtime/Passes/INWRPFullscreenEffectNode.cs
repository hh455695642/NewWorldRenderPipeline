using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal interface INWRPFullscreenEffectNode
    {
        NWRPPassEvent PassEvent { get; }
        bool RequiresDepthTexture { get; }

        bool IsActive(ref NWRPFrameData frameData);

        void Execute(
            ref NWRPFrameData frameData,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            bool destinationIsBackBuffer);
    }
}
