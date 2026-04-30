using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    internal static class NWRPBlitterResources
    {
        private const string k_CoreBlitShaderName = "Hidden/NWRP/CoreBlit";
        private const string k_CoreBlitColorAndDepthShaderName = "Hidden/NWRP/CoreBlitColorAndDepth";

        private static bool s_Initialized;
        private static bool s_InitializedByNWRP;

        public static void Initialize()
        {
            if (s_Initialized)
            {
                return;
            }

            Shader coreBlitShader = Shader.Find(k_CoreBlitShaderName);
            Shader coreBlitColorAndDepthShader = Shader.Find(k_CoreBlitColorAndDepthShaderName);
            if (coreBlitShader == null || coreBlitColorAndDepthShader == null)
            {
                Debug.LogError(
                    "NWRP Blitter requires Hidden/NWRP/CoreBlit and Hidden/NWRP/CoreBlitColorAndDepth.");
                return;
            }

            try
            {
                Blitter.Initialize(coreBlitShader, coreBlitColorAndDepthShader);
                s_InitializedByNWRP = true;
            }
            catch (Exception exception)
            {
                if (!exception.Message.Contains("already initialized"))
                {
                    Debug.LogException(exception);
                    return;
                }
            }

            s_Initialized = true;
        }

        public static Material CreateCoreBlitMaterial()
        {
            Initialize();

            Shader shader = Shader.Find(k_CoreBlitShaderName);
            if (shader == null)
            {
                Debug.LogError("NWRP copy color requires Hidden/NWRP/CoreBlit.");
                return null;
            }

            return CoreUtils.CreateEngineMaterial(shader);
        }

        public static void Cleanup()
        {
            if (s_InitializedByNWRP)
            {
                Blitter.Cleanup();
            }

            s_Initialized = false;
            s_InitializedByNWRP = false;
        }
    }
}
