using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    internal static class NWRPProfiling
    {
        private static readonly Dictionary<int, ProfilingSampler> s_CameraSamplerCache =
            new Dictionary<int, ProfilingSampler>();

        public static readonly ProfilingSampler UnknownCamera =
            new ProfilingSampler("NWRP.RenderSingleCamera: Unknown");
        public static readonly ProfilingSampler RendererExecute =
            new ProfilingSampler("NWRPRenderer.Execute");
        public static readonly ProfilingSampler SetupCamera =
            new ProfilingSampler("Setup Camera");
        public static readonly ProfilingSampler SetupLights =
            new ProfilingSampler("Setup Lights");
        public static readonly ProfilingSampler BeforeRendering =
            new ProfilingSampler("Before Rendering");
        public static readonly ProfilingSampler MainRenderingOpaque =
            new ProfilingSampler("Main Rendering Opaque");
        public static readonly ProfilingSampler MainRenderingTransparent =
            new ProfilingSampler("Main Rendering Transparent");
        public static readonly ProfilingSampler Submit =
            new ProfilingSampler("Submit");
        public static readonly ProfilingSampler MainLightShadow =
            new ProfilingSampler("Main Light Shadows");
        public static readonly ProfilingSampler AdditionalLightShadow =
            new ProfilingSampler("Additional Punctual Light Shadows");

        public static ProfilingSampler TryGetOrAddCameraSampler(Camera camera)
        {
            if (camera == null)
            {
                return UnknownCamera;
            }

            int cameraId = camera.GetInstanceID();
            if (!s_CameraSamplerCache.TryGetValue(cameraId, out ProfilingSampler sampler))
            {
                sampler = new ProfilingSampler($"NWRP.RenderSingleCamera: {camera.name}");
                s_CameraSamplerCache.Add(cameraId, sampler);
            }

            return sampler;
        }
    }
}
