using NWRP.Runtime.Passes;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Lighting
{
    internal struct NWRPAdditionalShadowLightEntry
    {
        public int visibleLightIndex;
        public int compactIndex;
        public int firstSliceIndex;
        public int sliceCount;
        public LightType lightType;
        public VisibleLight visibleLight;
        public Light light;
        public Vector4 position;
        public Vector4 spotDirection;
        public bool hasValidSlices;
    }

    internal struct NWRPAdditionalShadowSlice
    {
        public int visibleLightIndex;
        public int localSliceIndex;
        public int offsetX;
        public int offsetY;
        public int resolution;
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;
        public ShadowSplitData splitData;
        public bool isValid;
    }

    internal sealed class NWRPShadowCullingContext : System.IDisposable
    {
        private const int kMaxShadowSplitCount = AdditionalLightUtils.PointLightShadowFaceCount;
        private const int kMinimumAdditionalShadowTileResolution = 128;

        private readonly AdditionalLightData[] _additionalLights =
            new AdditionalLightData[AdditionalLightUtils.MaxAdditionalLights];
        private readonly int[] _additionalShadowCandidateIndices =
            new int[AdditionalLightUtils.MaxAdditionalLights];
        private readonly float[] _additionalShadowCandidateDistances =
            new float[AdditionalLightUtils.MaxAdditionalLights];
        private readonly ShadowSplitData[] _punctualShadowSplitData =
            new ShadowSplitData[AdditionalLightUtils.PointLightShadowFaceCount];
        private readonly bool[] _punctualShadowSliceValid =
            new bool[AdditionalLightUtils.PointLightShadowFaceCount];

#if UNITY_EDITOR
        private static readonly Camera[] s_EditorGameCameras = new Camera[32];
#endif

        private NativeArray<ShadowSplitData> _splitBuffer;
        private NativeArray<LightShadowCasterCullingInfo> _perLightInfos;
        private int _totalSplitCount;
        private bool _hasCullingInfos;
        private bool _cullingApplied;

        public readonly MainLightShadowCascadeData[] MainCascadeData =
            new MainLightShadowCascadeData[2];
        public readonly bool[] MainCascadeValid = new bool[2];
        public int MainLightIndex { get; private set; } = -1;
        public int MainCascadeCount { get; private set; }
        public int MainAtlasWidth { get; private set; }
        public int MainAtlasHeight { get; private set; }
        public int MainTileResolution { get; private set; }
        public bool HasMainLightShadows { get; private set; }

        public readonly NWRPAdditionalShadowLightEntry[] AdditionalLights =
            new NWRPAdditionalShadowLightEntry[AdditionalLightUtils.MaxShadowedAdditionalLights];
        public readonly NWRPAdditionalShadowSlice[] AdditionalSlices =
            new NWRPAdditionalShadowSlice[AdditionalLightUtils.MaxAdditionalLightShadowSlices];
        public readonly Matrix4x4[] AdditionalWorldToShadowMatrices =
            CreateWorldToShadowBuffer();
        public readonly Vector4[] AdditionalShadowParams =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        public readonly Vector4[] AdditionalAtlasRects =
            new Vector4[AdditionalLightUtils.MaxAdditionalLightShadowSlices];
        public int AdditionalLightCount { get; private set; }
        public int AdditionalValidLightCount { get; private set; }
        public int AdditionalShadowSliceCount { get; private set; }
        public int AdditionalAtlasWidth { get; private set; }
        public int AdditionalAtlasHeight { get; private set; }
        public int AdditionalTileResolution { get; private set; }
        public int AdditionalTileColumns { get; private set; }
        public bool HasAdditionalShadows => AdditionalValidLightCount > 0;

        public void Prepare(
            ref NWRPFrameData frameData,
            bool includeRealtimeMainLight,
            bool includeAdditionalLights)
        {
            Reset();

            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (asset == null
                || (!includeRealtimeMainLight && !includeAdditionalLights)
                || (!asset.EnableMainLightShadows && !asset.EnableAdditionalLightShadows))
            {
                return;
            }

            int visibleLightCount = frameData.cullingResults.visibleLights.Length;
            if (visibleLightCount <= 0)
            {
                return;
            }

            _splitBuffer = new NativeArray<ShadowSplitData>(
                visibleLightCount * kMaxShadowSplitCount,
                Allocator.Temp);
            _perLightInfos = new NativeArray<LightShadowCasterCullingInfo>(
                visibleLightCount,
                Allocator.Temp);

            if (includeRealtimeMainLight)
            {
                PrepareRealtimeMainLight(ref frameData);
            }

            if (includeAdditionalLights)
            {
                PrepareAdditionalLights(ref frameData);
            }

            _hasCullingInfos = _totalSplitCount > 0;
        }

        public bool HasMainLightShadowFor(int visibleLightIndex)
        {
            return HasMainLightShadows && MainLightIndex == visibleLightIndex;
        }

        public bool Apply(ref NWRPFrameData frameData)
        {
            if (!_hasCullingInfos || _cullingApplied)
            {
                return _hasCullingInfos;
            }

            ShadowCastersCullingInfos shadowCullingInfos = new ShadowCastersCullingInfos
            {
                splitBuffer = _splitBuffer.GetSubArray(0, _totalSplitCount),
                perLightInfos = _perLightInfos
            };
            frameData.context.CullShadowCasters(frameData.cullingResults, shadowCullingInfos);
            _cullingApplied = true;
            return true;
        }

        public void MarkDirty()
        {
            if (_hasCullingInfos)
            {
                _cullingApplied = false;
            }
        }

        public void Dispose()
        {
            ResetNativeArrays();
            ResetMetadata();
        }

        private void Reset()
        {
            ResetNativeArrays();
            ResetMetadata();
        }

        private void ResetNativeArrays()
        {
            if (_splitBuffer.IsCreated)
            {
                _splitBuffer.Dispose();
            }

            if (_perLightInfos.IsCreated)
            {
                _perLightInfos.Dispose();
            }

            _totalSplitCount = 0;
            _hasCullingInfos = false;
            _cullingApplied = false;
        }

        private void ResetMetadata()
        {
            MainLightIndex = -1;
            MainCascadeCount = 0;
            MainAtlasWidth = 0;
            MainAtlasHeight = 0;
            MainTileResolution = 0;
            HasMainLightShadows = false;
            for (int i = 0; i < MainCascadeData.Length; i++)
            {
                MainCascadeData[i] = default;
                MainCascadeValid[i] = false;
            }

            AdditionalLightCount = 0;
            AdditionalValidLightCount = 0;
            AdditionalShadowSliceCount = 0;
            AdditionalAtlasWidth = 0;
            AdditionalAtlasHeight = 0;
            AdditionalTileResolution = 0;
            AdditionalTileColumns = 0;
            for (int i = 0; i < AdditionalLights.Length; i++)
            {
                AdditionalLights[i] = default;
            }

            for (int i = 0; i < AdditionalSlices.Length; i++)
            {
                AdditionalSlices[i] = default;
                AdditionalWorldToShadowMatrices[i] = Matrix4x4.identity;
                AdditionalAtlasRects[i] = Vector4.zero;
            }

            for (int i = 0; i < AdditionalShadowParams.Length; i++)
            {
                AdditionalShadowParams[i] = Vector4.zero;
            }
        }

        private void PrepareRealtimeMainLight(ref NWRPFrameData frameData)
        {
            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (asset == null || !asset.EnableMainLightShadows)
            {
                return;
            }

            if (!TryFindMainLight(
                    ref frameData,
                    out int mainLightIndex,
                    out VisibleLight visibleLight,
                    out Light mainLight))
            {
                return;
            }

            if (mainLight == null
                || mainLight.shadows == LightShadows.None
                || mainLight.shadowStrength <= 0f
                || !frameData.cullingResults.GetShadowCasterBounds(mainLightIndex, out _))
            {
                return;
            }

            int cascadeCount = Mathf.Clamp(asset.MainLightShadowCascadeCount, 1, 2);
            int requestedResolution = Mathf.ClosestPowerOfTwo(
                Mathf.Clamp(asset.MainLightShadowResolution, 256, 4096));
            MainLightShadowPassUtils.GetAtlasSize(
                requestedResolution,
                cascadeCount,
                out int atlasWidth,
                out int atlasHeight,
                out int tileResolution);

            Vector3 cascadeRatios = cascadeCount == 2
                ? new Vector3(asset.MainLightShadowCascadeSplit, 1f, 1f)
                : Vector3.zero;

            int splitOffset = _totalSplitCount;
            bool anyCascadeValid = false;
            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
            {
                if (!MainLightShadowPassUtils.TryComputeDirectionalShadowCascade(
                        ref frameData,
                        mainLightIndex,
                        cascadeIndex,
                        cascadeCount,
                        cascadeRatios,
                        tileResolution,
                        mainLight.shadowNearPlane,
                        mainLight,
                        allowCameraFrustumFallback: false,
                        out Matrix4x4 viewMatrix,
                        out Matrix4x4 projectionMatrix,
                        out ShadowSplitData splitData))
                {
                    MainCascadeData[cascadeIndex] = default;
                    MainCascadeValid[cascadeIndex] = false;
                    _splitBuffer[splitOffset + cascadeIndex] = default;
                    continue;
                }

                splitData.shadowCascadeBlendCullingFactor = 1.0f;
                MainLightShadowPassUtils.GetTileOffset(
                    cascadeIndex,
                    tileResolution,
                    out int offsetX,
                    out int offsetY);
                MainCascadeData[cascadeIndex] = new MainLightShadowCascadeData
                {
                    viewMatrix = viewMatrix,
                    projectionMatrix = projectionMatrix,
                    splitData = splitData,
                    cullingSphere = splitData.cullingSphere,
                    offsetX = offsetX,
                    offsetY = offsetY,
                    resolution = tileResolution,
                    worldToShadowMatrix = MainLightShadowPassUtils.BuildWorldToShadowMatrix(
                        projectionMatrix,
                        viewMatrix,
                        offsetX,
                        offsetY,
                        tileResolution,
                        atlasWidth,
                        atlasHeight)
                };
                MainCascadeValid[cascadeIndex] = true;
                _splitBuffer[splitOffset + cascadeIndex] = splitData;
                anyCascadeValid = true;
            }

            if (!anyCascadeValid)
            {
                return;
            }

            _perLightInfos[mainLightIndex] = new LightShadowCasterCullingInfo
            {
                splitRange = new RangeInt(splitOffset, cascadeCount),
                projectionType = BatchCullingProjectionType.Orthographic
            };
            _totalSplitCount += cascadeCount;
            MainLightIndex = mainLightIndex;
            MainCascadeCount = cascadeCount;
            MainAtlasWidth = atlasWidth;
            MainAtlasHeight = atlasHeight;
            MainTileResolution = tileResolution;
            HasMainLightShadows = visibleLight.lightType == LightType.Directional;
        }

        private void PrepareAdditionalLights(ref NWRPFrameData frameData)
        {
            NewWorldRenderPipelineAsset asset = frameData.asset;
            if (asset == null
                || !asset.EnableAdditionalLightShadows
                || asset.MaxShadowedAdditionalLights <= 0)
            {
                return;
            }

            int additionalCount = AdditionalLightUtils.CollectAdditionalLights(
                ref frameData,
                _additionalLights,
                out _);
            int candidateCount = CollectAdditionalShadowCandidates(ref frameData, additionalCount);
            if (candidateCount <= 0)
            {
                return;
            }

            int selectedLightBudget = Mathf.Min(
                candidateCount,
                Mathf.Clamp(
                    asset.MaxShadowedAdditionalLights,
                    0,
                    AdditionalLightUtils.MaxShadowedAdditionalLights));
            if (!TryGetAdditionalShadowAtlasLayout(
                    selectedLightBudget,
                    Mathf.ClosestPowerOfTwo(
                        Mathf.Clamp(asset.AdditionalLightShadowResolution, 128, 1024)),
                    Mathf.ClosestPowerOfTwo(
                        Mathf.Clamp(asset.AdditionalLightShadowAtlasMaxSize, 512, 2048)),
                    out int selectedLightCount,
                    out int tileResolution,
                    out int atlasWidth,
                    out int atlasHeight,
                    out int tileColumns))
            {
                return;
            }

            AdditionalTileResolution = tileResolution;
            AdditionalAtlasWidth = atlasWidth;
            AdditionalAtlasHeight = atlasHeight;
            AdditionalTileColumns = tileColumns;

            int firstSliceIndex = 0;
            for (int candidateIndex = 0; candidateIndex < selectedLightCount; candidateIndex++)
            {
                AdditionalLightData lightData =
                    _additionalLights[_additionalShadowCandidateIndices[candidateIndex]];
                int sliceCount = AdditionalLightUtils.GetShadowSliceCount(
                    lightData.visibleLight.lightType);
                if (sliceCount <= 0
                    || firstSliceIndex + sliceCount > AdditionalSlices.Length
                    || AdditionalLightCount >= AdditionalLights.Length)
                {
                    break;
                }

                NWRPAdditionalShadowLightEntry entry = new NWRPAdditionalShadowLightEntry
                {
                    visibleLightIndex = lightData.visibleLightIndex,
                    compactIndex = lightData.compactIndex,
                    firstSliceIndex = firstSliceIndex,
                    sliceCount = sliceCount,
                    lightType = lightData.visibleLight.lightType,
                    visibleLight = lightData.visibleLight,
                    light = lightData.light,
                    position = lightData.position,
                    spotDirection = lightData.spotDirection,
                    hasValidSlices = PrepareAdditionalLightSlices(
                        ref frameData,
                        lightData,
                        firstSliceIndex,
                        sliceCount,
                        tileColumns,
                        tileResolution,
                        atlasWidth,
                        atlasHeight)
                };

                if (entry.hasValidSlices)
                {
                    AdditionalShadowParams[entry.compactIndex] = new Vector4(
                        1f,
                        entry.light != null ? entry.light.shadowStrength : 1f,
                        entry.lightType == LightType.Point
                            ? AdditionalLightUtils.PointLightShadowTypeId
                            : AdditionalLightUtils.SpotLightShadowTypeId,
                        entry.firstSliceIndex);
                    AdditionalValidLightCount++;
                }

                AdditionalLights[AdditionalLightCount++] = entry;
                firstSliceIndex += sliceCount;
            }

            AdditionalShadowSliceCount = firstSliceIndex;
        }

        private bool PrepareAdditionalLightSlices(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int firstSliceIndex,
            int sliceCount,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            for (int i = 0; i < _punctualShadowSplitData.Length; i++)
            {
                _punctualShadowSplitData[i] = default;
                _punctualShadowSliceValid[i] = false;
            }

            bool hasValidSlices = lightData.visibleLight.lightType switch
            {
                LightType.Spot => PrepareSpotLightSlices(
                    ref frameData,
                    lightData,
                    firstSliceIndex,
                    tileColumns,
                    tileResolution,
                    atlasWidth,
                    atlasHeight),
                LightType.Point => PreparePointLightSlices(
                    ref frameData,
                    lightData,
                    firstSliceIndex,
                    tileColumns,
                    tileResolution,
                    atlasWidth,
                    atlasHeight),
                _ => false
            };

            if (!hasValidSlices)
            {
                return false;
            }

            int splitOffset = _totalSplitCount;
            for (int sliceOffset = 0; sliceOffset < sliceCount; sliceOffset++)
            {
                _splitBuffer[splitOffset + sliceOffset] = _punctualShadowSplitData[sliceOffset];
            }

            _perLightInfos[lightData.visibleLightIndex] = new LightShadowCasterCullingInfo
            {
                splitRange = new RangeInt(splitOffset, sliceCount),
                projectionType = BatchCullingProjectionType.Perspective
            };
            _totalSplitCount += sliceCount;
            return true;
        }

        private bool PrepareSpotLightSlices(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int firstSliceIndex,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            if (!frameData.cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
                    lightData.visibleLightIndex,
                    out Matrix4x4 viewMatrix,
                    out Matrix4x4 projectionMatrix,
                    out ShadowSplitData splitData))
            {
                return false;
            }

            splitData.shadowCascadeBlendCullingFactor = 1.0f;
            _punctualShadowSplitData[0] = splitData;
            _punctualShadowSliceValid[0] = true;
            RecordAdditionalSlice(
                firstSliceIndex,
                localSliceIndex: 0,
                lightData.visibleLightIndex,
                projectionMatrix,
                viewMatrix,
                splitData,
                tileColumns,
                tileResolution,
                atlasWidth,
                atlasHeight);
            return true;
        }

        private bool PreparePointLightSlices(
            ref NWRPFrameData frameData,
            AdditionalLightData lightData,
            int firstSliceIndex,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            float fovBias = AdditionalLightShadowPassUtils
                .GetPointLightShadowFrustumFovBiasInDegrees(tileResolution);
            bool anyFaceValid = false;
            for (int faceIndex = 0;
                 faceIndex < AdditionalLightUtils.PointLightShadowFaceCount;
                 faceIndex++)
            {
                if (!frameData.cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                        lightData.visibleLightIndex,
                        (CubemapFace)faceIndex,
                        fovBias,
                        out Matrix4x4 viewMatrix,
                        out Matrix4x4 projectionMatrix,
                        out ShadowSplitData splitData))
                {
                    continue;
                }

                AdditionalLightShadowPassUtils.FixupPointShadowViewMatrix(ref viewMatrix);
                splitData.shadowCascadeBlendCullingFactor = 1.0f;
                _punctualShadowSplitData[faceIndex] = splitData;
                _punctualShadowSliceValid[faceIndex] = true;
                RecordAdditionalSlice(
                    firstSliceIndex + faceIndex,
                    faceIndex,
                    lightData.visibleLightIndex,
                    projectionMatrix,
                    viewMatrix,
                    splitData,
                    tileColumns,
                    tileResolution,
                    atlasWidth,
                    atlasHeight);
                anyFaceValid = true;
            }

            return anyFaceValid;
        }

        private void RecordAdditionalSlice(
            int globalSliceIndex,
            int localSliceIndex,
            int visibleLightIndex,
            Matrix4x4 projectionMatrix,
            Matrix4x4 viewMatrix,
            ShadowSplitData splitData,
            int tileColumns,
            int tileResolution,
            int atlasWidth,
            int atlasHeight)
        {
            GetAdditionalTileOffset(
                globalSliceIndex,
                tileColumns,
                tileResolution,
                out int offsetX,
                out int offsetY);
            AdditionalSlices[globalSliceIndex] = new NWRPAdditionalShadowSlice
            {
                visibleLightIndex = visibleLightIndex,
                localSliceIndex = localSliceIndex,
                offsetX = offsetX,
                offsetY = offsetY,
                resolution = tileResolution,
                viewMatrix = viewMatrix,
                projectionMatrix = projectionMatrix,
                splitData = splitData,
                isValid = true
            };
            AdditionalWorldToShadowMatrices[globalSliceIndex] =
                MainLightShadowPassUtils.BuildWorldToShadowMatrix(
                    projectionMatrix,
                    viewMatrix,
                    offsetX,
                    offsetY,
                    tileResolution,
                    atlasWidth,
                    atlasHeight);
            AdditionalAtlasRects[globalSliceIndex] = new Vector4(
                (float)offsetX / atlasWidth,
                (float)offsetY / atlasHeight,
                (float)(offsetX + tileResolution) / atlasWidth,
                (float)(offsetY + tileResolution) / atlasHeight);
        }

        private static bool TryFindMainLight(
            ref NWRPFrameData frameData,
            out int mainLightIndex,
            out VisibleLight visibleLight,
            out Light mainLight)
        {
            NativeArray<VisibleLight> visibleLights = frameData.cullingResults.visibleLights;
            for (int i = 0; i < visibleLights.Length; i++)
            {
                if (visibleLights[i].lightType != LightType.Directional)
                {
                    continue;
                }

                mainLightIndex = i;
                visibleLight = visibleLights[i];
                mainLight = visibleLight.light;
                return true;
            }

            mainLightIndex = -1;
            visibleLight = default;
            mainLight = null;
            return false;
        }

        private int CollectAdditionalShadowCandidates(
            ref NWRPFrameData frameData,
            int additionalCount)
        {
            ResolveShadowCandidateReference(
                frameData.camera,
                out Vector3 cameraPosition,
                out bool useDistanceGate,
                out bool useDistanceSort);
            float maxReceiverDistance = Mathf.Max(frameData.asset.AdditionalLightShadowDistance, 0f);
            int candidateCount = 0;

            for (int i = 0; i < additionalCount; i++)
            {
                AdditionalLightData lightData = _additionalLights[i];
                int sliceCount = AdditionalLightUtils.GetShadowSliceCount(
                    lightData.visibleLight.lightType);
                if (sliceCount <= 0
                    || lightData.light == null
                    || lightData.light.shadows == LightShadows.None
                    || lightData.light.shadowStrength <= 0f
                    || !frameData.cullingResults.GetShadowCasterBounds(
                        lightData.visibleLightIndex,
                        out _))
                {
                    continue;
                }

                float range = Mathf.Max(lightData.visibleLight.range, 0f);
                float maxLightDistance = maxReceiverDistance + range;
                Vector3 lightPosition = new Vector3(
                    lightData.position.x,
                    lightData.position.y,
                    lightData.position.z);
                float cameraDistanceSqr = useDistanceSort
                    ? (lightPosition - cameraPosition).sqrMagnitude
                    : 0f;
                if (useDistanceGate
                    && maxLightDistance > 0f
                    && cameraDistanceSqr > maxLightDistance * maxLightDistance)
                {
                    continue;
                }

                _additionalShadowCandidateIndices[candidateCount] = i;
                _additionalShadowCandidateDistances[i] = cameraDistanceSqr;
                candidateCount++;
            }

            SortAdditionalShadowCandidates(candidateCount);
            return candidateCount;
        }

        private void SortAdditionalShadowCandidates(int candidateCount)
        {
            for (int i = 1; i < candidateCount; i++)
            {
                int candidateLightIndex = _additionalShadowCandidateIndices[i];
                int insertionIndex = i - 1;
                while (insertionIndex >= 0
                    && CompareAdditionalShadowCandidates(
                        candidateLightIndex,
                        _additionalShadowCandidateIndices[insertionIndex]) < 0)
                {
                    _additionalShadowCandidateIndices[insertionIndex + 1] =
                        _additionalShadowCandidateIndices[insertionIndex];
                    insertionIndex--;
                }

                _additionalShadowCandidateIndices[insertionIndex + 1] = candidateLightIndex;
            }
        }

        private int CompareAdditionalShadowCandidates(int lhsLightIndex, int rhsLightIndex)
        {
            float lhsDistance = _additionalShadowCandidateDistances[lhsLightIndex];
            float rhsDistance = _additionalShadowCandidateDistances[rhsLightIndex];
            if (!Mathf.Approximately(lhsDistance, rhsDistance))
            {
                return lhsDistance < rhsDistance ? -1 : 1;
            }

            bool lhsIsSpot = _additionalLights[lhsLightIndex].visibleLight.lightType
                == LightType.Spot;
            bool rhsIsSpot = _additionalLights[rhsLightIndex].visibleLight.lightType
                == LightType.Spot;
            if (lhsIsSpot != rhsIsSpot)
            {
                return lhsIsSpot ? -1 : 1;
            }

            return _additionalLights[lhsLightIndex].visibleLightIndex.CompareTo(
                _additionalLights[rhsLightIndex].visibleLightIndex);
        }

        private static void ResolveShadowCandidateReference(
            Camera renderingCamera,
            out Vector3 referencePosition,
            out bool useDistanceGate,
            out bool useDistanceSort)
        {
            if (renderingCamera == null)
            {
                referencePosition = Vector3.zero;
                useDistanceGate = false;
                useDistanceSort = false;
                return;
            }

#if UNITY_EDITOR
            if (renderingCamera.cameraType == CameraType.SceneView)
            {
                Camera gameCamera = TryGetActiveGameCamera();
                if (gameCamera != null)
                {
                    referencePosition = gameCamera.transform.position;
                    useDistanceGate = true;
                    useDistanceSort = true;
                    return;
                }

                referencePosition = Vector3.zero;
                useDistanceGate = false;
                useDistanceSort = false;
                return;
            }
#endif

            referencePosition = renderingCamera.transform.position;
            useDistanceGate = true;
            useDistanceSort = true;
        }

