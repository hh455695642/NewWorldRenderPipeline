using UnityEngine;

namespace NWRP
{
    /// <summary>
    /// Centralized shader property IDs used by the runtime renderer.
    /// </summary>
    public static class NWRPShaderIds
    {
        // Camera targets
        public static readonly int CameraColorTexture = Shader.PropertyToID("_NWRPCameraColorTexture");
        public static readonly int CameraDepthAttachment = Shader.PropertyToID("_CameraDepthAttachment");
        public static readonly int CameraDepthAttachmentTexelSize = Shader.PropertyToID("_CameraDepthAttachment_TexelSize");
        public static readonly int CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
        public static readonly int CameraDepthTextureScaleBias = Shader.PropertyToID("_CameraDepthTextureScaleBias");
        public static readonly int CameraOpaqueTexture = Shader.PropertyToID("_CameraOpaqueTexture");
        public static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        public static readonly int ScreenParams = Shader.PropertyToID("_ScreenParams");
        public static readonly int ScaledScreenParams = Shader.PropertyToID("_ScaledScreenParams");
        public static readonly int ScaleBiasRt = Shader.PropertyToID("_ScaleBiasRt");
        public static readonly int TonemapParams = Shader.PropertyToID("_NWRPTonemapParams");
        public static readonly int ColorAdjustParams = Shader.PropertyToID("_NWRPColorAdjustParams");
        public static readonly int ColorAdjustParams2 = Shader.PropertyToID("_NWRPColorAdjustParams2");
        public static readonly int ColorAdjustTint = Shader.PropertyToID("_NWRPColorAdjustTint");
        public static readonly int VignetteColor = Shader.PropertyToID("_NWRPVignetteColor");
        public static readonly int VignetteParams = Shader.PropertyToID("_NWRPVignetteParams");
        public static readonly int VignetteParams2 = Shader.PropertyToID("_NWRPVignetteParams2");
        public static readonly int FogMode = Shader.PropertyToID("_NWRPFogMode");
        public static readonly int FogParams = Shader.PropertyToID("_NWRPFogParams");
        public static readonly int FogColor = Shader.PropertyToID("_NWRPFogColor");
        public static readonly int BloomThresholdParams = Shader.PropertyToID("_NWRPBloomThresholdParams");
        public static readonly int BloomCompositeParams = Shader.PropertyToID("_NWRPBloomCompositeParams");
        public static readonly int BloomTexelSize = Shader.PropertyToID("_NWRPBloomTexelSize");
        public static readonly int BloomBlurScale = Shader.PropertyToID("_NWRPBloomBlurScale");
        public static readonly int BloomSpread = Shader.PropertyToID("_NWRPBloomSpread");
        public static readonly int BloomWeights = Shader.PropertyToID("_NWRPBloomWeights");
        public static readonly int BloomWeights2 = Shader.PropertyToID("_NWRPBloomWeights2");
        public static readonly int BloomTint = Shader.PropertyToID("_NWRPBloomTint");
        public static readonly int BloomTint0 = Shader.PropertyToID("_NWRPBloomTint0");
        public static readonly int BloomTint1 = Shader.PropertyToID("_NWRPBloomTint1");
        public static readonly int BloomTint2 = Shader.PropertyToID("_NWRPBloomTint2");
        public static readonly int BloomTint3 = Shader.PropertyToID("_NWRPBloomTint3");
        public static readonly int BloomTint4 = Shader.PropertyToID("_NWRPBloomTint4");
        public static readonly int BloomTint5 = Shader.PropertyToID("_NWRPBloomTint5");
        public static readonly int BloomTexture = Shader.PropertyToID("_NWRPBloomTexture");
        public static readonly int BloomTexture1 = Shader.PropertyToID("_NWRPBloomTexture1");
        public static readonly int BloomTexture2 = Shader.PropertyToID("_NWRPBloomTexture2");
        public static readonly int BloomTexture3 = Shader.PropertyToID("_NWRPBloomTexture3");
        public static readonly int BloomTexture4 = Shader.PropertyToID("_NWRPBloomTexture4");
        public static readonly int BloomCombineTexture = Shader.PropertyToID("_NWRPBloomCombineTexture");
        public static readonly int BloomDirtTexture = Shader.PropertyToID("_NWRPBloomDirtTexture");
        public static readonly int BloomDirtSourceTexture = Shader.PropertyToID("_NWRPBloomDirtSourceTexture");
        public static readonly int InverseViewMatrix = Shader.PropertyToID("unity_MatrixInvV");
        public static readonly int InverseProjectionMatrix = Shader.PropertyToID("unity_MatrixInvP");
        public static readonly int InverseViewProjectionMatrix = Shader.PropertyToID("unity_MatrixInvVP");

