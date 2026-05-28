using System;
using System.Collections.Generic;

namespace NWRP
{
    internal struct NWRPSerializedFeatureState
    {
        public bool hasMainLightShadow;
        public bool hasAdditionalLightShadow;
        public bool hasDepthTexture;
        public bool hasOpaqueTexture;
        public bool hasOutline;
        public bool hasFog;
        public bool hasPostProcess;
        public bool hasVegetationIndirectShadow;
    }

    internal interface INWRPSerializedFeatureStateProvider
    {
        bool DeferSerializedPasses { get; }

        void RecordSerializedFeatureState(ref NWRPSerializedFeatureState state);
    }

    internal static class NWRPBuiltInFeatureCatalog
    {
        public static NWRPSerializedFeatureState AnalyzeSerializedFeatures(
            List<NWRPFeature> features)
        {
            NWRPSerializedFeatureState state = default;
            if (features == null)
            {
                return state;
            }

            for (int i = 0; i < features.Count; i++)
            {
                if (!ShouldProcessSerializedFeature(features, i))
                {
                    continue;
                }

                RecordFeature(ref state, features[i]);
            }

            return state;
        }

        public static bool ShouldProcessSerializedFeature(
            List<NWRPFeature> features,
            int index)
        {
            if (features == null || index < 0 || index >= features.Count)
            {
                return false;
            }

            NWRPFeature feature = features[index];
            if (feature == null || !feature.IsEnabled)
            {
                return false;
            }

            if (NWRPFeatureMetadataUtility.AllowsMultiple(feature))
            {
                return true;
            }

            Type featureType = feature.GetType();
            for (int i = 0; i < index; i++)
            {
                NWRPFeature previousFeature = features[i];
                if (previousFeature == null
                    || !previousFeature.IsEnabled
                    || NWRPFeatureMetadataUtility.AllowsMultiple(previousFeature))
                {
                    continue;
                }

                if (previousFeature.GetType() == featureType)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ShouldDeferSerializedFeature(NWRPFeature feature)
        {
            return feature is INWRPSerializedFeatureStateProvider provider
                && provider.DeferSerializedPasses;
        }

        private static void RecordFeature(
            ref NWRPSerializedFeatureState state,
            NWRPFeature feature)
        {
            if (feature is INWRPSerializedFeatureStateProvider provider)
            {
                provider.RecordSerializedFeatureState(ref state);
            }
        }
    }
}
