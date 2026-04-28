using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    internal struct MainLightShadowCascadeData
    {
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;
        public Matrix4x4 worldToShadowMatrix;
        public ShadowSplitData splitData;
        public Vector4 cullingSphere;
        public int offsetX;
        public int offsetY;
        public int resolution;
    }

    internal sealed class MainLightShadowCacheState
    {
        private const int kMaxCascades = 2;

        private readonly MainLightShadowCascadeData[] _cascadeData = new MainLightShadowCascadeData[kMaxCascades];
        private readonly Matrix4x4[] _worldToShadow = new Matrix4x4[kMaxCascades];
        private readonly Vector4[] _cascadeSplitSpheres = new Vector4[kMaxCascades];

        private int _cachedMainLightInstanceId;
        private Vector3 _cachedMainLightDirection = Vector3.forward;
        private Vector3 _cachedCameraPosition;
        private Quaternion _cachedCameraRotation = Quaternion.identity;
        private float _cachedShadowDistance;
        private int _cachedResolution;
        private int _cachedCascadeCount;
        private float _cachedCascadeSplit;
        private float _cachedShadowBias;
        private float _cachedShadowNormalBias;
        private int _cachedShadowCasterCullMode = (int)CullMode.Back;
        private int _cachedAtlasWidth;
        private int _cachedAtlasHeight;
        private int _cachedTileResolution;
        private int _cachedStaticCasterLayerMask = ~0;
        private int _cachedDynamicCasterLayerMask;
        private bool _cachedDynamicOverlayEnabled;
        private int _lastCacheCameraInstanceId;
        private CameraType _lastCacheCameraType = CameraType.Game;

        private RenderTexture _staticShadowmapTexture;
        private RenderTexture _combinedShadowmapTexture;
        private RenderTexture _emptyShadowmapTexture;

        public RenderTexture StaticShadowmapTexture => _staticShadowmapTexture;
        public RenderTexture CombinedShadowmapTexture => _combinedShadowmapTexture;
        public RenderTexture EmptyShadowmapTexture => _emptyShadowmapTexture;

        public int AtlasWidth { get; private set; }
        public int AtlasHeight { get; private set; }
        public int TileResolution { get; private set; }
        public int CascadeCount { get; private set; }
        public bool HasValidCache { get; private set; }
        public bool IsDirty { get; private set; } = true;
        public int LastCacheCameraInstanceId => _lastCacheCameraInstanceId;
        public CameraType LastCacheCameraType => _lastCacheCameraType;

        public MainLightShadowCascadeData[] CascadeData => _cascadeData;
        public Matrix4x4[] WorldToShadowMatrices => _worldToShadow;
        public Vector4[] CascadeSplitSpheres => _cascadeSplitSpheres;

        public void Dispose()
        {
            ReleaseTexture(ref _staticShadowmapTexture);
            ReleaseTexture(ref _combinedShadowmapTexture);
            ReleaseTexture(ref _emptyShadowmapTexture);
            ResetCachedData();
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void Invalidate()
        {
            HasValidCache = false;
            IsDirty = true;
            ResetCachedData();
        }

        public void Clear()
        {
            Dispose();
            IsDirty = true;
        }

        public bool EnsureStaticShadowmap(int width, int height)
        {
            return EnsureShadowmap(ref _staticShadowmapTexture, width, height, "NWRP_MainLightShadows_StaticShadowmap");
        }

        public bool EnsureCombinedShadowmap(int width, int height)
        {
            return EnsureShadowmap(ref _combinedShadowmapTexture, width, height, "NWRP_MainLightShadows_CombinedShadowmap");
        }

        public void ReleaseCombinedShadowmap()
        {
            ReleaseTexture(ref _combinedShadowmapTexture);
        }

        public bool EnsureEmptyShadowmap()
        {
            return EnsureShadowmap(ref _emptyShadowmapTexture, 1, 1, "NWRP_MainLightShadows_EmptyShadowmap");
        }

        public bool NeedsStaticCacheRebuild(
            NewWorldRenderPipelineAsset asset,
            Camera camera,
            Light mainLight,
            int atlasWidth,
            int atlasHeight,
            int tileResolution,
            int cascadeCount,
            float effectiveShadowDistance,
            int staticCasterLayerMask,
            int dynamicCasterLayerMask,
            bool dynamicOverlayEnabled
        )
        {
            if (IsDirty || !HasValidCache)
            {
                return true;
            }

            if (mainLight == null || camera == null)
            {
                return true;
            }

            if (_cachedMainLightInstanceId != mainLight.GetInstanceID())
            {
                return true;
            }

            Vector3 lightDirection = -mainLight.transform.forward;
            if (Vector3.Angle(_cachedMainLightDirection, lightDirection) > asset.LightDirectionInvalidationThreshold)
            {
                return true;
            }

            if (asset.EnableMainLightShadowCameraMotionInvalidation
                && HasCameraMotionExceededThreshold(asset, camera))
            {
                return true;
            }

            if (!Mathf.Approximately(_cachedShadowDistance, effectiveShadowDistance)
                || _cachedResolution != asset.MainLightShadowResolution
                || _cachedCascadeCount != cascadeCount
                || !Mathf.Approximately(_cachedCascadeSplit, asset.MainLightShadowCascadeSplit)
                || !Mathf.Approximately(_cachedShadowBias, asset.MainLightShadowBias)
                || !Mathf.Approximately(_cachedShadowNormalBias, asset.MainLightShadowNormalBias)
                || _cachedShadowCasterCullMode != (int)asset.MainLightShadowCasterCullModeSetting)
            {
                return true;
            }

            if (_cachedAtlasWidth != atlasWidth
                || _cachedAtlasHeight != atlasHeight
                || _cachedTileResolution != tileResolution)
            {
                return true;
            }

            if (_cachedStaticCasterLayerMask != staticCasterLayerMask
                || _cachedDynamicCasterLayerMask != dynamicCasterLayerMask
                || _cachedDynamicOverlayEnabled != dynamicOverlayEnabled)
            {
                return true;
            }

            return false;
        }

        private bool HasCameraMotionExceededThreshold(NewWorldRenderPipelineAsset asset, Camera camera)
        {
            if (Vector3.Distance(_cachedCameraPosition, camera.transform.position)
                > asset.CameraPositionInvalidationThreshold)
            {
                return true;
            }

            return Quaternion.Angle(_cachedCameraRotation, camera.transform.rotation)
                > asset.CameraRotationInvalidationThreshold;
        }

        public void CommitStaticCache(
            NewWorldRenderPipelineAsset asset,
            Camera camera,
            Light mainLight,
            int atlasWidth,
            int atlasHeight,
            int tileResolution,
            int cascadeCount,
            float effectiveShadowDistance,
            int staticCasterLayerMask,
            int dynamicCasterLayerMask,
            bool dynamicOverlayEnabled
        )
        {
            _cachedMainLightInstanceId = mainLight != null ? mainLight.GetInstanceID() : 0;
            _cachedMainLightDirection = mainLight != null ? -mainLight.transform.forward : Vector3.forward;
            _cachedCameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            _cachedCameraRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            _cachedShadowDistance = effectiveShadowDistance;
            _cachedResolution = asset.MainLightShadowResolution;
            _cachedCascadeCount = cascadeCount;
            _cachedCascadeSplit = asset.MainLightShadowCascadeSplit;
            _cachedShadowBias = asset.MainLightShadowBias;
            _cachedShadowNormalBias = asset.MainLightShadowNormalBias;
            _cachedShadowCasterCullMode = (int)asset.MainLightShadowCasterCullModeSetting;
            _cachedAtlasWidth = atlasWidth;
            _cachedAtlasHeight = atlasHeight;
            _cachedTileResolution = tileResolution;
            _cachedStaticCasterLayerMask = staticCasterLayerMask;
            _cachedDynamicCasterLayerMask = dynamicCasterLayerMask;
            _cachedDynamicOverlayEnabled = dynamicOverlayEnabled;
            _lastCacheCameraInstanceId = camera != null ? camera.GetInstanceID() : 0;
            _lastCacheCameraType = camera != null ? camera.cameraType : CameraType.Game;
            AtlasWidth = atlasWidth;
            AtlasHeight = atlasHeight;
            TileResolution = tileResolution;
            CascadeCount = cascadeCount;
            HasValidCache = true;
            IsDirty = false;
        }

        private static bool EnsureShadowmap(ref RenderTexture texture, int width, int height, string name)
        {
            if (texture != null && texture.width == width && texture.height == height)
            {
                return false;
            }

            ReleaseTexture(ref texture);

            texture = new RenderTexture(width, height, 32, RenderTextureFormat.Shadowmap)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return true;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }

            texture = null;
        }

        private void ResetCachedData()
        {
            HasValidCache = false;
            AtlasWidth = 0;
            AtlasHeight = 0;
            TileResolution = 0;
            CascadeCount = 0;
            _lastCacheCameraInstanceId = 0;
            _lastCacheCameraType = CameraType.Game;

            for (int i = 0; i < kMaxCascades; i++)
            {
                _cascadeData[i] = default;
                _worldToShadow[i] = Matrix4x4.identity;
                _cascadeSplitSpheres[i] = Vector4.zero;
            }
        }
    }
}
