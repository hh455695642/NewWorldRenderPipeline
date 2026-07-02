using System;
using System.Collections.Generic;

namespace NWRP
{
    internal static class NWRPFeatureScheduler
    {
        private static readonly List<int> s_SortedFeatureIndices =
            new List<int>(16);
        private static readonly Comparison<int> s_FeatureIndexComparer =
            CompareFeatureIndicesByMetadata;
        private static List<NWRPFeature> s_FeatureIndexSortSource;

        public static NWRPFrameTargetRequirements CollectFrameTargetRequirements(
            ref NWRPFrameData frameData)
        {
            NWRPFrameTargetRequirements requirements = default;
            NWRPRendererData rendererData = frameData.rendererData;
            if (frameData.asset == null || rendererData == null)
            {
                return requirements;
            }

            List<NWRPFeature> features = rendererData.Features;
            NWRPSerializedFeatureState state =
                NWRPBuiltInFeatureCatalog.AnalyzeSerializedFeatures(features);

            if (features != null)
            {
                List<int> featureIndices = GetSortedProcessableFeatureIndices(features);
                for (int sortedIndex = 0; sortedIndex < featureIndices.Count; sortedIndex++)
                {
                    int i = featureIndices[sortedIndex];
                    NWRPFeature feature = features[i];
                    if (feature.TryGetFrameTargetRequirements(
                            ref frameData,
                            out NWRPFrameTargetRequirements featureRequirements))
                    {
                        requirements.Merge(featureRequirements);
                    }
                }
            }

            if (!state.hasPostProcess
                && PostProcessFeature.HasAnyActivePostProcess(ref frameData))
            {
                PostProcessFeature runtimePostProcessFeature =
                    rendererData.GetOrCreateRuntimeFeature<PostProcessFeature>();
                runtimePostProcessFeature.EnsureCreated();
                if (runtimePostProcessFeature.TryGetFrameTargetRequirements(
                        ref frameData,
                        out NWRPFrameTargetRequirements postProcessRequirements))
                {
                    requirements.Merge(postProcessRequirements);
                }
            }

            if (rendererData.EnableOpaqueTexture)
            {
                requirements.requiresIntermediateColor = true;
                requirements.requiresIntermediateDepth = true;
                requirements.requiresOpaqueTexture = true;
                frameData.debugStats.RecordForcedOpaqueTextureCopy();
            }

            if (rendererData.EnableDepthTexture)
            {
                requirements.Merge(DepthTextureFeature.GetFrameTargetRequirements(
                    rendererData.DepthTextureCopyModeSetting,
                    frameData.camera));
                frameData.debugStats.RecordForcedDepthTextureCopy();
            }

            return requirements;
        }

        public static void EnqueueFeaturePasses(
            NWRPRenderer renderer,
            ref NWRPFrameData frameData)
        {
            NWRPRendererData rendererData = frameData.rendererData;
            if (renderer == null || rendererData == null)
            {
                return;
            }

            List<NWRPFeature> features = rendererData.Features;
            NWRPSerializedFeatureState state =
                NWRPBuiltInFeatureCatalog.AnalyzeSerializedFeatures(features);
            bool shouldEnqueueDepthTexture =
                ShouldEnqueueDepthTextureFeature(ref frameData, rendererData);
            bool serializedDepthTextureEnqueued = false;

            if (shouldEnqueueDepthTexture)
            {
                serializedDepthTextureEnqueued = EnqueueSerializedDepthTextureFeature(
                    renderer,
                    ref frameData,
                    features);
                if (!serializedDepthTextureEnqueued)
                {
                    EnqueueRuntimeFeature<DepthTextureFeature>(
                        renderer,
                        ref frameData,
                        rendererData);
                }
            }

            EnqueueSerializedFeatures(
                renderer,
                ref frameData,
                features,
                includeDeferredFeatures: false,
                skipDepthTextureFeature: serializedDepthTextureEnqueued);

            if (!state.hasMainLightShadow && frameData.asset != null)
            {
                EnqueueRuntimeFeature<MainLightShadowFeature>(
                    renderer,
                    ref frameData,
                    frameData.asset);
            }

            if (state.hasVegetationIndirectShadow)
            {
                EnqueueSerializedFeatures(
                    renderer,
                    ref frameData,
                    features,
                    includeDeferredFeatures: true,
                    skipDepthTextureFeature: false);
            }
            else if (ShouldEnqueueVegetationIndirectShadowFeature(
                         ref frameData,
                         rendererData))
            {
                EnqueueRuntimeFeature<VegetationIndirectShadowFeature>(
                    renderer,
                    ref frameData,
                    rendererData);
            }

            if (!state.hasAdditionalLightShadow && frameData.asset != null)
            {
                EnqueueRuntimeFeature<AdditionalLightShadowFeature>(
                    renderer,
                    ref frameData,
                    frameData.asset);
            }

            if (!state.hasOutline && rendererData.EnableOutline)
            {
                EnqueueRuntimeFeature<OutlineFeature>(renderer, ref frameData, rendererData);
            }

            if (!state.hasOpaqueTexture
                && ShouldEnqueueOpaqueTextureFeature(ref frameData, rendererData))
            {
                EnqueueRuntimeFeature<OpaqueTextureFeature>(
                    renderer,
                    ref frameData,
                    rendererData);
            }

            if (!state.hasFog)
            {
                EnqueueRuntimeFeature<FogFeature>(renderer, ref frameData, rendererData);
            }

            if (!state.hasPostProcess
                && PostProcessFeature.HasAnyActivePostProcess(ref frameData))
            {
                EnqueueRuntimeFeature<PostProcessFeature>(
                    renderer,
                    ref frameData,
                    rendererData);
            }
        }

