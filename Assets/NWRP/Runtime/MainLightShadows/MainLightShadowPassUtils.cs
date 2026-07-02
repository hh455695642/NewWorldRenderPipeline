using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal static class MainLightShadowPassUtils
    {
        private const float kRasterDepthBias = 1.0f;
        private const float kRasterSlopeBias = 2.5f;

        private static readonly Matrix4x4[] s_DisabledWorldToShadowMatrices =
        {
            Matrix4x4.identity,
            Matrix4x4.identity
        };
        private static readonly Vector3[] s_CascadeNearCorners = new Vector3[4];
        private static readonly Vector3[] s_CascadeFarCorners = new Vector3[4];
        private static readonly Vector3[] s_CascadeWorldCorners = new Vector3[8];

        internal static readonly ProfilingSampler RenderRealtimeShadowAtlasSampler =
            new ProfilingSampler("Render Main Light Realtime Cascades");
        internal static readonly ProfilingSampler RenderCachedShadowSampler =
            new ProfilingSampler("Render Main Light Cached Shadow");
        internal static readonly ProfilingSampler RenderDynamicOverlaySampler =
            new ProfilingSampler("Render Main Light Dynamic Overlay");
        internal static readonly ProfilingSampler DebugStaticCasterTintSampler =
            new ProfilingSampler("Tint Main Light Static Casters");
        internal static readonly ProfilingSampler DebugDynamicCasterTintSampler =
            new ProfilingSampler("Tint Main Light Dynamic Casters");

        public static bool TryGetMainLight(
            ref NWRPFrameData frameData,
            out int mainLightIndex,
            out VisibleLight mainVisibleLight,
            out Light mainLight
        )
        {
            return TryGetMainLight(frameData.cullingResults, out mainLightIndex, out mainVisibleLight, out mainLight);
        }

        public static bool TryGetMainLight(
            CullingResults cullingResults,
            out int mainLightIndex,
            out VisibleLight mainVisibleLight,
            out Light mainLight
        )
        {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            for (int i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType != LightType.Directional)
                {
                    continue;
                }

                mainLightIndex = i;
                mainVisibleLight = visibleLight;
                mainLight = visibleLight.light;
                return true;
            }

            mainLightIndex = -1;
            mainVisibleLight = default;
            mainLight = null;
            return false;
        }

        public static bool TryGetMainLightIndex(
            CullingResults cullingResults,
            Light expectedMainLight,
            out int mainLightIndex,
            out VisibleLight mainVisibleLight
        )
        {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            int fallbackIndex = -1;
            VisibleLight fallbackVisibleLight = default;

            for (int i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType != LightType.Directional)
                {
                    continue;
                }

                if (fallbackIndex == -1)
                {
                    fallbackIndex = i;
                    fallbackVisibleLight = visibleLight;
                }

                if (expectedMainLight != null && visibleLight.light == expectedMainLight)
                {
                    mainLightIndex = i;
                    mainVisibleLight = visibleLight;
                    return true;
                }
            }

            if (fallbackIndex != -1)
            {
                mainLightIndex = fallbackIndex;
                mainVisibleLight = fallbackVisibleLight;
                return true;
            }

            mainLightIndex = -1;
            mainVisibleLight = default;
            return false;
        }

        public static float GetEffectiveShadowDistance(NewWorldRenderPipelineAsset asset, Camera camera)
        {
            if (asset == null || camera == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, Mathf.Min(asset.MainLightShadowDistance, camera.farClipPlane));
        }

        public static int GetStaticCasterLayerMaskValue(NewWorldRenderPipelineAsset asset)
        {
            if (asset == null)
            {
                return ~0;
            }

            int staticMask = asset.StaticCasterLayerMask.value;
            if (ShouldRenderDynamicOverlay(asset))
            {
                staticMask &= ~asset.DynamicCasterLayerMask.value;
            }

            return staticMask;
        }

        public static bool ShouldRenderDynamicOverlay(NewWorldRenderPipelineAsset asset)
        {
            return asset != null
                && asset.EnableCachedMainLightShadows
                && asset.EnableDynamicShadowOverlay
                && asset.DynamicCasterLayerMask.value != 0;
        }

        public static bool ShouldRenderShadowDebugView(
            NewWorldRenderPipelineAsset asset,
            Camera camera)
        {
            return asset != null
                && asset.MainLightShadowDebugViewModeSetting
                    == NewWorldRenderPipelineAsset.MainLightShadowDebugViewMode.FinalShadowSourceTint
                && camera != null
                && camera.cameraType == CameraType.Game;
        }

        public static bool ShouldUseCachedMainLightShadow(Camera camera)
        {
            return camera != null && camera.cameraType == CameraType.Game;
        }

        public static bool TryCull(ref NWRPFrameData frameData, int layerMaskValue, out CullingResults cullResults)
        {
            if (!frameData.camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
            {
                cullResults = default;
                return false;
            }

            cullingParameters.shadowDistance = GetEffectiveShadowDistance(frameData.asset, frameData.camera);
            cullingParameters.cullingMask &= unchecked((uint)layerMaskValue);
            cullingParameters.cullingOptions |= CullingOptions.ShadowCasters;
            cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
            cullResults = frameData.context.Cull(ref cullingParameters);
            return true;
        }

        public static bool IsEverythingLayerMask(int layerMaskValue)
        {
            return layerMaskValue == ~0;
        }

        public static void GetAtlasSize(
            int resolution,
            int cascadeCount,
            out int atlasWidth,
            out int atlasHeight,
            out int tileResolution)
        {
            if (cascadeCount <= 1)
            {
                atlasWidth = resolution;
                atlasHeight = resolution;
                tileResolution = resolution;
                return;
            }

            tileResolution = Mathf.Max(1, resolution / 2);
            atlasWidth = tileResolution * 2;
            atlasHeight = tileResolution;
        }

        public static void GetTileOffset(int cascadeIndex, int tileResolution, out int offsetX, out int offsetY)
        {
            offsetX = cascadeIndex * tileResolution;
            offsetY = 0;
        }

        public static bool ComputeCascadeData(
            ref NWRPFrameData frameData,
            int mainLightIndex,
            Light mainLight,
            int cascadeCount,
            int atlasWidth,
            int atlasHeight,
            int tileResolution,
            MainLightShadowCacheState cacheState,
            bool allowCameraFrustumFallback = false
        )
        {
            Vector3 cascadeRatios = cascadeCount == 2
                ? new Vector3(frameData.asset.MainLightShadowCascadeSplit, 1f, 1f)
                : Vector3.zero;

            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
            {
                if (!TryComputeDirectionalShadowCascade(
                        ref frameData,
                        mainLightIndex,
                        cascadeIndex,
                        cascadeCount,
                        cascadeRatios,
                        tileResolution,
                        mainLight.shadowNearPlane,
                        mainLight,
                        allowCameraFrustumFallback,
                        out Matrix4x4 viewMatrix,
                        out Matrix4x4 projectionMatrix,
                        out ShadowSplitData splitData))
                {
                    return false;
                }

                splitData.shadowCascadeBlendCullingFactor = 1.0f;
                StabilizeDirectionalShadowProjection(ref viewMatrix, ref projectionMatrix, tileResolution);
                GetTileOffset(cascadeIndex, tileResolution, out int offsetX, out int offsetY);

                ref MainLightShadowCascadeData cascadeData = ref cacheState.CascadeData[cascadeIndex];
                cascadeData.viewMatrix = viewMatrix;
                cascadeData.projectionMatrix = projectionMatrix;
                cascadeData.splitData = splitData;
                cascadeData.cullingSphere = splitData.cullingSphere;
                cascadeData.offsetX = offsetX;
                cascadeData.offsetY = offsetY;
                cascadeData.resolution = tileResolution;
                cascadeData.worldToShadowMatrix = BuildWorldToShadowMatrix(
                    projectionMatrix,
                    viewMatrix,
                    offsetX,
                    offsetY,
                    tileResolution,
                    atlasWidth,
                    atlasHeight
                );

                cacheState.WorldToShadowMatrices[cascadeIndex] = cascadeData.worldToShadowMatrix;
                cacheState.CascadeSplitSpheres[cascadeIndex] = splitData.cullingSphere;
            }

            for (int cascadeIndex = cascadeCount;
                cascadeIndex < cacheState.WorldToShadowMatrices.Length;
                cascadeIndex++)
            {
                cacheState.CascadeData[cascadeIndex] = default;
                cacheState.WorldToShadowMatrices[cascadeIndex] = Matrix4x4.identity;
                cacheState.CascadeSplitSpheres[cascadeIndex] = Vector4.zero;
            }

            return true;
        }

        public static bool TryComputeDirectionalShadowCascade(
            ref NWRPFrameData frameData,
            int mainLightIndex,
            int cascadeIndex,
            int cascadeCount,
            Vector3 cascadeRatios,
            int tileResolution,
            float shadowNearPlane,
            Light mainLight,
            bool allowCameraFrustumFallback,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData
        )
        {
            if (frameData.cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    mainLightIndex,
                    cascadeIndex,
                    cascadeCount,
                    cascadeRatios,
                    tileResolution,
                    shadowNearPlane,
                    out viewMatrix,
                    out projectionMatrix,
                    out splitData))
            {
                return true;
            }

            if (!allowCameraFrustumFallback)
            {
                return false;
            }

            return TryComputeCameraFrustumDirectionalShadowCascade(
                ref frameData,
                mainLight,
                cascadeIndex,
                cascadeCount,
                tileResolution,
                out viewMatrix,
                out projectionMatrix,
                out splitData);
        }

        private static bool TryComputeCameraFrustumDirectionalShadowCascade(
            ref NWRPFrameData frameData,
            Light mainLight,
            int cascadeIndex,
            int cascadeCount,
            int tileResolution,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData
        )
        {
            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            splitData = default;

            Camera camera = frameData.camera;
            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (camera == null || asset == null || mainLight == null)
            {
                return false;
            }

            float nearClip = Mathf.Max(camera.nearClipPlane, 0.001f);
            float shadowDistance = Mathf.Max(
                GetEffectiveShadowDistance(asset, camera),
                nearClip + 0.001f);
            float cascadeSplit = Mathf.Clamp01(asset.MainLightShadowCascadeSplit);
            float splitNear = nearClip;
            float splitFar = shadowDistance;

            if (cascadeCount > 1)
            {
                float firstCascadeFar = Mathf.Lerp(nearClip, shadowDistance, cascadeSplit);
                if (cascadeIndex == 0)
                {
                    splitFar = firstCascadeFar;
                }
                else
                {
                    splitNear = firstCascadeFar;
                }
            }

            if (splitFar <= splitNear + 0.001f)
            {
                return false;
            }

            Rect fullViewport = new Rect(0f, 0f, 1f, 1f);
            camera.CalculateFrustumCorners(
                fullViewport,
                splitNear,
                Camera.MonoOrStereoscopicEye.Mono,
                s_CascadeNearCorners);
            camera.CalculateFrustumCorners(
                fullViewport,
                splitFar,
                Camera.MonoOrStereoscopicEye.Mono,
                s_CascadeFarCorners);

            Vector3 center = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                Vector3 nearCorner = camera.transform.TransformPoint(s_CascadeNearCorners[i]);
                Vector3 farCorner = camera.transform.TransformPoint(s_CascadeFarCorners[i]);
                s_CascadeWorldCorners[i] = nearCorner;
                s_CascadeWorldCorners[i + 4] = farCorner;
                center += nearCorner + farCorner;
            }

            center *= 0.125f;

            float radius = 0f;
            for (int i = 0; i < s_CascadeWorldCorners.Length; i++)
            {
                radius = Mathf.Max(radius, Vector3.Distance(center, s_CascadeWorldCorners[i]));
            }

            radius = Mathf.Max(radius, 0.001f);
            Vector3 lightDirection = -mainLight.transform.forward;
            if (lightDirection.sqrMagnitude < 0.0001f)
            {
                lightDirection = Vector3.forward;
            }

            lightDirection.Normalize();
            Vector3 shadowViewDirection = -lightDirection;
            Vector3 lightUp = Mathf.Abs(Vector3.Dot(shadowViewDirection, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;
            Quaternion lightRotation = Quaternion.LookRotation(shadowViewDirection, lightUp);
            Vector3 lightPosition = center - shadowViewDirection * radius;
            viewMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                * Matrix4x4.TRS(lightPosition, lightRotation, Vector3.one).inverse;

            Vector3 lightMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 lightMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < s_CascadeWorldCorners.Length; i++)
            {
                Vector3 lightSpaceCorner = viewMatrix.MultiplyPoint3x4(s_CascadeWorldCorners[i]);
                lightMin = Vector3.Min(lightMin, lightSpaceCorner);
                lightMax = Vector3.Max(lightMax, lightSpaceCorner);
            }

            float width = Mathf.Max(lightMax.x - lightMin.x, 0.001f);
            float height = Mathf.Max(lightMax.y - lightMin.y, 0.001f);
            float texelPadding = Mathf.Max(width, height) / Mathf.Max(tileResolution, 1);
            texelPadding *= 2f;

            float left = lightMin.x - texelPadding;
            float right = lightMax.x + texelPadding;
            float bottom = lightMin.y - texelPadding;
            float top = lightMax.y + texelPadding;
            float farPlane = Mathf.Max(-lightMin.z + texelPadding, radius * 2f + texelPadding);
            projectionMatrix = Matrix4x4.Ortho(left, right, bottom, top, 0.001f, farPlane);

            splitData.cullingSphere = new Vector4(center.x, center.y, center.z, radius);
            splitData.shadowCascadeBlendCullingFactor = 1.0f;
            return true;
        }

        public static void ClearShadowAtlas(ref NWRPFrameData frameData, RenderTexture shadowmapTexture)
        {
            NWRPRenderer.InvalidateCameraRenderTarget(ref frameData);
            frameData.cmd.SetRenderTarget(shadowmapTexture);
            frameData.cmd.ClearRenderTarget(true, false, Color.black);
            ExecuteBuffer(ref frameData);
        }

        public static bool CopyShadowAtlas(
            ref NWRPFrameData frameData,
            RenderTexture source,
            RenderTexture destination)
        {
            if (source == null
                || destination == null
                || source.width != destination.width
                || source.height != destination.height
                || (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
            {
                return false;
            }

            frameData.cmd.CopyTexture(source, destination);
            frameData.debugStats.RecordShadowAtlasCopy();
            ExecuteBuffer(ref frameData);
            return true;
        }

        public static void BindShadowAtlas(ref NWRPFrameData frameData, RenderTexture shadowmapTexture)
        {
            NWRPRenderer.InvalidateCameraRenderTarget(ref frameData);
            frameData.cmd.SetRenderTarget(
                shadowmapTexture,
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store);
            ExecuteBuffer(ref frameData);
        }

        public static bool RenderMainLightShadowAtlas(
            ref NWRPFrameData frameData,
            CullingResults cullResults,
            int shadowLightIndex,
            VisibleLight shadowVisibleLight,
            int cascadeCount,
            MainLightShadowCacheState cacheState,
            bool allowEmptyAtlas = false
        )
        {
            CommandBuffer cmd = frameData.cmd;
            bool hasRegularShadowCasters =
                cullResults.GetShadowCasterBounds(shadowLightIndex, out Bounds _);

            if (!hasRegularShadowCasters && !allowEmptyAtlas)
            {
                return false;
            }

            if (!hasRegularShadowCasters)
            {
                return HasValidCascadeData(cacheState, cascadeCount);
            }

            ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(
                cullResults,
                shadowLightIndex,
                BatchCullingProjectionType.Orthographic)
            {
                useRenderingLayerMaskTest = true
            };

            float shadowCasterCullMode = frameData.asset != null
                ? (float)frameData.asset.MainLightShadowCasterCullModeSetting
                : (float)CullMode.Back;
            cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, shadowCasterCullMode);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, GetShadowLightDirection(shadowVisibleLight));
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
            cmd.SetGlobalDepthBias(kRasterDepthBias, kRasterSlopeBias);
            ExecuteBuffer(ref frameData);

            bool anyCascadeRendered = false;
            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
            {
                MainLightShadowCascadeData cascadeData = cacheState.CascadeData[cascadeIndex];
                shadowDrawingSettings.splitData = cascadeData.splitData;

                cmd.SetViewport(new Rect(
                    cascadeData.offsetX,
                    cascadeData.offsetY,
                    cascadeData.resolution,
                    cascadeData.resolution));
                cmd.SetViewProjectionMatrices(cascadeData.viewMatrix, cascadeData.projectionMatrix);
                cmd.SetGlobalVector(
                    NWRPShaderIds.ShadowBias,
                    CalculateShadowBias(
                        frameData.asset.MainLightShadowBias,
                        frameData.asset.MainLightShadowNormalBias,
                        cascadeData.projectionMatrix,
                        cascadeData.resolution
                    )
                );
                ExecuteBuffer(ref frameData);

                frameData.context.DrawShadows(ref shadowDrawingSettings);
                anyCascadeRendered = true;
            }

            cmd.SetGlobalDepthBias(0f, 0f);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
            cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, (float)CullMode.Back);
            ExecuteBuffer(ref frameData);
            return anyCascadeRendered;
        }

        private static bool HasValidCascadeData(
            MainLightShadowCacheState cacheState,
            int cascadeCount)
        {
            if (cacheState == null || cascadeCount <= 0)
                return false;

            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
            {
                MainLightShadowCascadeData cascadeData = cacheState.CascadeData[cascadeIndex];
                if (cascadeData.resolution > 0)
                    return true;
            }

            return false;
        }

        public static void UploadDisabledGlobals(
            ref NWRPFrameData frameData,
            Texture fallbackShadowmap
        )
        {
            CommandBuffer cmd = frameData.cmd;

            cmd.SetGlobalTexture(
                NWRPShaderIds.MainLightShadowmapTexture,
                fallbackShadowmap != null ? fallbackShadowmap : Texture2D.blackTexture);
            cmd.SetGlobalMatrixArray(NWRPShaderIds.MainLightWorldToShadow, s_DisabledWorldToShadowMatrices);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowParams, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowmapSize, Vector4.zero);
            cmd.SetGlobalInt(NWRPShaderIds.MainLightShadowFilterMode, 0);
            cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowFilterRadius, 0f);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowReceiverBiasParams, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowAtlasTexelSize, Vector4.zero);
            cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, (float)CullMode.Back);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
            cmd.SetGlobalDepthBias(0.0f, 0.0f);
            cmd.SetGlobalInt(NWRPShaderIds.MainLightShadowCascadeCount, 0);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres0, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres1, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSphereRadii, Vector4.zero);
            UploadShadowDebugGlobals(
                ref frameData,
                NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.Disabled);

            ExecuteBuffer(ref frameData);
        }

        public static void UploadRealtimeReceiverGlobals(
            ref NWRPFrameData frameData,
            Texture staticShadowmap,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] cascadeSplitSpheres,
            float shadowStrength,
            int cascadeCount,
            int atlasWidth,
            int atlasHeight,
            int tileResolution
        )
        {
            CommandBuffer cmd = frameData.cmd;

            float maxDistance = Mathf.Max(frameData.asset.MainLightShadowDistance, 0.001f);
            float fadeRange = Mathf.Max(maxDistance * 0.1f, 0.001f);
            float invFadeRange = 1f / fadeRange;

            Vector4 shadowParams = new Vector4(
                shadowStrength,
                maxDistance,
                invFadeRange,
                0f
            );

            Vector4 shadowmapSize = new Vector4(
                1f / atlasWidth,
                1f / atlasHeight,
                atlasWidth,
                atlasHeight
            );
            float safeTileResolution = Mathf.Max(tileResolution, 1);
            Vector4 shadowAtlasTexelSize = new Vector4(
                1f / atlasWidth,
                1f / atlasHeight,
                1f / safeTileResolution,
                safeTileResolution
            );
            Vector4 receiverBiasParams = new Vector4(
                frameData.asset.MainLightShadowReceiverDepthBias,
                frameData.asset.MainLightShadowReceiverNormalBias,
                0f,
                0f
            );

            Vector4 sphere0 = cascadeSplitSpheres[0];
            Vector4 sphere1 = cascadeCount > 1 ? cascadeSplitSpheres[1] : cascadeSplitSpheres[0];
            Vector4 sphereRadii = new Vector4(
                sphere0.w * sphere0.w,
                sphere1.w * sphere1.w,
                0f,
                0f
            );

            cmd.SetGlobalTexture(NWRPShaderIds.MainLightShadowmapTexture, staticShadowmap);
            cmd.SetGlobalMatrixArray(NWRPShaderIds.MainLightWorldToShadow, worldToShadowMatrices);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowParams, shadowParams);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowmapSize, shadowmapSize);
            cmd.SetGlobalInt(
                NWRPShaderIds.MainLightShadowFilterMode,
                (int)frameData.asset.MainLightShadowFilterModeSetting);
            cmd.SetGlobalFloat(
                NWRPShaderIds.MainLightShadowFilterRadius,
                frameData.asset.MainLightShadowFilterRadius);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowReceiverBiasParams, receiverBiasParams);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowAtlasTexelSize, shadowAtlasTexelSize);
            cmd.SetGlobalInt(NWRPShaderIds.MainLightShadowCascadeCount, cascadeCount);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres0, sphere0);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres1, sphere1);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSphereRadii, sphereRadii);
            UploadShadowDebugGlobals(
                ref frameData,
                NewWorldRenderPipelineAsset.MainLightShadowExecutionPath.RealtimeAtlas);

            ExecuteBuffer(ref frameData);
        }

        public static void UploadCachedReceiverGlobals(
            ref NWRPFrameData frameData,
            Texture shadowmap,
            MainLightShadowCacheState cacheState,
            float shadowStrength,
            float maxShadowDistance,
            NewWorldRenderPipelineAsset.MainLightShadowExecutionPath executionPath
        )
        {
            CommandBuffer cmd = frameData.cmd;

            float safeMaxDistance = Mathf.Max(maxShadowDistance, 0.001f);
            float fadeRange = Mathf.Max(safeMaxDistance * 0.1f, 0.001f);
            float invFadeRange = 1f / fadeRange;

            Vector4 shadowParams = new Vector4(
                shadowStrength,
                safeMaxDistance,
                invFadeRange,
                0f
            );

            Vector4 shadowmapSize = new Vector4(
                1f / cacheState.AtlasWidth,
                1f / cacheState.AtlasHeight,
                cacheState.AtlasWidth,
                cacheState.AtlasHeight
            );
            float tileResolution = Mathf.Max(cacheState.TileResolution, 1);
            Vector4 shadowAtlasTexelSize = new Vector4(
                1f / cacheState.AtlasWidth,
                1f / cacheState.AtlasHeight,
                1f / tileResolution,
                tileResolution
            );
            Vector4 receiverBiasParams = new Vector4(
                frameData.asset.MainLightShadowReceiverDepthBias,
                frameData.asset.MainLightShadowReceiverNormalBias,
                0f,
                0f
            );

            Vector4 sphere0 = cacheState.CascadeSplitSpheres[0];
            Vector4 sphere1 = cacheState.CascadeCount > 1
                ? cacheState.CascadeSplitSpheres[1]
                : cacheState.CascadeSplitSpheres[0];
            Vector4 sphereRadii = new Vector4(
                sphere0.w * sphere0.w,
                sphere1.w * sphere1.w,
                0f,
                0f
            );

            cmd.SetGlobalTexture(
                NWRPShaderIds.MainLightShadowmapTexture,
                shadowmap != null ? shadowmap : Texture2D.blackTexture);
            cmd.SetGlobalMatrixArray(NWRPShaderIds.MainLightWorldToShadow, cacheState.WorldToShadowMatrices);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowParams, shadowParams);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowmapSize, shadowmapSize);
            cmd.SetGlobalInt(
                NWRPShaderIds.MainLightShadowFilterMode,
                (int)frameData.asset.MainLightShadowFilterModeSetting);
            cmd.SetGlobalFloat(
                NWRPShaderIds.MainLightShadowFilterRadius,
                frameData.asset.MainLightShadowFilterRadius);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowReceiverBiasParams, receiverBiasParams);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowAtlasTexelSize, shadowAtlasTexelSize);
            cmd.SetGlobalInt(NWRPShaderIds.MainLightShadowCascadeCount, cacheState.CascadeCount);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres0, sphere0);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres1, sphere1);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSphereRadii, sphereRadii);
            UploadShadowDebugGlobals(ref frameData, executionPath);

            ExecuteBuffer(ref frameData);
        }

        public static Vector4 CalculateShadowBias(
            float depthBias,
            float normalBias,
            Matrix4x4 projectionMatrix,
            int tileResolution
        )
        {
            float safeResolution = Mathf.Max(tileResolution, 1);
            float frustumSize = 2.0f / Mathf.Max(Mathf.Abs(projectionMatrix.m00), 0.00001f);
            float texelSize = frustumSize / safeResolution;

            return new Vector4(-depthBias * texelSize, -normalBias * texelSize, 0f, 0f);
        }

        public static void StabilizeDirectionalShadowProjection(
            ref Matrix4x4 viewMatrix,
            ref Matrix4x4 projectionMatrix,
            int tileResolution
        )
        {
            float texelScale = Mathf.Max(tileResolution, 1) * 0.5f;
            Matrix4x4 worldToClip = projectionMatrix * viewMatrix;
            Vector3 shadowOrigin = worldToClip.MultiplyPoint3x4(Vector3.zero) * texelScale;
            Vector3 roundedOrigin = new Vector3(
                Mathf.Round(shadowOrigin.x),
                Mathf.Round(shadowOrigin.y),
                shadowOrigin.z);
            Vector3 snapOffset = (roundedOrigin - shadowOrigin) / texelScale;

            // Keeps cached and dynamic overlay cascades aligned to the same shadow texel grid.
            projectionMatrix.m03 += snapOffset.x;
            projectionMatrix.m13 += snapOffset.y;
        }

        public static Matrix4x4 BuildWorldToShadowMatrix(
            Matrix4x4 projection,
            Matrix4x4 view,
            int tileOffsetX,
            int tileOffsetY,
            int tileResolution,
            int atlasWidth,
            int atlasHeight
        )
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                projection.m20 = -projection.m20;
                projection.m21 = -projection.m21;
                projection.m22 = -projection.m22;
                projection.m23 = -projection.m23;
            }

            Matrix4x4 worldToShadow = projection * view;

            Matrix4x4 textureScaleBias = Matrix4x4.identity;
            textureScaleBias.m00 = 0.5f;
            textureScaleBias.m11 = 0.5f;
            textureScaleBias.m22 = 0.5f;
            textureScaleBias.m03 = 0.5f;
            textureScaleBias.m13 = 0.5f;
            textureScaleBias.m23 = 0.5f;

            Matrix4x4 atlasTransform = Matrix4x4.identity;
            atlasTransform.m00 = (float)tileResolution / atlasWidth;
            atlasTransform.m11 = (float)tileResolution / atlasHeight;
            atlasTransform.m03 = (float)tileOffsetX / atlasWidth;
            atlasTransform.m13 = (float)tileOffsetY / atlasHeight;

            return atlasTransform * textureScaleBias * worldToShadow;
        }

        public static void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            frameData.context.ExecuteCommandBuffer(frameData.cmd);
            frameData.cmd.Clear();
        }

        private static void UploadShadowDebugGlobals(
            ref NWRPFrameData frameData,
            NewWorldRenderPipelineAsset.MainLightShadowExecutionPath executionPath)
        {
            NewWorldRenderPipelineAsset asset = frameData.asset;
            bool enableDebugView = ShouldRenderShadowDebugView(asset, frameData.camera);
            frameData.cmd.SetGlobalInt(
                NWRPShaderIds.MainLightShadowDebugViewMode,
                enableDebugView
                    ? (int)asset.MainLightShadowDebugViewModeSetting
                    : (int)NewWorldRenderPipelineAsset.MainLightShadowDebugViewMode.Off);
            frameData.cmd.SetGlobalInt(
                NWRPShaderIds.MainLightShadowDebugExecutionPath,
                (int)executionPath);
        }

        public static Vector4 GetShadowLightDirection(VisibleLight visibleLight)
        {
            Vector4 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
            lightDirection = lightDirection.normalized;
            lightDirection.w = 0f;
            return lightDirection;
        }
    }
}
