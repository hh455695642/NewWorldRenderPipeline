using System;
using System.Collections.Generic;

namespace NWRP
{
    internal static class NWRPFeatureScheduler
    {
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
                for (int i = 0; i < features.Count; i++)
                {
                    if (!NWRPBuiltInFeatureCatalog.ShouldProcessSerializedFeature(
                            features,
                            i))
                    {
                        continue;
                    }

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
            }

            if (rendererData.EnableDepthTexture)
            {
                requirements.Merge(DepthTextureFeature.GetFrameTargetRequirements(
                    rendererData.DepthTextureCopyModeSetting,
                    frameData.camera));
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

            if (!state.hasDepthTexture && rendererData.EnableDepthTexture)
            {
                EnqueueRuntimeFeature<DepthTextureFeature>(
                    renderer,
                    ref frameData,
                    rendererData);
            }

            EnqueueSerializedFeatures(
                renderer,
                ref frameData,
                features,
                includeDeferredFeatures: false);

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
                    includeDeferredFeatures: true);
            }
            else if (rendererData.EnableVegetationIndirectTreeShadows)
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

            if (!state.hasOpaqueTexture && rendererData.EnableOpaqueTexture)
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
            bool includeDeferredFeatures)
        {
            if (features == null)
            {
                return;
            }

            for (int i = 0; i < features.Count; i++)
            {
                if (!NWRPBuiltInFeatureCatalog.ShouldProcessSerializedFeature(
                        features,
                        i))
                {
                    continue;
                }

                NWRPFeature feature = features[i];
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
    }
}