        private static void EnqueueSerializedFeatures(
            NWRPRenderer renderer,
            ref NWRPFrameData frameData,
            List<NWRPFeature> features,
            bool includeDeferredFeatures,
            bool skipDepthTextureFeature = false)
        {
            if (features == null)
            {
                return;
            }

            List<int> featureIndices = GetSortedProcessableFeatureIndices(features);
            for (int sortedIndex = 0; sortedIndex < featureIndices.Count; sortedIndex++)
            {
                int i = featureIndices[sortedIndex];
                NWRPFeature feature = features[i];
                if (skipDepthTextureFeature && feature is DepthTextureFeature)
                {
                    continue;
                }

                bool isDeferred =
                    NWRPBuiltInFeatureCatalog.ShouldDeferSerializedFeature(feature);
                if (isDeferred != includeDeferredFeatures)
                {
                    continue;
                }

                feature.EnsureCreated();
                feature.AddPasses(renderer, ref frameData);
            }
        }

        private static bool EnqueueSerializedDepthTextureFeature(
            NWRPRenderer renderer,
            ref NWRPFrameData frameData,
            List<NWRPFeature> features)
        {
            if (features == null)
            {
                return false;
            }

            for (int i = 0; i < features.Count; i++)
            {
                if (!NWRPBuiltInFeatureCatalog.ShouldProcessSerializedFeature(
                        features,
                        i))
                {
                    continue;
                }

                if (features[i] is not DepthTextureFeature feature)
                {
                    continue;
                }

                feature.EnsureCreated();
                feature.AddPasses(renderer, ref frameData);
                return true;
            }

            return false;
        }

        private static void EnqueueRuntimeFeature<T>(
            NWRPRenderer renderer,
            ref NWRPFrameData frameData,
            NWRPRendererData rendererData)
            where T : NWRPFeature
        {
            T feature = rendererData.GetOrCreateRuntimeFeature<T>();
            EnqueueRuntimeFeature(renderer, ref frameData, feature);
        }

        private static void EnqueueRuntimeFeature<T>(
            NWRPRenderer renderer,
            ref NWRPFrameData frameData,
            NewWorldRenderPipelineAsset asset)
            where T : NWRPFeature
        {
            T feature = asset.GetOrCreateRuntimeFeature<T>();
            EnqueueRuntimeFeature(renderer, ref frameData, feature);
        }

        private static void EnqueueRuntimeFeature(
            NWRPRenderer renderer,
            ref NWRPFrameData frameData,
            NWRPFeature feature)
        {
            if (feature == null || !feature.IsEnabled)
            {
                return;
            }

            feature.EnsureCreated();
            feature.AddPasses(renderer, ref frameData);
        }

        private static bool ShouldEnqueueVegetationIndirectShadowFeature(
            ref NWRPFrameData frameData,
            NWRPRendererData rendererData)
        {
            if (rendererData != null
                && rendererData.EnableVegetationIndirectTreeShadows)
            {
                return true;
            }

#if UNITY_EDITOR
            return frameData.camera != null
                && frameData.camera.cameraType == UnityEngine.CameraType.SceneView;
#else
            return false;
#endif
        }

        private static bool ShouldEnqueueDepthTextureFeature(
            ref NWRPFrameData frameData,
            NWRPRendererData rendererData)
        {
            return (rendererData != null && rendererData.EnableDepthTexture)
                || frameData.targets.hasCameraDepthTexture;
        }

        private static bool ShouldEnqueueOpaqueTextureFeature(
            ref NWRPFrameData frameData,
            NWRPRendererData rendererData)
        {
            return frameData.targets.hasOpaqueTexture
                || (rendererData != null && rendererData.EnableOpaqueTexture);
        }

        private static List<int> GetSortedProcessableFeatureIndices(
            List<NWRPFeature> features)
        {
            s_SortedFeatureIndices.Clear();
            if (features == null)
            {
                return s_SortedFeatureIndices;
            }

            for (int i = 0; i < features.Count; i++)
            {
                if (NWRPBuiltInFeatureCatalog.ShouldProcessSerializedFeature(
                        features,
                        i))
                {
                    s_SortedFeatureIndices.Add(i);
                }
            }

            s_FeatureIndexSortSource = features;
            s_SortedFeatureIndices.Sort(s_FeatureIndexComparer);
            s_FeatureIndexSortSource = null;
            return s_SortedFeatureIndices;
        }

        private static int CompareFeatureIndicesByMetadata(int a, int b)
        {
            NWRPFeature featureA = s_FeatureIndexSortSource[a];
            NWRPFeature featureB = s_FeatureIndexSortSource[b];
            int orderA = NWRPFeatureMetadataUtility.Get(featureA.GetType()).sortOrder;
            int orderB = NWRPFeatureMetadataUtility.Get(featureB.GetType()).sortOrder;
            int orderCompare = orderA.CompareTo(orderB);
            return orderCompare != 0 ? orderCompare : a.CompareTo(b);
        }
    }
}
