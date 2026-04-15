using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class MainLightShadowCasterPass : NWRPPass
    {
        private const string kMainLightShadowsSampleName = "NWRP Main Light Shadows";
        private const float kRasterDepthBias = 1.0f;
        private const float kRasterSlopeBias = 2.5f;

        private readonly Matrix4x4[] _mainLightWorldToShadow = new Matrix4x4[2];
        private readonly Vector4[] _cascadeSplitSpheres = new Vector4[2];

        private RenderTexture _shadowmapTexture;
        private int _shadowmapWidth;
        private int _shadowmapHeight;

        public MainLightShadowCasterPass()
            : base(NWRPPassEvent.ShadowMap)
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

            int cascadeCount = Mathf.Clamp(asset.MainLightShadowCascadeCount, 1, 2);
            int requestedResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(asset.MainLightShadowResolution, 256, 4096));
            GetAtlasSize(requestedResolution, cascadeCount, out int atlasWidth, out int atlasHeight, out int tileResolution);

            if (!EnsureShadowmap(atlasWidth, atlasHeight))
            {
                UploadDisabledGlobals(ref frameData);
                return;
            }

            CommandBuffer cmd = frameData.cmd;
            cmd.BeginSample(kMainLightShadowsSampleName);
            cmd.SetRenderTarget(_shadowmapTexture);
            cmd.ClearRenderTarget(true, false, Color.black);
            ExecuteBuffer(ref frameData);

            ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(frameData.cullingResults, mainLightIndex);
            Vector4 shadowLightDirection = GetShadowLightDirection(ref frameData, mainLightIndex);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, shadowLightDirection);
            cmd.SetGlobalDepthBias(kRasterDepthBias, kRasterSlopeBias);
            ExecuteBuffer(ref frameData);

            Vector3 cascadeRatios = cascadeCount == 2
                ? new Vector3(asset.MainLightShadowCascadeSplit, 1f, 1f)
                : Vector3.zero;

            bool anyCascadeRendered = false;
            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
            {
                if (!frameData.cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                        mainLightIndex,
                        cascadeIndex,
                        cascadeCount,
                        cascadeRatios,
                        tileResolution,
                        mainLight.shadowNearPlane,
                        out Matrix4x4 viewMatrix,
                        out Matrix4x4 projMatrix,
                        out ShadowSplitData splitData))
                {
                    _mainLightWorldToShadow[cascadeIndex] = Matrix4x4.identity;
                    _cascadeSplitSpheres[cascadeIndex] = Vector4.zero;
                    continue;
                }

                splitData.shadowCascadeBlendCullingFactor = 1.0f;
                shadowDrawingSettings.splitData = splitData;
                _cascadeSplitSpheres[cascadeIndex] = splitData.cullingSphere;

                GetTileOffset(cascadeIndex, tileResolution, out int offsetX, out int offsetY);
                Rect viewport = new Rect(offsetX, offsetY, tileResolution, tileResolution);

                cmd.SetViewport(viewport);
                cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);
                cmd.SetGlobalVector(
                    NWRPShaderIds.ShadowBias,
                    CalculateShadowBias(asset, projMatrix, tileResolution)
                );
                ExecuteBuffer(ref frameData);

                frameData.context.DrawShadows(ref shadowDrawingSettings);
                anyCascadeRendered = true;

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

            if (!anyCascadeRendered)
            {
                cmd.SetGlobalDepthBias(0.0f, 0.0f);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
                cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
                cmd.EndSample(kMainLightShadowsSampleName);
                ExecuteBuffer(ref frameData);
                UploadDisabledGlobals(ref frameData);
                frameData.context.SetupCameraProperties(frameData.camera);
                return;
            }

            // Reset bias and camera matrices for camera-space rendering passes.
            cmd.SetGlobalDepthBias(0.0f, 0.0f);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
            cmd.EndSample(kMainLightShadowsSampleName);
            ExecuteBuffer(ref frameData);
            frameData.context.SetupCameraProperties(frameData.camera);

            UploadReceiverGlobals(
                ref frameData,
                mainLight.shadowStrength,
                cascadeCount,
                atlasWidth,
                atlasHeight
            );
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

        private static void GetAtlasSize(int resolution, int cascadeCount, out int atlasWidth, out int atlasHeight, out int tileResolution)
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

        private void UploadDisabledGlobals(ref NWRPFrameData frameData)
        {
            CommandBuffer cmd = frameData.cmd;

            _mainLightWorldToShadow[0] = Matrix4x4.identity;
            _mainLightWorldToShadow[1] = Matrix4x4.identity;
            _cascadeSplitSpheres[0] = Vector4.zero;
            _cascadeSplitSpheres[1] = Vector4.zero;

            cmd.SetGlobalTexture(NWRPShaderIds.MainLightShadowmapTexture, Texture2D.blackTexture);
            cmd.SetGlobalTexture(NWRPShaderIds.MainLightDynamicShadowmapTexture, Texture2D.blackTexture);
            cmd.SetGlobalMatrixArray(NWRPShaderIds.MainLightWorldToShadow, _mainLightWorldToShadow);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowParams, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightDynamicShadowParams, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowmapSize, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
            cmd.SetGlobalDepthBias(0.0f, 0.0f);
            cmd.SetGlobalInt(NWRPShaderIds.MainLightShadowCascadeCount, 0);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres0, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres1, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSphereRadii, Vector4.zero);

            ExecuteBuffer(ref frameData);
        }

        private void UploadReceiverGlobals(
            ref NWRPFrameData frameData,
            float shadowStrength,
            int cascadeCount,
            int atlasWidth,
            int atlasHeight)
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

            Vector4 sphere0 = _cascadeSplitSpheres[0];
            Vector4 sphere1 = cascadeCount > 1 ? _cascadeSplitSpheres[1] : _cascadeSplitSpheres[0];

            Vector4 sphereRadii = new Vector4(
                sphere0.w * sphere0.w,
                sphere1.w * sphere1.w,
                0f,
                0f
            );

            cmd.SetGlobalTexture(NWRPShaderIds.MainLightShadowmapTexture, _shadowmapTexture);
            cmd.SetGlobalTexture(NWRPShaderIds.MainLightDynamicShadowmapTexture, Texture2D.blackTexture);
            cmd.SetGlobalMatrixArray(NWRPShaderIds.MainLightWorldToShadow, _mainLightWorldToShadow);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowParams, shadowParams);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightDynamicShadowParams, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.MainLightShadowmapSize, shadowmapSize);
            cmd.SetGlobalInt(NWRPShaderIds.MainLightShadowCascadeCount, cascadeCount);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres0, sphere0);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSpheres1, sphere1);
            cmd.SetGlobalVector(NWRPShaderIds.CascadeShadowSplitSphereRadii, sphereRadii);

            ExecuteBuffer(ref frameData);
        }

        private static void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            frameData.context.ExecuteCommandBuffer(frameData.cmd);
            frameData.cmd.Clear();
        }
    }
}
