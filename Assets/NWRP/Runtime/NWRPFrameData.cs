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
        public NWRPRendererData rendererData;
        public int rendererDataIndex;
        public NWRPCameraData cameraData;
        public VolumeStack volumeStack;
        public bool postProcessingEnabled;
        public bool tonemappingActive;
        public bool bloomActive;
        public bool colorAdjustmentsActive;
        public bool vignetteActive;
        public bool antiAliasingActive;
        public bool screenBlurActive;
        public bool valleyHeightFogActive;
        public bool cloudShadowProjectorActive;
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
        public NWRPScreenBlur screenBlur;
        public NWRPValleyHeightFog valleyHeightFog;
        public NWRPCloudShadowProjector cloudShadowProjector;
        public NWRPFog fog;
        public NWRPFrameTargets targets;
        public float resolvedRenderScale;
        public int cameraTargetWidth;
        public int cameraTargetHeight;
        public FilterMode renderScaleFilterMode;
        public bool renderScaleActive;
        public NWRPCameraAttachmentState cameraAttachmentState;
        public NWRPFrameGraphData frameGraph;
        public NWRPTransientResourceAllocator transientResources;
        public int currentPassIndex;
        public NWRPFrameDebugStats debugStats;
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
        public bool cameraDepthTextureWrittenByPrepass;
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

    public enum NWRPFrameResourceAccess
    {
        None,
        Read,
        Write,
        ReadWrite
    }

    public struct NWRPFramePassResourceUsage
    {
        public NWRPFrameResourceAccess cameraColor;
        public NWRPFrameResourceAccess cameraDepth;
        public NWRPFrameResourceAccess cameraDepthTexture;
        public NWRPFrameResourceAccess opaqueTexture;
        public NWRPFrameResourceAccess transientColor;
        public bool keepsCameraDepthAfterPass;
        public bool canPresentCameraColorToBackBuffer;
        public bool writesBackBuffer;

        public static NWRPFramePassResourceUsage CameraColorReadWrite(
            bool canPresentToBackBuffer)
        {
            return new NWRPFramePassResourceUsage
            {
                cameraColor = NWRPFrameResourceAccess.ReadWrite,
                canPresentCameraColorToBackBuffer = canPresentToBackBuffer
            };
        }

        public bool ReadsCameraColor => Reads(cameraColor);
        public bool WritesCameraColor => Writes(cameraColor);
        public bool UsesCameraColor => Reads(cameraColor) || Writes(cameraColor);
        public bool UsesCameraDepth => Reads(cameraDepth) || Writes(cameraDepth);
        public bool ReadsCameraDepthTexture => Reads(cameraDepthTexture);
        public bool ReadsOpaqueTexture => Reads(opaqueTexture);
        public bool UsesTransientColor => Reads(transientColor) || Writes(transientColor);

        private static bool Reads(NWRPFrameResourceAccess access)
        {
            return access == NWRPFrameResourceAccess.Read
                || access == NWRPFrameResourceAccess.ReadWrite;
        }

        private static bool Writes(NWRPFrameResourceAccess access)
        {
            return access == NWRPFrameResourceAccess.Write
                || access == NWRPFrameResourceAccess.ReadWrite;
        }
    }

    /// <summary>
    /// Lightweight per-camera frame graph decisions derived from the queued pass list.
    /// </summary>
    public struct NWRPFrameGraphData
    {
        public NWRPPass cameraColorFinalPresentPass;
        public int cameraColorReadPassCount;
        public int cameraColorWritePassCount;
        public int depthTextureReadPassCount;
        public int opaqueTextureReadPassCount;
        public int cameraColorFinalPresentPassIndex;
        public int cameraDepthLastUsePassIndex;
        public int renderPassClusterCount;
        public bool canDiscardCameraDepthAfterLastUse;
        public bool hasBackBufferWriterBeforeDebug;

        public void RecordPassUsage(NWRPFramePassResourceUsage usage)
        {
            if (usage.ReadsCameraColor)
            {
                cameraColorReadPassCount++;
            }

            if (usage.WritesCameraColor)
            {
                cameraColorWritePassCount++;
            }

            if (usage.ReadsCameraDepthTexture)
            {
                depthTextureReadPassCount++;
            }

            if (usage.ReadsOpaqueTexture)
            {
                opaqueTextureReadPassCount++;
            }

            hasBackBufferWriterBeforeDebug |= usage.writesBackBuffer;
        }

        public bool IsCameraColorFinalPresentPass(NWRPPass pass)
        {
            return pass != null && ReferenceEquals(cameraColorFinalPresentPass, pass);
        }
    }
}