#if UNITY_EDITOR
        private static Camera TryGetActiveGameCamera()
        {
            Camera mainCamera = Camera.main;
            if (IsUsableGameCamera(mainCamera))
            {
                return mainCamera;
            }

            int cameraCount = Mathf.Min(
                Camera.GetAllCameras(s_EditorGameCameras),
                s_EditorGameCameras.Length);
            for (int i = 0; i < cameraCount; i++)
            {
                Camera camera = s_EditorGameCameras[i];
                if (IsUsableGameCamera(camera))
                {
                    return camera;
                }
            }

            return null;
        }

        private static bool IsUsableGameCamera(Camera camera)
        {
            return camera != null
                && camera.cameraType == CameraType.Game
                && camera.isActiveAndEnabled;
        }
#endif

        private bool TryGetAdditionalShadowAtlasLayout(
            int selectedLightBudget,
            int requestedTileResolution,
            int atlasMaxSize,
            out int selectedLightCount,
            out int tileResolution,
            out int atlasWidth,
            out int atlasHeight,
            out int tileColumns)
        {
            selectedLightCount = selectedLightBudget;
            while (selectedLightCount > 0)
            {
                int totalShadowSlices = GetSelectedAdditionalShadowSliceCount(selectedLightCount);
                GetAdditionalShadowAtlasLayout(
                    totalShadowSlices,
                    requestedTileResolution,
                    atlasMaxSize,
                    out tileResolution,
                    out atlasWidth,
                    out atlasHeight,
                    out tileColumns);
                if (tileResolution >= kMinimumAdditionalShadowTileResolution)
                {
                    return true;
                }

                selectedLightCount--;
            }

            tileResolution = 0;
            atlasWidth = 0;
            atlasHeight = 0;
            tileColumns = 0;
            return false;
        }

        private int GetSelectedAdditionalShadowSliceCount(int selectedLightCount)
        {
            int totalShadowSlices = 0;
            for (int i = 0; i < selectedLightCount; i++)
            {
                AdditionalLightData lightData =
                    _additionalLights[_additionalShadowCandidateIndices[i]];
                totalShadowSlices += AdditionalLightUtils.GetShadowSliceCount(
                    lightData.visibleLight.lightType);
            }

            return totalShadowSlices;
        }

        private static void GetAdditionalShadowAtlasLayout(
            int totalShadowSlices,
            int requestedTileResolution,
            int atlasMaxSize,
            out int tileResolution,
            out int atlasWidth,
            out int atlasHeight,
            out int tileColumns)
        {
            totalShadowSlices = Mathf.Max(totalShadowSlices, 1);
            tileColumns = Mathf.CeilToInt(Mathf.Sqrt(totalShadowSlices));
            int tileRows = Mathf.CeilToInt((float)totalShadowSlices / tileColumns);
            tileResolution = Mathf.Min(
                requestedTileResolution,
                Mathf.FloorToInt(Mathf.Min(
                    (float)atlasMaxSize / tileColumns,
                    (float)atlasMaxSize / tileRows)));
            tileResolution = Mathf.ClosestPowerOfTwo(Mathf.Max(tileResolution, 1));
            atlasWidth = tileColumns * tileResolution;
            atlasHeight = tileRows * tileResolution;
        }

        private static void GetAdditionalTileOffset(
            int shadowSliceIndex,
            int tileColumns,
            int tileResolution,
            out int offsetX,
            out int offsetY)
        {
            offsetX = shadowSliceIndex % tileColumns * tileResolution;
            offsetY = shadowSliceIndex / tileColumns * tileResolution;
        }

        private static Matrix4x4[] CreateWorldToShadowBuffer()
        {
            Matrix4x4[] matrices =
                new Matrix4x4[AdditionalLightUtils.MaxAdditionalLightShadowSlices];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.identity;
            }

            return matrices;
        }

        private static int[] BuildAdditionalShadowFirstSliceIndicesForTest(
            LightType[] lightTypes,
            int requestedTileResolution,
            int atlasMaxSize,
            int maxShadowedLights)
        {
            if (lightTypes == null || lightTypes.Length == 0 || maxShadowedLights <= 0)
            {
                return System.Array.Empty<int>();
            }

            int selectedLightCount = Mathf.Min(
                lightTypes.Length,
                Mathf.Clamp(
                    maxShadowedLights,
                    0,
                    AdditionalLightUtils.MaxShadowedAdditionalLights));
            while (selectedLightCount > 0)
            {
                int totalShadowSlices = 0;
                for (int i = 0; i < selectedLightCount; i++)
                {
                    totalShadowSlices += AdditionalLightUtils.GetShadowSliceCount(lightTypes[i]);
                }

                GetAdditionalShadowAtlasLayout(
                    totalShadowSlices,
                    requestedTileResolution,
                    atlasMaxSize,
                    out int tileResolution,
                    out _,
                    out _,
                    out _);
                if (tileResolution >= kMinimumAdditionalShadowTileResolution)
                {
                    break;
                }

                selectedLightCount--;
            }

            int[] firstSliceIndices = new int[selectedLightCount];
            int firstSliceIndex = 0;
            for (int i = 0; i < selectedLightCount; i++)
            {
                firstSliceIndices[i] = firstSliceIndex;
                firstSliceIndex += AdditionalLightUtils.GetShadowSliceCount(lightTypes[i]);
            }

            return firstSliceIndices;
        }
    }
}
