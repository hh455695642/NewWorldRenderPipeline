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
        public static readonly int CameraDepthTexture = Shader.PropertyToID("_NWRPCameraDepthTexture");
        public static readonly int CameraOpaqueTexture = Shader.PropertyToID("_CameraOpaqueTexture");
        public static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");

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
