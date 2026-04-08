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

        private readonly CommandBuffer _cmd = new CommandBuffer { name = "NewWorld Camera" };
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
            _cmd.Release();
        }

        public void Render(
            ScriptableRenderContext context,
            Camera camera,
            NewWorldRenderPipelineAsset asset
        )
        {
            if (!TryCull(context, camera, out CullingResults cullingResults))
            {
                return;
            }

            NWRPFrameData frameData = new NWRPFrameData
            {
                context = context,
                camera = camera,
                cullingResults = cullingResults,
                cmd = _cmd,
                asset = asset
            };

            BuildPassQueue(ref frameData);
            ExecutePassQueue(ref frameData);
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
            _cmd.ClearRenderTarget(
                clearDepth: clearFlags <= CameraClearFlags.Depth,
                clearColor: clearFlags <= CameraClearFlags.SolidColor,
                backgroundColor: clearFlags == CameraClearFlags.SolidColor
                    ? frameData.camera.backgroundColor.linear
                    : Color.clear
            );

            _cmd.BeginSample("NewWorld Render Camera");
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
                mainLightPosition = -visibleLight.localToWorldMatrix.GetColumn(2);
                mainLightPosition.w = 0f;
                mainLightColor = visibleLight.finalColor;
                mainLightIndex = i;
                break;
            }

            _cmd.SetGlobalVector(NWRPShaderIds.MainLightPosition, mainLightPosition);
            _cmd.SetGlobalVector(NWRPShaderIds.MainLightColor, mainLightColor);

            int additionalCount = 0;
            int limit = Mathf.Min(frameData.asset.maxAdditionalLights, kMaxAdditionalLights);

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

            _cmd.SetGlobalInt(NWRPShaderIds.AdditionalLightsCount, additionalCount);
            _cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsPosition, _additionalLightsPosition);
            _cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsColor, _additionalLightsColor);
            _cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsAttenuation, _additionalLightsAttenuation);
            _cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsSpotDir, _additionalLightsSpotDir);

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
            _cmd.EndSample("NewWorld Render Camera");
            ExecuteBuffer(ref frameData);
            frameData.context.Submit();
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
            for (int i = 0; i < _activePasses.Count; i++)
            {
                _activePasses[i].pass.Execute(ref frameData);
            }
        }

        private void EnqueueFeaturePasses(ref NWRPFrameData frameData)
        {
            List<NWRPFeature> features = frameData.asset.Features;
            for (int i = 0; i < features.Count; i++)
            {
                NWRPFeature feature = features[i];
                if (feature == null || !feature.IsEnabled)
                {
                    continue;
                }

                feature.EnsureCreated();
                feature.AddPasses(this, ref frameData);
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
            out CullingResults cullingResults
        )
        {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
            {
                cullingResults = context.Cull(ref cullingParameters);
                return true;
            }

            cullingResults = default;
            return false;
        }

        private void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            frameData.context.ExecuteCommandBuffer(_cmd);
            _cmd.Clear();
        }
    }
}
