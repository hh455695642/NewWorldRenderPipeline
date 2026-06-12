using System.Collections.Generic;
using UnityEngine;

namespace NWRP
{
    public struct VegetationIndirectShadowDraw
    {
        public Mesh mesh;
        public Material material;
        public Bounds bounds;
        public ComputeShader cullingShader;
        public int cullingKernelIndex;
        public int instanceCount;
        public ComputeBuffer allInstancesBuffer;
        public ComputeBuffer shadowVisibleBuffer;
        public ComputeBuffer shadowArgsBuffer;
        public MaterialPropertyBlock materialProperties;
        public int shadowCasterPassIndex;
        public bool isDynamicShadowCaster;
    }

    public interface IVegetationIndirectShadowProvider
    {
        // Extension point: future GPU-driven vegetation systems can register providers without
        // adding renderer-specific loops to the main shadow passes.
        bool TryCollectIndirectShadowDraws(
            bool includeStaticCasters,
            bool includeDynamicCasters,
            List<VegetationIndirectShadowDraw> draws);
    }

    public interface IVegetationIndirectShadowCasterQuery
    {
        bool HasIndirectShadowCasters(
            bool includeStaticCasters,
            bool includeDynamicCasters);
    }

    public static class VegetationIndirectShadowRegistry
    {
        private static readonly List<IVegetationIndirectShadowProvider> s_Providers =
            new List<IVegetationIndirectShadowProvider>(16);
        private static readonly List<VegetationIndirectShadowDraw> s_QueryScratchDraws =
            new List<VegetationIndirectShadowDraw>(8);

        public static int ProviderCount => s_Providers.Count;

        public static void Register(IVegetationIndirectShadowProvider provider)
        {
            if (provider == null || s_Providers.Contains(provider))
                return;

            s_Providers.Add(provider);
        }

        public static void Unregister(IVegetationIndirectShadowProvider provider)
        {
            if (provider == null)
                return;

            s_Providers.Remove(provider);
        }

        public static IVegetationIndirectShadowProvider GetProvider(int index)
        {
            return index >= 0 && index < s_Providers.Count ? s_Providers[index] : null;
        }

        public static bool HasIndirectShadowCasters(
            bool includeStaticCasters,
            bool includeDynamicCasters)
        {
            if (!includeStaticCasters && !includeDynamicCasters)
                return false;

            Compact();

            for (int i = 0; i < s_Providers.Count; i++)
            {
                IVegetationIndirectShadowProvider provider = s_Providers[i];
                if (provider == null)
                    continue;

                if (provider is IVegetationIndirectShadowCasterQuery query)
                {
                    if (query.HasIndirectShadowCasters(
                            includeStaticCasters,
                            includeDynamicCasters))
                    {
                        return true;
                    }

                    continue;
                }

                s_QueryScratchDraws.Clear();
                if (provider.TryCollectIndirectShadowDraws(
                        includeStaticCasters,
                        includeDynamicCasters,
                        s_QueryScratchDraws)
                    && s_QueryScratchDraws.Count > 0)
                {
                    s_QueryScratchDraws.Clear();
                    return true;
                }
            }

            s_QueryScratchDraws.Clear();
            return false;
        }

        public static void Compact()
        {
            for (int i = s_Providers.Count - 1; i >= 0; i--)
            {
                IVegetationIndirectShadowProvider provider = s_Providers[i];
                if (provider == null || provider is Object unityObject && unityObject == null)
                    s_Providers.RemoveAt(i);
            }
        }
    }
}
