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
        public NWRPCameraData cameraData;
        public VolumeStack volumeStack;
        public bool postProcessingEnabled;
        public bool tonemappingActive;
        public bool bloomActive;
        public bool colorAdjustmentsActive;
        public bool vignetteActive;
        public bool antiAliasingActive;
        public bool valleyHeightFogActive;
        public bool fogActive;
        public NWRPFogMode fogMode;
        public Color fogColor;
        public float fogStartDistance;
        public float fogEndDistance;
        public float fogDensity;
        public NWRPTonemapping tonemapping;
        public NWRPBloom bloom;
        public NWRPColorAdjustments colorAdjustments;
        public NWRPVignette vignette;
        public NWRPAntiAliasing antiAliasing;
        public NWRPValleyHeightFog valleyHeightFog;
        public NWRPFog fog;
        public NWRPFrameTargets targets;
        public float resolvedRenderScale;
        public int cameraTargetWidth;
        public int cameraTargetHeight;
        public FilterMode renderScaleFilterMode;
        public bool renderScaleActive;
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
        public RenderTargetIdentifier cameraDepthTexture;
        public RenderTargetIdentifier opaqueTexture;
        public RTHandle backBufferColorHandle;
        public RTHandle cameraColorHandle;
        public RTHandle cameraDepthHandle;
        public RTHandle cameraDepthTextureHandle;
        public RTHandle opaqueTextureHandle;
        public bool hasCameraTargets;
        public bool ownsIntermediateColor;
        public bool ownsIntermediateDepth;
        public bool ownsCameraDepthTexture;
        public bool ownsOpaqueTexture;
        public bool usesIntermediateColor;
        public bool usesIntermediateDepth;
        public bool hasCameraDepthTexture;
        public bool cameraDepthTextureIsDepthTarget;
        public bool hasOpaqueTexture;
        public bool cameraColorPresented;
    }

    /// <summary>
    /// Per-frame target requests declared by features before pass queue construction.
    /// </summary>
    public struct NWRPFrameTargetRequirements
    {
        public bool requiresIntermediateColor;
        public bool requiresIntermediateDepth;
        public bool requiresDepthTexture;
        public bool requiresDepthTextureCopy;
        public bool requiresDepthTexturePrepass;
        public bool requiresOpaqueTexture;

        public void Merge(NWRPFrameTargetRequirements other)
        {
            requiresIntermediateColor |= other.requiresIntermediateColor;
            requiresIntermediateDepth |= other.requiresIntermediateDepth;
            requiresDepthTexture |= other.requiresDepthTexture;
            requiresDepthTextureCopy |= other.requiresDepthTextureCopy;
            requiresDepthTexturePrepass |= other.requiresDepthTexturePrepass;
            requiresOpaqueTexture |= other.requiresOpaqueTexture;
        }
    }
}
