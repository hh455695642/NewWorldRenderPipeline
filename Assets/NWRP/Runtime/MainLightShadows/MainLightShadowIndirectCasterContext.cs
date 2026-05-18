using UnityEngine;

namespace NWRP
{
    internal static class MainLightShadowIndirectCasterContext
    {
        private const int kMaxCascades = 2;
        private const int kMaxTargets = 2;

        private static readonly Target[] s_Targets =
        {
            new Target(),
            new Target()
        };

        public sealed class Target
        {
            public readonly MainLightShadowCascadeData[] cascadeData =
                new MainLightShadowCascadeData[kMaxCascades];

            public RenderTexture shadowmapTexture;
            public int cascadeCount;
            public Vector4 shadowLightDirection;
            public bool includeStaticCasters;
            public bool includeDynamicCasters;

            public void Clear()
            {
                shadowmapTexture = null;
                cascadeCount = 0;
                shadowLightDirection = Vector4.zero;
                includeStaticCasters = false;
                includeDynamicCasters = false;

                for (int i = 0; i < kMaxCascades; i++)
                    cascadeData[i] = default;
            }
        }

        public static bool IsValid => TargetCount > 0;
        public static int TargetCount { get; private set; }
        public static bool HasPendingStaticCacheDraw { get; private set; }

        public static Target GetTarget(int targetIndex)
        {
            return targetIndex >= 0 && targetIndex < TargetCount
                ? s_Targets[targetIndex]
                : null;
        }

        public static void AddTarget(
            RenderTexture shadowmapTexture,
            MainLightShadowCascadeData[] cascadeData,
            int cascadeCount,
            Vector4 shadowLightDirection,
            bool includeStaticCasters,
            bool includeDynamicCasters)
        {
            if (shadowmapTexture == null
                || cascadeData == null
                || cascadeCount <= 0
                || TargetCount >= kMaxTargets)
            {
                return;
            }

            Target target = s_Targets[TargetCount++];
            target.Clear();
            target.cascadeCount = Mathf.Clamp(cascadeCount, 1, kMaxCascades);
            target.shadowmapTexture = shadowmapTexture;
            target.shadowLightDirection = shadowLightDirection;
            target.includeStaticCasters = includeStaticCasters;
            target.includeDynamicCasters = includeDynamicCasters;

            for (int i = 0; i < target.cascadeCount; i++)
                target.cascadeData[i] = cascadeData[i];

            HasPendingStaticCacheDraw |= includeStaticCasters && !includeDynamicCasters;
        }

        public static void Clear()
        {
            TargetCount = 0;
            HasPendingStaticCacheDraw = false;

            for (int i = 0; i < kMaxTargets; i++)
                s_Targets[i].Clear();
        }
    }
}
