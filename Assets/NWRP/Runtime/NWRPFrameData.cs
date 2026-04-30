using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// Per-camera frame context passed through all NWRP passes.
    /// </summary>
    public struct NWRPFrameData
    {
        public ScriptableRenderContext context;
        public Camera camera;
        public CullingResults cullingResults;
        public CommandBuffer cmd;
        public NewWorldRenderPipelineAsset asset;
        public NWRPFrameTargets targets;
    }

    /// <summary>
    /// Reserved shared target handles for pass-to-pass communication.
    /// </summary>
    public struct NWRPFrameTargets
    {
        public RenderTargetIdentifier backBufferColor;
        public RenderTargetIdentifier backBufferDepth;
        public RenderTargetIdentifier cameraColor;
        public RenderTargetIdentifier cameraDepth;
        public RenderTargetIdentifier opaqueTexture;
        public RTHandle backBufferColorHandle;
        public RTHandle cameraColorHandle;
        public RTHandle cameraDepthHandle;
        public RTHandle opaqueTextureHandle;
        public bool hasCameraTargets;
        public bool ownsIntermediateColor;
        public bool ownsIntermediateDepth;
        public bool ownsOpaqueTexture;
        public bool usesIntermediateColor;
        public bool usesIntermediateDepth;
        public bool hasOpaqueTexture;
    }

    /// <summary>
    /// Per-frame target requests declared by features before pass queue construction.
    /// </summary>
    public struct NWRPFrameTargetRequirements
    {
        public bool requiresIntermediateColor;
        public bool requiresIntermediateDepth;
        public bool requiresOpaqueTexture;

        public void Merge(NWRPFrameTargetRequirements other)
        {
            requiresIntermediateColor |= other.requiresIntermediateColor;
            requiresIntermediateDepth |= other.requiresIntermediateDepth;
            requiresOpaqueTexture |= other.requiresOpaqueTexture;
        }
    }
}
