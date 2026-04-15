using UnityEngine;

namespace NWRP
{
    /// <summary>
    /// Centralized shader property IDs used by the runtime renderer.
    /// </summary>
    public static class NWRPShaderIds
    {
        // Lighting globals
        public static readonly int MainLightPosition = Shader.PropertyToID("_MainLightPosition");
        public static readonly int MainLightColor = Shader.PropertyToID("_MainLightColor");
        public static readonly int AdditionalLightsCount = Shader.PropertyToID("_AdditionalLightsCount");
        public static readonly int AdditionalLightsPosition = Shader.PropertyToID("_AdditionalLightsPosition");
        public static readonly int AdditionalLightsColor = Shader.PropertyToID("_AdditionalLightsColor");
        public static readonly int AdditionalLightsAttenuation = Shader.PropertyToID("_AdditionalLightsAttenuation");
        public static readonly int AdditionalLightsSpotDir = Shader.PropertyToID("_AdditionalLightsSpotDir");

        // Main light shadow globals
        public static readonly int MainLightShadowmapTexture = Shader.PropertyToID("_MainLightShadowmapTexture");
        public static readonly int MainLightDynamicShadowmapTexture = Shader.PropertyToID("_MainLightDynamicShadowmapTexture");
        public static readonly int MainLightWorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
        public static readonly int MainLightShadowParams = Shader.PropertyToID("_MainLightShadowParams");
        public static readonly int MainLightDynamicShadowParams = Shader.PropertyToID("_MainLightDynamicShadowParams");
        public static readonly int MainLightShadowmapSize = Shader.PropertyToID("_MainLightShadowmapSize");
        public static readonly int ShadowBias = Shader.PropertyToID("_ShadowBias");
        public static readonly int ShadowLightDirection = Shader.PropertyToID("_ShadowLightDirection");
        public static readonly int MainLightShadowCascadeCount = Shader.PropertyToID("_MainLightShadowCascadeCount");
        public static readonly int CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
        public static readonly int CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
        public static readonly int CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
    }
}
