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
    }
}
