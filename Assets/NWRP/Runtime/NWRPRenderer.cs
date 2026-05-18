using System;
using System.Collections.Generic;
using NWRP.Runtime.Lighting;
using NWRP.Runtime.Passes;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
        private const float kRenderScaleThreshold = 0.05f;

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
        private RTHandle _cameraDepthTextureHandle;
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
                        ConfigureCameraData(ref frameData);
                        ResolveCameraRenderScale(ref frameData);
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

        private static void SetCameraScreenGlobals(ref NWRPFrameData frameData)
        {
            Camera camera = frameData.camera;
            Vector2Int nativeSize = GetNativeCameraTargetSize(camera);
            float cameraWidth = Mathf.Max(nativeSize.x, 1);
            float cameraHeight = Mathf.Max(nativeSize.y, 1);
            Vector2 scaledCameraSize = GetScaledCameraTargetSize(ref frameData);
            float scaledCameraWidth = Mathf.Max(scaledCameraSize.x, 1f);
            float scaledCameraHeight = Mathf.Max(scaledCameraSize.y, 1f);

            frameData.cmd.SetGlobalVector(
                NWRPShaderIds.ScreenParams,
                new Vector4(
                    cameraWidth,
                    cameraHeight,
                    1.0f + 1.0f / cameraWidth,
                    1.0f + 1.0f / cameraHeight));

            frameData.cmd.SetGlobalVector(
                NWRPShaderIds.ScaledScreenParams,
                new Vector4(
                    scaledCameraWidth,
                    scaledCameraHeight,
                    1.0f + 1.0f / scaledCameraWidth,
                    1.0f + 1.0f / scaledCameraHeight));

            bool isCameraColorFinalTarget =
                camera != null
                && camera.cameraType == CameraType.Game
                && frameData.targets.cameraColorHandle != null
                && frameData.targets.cameraColorHandle.nameID == BuiltinRenderTextureType.CameraTarget
                && camera.targetTexture == null;
            bool yFlip = !isCameraColorFinalTarget;
            float flipSign = yFlip ? -1.0f : 1.0f;
            Vector4 scaleBiasRt = flipSign < 0.0f
                ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);

            frameData.cmd.SetGlobalVector(NWRPShaderIds.ScaleBiasRt, scaleBiasRt);
            frameData.cmd.SetGlobalVector(
                NWRPShaderIds.CameraDepthTextureScaleBias,
                GetCameraDepthTextureScaleBias(camera));
        }

        private static Vector4 GetCameraDepthTextureScaleBias(Camera camera)
        {
            if (SystemInfo.graphicsUVStartsAtTop
                && camera != null
                && (camera.cameraType == CameraType.SceneView
                    || camera.cameraType == CameraType.Preview))
            {
                return new Vector4(1.0f, -1.0f, 0.0f, 1.0f);
            }

            return new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
        }

        private static Vector2 GetScaledCameraTargetSize(ref NWRPFrameData frameData)
        {
            return new Vector2(
                Mathf.Max(frameData.cameraTargetWidth, 1),
                Mathf.Max(frameData.cameraTargetHeight, 1));
        }

        private static void SetCameraMatrices(ref NWRPFrameData frameData)
        {
            Camera camera = frameData.camera;
            if (camera == null)
            {
                return;
            }

            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projectionMatrix = camera.projectionMatrix;
            frameData.cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            bool projectionFlipped = IsCameraProjectionMatrixFlipped(camera, frameData.targets.cameraColorHandle);
            Matrix4x4 gpuProjectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, projectionFlipped);
            Matrix4x4 inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
            Matrix4x4 inverseProjectionMatrix = Matrix4x4.Inverse(gpuProjectionMatrix);
            frameData.cmd.SetGlobalMatrix(NWRPShaderIds.InverseViewMatrix, inverseViewMatrix);
            frameData.cmd.SetGlobalMatrix(NWRPShaderIds.InverseProjectionMatrix, inverseProjectionMatrix);
            frameData.cmd.SetGlobalMatrix(
                NWRPShaderIds.InverseViewProjectionMatrix,
                inverseViewMatrix * inverseProjectionMatrix);
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
            if (frameData.targets.cameraColorPresented)
            {
                ExecuteBuffer(ref frameData);
                return;
            }

            if (frameData.targets.usesIntermediateColor)
            {
                PresentIntermediateColor(ref frameData);
            }

            ExecuteBuffer(ref frameData);
        }

        private static void ConfigureCameraData(ref NWRPFrameData frameData)
        {
            frameData.cameraData = null;
            frameData.volumeStack = null;
            frameData.postProcessingEnabled = false;
            frameData.tonemappingActive = false;
            frameData.bloomActive = false;
            frameData.colorAdjustmentsActive = false;
            frameData.vignetteActive = false;
            frameData.tonemapping = null;
            frameData.bloom = null;
            frameData.colorAdjustments = null;
            frameData.vignette = null;

            Camera camera = frameData.camera;
            if (camera != null)
            {
                if (camera.TryGetComponent(out NWRPCameraData cameraData))
                {
                    frameData.cameraData = cameraData;
                }
            }

            if (camera == null
                || frameData.asset == null
                || !frameData.asset.SupportsPostProcessing
                || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2)
            {
                return;
            }

#if UNITY_EDITOR
            // Match URP's Scene View behavior: the Scene window toolbar Effects/Post
            // Processing toggle controls whether editor-owned Scene View cameras run
            // post-processing. Scene View cameras do not reliably carry NWRPCameraData,
            // so they sample volumes from the Scene View camera instead.
            if (camera.cameraType == CameraType.SceneView)
            {
                if (!IsSceneViewPostProcessingEnabled(camera))
                {
                    return;
                }

                ConfigurePostProcessingFromVolume(
                    ref frameData,
                    camera.transform,
                    camera.cullingMask,
                    null);
                return;
            }
#endif

            if (frameData.cameraData == null
                || !frameData.cameraData.renderPostProcessing)
            {
                return;
            }

            ConfigurePostProcessingFromVolume(
                ref frameData,
                frameData.cameraData.GetVolumeTrigger(camera),
                frameData.cameraData.VolumeLayerMask,
                frameData.cameraData);
        }

