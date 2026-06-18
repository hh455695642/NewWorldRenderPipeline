using Unity.Collections;
using NWRP.Runtime.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class MainLightShadowCasterPass : NWRPPass
    {
        private const float kRasterDepthBias = 1.0f;
        private const float kRasterSlopeBias = 2.5f;

        private readonly Matrix4x4[] _mainLightWorldToShadow = new Matrix4x4[2];
        private readonly Vector4[] _cascadeSplitSpheres = new Vector4[2];
        private readonly MainLightShadowCascadeData[] _cascadeData =
            new MainLightShadowCascadeData[2];
        private readonly bool[] _cascadeValid = new bool[2];

        private RenderTexture _shadowmapTexture;
        private int _shadowmapWidth;
        private int _shadowmapHeight;

        public MainLightShadowCasterPass()
            : base(
                NWRPPassEvent.ShadowMap,
                "Render Main Light Realtime Cascades",
                NWRPProfiling.MainLightShadow,
                usePassProfilingScope: false)
        {
            _mainLightWorldToShadow[0] = Matrix4x4.identity;
            _mainLightWorldToShadow[1] = Matrix4x4.identity;
        }

        public void Dispose()
        {
            ReleaseShadowmap();
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (asset == null || !asset.EnableMainLightShadows)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (!TryGetMainLightIndex(ref frameData, out int mainLightIndex, out Light mainLight))
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            if (mainLight == null || mainLight.shadows == LightShadows.None || mainLight.shadowStrength <= 0f)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            bool hasRegularShadowCasters =
                frameData.cullingResults.GetShadowCasterBounds(mainLightIndex, out Bounds _);
            bool hasIndirectShadowCasters = HasIndirectShadowCasters(
                ref frameData,
                includeStaticCasters: true,
                includeDynamicCasters: true);

            if (!hasRegularShadowCasters && !hasIndirectShadowCasters)
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            int cascadeCount = Mathf.Clamp(asset.MainLightShadowCascadeCount, 1, 2);
            int requestedResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(asset.MainLightShadowResolution, 256, 4096));
            GetAtlasSize(
                requestedResolution,
                cascadeCount,
                out int atlasWidth,
                out int atlasHeight,
                out int tileResolution);
            NWRPShadowCullingContext shadowCullingContext = frameData.shadowCullingContext;
            bool usePreparedMainCascades = hasRegularShadowCasters
                && shadowCullingContext != null
                && shadowCullingContext.HasMainLightShadowFor(mainLightIndex);
            if (usePreparedMainCascades)
            {
                cascadeCount = shadowCullingContext.MainCascadeCount;
                atlasWidth = shadowCullingContext.MainAtlasWidth;
                atlasHeight = shadowCullingContext.MainAtlasHeight;
                tileResolution = shadowCullingContext.MainTileResolution;
            }

            bool anyCascadeRendered;
            using (new ProfilingScope(frameData.cmd, MainLightShadowPassUtils.RenderRealtimeShadowAtlasSampler))
            {
                if (!EnsureShadowmap(atlasWidth, atlasHeight))
                {
                    UploadDisabledGlobals(ref frameData);
                    return;
                }

                MainLightShadowPassUtils.ClearShadowAtlas(ref frameData, _shadowmapTexture);
                CommandBuffer cmd = frameData.cmd;

                Vector4 shadowLightDirection = GetShadowLightDirection(ref frameData, mainLightIndex);
                cmd.SetGlobalFloat(
                    NWRPShaderIds.MainLightShadowCasterCull,
                    (float)asset.MainLightShadowCasterCullModeSetting);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, shadowLightDirection);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
                cmd.SetGlobalDepthBias(kRasterDepthBias, kRasterSlopeBias);
                ExecuteBuffer(ref frameData);

                anyCascadeRendered = false;
                if (usePreparedMainCascades)
                {
                    for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
                    {
                        _cascadeValid[cascadeIndex] =
                            shadowCullingContext.MainCascadeValid[cascadeIndex];
                        _cascadeData[cascadeIndex] =
                            shadowCullingContext.MainCascadeData[cascadeIndex];
                        _cascadeSplitSpheres[cascadeIndex] =
                            _cascadeData[cascadeIndex].cullingSphere;
                        _mainLightWorldToShadow[cascadeIndex] = _cascadeValid[cascadeIndex]
                            ? _cascadeData[cascadeIndex].worldToShadowMatrix
                            : Matrix4x4.identity;
                        anyCascadeRendered |= _cascadeValid[cascadeIndex];
                    }
                }
                else
                {
                    Vector3 cascadeRatios = cascadeCount == 2
                        ? new Vector3(asset.MainLightShadowCascadeSplit, 1f, 1f)
                        : Vector3.zero;

                    for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
                    {
                        _cascadeValid[cascadeIndex] = false;
                        if (!MainLightShadowPassUtils.TryComputeDirectionalShadowCascade(
                                ref frameData,
                                mainLightIndex,
                                cascadeIndex,
                                cascadeCount,
                                cascadeRatios,
                                tileResolution,
                                mainLight.shadowNearPlane,
                                mainLight,
                                allowCameraFrustumFallback: !hasRegularShadowCasters && hasIndirectShadowCasters,
                                out Matrix4x4 viewMatrix,
                                out Matrix4x4 projMatrix,
                                out ShadowSplitData splitData))
                        {
                            _mainLightWorldToShadow[cascadeIndex] = Matrix4x4.identity;
                            _cascadeSplitSpheres[cascadeIndex] = Vector4.zero;
                            _cascadeData[cascadeIndex] = default;
                            continue;
                        }

                        splitData.shadowCascadeBlendCullingFactor = 1.0f;
                        _cascadeSplitSpheres[cascadeIndex] = splitData.cullingSphere;

                        GetTileOffset(cascadeIndex, tileResolution, out int offsetX, out int offsetY);
                        _cascadeValid[cascadeIndex] = true;
                        anyCascadeRendered = true;

                        _cascadeData[cascadeIndex] = new MainLightShadowCascadeData
                        {
                            viewMatrix = viewMatrix,
                            projectionMatrix = projMatrix,
                            splitData = splitData,
                            cullingSphere = splitData.cullingSphere,
                            offsetX = offsetX,
                            offsetY = offsetY,
                            resolution = tileResolution
                        };

                        _mainLightWorldToShadow[cascadeIndex] = BuildWorldToShadowMatrix(
                            projMatrix,
                            viewMatrix,
                            offsetX,
                            offsetY,
                            tileResolution,
                            atlasWidth,
                            atlasHeight
                        );
                    }
                }

                for (int cascadeIndex = cascadeCount; cascadeIndex < _cascadeValid.Length; cascadeIndex++)
                {
                    _cascadeValid[cascadeIndex] = false;
                    _cascadeData[cascadeIndex] = default;
                    _mainLightWorldToShadow[cascadeIndex] = Matrix4x4.identity;
                    _cascadeSplitSpheres[cascadeIndex] = Vector4.zero;
                }

                bool canDrawRegularShadowCasters = !hasRegularShadowCasters
                    || (usePreparedMainCascades && shadowCullingContext.Apply(ref frameData));
                for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
                {
                    if (!_cascadeValid[cascadeIndex])
                    {
                        continue;
                    }

                    MainLightShadowCascadeData cascadeData = _cascadeData[cascadeIndex];
                    cmd.SetViewport(new Rect(
                        cascadeData.offsetX,
                        cascadeData.offsetY,
                        cascadeData.resolution,
                        cascadeData.resolution));
                    cmd.SetViewProjectionMatrices(
                        cascadeData.viewMatrix,
                        cascadeData.projectionMatrix);
                    cmd.SetGlobalVector(
                        NWRPShaderIds.ShadowBias,
                        CalculateShadowBias(asset, cascadeData.projectionMatrix, cascadeData.resolution)
                    );
                    ExecuteBuffer(ref frameData);

                    if (hasRegularShadowCasters && canDrawRegularShadowCasters)
                    {
                        ShadowDrawingSettings shadowDrawingSettings =
                            MainLightShadowPassUtils.CreateShadowDrawingSettings(
                                frameData.cullingResults,
                                mainLightIndex,
                                useRenderingLayerMaskTest: false,
                                splitIndex: cascadeIndex);
                        MainLightShadowPassUtils.DrawShadowRendererList(
                            ref frameData,
                            ref shadowDrawingSettings);
                    }
                }

                cmd.SetGlobalDepthBias(0.0f, 0.0f);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
                cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, (float)CullMode.Back);
                ExecuteBuffer(ref frameData);
            }

            if (!anyCascadeRendered)
            {
                UploadDisabledGlobals(ref frameData);
                frameData.context.SetupCameraProperties(frameData.camera);
                return;
            }

            frameData.context.SetupCameraProperties(frameData.camera);

            if (hasIndirectShadowCasters)
            {
                MainLightShadowIndirectCasterContext.AddTarget(
                    _shadowmapTexture,
                    _cascadeData,
                    cascadeCount,
                    GetShadowLightDirection(ref frameData, mainLightIndex),
                    includeStaticCasters: true,
                    includeDynamicCasters: true);
            }

            MainLightShadowPassUtils.UploadRealtimeReceiverGlobals(
                ref frameData,
                _shadowmapTexture,
                _mainLightWorldToShadow,
                _cascadeSplitSpheres,
                mainLight.shadowStrength,
                cascadeCount,
                atlasWidth,
                atlasHeight,
                tileResolution
            );
        }

        private static bool HasIndirectShadowCasters(
            ref NWRPFrameData frameData,
            bool includeStaticCasters,
            bool includeDynamicCasters)
        {
            bool allowIndirectTreeShadows = frameData.rendererData != null
                && frameData.rendererData.EnableVegetationIndirectTreeShadows;

#if UNITY_EDITOR
            allowIndirectTreeShadows |= frameData.camera != null
                && frameData.camera.cameraType == CameraType.SceneView;
#endif

            return allowIndirectTreeShadows
                && VegetationIndirectShadowRegistry.HasIndirectShadowCasters(
                    new VegetationIndirectShadowContext(
                        frameData.camera,
                        frameData.rendererData),
                    includeStaticCasters,
                    includeDynamicCasters);
        }

        private void UploadDisabledGlobals(ref NWRPFrameData frameData)
        {
            MainLightShadowPassUtils.UploadDisabledGlobals(ref frameData, null);
        }

        private bool TryGetMainLightIndex(ref NWRPFrameData frameData, out int mainLightIndex, out Light mainLight)
        {
            NativeArray<VisibleLight> visibleLights = frameData.cullingResults.visibleLights;
            for (int i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType != LightType.Directional)
                {
                    continue;
                }

                mainLightIndex = i;
                mainLight = visibleLight.light;
                return true;
            }

            mainLightIndex = -1;
            mainLight = null;
            return false;
        }

        private static Vector4 GetShadowLightDirection(ref NWRPFrameData frameData, int mainLightIndex)
        {
            if (mainLightIndex < 0 || mainLightIndex >= frameData.cullingResults.visibleLights.Length)
            {
                return Vector4.zero;
            }

            VisibleLight visibleLight = frameData.cullingResults.visibleLights[mainLightIndex];
            Vector4 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
            lightDirection = lightDirection.normalized;
            lightDirection.w = 0f;
            return lightDirection;
        }

        private bool EnsureShadowmap(int width, int height)
        {
            if (_shadowmapTexture != null && _shadowmapWidth == width && _shadowmapHeight == height)
            {
                return true;
            }

            ReleaseShadowmap();

            _shadowmapTexture = new RenderTexture(width, height, 32, RenderTextureFormat.Shadowmap)
            {
                name = "NWRP_MainLightShadows_Shadowmap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                anisoLevel = 0
            };
            _shadowmapTexture.Create();

            _shadowmapWidth = width;
            _shadowmapHeight = height;
            return _shadowmapTexture.IsCreated();
        }

        private void ReleaseShadowmap()
        {
            if (_shadowmapTexture == null)
            {
                return;
            }

            _shadowmapTexture.Release();
            if (Application.isPlaying)
            {
                Object.Destroy(_shadowmapTexture);
            }
            else
            {
                Object.DestroyImmediate(_shadowmapTexture);
            }

            _shadowmapTexture = null;
            _shadowmapWidth = 0;
            _shadowmapHeight = 0;
        }

        private static void GetAtlasSize(
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

        private static void GetTileOffset(int cascadeIndex, int tileResolution, out int offsetX, out int offsetY)
        {
            offsetX = cascadeIndex * tileResolution;
            offsetY = 0;
        }

        private static Vector4 CalculateShadowBias(
            NewWorldRenderPipelineAsset asset,
            Matrix4x4 projectionMatrix,
            int tileResolution)
        {
            float safeResolution = Mathf.Max(tileResolution, 1);
            float frustumSize = 2.0f / Mathf.Max(Mathf.Abs(projectionMatrix.m00), 0.00001f);
            float texelSize = frustumSize / safeResolution;

            float depthBias = -asset.MainLightShadowBias * texelSize;
            float normalBias = -asset.MainLightShadowNormalBias * texelSize;
            return new Vector4(depthBias, normalBias, 0f, 0f);
        }

        private static Matrix4x4 BuildWorldToShadowMatrix(
            Matrix4x4 projection,
            Matrix4x4 view,
            int tileOffsetX,
            int tileOffsetY,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
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

        private static void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            frameData.context.ExecuteCommandBuffer(frameData.cmd);
            frameData.cmd.Clear();
        }
    }
}
