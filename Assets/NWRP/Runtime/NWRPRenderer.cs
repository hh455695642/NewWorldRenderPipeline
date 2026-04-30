using System;
using System.Collections.Generic;
using NWRP.Runtime.Lighting;
using NWRP.Runtime.Passes;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        private static readonly Comparison<QueuedPass> s_QueuedPassComparer = CompareQueuedPass;

        // Pre-allocated light arrays to avoid per-frame GC.
        private readonly AdditionalLightData[] _additionalLightData =
            new AdditionalLightData[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Vector4[] _additionalLightsPosition =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Vector4[] _additionalLightsColor =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Vector4[] _additionalLightsAttenuation =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private readonly Vector4[] _additionalLightsSpotDir =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];

        private readonly List<QueuedPass> _activePasses = new List<QueuedPass>(32);

        private readonly SetupCameraPass _setupCameraPass;
        private readonly SetupLightsPass _setupLightsPass;
        private readonly DrawOpaquePass _drawOpaquePass;
        private readonly DrawSkyboxPass _drawSkyboxPass;
        private readonly DrawTransparentPass _drawTransparentPass;
#if UNITY_EDITOR
        private readonly DrawGizmosPass _drawGizmosPreImageEffectsPass;
        private readonly DrawGizmosPass _drawGizmosPostImageEffectsPass;