#if UNITY_EDITOR
        private static bool IsSceneViewPostProcessingEnabled(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.SceneView)
            {
                return false;
            }

            if (CoreUtils.ArePostProcessesEnabled(camera))
            {
                return true;
            }

            // CoreUtils requires the SceneView camera reference to match exactly.
            // Some editor render paths can pass an equivalent temporary SceneView camera,
            // so fall back to the currently drawing SceneView state while keeping the
            // same toolbar and wireframe rules as URP.
            SceneView currentSceneView = SceneView.currentDrawingSceneView;
            return currentSceneView != null
                && currentSceneView.sceneViewState.imageEffectsEnabled
                && currentSceneView.cameraMode.drawMode != DrawCameraMode.Wireframe;
        }
#endif

        private static void ConfigurePostProcessingFromVolume(
            ref NWRPFrameData frameData,
            Transform volumeTrigger,
            LayerMask volumeLayerMask,
            NWRPCameraData cameraData)
        {
            // Keep the global stack deterministic between Game and Scene View cameras,
            // mirroring URP's per-camera volume update path.
            VolumeManager.instance.ResetMainStack();
            VolumeManager.instance.Update(volumeTrigger, volumeLayerMask);

            frameData.cameraData = cameraData;
            frameData.volumeStack = VolumeManager.instance.stack;
            if (frameData.volumeStack == null)
            {
                return;
            }

            frameData.postProcessingEnabled = true;

            NWRPTonemapping tonemapping = frameData.volumeStack.GetComponent<NWRPTonemapping>();
            frameData.tonemapping = tonemapping;
            frameData.tonemappingActive = tonemapping != null && tonemapping.IsActive();

            NWRPBloom bloom = frameData.volumeStack.GetComponent<NWRPBloom>();
            frameData.bloom = bloom;
            frameData.bloomActive =
                bloom != null
                && bloom.IsActive();

            NWRPColorAdjustments colorAdjustments =
                frameData.volumeStack.GetComponent<NWRPColorAdjustments>();
            frameData.colorAdjustments = colorAdjustments;
            frameData.colorAdjustmentsActive =
                colorAdjustments != null
                && colorAdjustments.IsActive();

            NWRPVignette vignette = frameData.volumeStack.GetComponent<NWRPVignette>();
            frameData.vignette = vignette;
            frameData.vignetteActive =
                vignette != null
                && vignette.IsActive();
        }

        private static void ResolveCameraRenderScale(ref NWRPFrameData frameData)
        {
            Vector2Int nativeSize = GetNativeCameraTargetSize(frameData.camera);
            frameData.cameraTargetWidth = nativeSize.x;
            frameData.cameraTargetHeight = nativeSize.y;
            frameData.resolvedRenderScale = 1.0f;
            frameData.renderScaleActive = false;
            frameData.renderScaleFilterMode = frameData.asset != null
                ? frameData.asset.RenderScaleFilterModeSetting
                : FilterMode.Bilinear;

            if (frameData.asset == null
                || !frameData.asset.EnableRenderScale
                || !IsRenderScaleEligible(frameData.camera))
            {
                return;
            }

            if (frameData.cameraData != null
                && frameData.cameraData.CameraRenderScaleMode == NWRPCameraData.RenderScaleMode.ForceNative)
            {
                return;
            }

            float requestedScale = frameData.asset.RenderScale;
            if (frameData.cameraData != null
                && frameData.cameraData.CameraRenderScaleMode == NWRPCameraData.RenderScaleMode.Override)
            {
                requestedScale = frameData.cameraData.RenderScaleOverride;
            }

            if (Mathf.Abs(1.0f - requestedScale) < kRenderScaleThreshold)
            {
                return;
            }

            frameData.resolvedRenderScale = requestedScale;
            frameData.renderScaleActive = true;
            frameData.cameraTargetWidth = Mathf.Max(1, (int)(nativeSize.x * requestedScale));
            frameData.cameraTargetHeight = Mathf.Max(1, (int)(nativeSize.y * requestedScale));
        }

        private static Vector2Int GetNativeCameraTargetSize(Camera camera)
        {
            if (camera == null)
            {
                return Vector2Int.one;
            }

            RenderTexture targetTexture = camera.targetTexture;
            if (targetTexture != null)
            {
                RenderTextureDescriptor descriptor = targetTexture.descriptor;
                return new Vector2Int(
                    Mathf.Max(descriptor.width, 1),
                    Mathf.Max(descriptor.height, 1));
            }

            return new Vector2Int(
                Mathf.Max(camera.pixelWidth, 1),
                Mathf.Max(camera.pixelHeight, 1));
        }

        private static bool IsRenderScaleEligible(Camera camera)
        {
            if (camera == null || camera.targetTexture != null)
            {
                return false;
            }

            return camera.cameraType != CameraType.SceneView
                && camera.cameraType != CameraType.Preview
                && camera.cameraType != CameraType.Reflection;
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
                requirements.requiresIntermediateColor
                || requirements.requiresOpaqueTexture
                || RequiresHDRIntermediateColor(frameData.camera, frameData.asset)
                || frameData.renderScaleActive;
            bool needIntermediateDepth =
                requirements.requiresIntermediateDepth
                || requirements.requiresDepthTextureCopy
                || needIntermediateColor;

            if (needIntermediateColor)
            {
                RenderTextureDescriptor colorDescriptor = CreateCameraColorDescriptor(
                    frameData.camera,
                    frameData.asset,
                    frameData.cameraTargetWidth,
                    frameData.cameraTargetHeight);
                ReAllocateFrameTargetIfNeeded(
                    ref _cameraColorHandle,
                    colorDescriptor,
                    frameData.renderScaleActive ? frameData.renderScaleFilterMode : FilterMode.Bilinear,
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
                RenderTextureDescriptor depthDescriptor = CreateCameraDepthDescriptor(
                    frameData.cameraTargetWidth,
                    frameData.cameraTargetHeight);
                ReAllocateFrameTargetIfNeeded(
                    ref _cameraDepthHandle,
                    depthDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: "_NWRPCameraDepthAttachment");

                frameData.targets.cameraDepthHandle = _cameraDepthHandle;
                frameData.targets.cameraDepth = frameData.targets.cameraDepthHandle.nameID;
                frameData.targets.ownsIntermediateDepth = true;
                frameData.targets.usesIntermediateDepth = true;
                frameData.cmd.SetGlobalTexture(
                    NWRPShaderIds.CameraDepthAttachment,
                    frameData.targets.cameraDepthHandle);
            }
            else
            {
                ReleaseRTHandle(ref _cameraDepthHandle);
            }

            frameData.cmd.SetGlobalTexture(
                NWRPShaderIds.CameraDepthTexture,
                GetDefaultDepthTexture());

            if (requirements.requiresDepthTexture)
            {
                bool depthTextureIsDepthTarget =
                    requirements.requiresDepthTexturePrepass || !SupportsDepthTextureColorTarget();
                RenderTextureDescriptor depthTextureDescriptor =
                    CreateCameraDepthTextureDescriptor(
                        frameData.cameraTargetWidth,
                        frameData.cameraTargetHeight,
                        depthTextureIsDepthTarget);
                ReAllocateFrameTargetIfNeeded(
                    ref _cameraDepthTextureHandle,
                    depthTextureDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: "_CameraDepthTexture");

                frameData.targets.cameraDepthTextureHandle = _cameraDepthTextureHandle;
                frameData.targets.cameraDepthTexture = frameData.targets.cameraDepthTextureHandle.nameID;
                frameData.targets.ownsCameraDepthTexture = true;
                frameData.targets.hasCameraDepthTexture = true;
                frameData.targets.cameraDepthTextureIsDepthTarget = depthTextureIsDepthTarget;
            }
            else
            {
                ReleaseRTHandle(ref _cameraDepthTextureHandle);
            }

            if (requirements.requiresOpaqueTexture)
            {
                RenderTextureDescriptor opaqueDescriptor = CreateCameraOpaqueTextureDescriptor(
                    frameData.camera,
                    frameData.asset,
                    frameData.cameraTargetWidth,
                    frameData.cameraTargetHeight);
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

            if (frameData.targets.ownsCameraDepthTexture)
            {
                frameData.targets.cameraDepthTextureHandle = null;
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
            ReleaseRTHandle(ref _cameraDepthTextureHandle);
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
                && current.depthStencilFormat == descriptor.depthStencilFormat
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
            bool hasActiveSerializedPostProcessFeature = false;
            for (int i = 0; i < features.Count; i++)
            {
                NWRPFeature feature = features[i];
                if (feature == null || !feature.IsEnabled)
                {
                    continue;
                }

                if (feature is PostProcessFeature)
                {
                    hasActiveSerializedPostProcessFeature = true;
                }

                if (feature.TryGetFrameTargetRequirements(
                        ref frameData,
                        out NWRPFrameTargetRequirements featureRequirements))
                {
                    requirements.Merge(featureRequirements);
                }
            }

            if (!hasActiveSerializedPostProcessFeature && PostProcessFeature.HasAnyActivePostProcess(ref frameData))
            {
                PostProcessFeature runtimePostProcessFeature =
                    frameData.asset.GetOrCreatePostProcessFeature();
                runtimePostProcessFeature.EnsureCreated();
                if (runtimePostProcessFeature.TryGetFrameTargetRequirements(
                        ref frameData,
                        out NWRPFrameTargetRequirements postProcessRequirements))
                {
                    requirements.Merge(postProcessRequirements);
                }
            }

            if (frameData.asset.EnableOpaqueTexture)
            {
                requirements.requiresIntermediateColor = true;
                requirements.requiresIntermediateDepth = true;
                requirements.requiresOpaqueTexture = true;
            }

            if (frameData.asset.EnableDepthTexture)
            {
                requirements.Merge(DepthTextureFeature.GetFrameTargetRequirements(
                    frameData.asset.DepthTextureCopyModeSetting,
                    frameData.camera));
            }

            return requirements;
        }

        private static bool RequiresHDRIntermediateColor(
            Camera camera,
            NewWorldRenderPipelineAsset asset)
        {
            return IsCameraHDRRenderingEnabled(camera, asset)
                && camera.targetTexture == null;
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

                frameData.targets.cameraColorPresented = true;
            }
        }

        internal static Vector4 GetFinalBlitScaleBias(Camera camera, RTHandle source)
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

        internal static bool IsDefaultViewport(Camera camera, Rect cameraViewport)
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
            frameData.cmd.SetViewport(GetCameraRenderViewport(ref frameData));
            SetCameraMatrices(ref frameData);
            SetCameraScreenGlobals(ref frameData);
        }

        private static bool IsCameraProjectionMatrixFlipped(Camera camera, RTHandle colorHandle)
        {
            if (!SystemInfo.graphicsUVStartsAtTop)
            {
                return false;
            }

            return camera.targetTexture != null || IsHandleYFlipped(camera, colorHandle);
        }

        private static bool IsHandleYFlipped(Camera camera, RTHandle handle)
        {
            if (!SystemInfo.graphicsUVStartsAtTop)
            {
                return false;
            }

            if (camera != null
                && (camera.cameraType == CameraType.SceneView
                    || camera.cameraType == CameraType.Preview))
            {
                return true;
            }

            return handle != null && handle.nameID != BuiltinRenderTextureType.CameraTarget;
        }

        internal static Rect GetCameraViewport(Camera camera)
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

        internal static Rect GetCameraRenderViewport(ref NWRPFrameData frameData)
        {
            RTHandle colorHandle = frameData.targets.cameraColorHandle;
            if (colorHandle != null && colorHandle.rt != null)
            {
                return GetCameraTargetViewport(ref frameData);
            }

            return GetCameraViewport(frameData.camera);
        }

        internal static Rect GetCameraTargetViewport(ref NWRPFrameData frameData)
        {
            return new Rect(
                0f,
                0f,
                Mathf.Max(frameData.cameraTargetWidth, 1),
                Mathf.Max(frameData.cameraTargetHeight, 1));
        }

        private static RenderTextureDescriptor CreateCameraColorDescriptor(
            Camera camera,
            NewWorldRenderPipelineAsset asset,
            int width,
            int height)
        {
            RenderTexture targetTexture = camera != null ? camera.targetTexture : null;
            RenderTextureDescriptor descriptor;
            if (targetTexture != null)
            {
                descriptor = targetTexture.descriptor;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.depthBufferBits = 0;
            }
            else
            {
                descriptor = new RenderTextureDescriptor(
                    Mathf.Max(width, 1),
                    Mathf.Max(height, 1))
                {
                    graphicsFormat = MakeCameraColorGraphicsFormat(
                        IsCameraHDRRenderingEnabled(camera, asset),
                        asset != null
                            ? asset.HDRColorBufferPrecisionSetting
                            : NewWorldRenderPipelineAsset.HDRColorBufferPrecision._32Bits,
                        needsAlpha: Graphics.preserveFramebufferAlpha),
                    depthStencilFormat = GraphicsFormat.None,
                    depthBufferBits = 0
                };
            }

            descriptor.width = Mathf.Max(descriptor.width, 1);
            descriptor.height = Mathf.Max(descriptor.height, 1);
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static bool IsCameraHDRRenderingEnabled(
            Camera camera,
            NewWorldRenderPipelineAsset asset)
        {
            return camera != null
                && asset != null
                && camera.allowHDR
                && asset.SupportsHDR;
        }

        private static GraphicsFormat MakeCameraColorGraphicsFormat(
            bool isHdrEnabled,
            NewWorldRenderPipelineAsset.HDRColorBufferPrecision hdrColorBufferPrecision,
            bool needsAlpha)
        {
            if (!isHdrEnabled)
            {
                return SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            }

            // Mobile HDR baseline: prefer 32-bit R11G11B10 when alpha is not required.
            if (!needsAlpha
                && hdrColorBufferPrecision != NewWorldRenderPipelineAsset.HDRColorBufferPrecision._64Bits
                && SupportsRenderGraphicsFormat(GraphicsFormat.B10G11R11_UFloatPack32))
            {
                return GraphicsFormat.B10G11R11_UFloatPack32;
            }

            if (SupportsRenderGraphicsFormat(GraphicsFormat.R16G16B16A16_SFloat))
            {
                return GraphicsFormat.R16G16B16A16_SFloat;
            }

            return SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
        }

        private static bool SupportsRenderGraphicsFormat(GraphicsFormat format)
        {
            return SystemInfo.IsFormatSupported(format, FormatUsage.Linear | FormatUsage.Render);
        }

        private static RenderTextureDescriptor CreateCameraDepthDescriptor(int width, int height)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                Mathf.Max(width, 1),
                Mathf.Max(height, 1),
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

        private static RenderTextureDescriptor CreateCameraDepthTextureDescriptor(
            int width,
            int height,
            bool depthTarget)
        {
            RenderTextureDescriptor descriptor = depthTarget
                ? new RenderTextureDescriptor(
                    Mathf.Max(width, 1),
                    Mathf.Max(height, 1),
                    RenderTextureFormat.Depth,
                    24)
                : new RenderTextureDescriptor(
                    Mathf.Max(width, 1),
                    Mathf.Max(height, 1),
                    RenderTextureFormat.Default,
                    0);

            if (!depthTarget)
            {
                descriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.depthBufferBits = 0;
            }

            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateCameraOpaqueTextureDescriptor(
            Camera camera,
            NewWorldRenderPipelineAsset asset,
            int width,
            int height)
        {
            RenderTextureDescriptor descriptor = CreateCameraColorDescriptor(
                camera,
                asset,
                width,
                height);
            descriptor.depthBufferBits = 0;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static bool SupportsDepthTextureColorTarget()
        {
            return SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.Render);
        }

        private static Texture GetDefaultDepthTexture()
        {
            return SystemInfo.usesReversedZBuffer
                ? Texture2D.blackTexture
                : Texture2D.whiteTexture;
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
            bool hasSerializedVegetationIndirectShadowFeature = false;
            bool hasActiveSerializedOutlineFeature = false;
            bool hasActiveSerializedOpaqueTextureFeature = false;
            bool hasActiveSerializedFogFeature = false;
            bool hasActiveSerializedPostProcessFeature = false;
            bool hasActiveSerializedDepthTextureFeature =
                HasActiveSerializedDepthTextureFeature(features);

            if (!hasActiveSerializedDepthTextureFeature && frameData.asset.EnableDepthTexture)
            {
                DepthTextureFeature runtimeDepthTextureFeature =
                    frameData.asset.GetOrCreateDepthTextureFeature();
                if (runtimeDepthTextureFeature != null && runtimeDepthTextureFeature.IsEnabled)
                {
                    runtimeDepthTextureFeature.EnsureCreated();
                    runtimeDepthTextureFeature.AddPasses(this, ref frameData);
                }
            }

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

                if (feature is VegetationIndirectShadowFeature)
                {
                    hasSerializedVegetationIndirectShadowFeature = true;
                    continue;
                }

                if (feature is OutlineFeature)
                {
                    hasActiveSerializedOutlineFeature = true;
                }

                if (feature is OpaqueTextureFeature)
                {
                    hasActiveSerializedOpaqueTextureFeature = true;
                }

                if (feature is NWRPFogFeature)
                {
                    hasActiveSerializedFogFeature = true;
                }

                if (feature is PostProcessFeature)
                {
                    hasActiveSerializedPostProcessFeature = true;
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

            if (hasSerializedVegetationIndirectShadowFeature)
            {
                for (int i = 0; i < features.Count; i++)
                {
                    if (features[i] is not VegetationIndirectShadowFeature feature
                        || !feature.IsEnabled)
                    {
                        continue;
                    }

                    feature.EnsureCreated();
                    feature.AddPasses(this, ref frameData);
                }
            }
            else if (frameData.asset.EnableVegetationIndirectTreeShadows)
            {
                VegetationIndirectShadowFeature runtimeVegetationIndirectShadowFeature =
                    frameData.asset.GetOrCreateVegetationIndirectShadowFeature();
                if (runtimeVegetationIndirectShadowFeature != null
                    && runtimeVegetationIndirectShadowFeature.IsEnabled)
                {
                    runtimeVegetationIndirectShadowFeature.EnsureCreated();
                    runtimeVegetationIndirectShadowFeature.AddPasses(this, ref frameData);
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

            if (!hasActiveSerializedFogFeature)
            {
                NWRPFogFeature runtimeFogFeature = frameData.asset.GetOrCreateFogFeature();
                if (runtimeFogFeature != null && runtimeFogFeature.IsEnabled)
                {
                    runtimeFogFeature.EnsureCreated();
                    runtimeFogFeature.AddPasses(this, ref frameData);
                }
            }

            if (!hasActiveSerializedPostProcessFeature && PostProcessFeature.HasAnyActivePostProcess(ref frameData))
            {
                PostProcessFeature runtimePostProcessFeature =
                    frameData.asset.GetOrCreatePostProcessFeature();
                if (runtimePostProcessFeature != null && runtimePostProcessFeature.IsEnabled)
                {
                    runtimePostProcessFeature.EnsureCreated();
                    runtimePostProcessFeature.AddPasses(this, ref frameData);
                }
            }
        }

        private static bool HasActiveSerializedDepthTextureFeature(List<NWRPFeature> features)
        {
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] is DepthTextureFeature feature && feature.IsEnabled)
                {
                    return true;
                }
            }

            return false;
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