        // Lighting globals
        public static readonly int MainLightPosition = Shader.PropertyToID("_MainLightPosition");
        public static readonly int MainLightColor = Shader.PropertyToID("_MainLightColor");
        public static readonly int AdditionalLightsCount = Shader.PropertyToID("_AdditionalLightsCount");
        public static readonly int AdditionalLightsPosition = Shader.PropertyToID("_AdditionalLightsPosition");
        public static readonly int AdditionalLightsColor = Shader.PropertyToID("_AdditionalLightsColor");
        public static readonly int AdditionalLightsAttenuation = Shader.PropertyToID("_AdditionalLightsAttenuation");
        public static readonly int AdditionalLightsSpotDir = Shader.PropertyToID("_AdditionalLightsSpotDir");
        public static readonly int AdditionalLightsShadowmapTexture = Shader.PropertyToID("_AdditionalLightsShadowmapTexture");
        public static readonly int AdditionalLightsWorldToShadow = Shader.PropertyToID("_AdditionalLightsShadowSliceWorldToShadow");
        public static readonly int AdditionalLightsShadowParams = Shader.PropertyToID("_AdditionalLightsShadowParams");
        public static readonly int AdditionalLightsShadowAtlasRects = Shader.PropertyToID("_AdditionalLightsShadowSliceAtlasRects");
        public static readonly int AdditionalLightsShadowAtlasSize = Shader.PropertyToID("_AdditionalLightsShadowAtlasSize");
        public static readonly int AdditionalLightsShadowGlobalParams = Shader.PropertyToID("_AdditionalLightsShadowGlobalParams");
        public static readonly int AdditionalLightsShadowFilterMode = Shader.PropertyToID("_AdditionalLightsShadowFilterMode");
        public static readonly int AdditionalLightsShadowFilterRadius = Shader.PropertyToID("_AdditionalLightsShadowFilterRadius");

        // Main light shadow globals
        public static readonly int MainLightShadowmapTexture = Shader.PropertyToID("_MainLightShadowmapTexture");
        public static readonly int MainLightWorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
        public static readonly int MainLightShadowParams = Shader.PropertyToID("_MainLightShadowParams");
        public static readonly int MainLightShadowmapSize = Shader.PropertyToID("_MainLightShadowmapSize");
        public static readonly int MainLightShadowFilterMode = Shader.PropertyToID("_MainLightShadowFilterMode");
        public static readonly int MainLightShadowFilterRadius = Shader.PropertyToID("_MainLightShadowFilterRadius");
        public static readonly int MainLightShadowReceiverBiasParams = Shader.PropertyToID("_MainLightShadowReceiverBiasParams");
        public static readonly int MainLightShadowAtlasTexelSize = Shader.PropertyToID("_MainLightShadowAtlasTexelSize");
        public static readonly int MainLightShadowCasterCull = Shader.PropertyToID("_MainLightShadowCasterCull");
        public static readonly int MainLightShadowDebugViewMode = Shader.PropertyToID("_MainLightShadowDebugViewMode");
        public static readonly int MainLightShadowDebugExecutionPath = Shader.PropertyToID("_MainLightShadowDebugExecutionPath");
        public static readonly int ShadowBias = Shader.PropertyToID("_ShadowBias");
        public static readonly int ShadowLightDirection = Shader.PropertyToID("_ShadowLightDirection");
        public static readonly int ShadowLightPosition = Shader.PropertyToID("_ShadowLightPosition");
        public static readonly int MainLightShadowCascadeCount = Shader.PropertyToID("_MainLightShadowCascadeCount");
        public static readonly int CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
        public static readonly int CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
        public static readonly int CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
    }
}
