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
        public RenderTargetIdentifier cameraColor;
        public RenderTargetIdentifier cameraDepth;
        public bool hasCameraTargets;
    }
}
