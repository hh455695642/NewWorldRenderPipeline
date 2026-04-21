using System;
using System.Collections.Generic;
using NWRP.Runtime.Passes;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// Lightweight pass scheduler and built-in renderer implementation for NWRP.
    /// </summary>
    public sealed class NWRPRenderer : IDisposable
    {
        private struct QueuedPass
        {
            public NWRPPass pass;
            public int enqueueIndex;
        }

        private static readonly ShaderTagId s_NewWorldUnlitTagId = new ShaderTagId("NewWorldUnlit");
        private static readonly ShaderTagId s_SrpDefaultUnlitTagId = new ShaderTagId("SRPDefaultUnlit");
        private static readonly ShaderTagId s_NewWorldForwardTagId = new ShaderTagId("NewWorldForward");
        private static readonly ShaderTagId s_NewWorldOutlineTagId = new ShaderTagId("NewWorldOutline");

        private const int kMaxAdditionalLights = 8;

        private static readonly Comparison<QueuedPass> s_QueuedPassComparer = CompareQueuedPass;

        // Pre-allocated light arrays to avoid per-frame GC.
        private readonly Vector4[] _additionalLightsPosition = new Vector4[kMaxAdditionalLights];
        private readonly Vector4[] _additionalLightsColor = new Vector4[kMaxAdditionalLights];
        private readonly Vector4[] _additionalLightsAttenuation = new Vector4[kMaxAdditionalLights];
        private readonly Vector4[] _additionalLightsSpotDir = new Vector4[kMaxAdditionalLights];

        private readonly List<QueuedPass> _activePasses = new List<QueuedPass>(32);

        private readonly SetupCameraPass _setupCameraPass;
        private readonly SetupLightsPass _setupLightsPass;
        private readonly DrawOpaquePass _drawOpaquePass;
        private readonly DrawOutlinePass _drawOutlinePass;
        private readonly DrawSkyboxPass _drawSkyboxPass;
        private readonly DrawTransparentPass _drawTransparentPass;
        private readonly SubmitPass _submitPass;

        private int _enqueueCounter;

        public NWRPRenderer()
        {
            _setupCameraPass = new SetupCameraPass(this);
            _setupLightsPass = new SetupLightsPass(this);
            _drawOpaquePass = new DrawOpaquePass(this);
            _drawOutlinePass = new DrawOutlinePass(this);
            _drawSkyboxPass = new DrawSkyboxPass(this);
            _drawTransparentPass = new DrawTransparentPass(this);
            _submitPass = new SubmitPass(this);
        }

        public void Dispose()
        {
        }

        public void Render(
            ScriptableRenderContext context,
            Camera camera,
            NewWorldRenderPipelineAsset asset
        )
        {
            if (!TryCull(context, camera, asset, out CullingResults cullingResults))
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            NWRPFrameData frameData = new NWRPFrameData
            {
                context = context,
                camera = camera,
                cullingResults = cullingResults,
                cmd = cmd,
                asset = asset
            };

            try
            {
                using (new ProfilingScope(cmd, NWRPProfiling.TryGetOrAddCameraSampler(camera)))
                {
                    using (new ProfilingScope(cmd, NWRPProfiling.RendererExecute))
                    {
                        BuildPassQueue(ref frameData);
                        ExecutePassQueue(ref frameData);
                    }
                }

                ExecuteBuffer(ref frameData);
                frameData.context.Submit();
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }

        public void EnqueuePass(NWRPPass pass)
        {
            if (pass == null)
            {
                return;
            }

            _activePasses.Add(new QueuedPass
            {
                pass = pass,
                enqueueIndex = _enqueueCounter++
            });
        }

        internal void ExecuteSetupCamera(ref NWRPFrameData frameData)
        {
            frameData.context.SetupCameraProperties(frameData.camera);

            CameraClearFlags clearFlags = frameData.camera.clearFlags;
            frameData.cmd.ClearRenderTarget(
                clearDepth: clearFlags <= CameraClearFlags.Depth,
                clearColor: clearFlags <= CameraClearFlags.SolidColor,
                backgroundColor: clearFlags == CameraClearFlags.SolidColor
                    ? frameData.camera.backgroundColor.linear
                    : Color.clear
            );

            ExecuteBuffer(ref frameData);
        }

        internal void ExecuteSetupLights(ref NWRPFrameData frameData)
        {
            Vector4 mainLightPosition = Vector4.zero;
            Vector4 mainLightColor = Vector4.zero;
            int mainLightIndex = -1;

            NativeArray<VisibleLight> visibleLights = frameData.cullingResults.visibleLights;
            for (int i = 0; i < visibleLights.Length; i++)
            {
                if (visibleLights[i].lightType != LightType.Directional)
                {
                    continue;
                }

                VisibleLight visibleLight = visibleLights[i];
                Vector4 mainLightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                mainLightDirection = mainLightDirection.normalized;
                mainLightDirection.w = 0f;
                mainLightPosition = mainLightDirection;
                mainLightColor = visibleLight.finalColor;
                mainLightIndex = i;
                break;
            }

            frameData.cmd.SetGlobalVector(NWRPShaderIds.MainLightPosition, mainLightPosition);
            frameData.cmd.SetGlobalVector(NWRPShaderIds.MainLightColor, mainLightColor);

            int additionalCount = 0;
            int limit = kMaxAdditionalLights;

            for (int i = 0; i < visibleLights.Length && additionalCount < limit; i++)
            {
                if (i == mainLightIndex)
                {
                    continue;
                }

                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType != LightType.Point && visibleLight.lightType != LightType.Spot)
                {
                    continue;
                }

                Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
                position.w = 1f;
                _additionalLightsPosition[additionalCount] = position;
                _additionalLightsColor[additionalCount] = visibleLight.finalColor;

                float range = visibleLight.range;
                float invRangeSqr = 1f / Mathf.Max(range * range, 0.00001f);

                float spotScale = 0f;
                float spotOffset = 1f;

                if (visibleLight.lightType == LightType.Spot)
                {
                    float outerRad = Mathf.Deg2Rad * visibleLight.spotAngle * 0.5f;
                    float outerCos = Mathf.Cos(outerRad);
                    float innerCos = Mathf.Cos(outerRad * 0.8f);
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    spotScale = 1f / angleRange;
                    spotOffset = -outerCos * spotScale;
                }

                _additionalLightsAttenuation[additionalCount] = new Vector4(
                    invRangeSqr, 0f, spotScale, spotOffset
                );

                Vector4 spotDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                spotDirection.w = 0f;
                _additionalLightsSpotDir[additionalCount] = spotDirection;

                additionalCount++;
            }

            for (int i = additionalCount; i < kMaxAdditionalLights; i++)
            {
                _additionalLightsPosition[i] = Vector4.zero;
                _additionalLightsColor[i] = Vector4.zero;
                _additionalLightsAttenuation[i] = Vector4.zero;
                _additionalLightsSpotDir[i] = Vector4.zero;
            }

            frameData.cmd.SetGlobalInt(NWRPShaderIds.AdditionalLightsCount, additionalCount);
            frameData.cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsPosition, _additionalLightsPosition);
            frameData.cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsColor, _additionalLightsColor);
            frameData.cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsAttenuation, _additionalLightsAttenuation);
            frameData.cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsSpotDir, _additionalLightsSpotDir);

            ExecuteBuffer(ref frameData);
        }

        internal void ExecuteDrawOpaque(ref NWRPFrameData frameData)
        {
            SortingSettings sortingSettings = new SortingSettings(frameData.camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            DrawingSettings drawingSettings = new DrawingSettings(s_NewWorldUnlitTagId, sortingSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = frameData.asset.useGPUInstancing
            };

            drawingSettings.SetShaderPassName(1, s_SrpDefaultUnlitTagId);
            drawingSettings.SetShaderPassName(2, s_NewWorldForwardTagId);

            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            frameData.context.DrawRenderers(
                frameData.cullingResults,
                ref drawingSettings,
                ref filteringSettings
            );
        }

        internal void ExecuteDrawOutline(ref NWRPFrameData frameData)
        {
            SortingSettings sortingSettings = new SortingSettings(frameData.camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            DrawingSettings drawingSettings = new DrawingSettings(s_NewWorldOutlineTagId, sortingSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = frameData.asset.useGPUInstancing
            };

            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            frameData.context.DrawRenderers(
                frameData.cullingResults,
                ref drawingSettings,
                ref filteringSettings
            );
        }

        internal void ExecuteDrawSkybox(ref NWRPFrameData frameData)
        {
            frameData.context.DrawSkybox(frameData.camera);
        }

        internal void ExecuteDrawTransparent(ref NWRPFrameData frameData)
        {
            SortingSettings sortingSettings = new SortingSettings(frameData.camera)
            {
                criteria = SortingCriteria.CommonTransparent
            };

            DrawingSettings drawingSettings = new DrawingSettings(s_NewWorldUnlitTagId, sortingSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = frameData.asset.useGPUInstancing
            };

            drawingSettings.SetShaderPassName(1, s_SrpDefaultUnlitTagId);
            drawingSettings.SetShaderPassName(2, s_NewWorldForwardTagId);

            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            frameData.context.DrawRenderers(
                frameData.cullingResults,
                ref drawingSettings,
                ref filteringSettings
            );
        }

        internal void ExecuteSubmit(ref NWRPFrameData frameData)
        {
            ExecuteBuffer(ref frameData);
        }

        private void BuildPassQueue(ref NWRPFrameData frameData)
        {
            _activePasses.Clear();
            _enqueueCounter = 0;

            EnqueuePass(_setupCameraPass);
            EnqueuePass(_setupLightsPass);
            EnqueuePass(_drawOpaquePass);
            EnqueuePass(_drawOutlinePass);
            EnqueuePass(_drawSkyboxPass);
            EnqueuePass(_drawTransparentPass);

            EnqueueFeaturePasses(ref frameData);

            // Submit must stay last after all rendering/debug passes.
            EnqueuePass(_submitPass);

            _activePasses.Sort(s_QueuedPassComparer);
        }

        private void ExecutePassQueue(ref NWRPFrameData frameData)
        {
            ExecuteStage(ref frameData, NWRPProfiling.SetupCamera, IsSetupCameraPass);
            ExecuteStage(ref frameData, NWRPProfiling.SetupLights, IsSetupLightsPass);
            ExecuteMainLightShadowStage(ref frameData);
            ExecuteStage(ref frameData, NWRPProfiling.BeforeRendering, IsBeforeRenderingPass);
            ExecuteStage(ref frameData, NWRPProfiling.MainRenderingOpaque, IsMainRenderingOpaquePass);
            ExecuteStage(ref frameData, NWRPProfiling.MainRenderingTransparent, IsMainRenderingTransparentPass);
            ExecuteStage(ref frameData, NWRPProfiling.Submit, IsSubmitStagePass);
        }

        private void ExecuteMainLightShadowStage(ref NWRPFrameData frameData)
        {
            if (!HasMatchingPass(IsMainLightShadowPass))
            {
                return;
            }

            using (new ProfilingScope(frameData.cmd, NWRPProfiling.MainLightShadow))
            {
                ExecuteBuffer(ref frameData);

                for (int passIndex = 0; passIndex < _activePasses.Count; passIndex++)
                {
                    if (!IsMainLightShadowPass(_activePasses[passIndex]))
                    {
                        continue;
                    }

                    ExecutePass(ref frameData, _activePasses[passIndex].pass);
                }
            }

            ExecuteBuffer(ref frameData);
        }

        private void ExecuteStage(
            ref NWRPFrameData frameData,
            ProfilingSampler stageSampler,
            Func<QueuedPass, bool> predicate)
        {
            if (!HasMatchingPass(predicate))
            {
                return;
            }

            using (new ProfilingScope(frameData.cmd, stageSampler))
            {
                ExecuteBuffer(ref frameData);

                int passIndex = 0;
                while (passIndex < _activePasses.Count)
                {
                    if (!predicate(_activePasses[passIndex]))
                    {
                        passIndex++;
                        continue;
                    }

                    ProfilingSampler groupSampler = _activePasses[passIndex].pass.profilingGroupSampler;
                    if (groupSampler != null)
                    {
                        int groupEnd = passIndex + 1;
                        while (groupEnd < _activePasses.Count
                            && predicate(_activePasses[groupEnd])
                            && ReferenceEquals(
                                _activePasses[groupEnd].pass.profilingGroupSampler,
                                groupSampler))
                        {
                            groupEnd++;
                        }

                        using (new ProfilingScope(frameData.cmd, groupSampler))
                        {
                            ExecuteBuffer(ref frameData);

                            for (int groupedIndex = passIndex; groupedIndex < groupEnd; groupedIndex++)
                            {
                                ExecutePass(ref frameData, _activePasses[groupedIndex].pass);
                            }
                        }

                        ExecuteBuffer(ref frameData);

                        passIndex = groupEnd;
                        continue;
                    }

                    ExecutePass(ref frameData, _activePasses[passIndex].pass);
                    passIndex++;
                }
            }

            ExecuteBuffer(ref frameData);
        }

        private static void ExecutePass(ref NWRPFrameData frameData, NWRPPass pass)
        {
            if (pass.usePassProfilingScope)
            {
                using (new ProfilingScope(frameData.cmd, pass.profilingSampler))
                {
                    ExecuteBuffer(ref frameData);
                    pass.Execute(ref frameData);
                }

                ExecuteBuffer(ref frameData);

                return;
            }

            pass.Execute(ref frameData);
        }

        private bool HasMatchingPass(Func<QueuedPass, bool> predicate)
        {
            for (int i = 0; i < _activePasses.Count; i++)
            {
                if (predicate(_activePasses[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSetupCameraPass(QueuedPass queuedPass)
        {
            return ReferenceEquals(queuedPass.pass, _setupCameraPass);
        }

        private bool IsSetupLightsPass(QueuedPass queuedPass)
        {
            return ReferenceEquals(queuedPass.pass, _setupLightsPass);
        }

        private bool IsBeforeRenderingPass(QueuedPass queuedPass)
        {
            return !IsSetupCameraPass(queuedPass)
                && !IsSetupLightsPass(queuedPass)
                && !IsMainLightShadowPass(queuedPass)
                && queuedPass.pass.passEvent <= NWRPPassEvent.ShadowMap;
        }

        private static bool IsMainLightShadowPass(QueuedPass queuedPass)
        {
            return ReferenceEquals(queuedPass.pass.profilingGroupSampler, NWRPProfiling.MainLightShadow);
        }

        private static bool IsMainRenderingOpaquePass(QueuedPass queuedPass)
        {
            return queuedPass.pass.passEvent > NWRPPassEvent.ShadowMap
                && queuedPass.pass.passEvent <= NWRPPassEvent.Skybox;
        }

        private static bool IsMainRenderingTransparentPass(QueuedPass queuedPass)
        {
            return queuedPass.pass.passEvent > NWRPPassEvent.Skybox
                && queuedPass.pass.passEvent < NWRPPassEvent.DebugOverlay;
        }

        private static bool IsSubmitStagePass(QueuedPass queuedPass)
        {
            return queuedPass.pass.passEvent >= NWRPPassEvent.DebugOverlay;
        }

        private void EnqueueFeaturePasses(ref NWRPFrameData frameData)
        {
            List<NWRPFeature> features = frameData.asset.Features;
            bool hasSerializedMainLightShadowFeature = false;
            for (int i = 0; i < features.Count; i++)
            {
                NWRPFeature feature = features[i];
                if (feature == null || !feature.IsEnabled)
                {
                    continue;
                }

                if (feature is MainLightShadowFeature)
                {
                    hasSerializedMainLightShadowFeature = true;
                }

                feature.EnsureCreated();
                feature.AddPasses(this, ref frameData);
            }

            if (!hasSerializedMainLightShadowFeature)
            {
                MainLightShadowFeature runtimeMainLightShadowFeature =
                    frameData.asset.GetOrCreateMainLightShadowFeature();
                if (runtimeMainLightShadowFeature != null && runtimeMainLightShadowFeature.IsEnabled)
                {
                    runtimeMainLightShadowFeature.EnsureCreated();
                    runtimeMainLightShadowFeature.AddPasses(this, ref frameData);
                }
            }
        }

        private static int CompareQueuedPass(QueuedPass a, QueuedPass b)
        {
            int eventCompare = a.pass.passEvent.CompareTo(b.pass.passEvent);
            if (eventCompare != 0)
            {
                return eventCompare;
            }

            return a.enqueueIndex.CompareTo(b.enqueueIndex);
        }

        private static bool TryCull(
            ScriptableRenderContext context,
            Camera camera,
            NewWorldRenderPipelineAsset asset,
            out CullingResults cullingResults
        )
        {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
            {
                float shadowDistance = asset != null && asset.EnableMainLightShadows
                    ? Mathf.Min(asset.MainLightShadowDistance, camera.farClipPlane)
                    : 0f;
                cullingParameters.shadowDistance = Mathf.Max(0f, shadowDistance);

                cullingResults = context.Cull(ref cullingParameters);
                return true;
            }

            cullingResults = default;
            return false;
        }

        private static void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            if (frameData.cmd == null)
            {
                return;
            }

            frameData.context.ExecuteCommandBuffer(frameData.cmd);
            frameData.cmd.Clear();
        }
    }
}