#endif
        private readonly FinalBlitPass _finalBlitPass;

        private Material _coreBlitMaterial;
        private RTHandle _cameraColorHandle;
        private RTHandle _cameraDepthHandle;
        private RTHandle _opaqueTextureHandle;
        private int _enqueueCounter;

        public NWRPRenderer()
        {
            _setupCameraPass = new SetupCameraPass(this);
            _setupLightsPass = new SetupLightsPass(this);
            _drawOpaquePass = new DrawOpaquePass(this);
            _drawSkyboxPass = new DrawSkyboxPass(this);
            _drawTransparentPass = new DrawTransparentPass(this);
#if UNITY_EDITOR
            _drawGizmosPreImageEffectsPass = new DrawGizmosPass(
                this,
                NWRPPassEvent.AfterTransparent,
                GizmoSubset.PreImageEffects,
                "Draw Gizmos Pre Image Effects");
            _drawGizmosPostImageEffectsPass = new DrawGizmosPass(
                this,
                NWRPPassEvent.DebugOverlay,
                GizmoSubset.PostImageEffects,
                "Draw Gizmos Post Image Effects");
#endif
            _finalBlitPass = new FinalBlitPass(this);
            NWRPBlitterResources.Initialize();
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_coreBlitMaterial);
            _coreBlitMaterial = null;
            ReleaseRendererTargetHandles();
            NWRPBlitterResources.Cleanup();
        }

        public void Render(
            ScriptableRenderContext context,
            Camera camera,
            NewWorldRenderPipelineAsset asset
        )
        {
#if UNITY_EDITOR
            EmitSceneViewGeometry(camera);
#endif

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
                        ConfigureFrameTargets(ref frameData);
                        BuildPassQueue(ref frameData);
                        ExecutePassQueue(ref frameData);
                    }
                }

                ExecuteBuffer(ref frameData);
            }
            finally
            {
                ReleaseFrameTargets(ref frameData);
                ExecuteBuffer(ref frameData);
                frameData.context.Submit();
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

            SetCameraRenderTarget(ref frameData);

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

            NativeArray<VisibleLight> visibleLights = frameData.cullingResults.visibleLights;
            int additionalCount = AdditionalLightUtils.CollectAdditionalLights(
                ref frameData,
                _additionalLightData,
                out int mainLightIndex);

            if (mainLightIndex >= 0 && mainLightIndex < visibleLights.Length)
            {
                VisibleLight visibleLight = visibleLights[mainLightIndex];
                Vector4 mainLightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                mainLightDirection = mainLightDirection.normalized;
                mainLightDirection.w = 0f;
                mainLightPosition = mainLightDirection;
                mainLightColor = visibleLight.finalColor;
            }

            frameData.cmd.SetGlobalVector(NWRPShaderIds.MainLightPosition, mainLightPosition);
            frameData.cmd.SetGlobalVector(NWRPShaderIds.MainLightColor, mainLightColor);

            for (int i = 0; i < additionalCount; i++)
            {
                AdditionalLightData additionalLight = _additionalLightData[i];
                _additionalLightsPosition[i] = additionalLight.position;
                _additionalLightsColor[i] = additionalLight.color;
                _additionalLightsAttenuation[i] = additionalLight.attenuation;
                _additionalLightsSpotDir[i] = additionalLight.spotDirection;
            }

            for (int i = additionalCount; i < AdditionalLightUtils.MaxAdditionalLights; i++)
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
            SetCameraRenderTarget(ref frameData);
            ExecuteBuffer(ref frameData);

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

        internal void ExecuteDrawSkybox(ref NWRPFrameData frameData)
        {
            SetCameraRenderTarget(ref frameData);
            ExecuteBuffer(ref frameData);

            frameData.context.DrawSkybox(frameData.camera);
        }

        internal void ExecuteDrawTransparent(ref NWRPFrameData frameData)
        {
            SetCameraRenderTarget(ref frameData);
            ExecuteBuffer(ref frameData);

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

#if UNITY_EDITOR
        internal void ExecuteDrawGizmos(ref NWRPFrameData frameData, GizmoSubset gizmoSubset)
        {
            if (!ShouldDrawGizmos(frameData.camera))
            {
                return;
            }

            frameData.context.DrawGizmos(frameData.camera, gizmoSubset);
        }
#endif

        internal void ExecuteFinalBlit(ref NWRPFrameData frameData)
        {
            if (frameData.targets.usesIntermediateColor)
            {
                PresentIntermediateColor(ref frameData);
            }

            ExecuteBuffer(ref frameData);
        }

        private void ConfigureFrameTargets(ref NWRPFrameData frameData)
        {
            NWRPFrameTargetRequirements requirements = CollectFrameTargetRequirements(ref frameData);

            RenderTargetIdentifier backBuffer = BuiltinRenderTextureType.CameraTarget;
            RTHandle backBufferHandle = RTHandles.Alloc(backBuffer, "CameraTarget");
            frameData.targets = new NWRPFrameTargets
            {
                backBufferColor = backBuffer,
                backBufferDepth = backBuffer,
                cameraColor = backBuffer,
                cameraDepth = backBuffer,
                backBufferColorHandle = backBufferHandle,
                cameraColorHandle = backBufferHandle,
                cameraDepthHandle = backBufferHandle,
                hasCameraTargets = true
            };

            bool needIntermediateColor =
                requirements.requiresIntermediateColor || requirements.requiresOpaqueTexture;
            bool needIntermediateDepth =
                requirements.requiresIntermediateDepth || needIntermediateColor;

            if (needIntermediateColor)
            {
                RenderTextureDescriptor colorDescriptor = CreateCameraColorDescriptor(frameData.camera);
                ReAllocateFrameTargetIfNeeded(
                    ref _cameraColorHandle,
                    colorDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_NWRPCameraColorTexture");

                frameData.targets.cameraColorHandle = _cameraColorHandle;
                frameData.targets.cameraColor = frameData.targets.cameraColorHandle.nameID;
                frameData.targets.ownsIntermediateColor = true;
                frameData.targets.usesIntermediateColor = true;
                frameData.cmd.SetGlobalTexture(
                    NWRPShaderIds.CameraColorTexture,
                    frameData.targets.cameraColorHandle);
            }
            else
            {
                ReleaseRTHandle(ref _cameraColorHandle);
            }

            if (needIntermediateDepth)
            {
                RenderTextureDescriptor depthDescriptor = CreateCameraDepthDescriptor(frameData.camera);
                ReAllocateFrameTargetIfNeeded(
                    ref _cameraDepthHandle,
                    depthDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: "_NWRPCameraDepthTexture");

                frameData.targets.cameraDepthHandle = _cameraDepthHandle;
                frameData.targets.cameraDepth = frameData.targets.cameraDepthHandle.nameID;
                frameData.targets.ownsIntermediateDepth = true;
                frameData.targets.usesIntermediateDepth = true;
                frameData.cmd.SetGlobalTexture(
                    NWRPShaderIds.CameraDepthTexture,
                    frameData.targets.cameraDepthHandle);
            }
            else
            {
                ReleaseRTHandle(ref _cameraDepthHandle);
            }

            if (requirements.requiresOpaqueTexture)
            {
                RenderTextureDescriptor opaqueDescriptor = CreateCameraOpaqueTextureDescriptor(frameData.camera);
                ReAllocateFrameTargetIfNeeded(
                    ref _opaqueTextureHandle,
                    opaqueDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: "_CameraOpaqueTexture");

                frameData.targets.opaqueTextureHandle = _opaqueTextureHandle;
                frameData.targets.opaqueTexture = frameData.targets.opaqueTextureHandle.nameID;
                frameData.targets.ownsOpaqueTexture = true;
                frameData.targets.hasOpaqueTexture = true;
            }
            else
            {
                ReleaseRTHandle(ref _opaqueTextureHandle);
                frameData.cmd.SetGlobalTexture(
                    NWRPShaderIds.CameraOpaqueTexture,
                    Texture2D.blackTexture);
            }
        }

        private void ReleaseFrameTargets(ref NWRPFrameData frameData)
        {
            if (frameData.cmd == null || !frameData.targets.hasCameraTargets)
            {
                return;
            }

            if (frameData.targets.ownsIntermediateColor)
            {
                frameData.targets.cameraColorHandle = null;
            }

            if (frameData.targets.ownsIntermediateDepth)
            {
                frameData.targets.cameraDepthHandle = null;
            }

            if (frameData.targets.ownsOpaqueTexture)
            {
                frameData.targets.opaqueTextureHandle = null;
            }

            if (frameData.targets.backBufferColorHandle != null)
            {
                RTHandles.Release(frameData.targets.backBufferColorHandle);
            }

            frameData.targets = default;
        }

        private void ReleaseRendererTargetHandles()
        {
            ReleaseRTHandle(ref _cameraColorHandle);
            ReleaseRTHandle(ref _cameraDepthHandle);
            ReleaseRTHandle(ref _opaqueTextureHandle);
        }

        private static void ReleaseRTHandle(ref RTHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            RTHandles.Release(handle);
            handle = null;
        }

        private static void ReAllocateFrameTargetIfNeeded(
            ref RTHandle handle,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            TextureWrapMode wrapMode,
            string name)
        {
            if (IsCompatibleFrameTarget(handle, descriptor, filterMode, wrapMode))
            {
                return;
            }

            ReleaseRTHandle(ref handle);
            handle = RTHandles.Alloc(
                descriptor,
                filterMode,
                wrapMode,
                name: name);
        }

        private static bool IsCompatibleFrameTarget(
            RTHandle handle,
            in RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            TextureWrapMode wrapMode)
        {
            RenderTexture renderTexture = handle != null ? handle.rt : null;
            if (renderTexture == null)
            {
                return false;
            }

            RenderTextureDescriptor current = renderTexture.descriptor;
            return current.width == descriptor.width
                && current.height == descriptor.height
                && current.volumeDepth == descriptor.volumeDepth
                && current.graphicsFormat == descriptor.graphicsFormat
                && current.depthBufferBits == descriptor.depthBufferBits
                && current.msaaSamples == descriptor.msaaSamples
                && current.dimension == descriptor.dimension
                && current.useMipMap == descriptor.useMipMap
                && current.autoGenerateMips == descriptor.autoGenerateMips
                && current.bindMS == descriptor.bindMS
                && current.memoryless == descriptor.memoryless
                && current.vrUsage == descriptor.vrUsage
                && renderTexture.filterMode == filterMode
                && renderTexture.wrapMode == wrapMode;
        }

        private NWRPFrameTargetRequirements CollectFrameTargetRequirements(ref NWRPFrameData frameData)
        {
            NWRPFrameTargetRequirements requirements = default;
            if (frameData.asset == null)
            {
                return requirements;
            }

            List<NWRPFeature> features = frameData.asset.Features;
            for (int i = 0; i < features.Count; i++)
            {
                NWRPFeature feature = features[i];
                if (feature == null || !feature.IsEnabled)
                {
                    continue;
                }

                if (feature.TryGetFrameTargetRequirements(
                        ref frameData,
                        out NWRPFrameTargetRequirements featureRequirements))
                {
                    requirements.Merge(featureRequirements);
                }
            }

            if (frameData.asset.EnableOpaqueTexture)
            {
                requirements.requiresIntermediateColor = true;
                requirements.requiresIntermediateDepth = true;
                requirements.requiresOpaqueTexture = true;
            }

            return requirements;
        }

        private void PresentIntermediateColor(ref NWRPFrameData frameData)
        {
            if (!EnsureCoreBlitMaterial())
            {
                return;
            }

            if (frameData.targets.cameraColorHandle == null
                || frameData.camera == null)
            {
                return;
            }

            Rect cameraViewport = GetCameraViewport(frameData.camera);
            RTHandle source = frameData.targets.cameraColorHandle;
            Vector4 scaleBias = GetFinalBlitScaleBias(frameData.camera, source);
            RenderBufferLoadAction loadAction = IsDefaultViewport(frameData.camera, cameraViewport)
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load;
            int passIndex = source.rt != null && source.rt.filterMode == FilterMode.Bilinear ? 1 : 0;

            using (new ProfilingScope(frameData.cmd, NWRPProfiling.FinalBlit))
            {
                CoreUtils.SetRenderTarget(
                    frameData.cmd,
                    frameData.targets.backBufferColor,
                    loadAction,
                    RenderBufferStoreAction.Store,
                    ClearFlag.None,
                    Color.clear);
                frameData.cmd.SetViewport(cameraViewport);
                Blitter.BlitTexture(
                    frameData.cmd,
                    source,
                    scaleBias,
                    _coreBlitMaterial,
                    passIndex);
            }
        }

        private static Vector4 GetFinalBlitScaleBias(Camera camera, RTHandle source)
        {
            Vector2 viewportScale = source.useScaling
                ? new Vector2(
                    source.rtHandleProperties.rtHandleScale.x,
                    source.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;

            // Match URP FinalBlit: RT -> backbuffer needs a Y flip on top-left UV backends.
            bool yFlip = camera != null
                && camera.cameraType != CameraType.SceneView
                && camera.targetTexture == null
                && SystemInfo.graphicsUVStartsAtTop;

            return yFlip
                ? new Vector4(viewportScale.x, -viewportScale.y, 0f, viewportScale.y)
                : new Vector4(viewportScale.x, viewportScale.y, 0f, 0f);
        }

        private static bool IsDefaultViewport(Camera camera, Rect cameraViewport)
        {
            if (camera == null)
            {
                return true;
            }

            return Mathf.Approximately(cameraViewport.x, 0f)
                && Mathf.Approximately(cameraViewport.y, 0f)
                && Mathf.Approximately(cameraViewport.width, camera.pixelWidth)
                && Mathf.Approximately(cameraViewport.height, camera.pixelHeight);
        }

        private bool EnsureCoreBlitMaterial()
        {
            if (_coreBlitMaterial != null)
            {
                return true;
            }

            _coreBlitMaterial = NWRPBlitterResources.CreateCoreBlitMaterial();
            return _coreBlitMaterial != null;
        }

        private static void SetCameraRenderTarget(ref NWRPFrameData frameData)
        {
            if (frameData.cmd == null || !frameData.targets.hasCameraTargets)
            {
                return;
            }

            frameData.cmd.SetRenderTarget(
                frameData.targets.cameraColor,
                frameData.targets.cameraDepth);
            frameData.cmd.SetViewport(GetCameraViewport(frameData.camera));
        }

        private static Rect GetCameraViewport(Camera camera)
        {
            if (camera == null)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            Rect cameraViewport = camera.pixelRect;
            if (cameraViewport.width <= 0f || cameraViewport.height <= 0f)
            {
                cameraViewport = new Rect(
                    0f,
                    0f,
                    Mathf.Max(camera.pixelWidth, 1),
                    Mathf.Max(camera.pixelHeight, 1));
            }

            return cameraViewport;
        }

        private static RenderTextureDescriptor CreateCameraColorDescriptor(Camera camera)
        {
            RenderTextureFormat format = camera.allowHDR
                ? RenderTextureFormat.DefaultHDR
                : RenderTextureFormat.Default;

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                Mathf.Max(camera.pixelWidth, 1),
                Mathf.Max(camera.pixelHeight, 1),
                format,
                0)
            {
                msaaSamples = 1,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                useMipMap = false,
                autoGenerateMips = false
            };

            return descriptor;
        }

        private static RenderTextureDescriptor CreateCameraDepthDescriptor(Camera camera)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                Mathf.Max(camera.pixelWidth, 1),
                Mathf.Max(camera.pixelHeight, 1),
                RenderTextureFormat.Depth,
                24)
            {
                msaaSamples = 1,
                bindMS = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            return descriptor;
        }

        private static RenderTextureDescriptor CreateCameraOpaqueTextureDescriptor(Camera camera)
        {
            RenderTextureDescriptor descriptor = CreateCameraColorDescriptor(camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private void BuildPassQueue(ref NWRPFrameData frameData)
        {
            _activePasses.Clear();
            _enqueueCounter = 0;

            EnqueuePass(_setupCameraPass);
            EnqueuePass(_setupLightsPass);
            EnqueuePass(_drawOpaquePass);
            EnqueuePass(_drawSkyboxPass);
            EnqueuePass(_drawTransparentPass);

            EnqueueFeaturePasses(ref frameData);

#if UNITY_EDITOR
            EnqueueEditorGizmoPasses();
#endif

            // Final blit must stay last after all rendering/debug passes.
            EnqueuePass(_finalBlitPass);

            _activePasses.Sort(s_QueuedPassComparer);
        }

        private void ExecutePassQueue(ref NWRPFrameData frameData)
        {
            ExecuteStage(ref frameData, IsSetupCameraPass);
            ExecuteStage(ref frameData, IsSetupLightsPass);
            ExecuteShadowStage(ref frameData, NWRPProfiling.MainLightShadow, IsMainLightShadowPass);
            ExecuteShadowStage(ref frameData, NWRPProfiling.AdditionalLightShadow, IsAdditionalLightShadowPass);
            ExecuteStage(ref frameData, IsBeforeRenderingPass);
            ExecuteStage(ref frameData, IsMainRenderingOpaquePass);
            ExecuteStage(ref frameData, IsBeforeTransparentPass);
            ExecuteStage(ref frameData, IsMainRenderingTransparentPass);
            ExecuteStage(ref frameData, IsFinalBlitStagePass);
        }

        private void ExecuteShadowStage(
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

                for (int passIndex = 0; passIndex < _activePasses.Count; passIndex++)
                {
                    if (!predicate(_activePasses[passIndex]))
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
            Func<QueuedPass, bool> predicate)
        {
            if (!HasMatchingPass(predicate))
            {
                return;
            }

            ExecuteBuffer(ref frameData);

            for (int passIndex = 0; passIndex < _activePasses.Count; passIndex++)
            {
                if (!predicate(_activePasses[passIndex]))
                {
                    continue;
                }

                ExecutePass(ref frameData, _activePasses[passIndex].pass);
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
                && !IsShadowPass(queuedPass)
                && queuedPass.pass.passEvent <= NWRPPassEvent.ShadowMap;
        }

        private static bool IsMainLightShadowPass(QueuedPass queuedPass)
        {
            return ReferenceEquals(queuedPass.pass.profilingGroupSampler, NWRPProfiling.MainLightShadow);
        }

        private static bool IsAdditionalLightShadowPass(QueuedPass queuedPass)
        {
            return ReferenceEquals(
                queuedPass.pass.profilingGroupSampler,
                NWRPProfiling.AdditionalLightShadow);
        }

        private static bool IsShadowPass(QueuedPass queuedPass)
        {
            return IsMainLightShadowPass(queuedPass) || IsAdditionalLightShadowPass(queuedPass);
        }

        private static bool IsMainRenderingOpaquePass(QueuedPass queuedPass)
        {
            return queuedPass.pass.passEvent > NWRPPassEvent.ShadowMap
                && queuedPass.pass.passEvent <= NWRPPassEvent.Skybox;
        }

        private static bool IsMainRenderingTransparentPass(QueuedPass queuedPass)
        {
            return queuedPass.pass.passEvent >= NWRPPassEvent.Transparent
                && queuedPass.pass.passEvent < NWRPPassEvent.DebugOverlay;
        }

        private static bool IsBeforeTransparentPass(QueuedPass queuedPass)
        {
            return queuedPass.pass.passEvent > NWRPPassEvent.Skybox
                && queuedPass.pass.passEvent < NWRPPassEvent.Transparent;
        }

        private static bool IsFinalBlitStagePass(QueuedPass queuedPass)
        {
            return queuedPass.pass.passEvent >= NWRPPassEvent.DebugOverlay;
        }

        private void EnqueueFeaturePasses(ref NWRPFrameData frameData)
        {
            List<NWRPFeature> features = frameData.asset.Features;
            bool hasSerializedMainLightShadowFeature = false;
            bool hasSerializedAdditionalLightShadowFeature = false;
            bool hasActiveSerializedOutlineFeature = false;
            bool hasActiveSerializedOpaqueTextureFeature = false;
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

                if (feature is AdditionalLightShadowFeature)
                {
                    hasSerializedAdditionalLightShadowFeature = true;
                }

                if (feature is OutlineFeature)
                {
                    hasActiveSerializedOutlineFeature = true;
                }

                if (feature is OpaqueTextureFeature)
                {
                    hasActiveSerializedOpaqueTextureFeature = true;
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

            if (!hasSerializedAdditionalLightShadowFeature)
            {
                AdditionalLightShadowFeature runtimeAdditionalLightShadowFeature =
                    frameData.asset.GetOrCreateAdditionalLightShadowFeature();
                if (runtimeAdditionalLightShadowFeature != null && runtimeAdditionalLightShadowFeature.IsEnabled)
                {
                    runtimeAdditionalLightShadowFeature.EnsureCreated();
                    runtimeAdditionalLightShadowFeature.AddPasses(this, ref frameData);
                }
            }

            if (!hasActiveSerializedOutlineFeature && frameData.asset.EnableOutline)
            {
                OutlineFeature runtimeOutlineFeature = frameData.asset.GetOrCreateOutlineFeature();
                if (runtimeOutlineFeature != null && runtimeOutlineFeature.IsEnabled)
                {
                    runtimeOutlineFeature.EnsureCreated();
                    runtimeOutlineFeature.AddPasses(this, ref frameData);
                }
            }

            if (!hasActiveSerializedOpaqueTextureFeature && frameData.asset.EnableOpaqueTexture)
            {
                OpaqueTextureFeature runtimeOpaqueTextureFeature =
                    frameData.asset.GetOrCreateOpaqueTextureFeature();
                if (runtimeOpaqueTextureFeature != null && runtimeOpaqueTextureFeature.IsEnabled)
                {
                    runtimeOpaqueTextureFeature.EnsureCreated();
                    runtimeOpaqueTextureFeature.AddPasses(this, ref frameData);
                }
            }
        }

#if UNITY_EDITOR
        private void EnqueueEditorGizmoPasses()
        {
            EnqueuePass(_drawGizmosPreImageEffectsPass);
            EnqueuePass(_drawGizmosPostImageEffectsPass);
        }
#endif

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
                float shadowDistance = 0f;
                if (asset != null)
                {
                    if (asset.EnableMainLightShadows)
                    {
                        shadowDistance = Mathf.Max(
                            shadowDistance,
                            Mathf.Min(asset.MainLightShadowDistance, camera.farClipPlane));
                    }

                    if (asset.EnableAdditionalLightShadows)
                    {
                        shadowDistance = Mathf.Max(
                            shadowDistance,
                            Mathf.Min(asset.AdditionalLightShadowDistance, camera.farClipPlane));
                    }
                }

                cullingParameters.shadowDistance = Mathf.Max(0f, shadowDistance);

                cullingResults = context.Cull(ref cullingParameters);
                return true;
            }

            cullingResults = default;
            return false;
        }

#if UNITY_EDITOR
        private static void EmitSceneViewGeometry(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.SceneView)
            {
                return;
            }

            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }

        private static bool ShouldDrawGizmos(Camera camera)
        {
            return camera != null
                && camera.sceneViewFilterMode != Camera.SceneViewFilterMode.ShowFiltered
                && Handles.ShouldRenderGizmos();
        }
#endif

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
