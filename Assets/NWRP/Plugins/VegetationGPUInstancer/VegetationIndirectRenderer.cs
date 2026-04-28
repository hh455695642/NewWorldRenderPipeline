using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[ExecuteAlways]
public class VegetationIndirectRenderer : MonoBehaviour
{
    // Rendering flow:
    // 1) Collect MeshRenderers from VegetationRoots and group by Chunk, then by (mesh+material).
    // 2) CPU performs coarse chunk/group culling (distance + frustum or cylinder-frustum).
    // 3) Compute shader performs per-instance culling and appends visible matrices to _VisibleVegetationBuffer.
    // 4) Graphics.RenderMeshIndirect draws per group; shaders read instance matrices procedurally.
    //
    // Notes:
    // - GPU instancing path is used at runtime; editor non-play mode keeps original MeshRenderer rendering.
    // - Until vegetation gets a dedicated NWRP indirect shadow pass, tree shadows use a shadow-only
    //   MeshRenderer fallback so DrawShadows can see them in the current pipeline culling results.
    // - Scene view camera is separate from game camera; optional dual rendering keeps Scene preview consistent.
    //
    // IMPORTANT RUNTIME SETUP:
    // - Prefer one renderer for trees and another renderer for grass/flowers.
    // - This component applies one global runtime policy to every group in VegetationRoots:
    //   cull mode, cull distance, cylinder params, render layer, and shadow policy.
    // - If trees and grass are mixed in one renderer, shadow/cull requirements can conflict and
    //   cause local darkening artifacts (for example, tree shadow receiving/casting state leaking
    //   into grass groups, or shadow-relevant tree instances culled too aggressively).
    // - Recommended split:
    //   Trees  -> castShadows = true, receiveShadows = true, cullDistance >= shadow distance + safety margin.
    //             Tree shaders need a dedicated ShadowCaster pass and stricter normal/matrix handling.
    //   Grass  -> castShadows = false, receiveShadows = true, shorter cullDistance for better CPU/GPU cost.

    public enum CullMode
    {
        AccurateFrustum,
        CylinderFrustum
    }

    [Header("Data Source: vegetation roots (grass/flower/shrub)")]
    public List<Transform> VegetationRoots = new List<Transform>();

    [Header("Chunk Origin: terrain center (fallback to root transform)")]
    public Transform terrainCenter;

    [Header("Target Camera: fallback to Camera.main")]
    public Camera targetCamera;

    [Header("Render Layer (matches camera culling mask)")]
    public int renderLayer = 0;

    [Header("Chunk Size (XZ)")]
    public float ChunkSize = 32f;

    [Header("Chunk Height (Y)")]
    public float chunkHeight = 20f;

    [Header("Auto-calculate chunk height from vegetation bounds")]
    public bool autoCalculateHeight = false;

    [Header("Height Padding (avoid top clipping)")]
    public float heightPadding = 1f;

    [Header("Cull Mode")]
    public CullMode cullMode = CullMode.AccurateFrustum;

    [Header("Distance Culling: max distance (AccurateFrustum)")]
    // Global for this renderer instance. If this renderer also draws trees, keep this large enough
    // for stable shadow continuity; otherwise tree shadows can pop or darken unexpectedly by region.
    public float cullDistance = 50f;

    [Header("Cylinder Frustum: max forward distance")]
    public float cylinderMaxDistance = 100f;

    [Header("Cylinder Frustum: near radius")]
    public float cylinderNearRadius = 20f;

    [Header("Cylinder Frustum: far radius")]
    public float cylinderFarRadius = 200f;

    [Header("Shadow Policy")]
    // Global policy for ALL groups in this renderer instance.
    // Keep trees and grass in separate renderer components if they need different shadow policies.
    [FormerlySerializedAs("enableShadow")]
    public bool castShadows = false;
    public bool receiveShadows = true;

    [Header("Compute Shader (VegetationCulling.compute)")]
    public ComputeShader CullingComputeShader;

    [Header("Debug: disable instancing and use original MeshRenderer")]
    public bool debugUseOriginalRenderer = false;

    [Header("Editor: draw chunk gizmos")]
    public bool drawChunkGizmos = true;

    [Header("Editor: render in Scene view while playing")]
    public bool editorRenderInSceneView = true;

    [Header("Editor: use Scene view camera for culling preview")]
    public bool editorPreviewCullingInSceneView = true;

    [Header("Debug: log submitted instance count (has overhead)")]
    public bool debugLogVisibleCount = false;

    struct GrassInstance
    {
        public Matrix4x4 localToWorld;
        public Vector3 boundCenter;
        public float boundRadius;
    }

    const int kInstanceStride = 80;

    struct CameraCullingContext
    {
        public Plane[] planes;
        public Vector4[] frustumPlanes;
        public Vector3 camPos;
        public Vector3 camForward;
        public float cullDistSqr;
    }

    class RenderGroup
    {
        public Mesh mesh;
        public Material material;
        public readonly List<GrassInstance> instances = new List<GrassInstance>();
        public Bounds bounds;
        public bool hasBounds;
        public ComputeBuffer allGrassBuffer;
        public ComputeBuffer visibleGrassBuffer;
        public GraphicsBuffer indirectCommandBuffer;
        public MaterialPropertyBlock mpb;
        public RenderParams rpTemplate;

        public void ReleaseBuffers()
        {
            allGrassBuffer?.Release();
            visibleGrassBuffer?.Release();
            indirectCommandBuffer?.Release();
            allGrassBuffer = null;
            visibleGrassBuffer = null;
            indirectCommandBuffer = null;
        }
    }

    class VegetationChunk
    {
        public Vector2Int coord;
        public Vector3 center;
        public Bounds bounds;
        public readonly List<RenderGroup> groups = new List<RenderGroup>();
    }

    struct RendererState
    {
        public bool enabled;
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public int layer;
    }

    struct ShadowCasterEntry
    {
        public MeshRenderer renderer;
        public Bounds bounds;
    }

    readonly Dictionary<Vector2Int, VegetationChunk> _chunks = new Dictionary<Vector2Int, VegetationChunk>();
    readonly List<MeshRenderer> _cachedRenderers = new List<MeshRenderer>();
    readonly List<ShadowCasterEntry> _shadowCasterEntries = new List<ShadowCasterEntry>();
    readonly Dictionary<MeshRenderer, RendererState> _originalRendererStates = new Dictionary<MeshRenderer, RendererState>();
    readonly HashSet<Vector2Int> _cpuVisibleChunkCoords = new HashSet<Vector2Int>();
    readonly HashSet<Vector2Int> _submittedChunkCoords = new HashSet<Vector2Int>();

    int _kernelIndex = -1;
    bool _csReady = false;
    bool _lastDebugState = false;
    bool _lastCastShadows = false;
    Camera _cam;

    static readonly int _idVisibleBuffer = Shader.PropertyToID("_VisibleVegetationBuffer");
    static readonly int _idReceiveShadows = Shader.PropertyToID("_ReceiveShadows");
    static readonly int _idAllGrass = Shader.PropertyToID("_AllGrass");
    static readonly int _idVisibleGrass = Shader.PropertyToID("_VisibleGrass");
    static readonly int _idGrassCount = Shader.PropertyToID("_GrassCount");
    static readonly int _idCullMode = Shader.PropertyToID("_CullMode");
    static readonly int _idFrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
    static readonly int _idCamPos = Shader.PropertyToID("_CamPos");
    static readonly int _idCamForward = Shader.PropertyToID("_CamForward");
    static readonly int _idCullDist = Shader.PropertyToID("_CullDist");
    static readonly int _idCullDistSqr = Shader.PropertyToID("_CullDistSqr");
    static readonly int _idCylinderMaxDistance = Shader.PropertyToID("_CylinderMaxDistance");
    static readonly int _idCylinderNearRadius = Shader.PropertyToID("_CylinderNearRadius");
    static readonly int _idCylinderFarRadius = Shader.PropertyToID("_CylinderFarRadius");

    readonly Plane[] _cameraFrustumPlaneCache = new Plane[6];
    readonly Vector4[] _cameraFrustumPlaneVectorCache = new Vector4[6];

    void Start()
    {
        _cam = targetCamera != null ? targetCamera : Camera.main;
        Initialize();
    }
    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Keep original MeshRenderer path in editor non-play mode; no GPU buffer allocation.
            // Build chunk data only for gizmo/debug inspection.
            RestoreOriginalRenderers();
            ReleaseAllBuffers();
            CacheRenderers();
            BuildChunks();
            _csReady = false;
            RestoreOriginalRenderers();
        }
#endif
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RestoreOriginalRenderers();
        _cpuVisibleChunkCoords.Clear();
        _submittedChunkCoords.Clear();
        ReleaseAllBuffers();
    }

    void OnDestroy()
    {
        RestoreOriginalRenderers();
        _cpuVisibleChunkCoords.Clear();
        _submittedChunkCoords.Clear();
        ReleaseAllBuffers();
    }

    [ContextMenu("Reinitialize Grass Renderer")]
    public void Initialize()
    {
        RestoreOriginalRenderers();
        ReleaseAllBuffers();
        CacheRenderers();
        BuildChunks();
        RebuildAllBuffers();

        _lastDebugState = debugUseOriginalRenderer;
        _lastCastShadows = castShadows;
        if (Application.isPlaying)
            ApplyOriginalRendererRuntimeState();

        _csReady = false;
        if (CullingComputeShader == null)
        {
            Debug.LogError("[VegetationRenderer] Missing compute shader reference.");
            return;
        }

        _kernelIndex = CullingComputeShader.FindKernel("CSVegetationCulling");
        _csReady = _kernelIndex >= 0;

        if (!_csReady)
        {
            Debug.LogError("[VegetationRenderer] Kernel 'CSVegetationCulling' not found.");
            return;
        }

        Debug.Log($"[VegetationRenderer] Initialize complete. Chunks={_chunks.Count}, Instances={GetTotalInstanceCount()}");
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!Application.isPlaying || debugUseOriginalRenderer || !castShadows)
            return;

        if (!ShouldPrepareShadowCastersForCamera(camera))
            return;

        UpdateShadowOnlyRenderersForCamera(camera);
    }

    bool ShouldPrepareShadowCastersForCamera(Camera camera)
    {
        if (camera == null)
            return false;

        if (targetCamera != null)
            return camera == targetCamera || IsSupportedEditorSceneCamera(camera);

        if (_cam == null)
            _cam = Camera.main;

        return camera == _cam || IsSupportedEditorSceneCamera(camera);
    }

    bool IsSupportedEditorSceneCamera(Camera camera)
    {
#if UNITY_EDITOR
        return editorRenderInSceneView
            && editorPreviewCullingInSceneView
            && camera != null
            && camera.cameraType == CameraType.SceneView;
#else
        return false;
#endif
    }

    int GetTotalInstanceCount()
    {
        int count = 0;
        foreach (var chunk in _chunks.Values)
            foreach (var group in chunk.groups)
                count += group.instances.Count;
        return count;
    }

    void CacheRenderers()
    {
        _cachedRenderers.Clear();
        foreach (var root in VegetationRoots)
        {
            if (root == null)
                continue;

            _cachedRenderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));
        }

        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            MeshRenderer renderer = _cachedRenderers[i];
            if (renderer == null || _originalRendererStates.ContainsKey(renderer))
                continue;

            _originalRendererStates.Add(renderer, new RendererState
            {
                enabled = renderer.enabled,
                shadowCastingMode = renderer.shadowCastingMode,
                receiveShadows = renderer.receiveShadows,
                layer = renderer.gameObject.layer
            });
        }
    }

    void BuildChunks()
    {
        _chunks.Clear();
        _shadowCasterEntries.Clear();

        Vector3 origin = terrainCenter != null ? terrainCenter.position : transform.root.position;

        foreach (var root in VegetationRoots)
        {
            if (root == null || !root.gameObject.activeInHierarchy)
                continue;

            var renderers = root.GetComponentsInChildren<MeshRenderer>(false);
            foreach (var mr in renderers)
            {
                if (!IsRendererEnabledInSource(mr))
                    continue;

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null)
                    continue;

                _shadowCasterEntries.Add(new ShadowCasterEntry
                {
                    renderer = mr,
                    bounds = mr.bounds
                });

                Vector3 pos = mr.transform.position;
                int cx = Mathf.FloorToInt((pos.x - origin.x) / ChunkSize);
                int cz = Mathf.FloorToInt((pos.z - origin.z) / ChunkSize);
                var coord = new Vector2Int(cx, cz);

                if (!_chunks.TryGetValue(coord, out var chunk))
                {
                    Vector3 chunkCenter = origin + new Vector3(
                        cx * ChunkSize + ChunkSize * 0.5f,
                        0f,
                        cz * ChunkSize + ChunkSize * 0.5f);

                    chunk = new VegetationChunk
                    {
                        coord = coord,
                        center = chunkCenter,
                        bounds = new Bounds(chunkCenter, new Vector3(ChunkSize, chunkHeight, ChunkSize))
                    };

                    _chunks.Add(coord, chunk);
                }

                if (autoCalculateHeight)
                {
                    chunk.bounds.Encapsulate(mr.bounds);
                    Vector3 size = chunk.bounds.size;
                    size.x = ChunkSize;
                    size.z = ChunkSize;
                    size.y += heightPadding;
                    chunk.bounds.size = size;
                    chunk.center = chunk.bounds.center;
                }

                Mesh mesh = mf.sharedMesh;
                Material material = mr.sharedMaterial;

                RenderGroup group = chunk.groups.Find(g => g.mesh == mesh && g.material == material);
                if (group == null)
                {
                    // Indirect rendering still expects GPU instancing enabled on the material.
                    // Otherwise Unity may not treat the draw as instanced on some backends/tools.
                    if (!material.enableInstancing)
                        material.enableInstancing = true;

                    group = new RenderGroup
                    {
                        mesh = mesh,
                        material = material,
                        mpb = new MaterialPropertyBlock()
                    };
                    group.rpTemplate = new RenderParams(material);
                    chunk.groups.Add(group);
                }

                group.instances.Add(new GrassInstance
                {
                    localToWorld = mr.transform.localToWorldMatrix,
                    boundCenter = mr.bounds.center,
                    boundRadius = mr.bounds.extents.magnitude
                });

                if (group.hasBounds)
                    group.bounds.Encapsulate(mr.bounds);
                else
                {
                    group.bounds = mr.bounds;
                    group.hasBounds = true;
                }
            }
        }
    }

    void RebuildAllBuffers()
    {
        foreach (var chunk in _chunks.Values)
            foreach (var group in chunk.groups)
                RebuildGroupBuffers(group);
    }

    void RebuildGroupBuffers(RenderGroup group)
    {
        group.ReleaseBuffers();

        int count = group.instances.Count;
        if (count == 0)
            return;

        group.allGrassBuffer = new ComputeBuffer(count, kInstanceStride);
        group.allGrassBuffer.SetData(group.instances);

        group.visibleGrassBuffer = new ComputeBuffer(count, 64, ComputeBufferType.Append);

        group.indirectCommandBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size);
 
        var cmd = new GraphicsBuffer.IndirectDrawIndexedArgs
        {
            indexCountPerInstance = group.mesh.GetIndexCount(0),
            instanceCount = 0,
            startIndex = group.mesh.GetIndexStart(0),
            baseVertexIndex = group.mesh.GetBaseVertex(0),
            startInstance = 0
        };

        // Use IndirectDrawIndexedArgs to avoid platform-dependent layout issues.
        group.indirectCommandBuffer.SetData(new[] { cmd });
    }

    void ReleaseAllBuffers()
    {
        foreach (var chunk in _chunks.Values)
            foreach (var group in chunk.groups)
                group.ReleaseBuffers();
    }
    void Update()
    {
        if (!Application.isPlaying)
        {
            // In editor non-play mode always keep original MeshRenderer rendering.
            RestoreOriginalRenderers();
            return;
        }

        if (targetCamera != null)
            _cam = targetCamera;
        else if (_cam == null)
            _cam = Camera.main;

        if (_cam == null || !_cam.enabled)
            return;

        if (Application.isPlaying && debugUseOriginalRenderer != _lastDebugState)
        {
            ApplyOriginalRendererRuntimeState();
            _lastDebugState = debugUseOriginalRenderer;
        }

        if (Application.isPlaying && castShadows != _lastCastShadows)
        {
            ApplyOriginalRendererRuntimeState();
            _lastCastShadows = castShadows;
        }

        if (Application.isPlaying && debugUseOriginalRenderer)
            return;

        if (!_csReady)
            return;

#if UNITY_EDITOR
        Camera sceneCam = null;
        if (editorRenderInSceneView)
        {
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            sceneCam = sceneView != null ? sceneView.camera : null;
        }

        // Optional: use SceneView camera as debug-set source so gizmo colors match Scene culling.
        if (editorPreviewCullingInSceneView && sceneCam != null)
        {
            RenderForCamera(sceneCam, updateDebugSets: true);
            if (_cam != null && _cam != sceneCam)
                RenderForCamera(_cam, updateDebugSets: false);
        }
        else
        {
            RenderForCamera(_cam, updateDebugSets: true);
            if (sceneCam != null && sceneCam != _cam)
                RenderForCamera(sceneCam, updateDebugSets: false);
        }
#else
        RenderForCamera(_cam, updateDebugSets: true);
#endif
    }

    CameraCullingContext BuildCameraCullingContext(Camera cam)
    {
        GeometryUtility.CalculateFrustumPlanes(cam, _cameraFrustumPlaneCache);
        for (int i = 0; i < 6; i++)
        {
            _cameraFrustumPlaneVectorCache[i] = new Vector4(
                _cameraFrustumPlaneCache[i].normal.x,
                _cameraFrustumPlaneCache[i].normal.y,
                _cameraFrustumPlaneCache[i].normal.z,
                _cameraFrustumPlaneCache[i].distance);
        }

        return new CameraCullingContext
        {
            planes = _cameraFrustumPlaneCache,
            frustumPlanes = _cameraFrustumPlaneVectorCache,
            camPos = cam.transform.position,
            camForward = cam.transform.forward,
            cullDistSqr = cullDistance * cullDistance
        };
    }

    void ApplyCameraCullingParams(CameraCullingContext context)
    {
        CullingComputeShader.SetInt(_idCullMode, (int)cullMode);
        CullingComputeShader.SetVectorArray(_idFrustumPlanes, context.frustumPlanes);
        CullingComputeShader.SetVector(_idCamPos, new Vector4(context.camPos.x, context.camPos.y, context.camPos.z, 0f));
        CullingComputeShader.SetVector(_idCamForward, new Vector4(context.camForward.x, context.camForward.y, context.camForward.z, 0f));
        CullingComputeShader.SetFloat(_idCullDist, cullDistance);
        CullingComputeShader.SetFloat(_idCullDistSqr, context.cullDistSqr);
        CullingComputeShader.SetFloat(_idCylinderMaxDistance, cylinderMaxDistance);
        CullingComputeShader.SetFloat(_idCylinderNearRadius, cylinderNearRadius);
        CullingComputeShader.SetFloat(_idCylinderFarRadius, cylinderFarRadius);
    }
    void RenderForCamera(Camera cam, bool updateDebugSets)
    {
        // Run culling and indirect draw per camera so Game/Scene views stay consistent.
        CameraCullingContext cullingContext = BuildCameraCullingContext(cam);
        ApplyCameraCullingParams(cullingContext);

        int totalSubmittedInstanceCount = 0;
        if (updateDebugSets)
        {
            _cpuVisibleChunkCoords.Clear();
            _submittedChunkCoords.Clear();
        }

        foreach (var chunk in _chunks.Values)
        {
            if (!PassChunkCulling(chunk, cullingContext))
                continue;

            if (updateDebugSets)
                _cpuVisibleChunkCoords.Add(chunk.coord);

            bool submittedAnyGroup = false;
            foreach (var group in chunk.groups)
            {
                if (group.instances.Count == 0 || group.allGrassBuffer == null)
                    continue;
                if (!PassGroupCulling(group, cullingContext))
                    continue;

                int submittedInstanceCount = DispatchCullingAndDrawGroup(
                    cam,
                    group,
                    group.hasBounds ? group.bounds : chunk.bounds);

                totalSubmittedInstanceCount += submittedInstanceCount;
                submittedAnyGroup = true;
            }

            if (updateDebugSets && submittedAnyGroup)
                _submittedChunkCoords.Add(chunk.coord);
        }

        if (updateDebugSets && debugLogVisibleCount)
            Debug.Log($"[GrassRenderer] Submitted instance count (pre GPU cull): {totalSubmittedInstanceCount}");
    }

    bool PassChunkCulling(VegetationChunk chunk, CameraCullingContext context)
    {
        // Chunk coarse culling: reduce the number of instances entering compute.
        return PassBoundsCulling(chunk.bounds, chunk.center, context);
    }

    bool PassGroupCulling(RenderGroup group, CameraCullingContext context)
    {
        if (!group.hasBounds)
            return true;

        return PassBoundsCulling(group.bounds, group.bounds.center, context);
    }

    bool PassBoundsCulling(Bounds bounds, Vector3 center, CameraCullingContext context)
    {
        float boundsRadius = bounds.extents.magnitude;
        Vector3 toCenter = center - context.camPos;

        if (cullMode == CullMode.CylinderFrustum)
        {
            float forwardDist = Vector3.Dot(context.camForward, toCenter);
            if (forwardDist < -boundsRadius || forwardDist > cylinderMaxDistance + boundsRadius)
                return false;

            Vector3 projected = context.camForward * forwardDist;
            float radialDistSqr = (toCenter - projected).sqrMagnitude;
            float t = cylinderMaxDistance > 0.0001f
                ? Mathf.Clamp01(forwardDist / cylinderMaxDistance)
                : 0f;
            float allowedRadius = Mathf.Lerp(cylinderNearRadius, cylinderFarRadius, t) + boundsRadius;
            return radialDistSqr <= allowedRadius * allowedRadius;
        }

        // Use closest-point test for tighter distance culling against AABB.
        Vector3 closestPoint = bounds.ClosestPoint(context.camPos);
        if ((closestPoint - context.camPos).sqrMagnitude > context.cullDistSqr)
            return false;

        return GeometryUtility.TestPlanesAABB(context.planes, bounds);
    }

    bool IsRendererEnabledInSource(MeshRenderer renderer)
    {
        if (renderer == null)
            return false;

        if (_originalRendererStates.TryGetValue(renderer, out RendererState state))
            return state.enabled;

        return renderer.enabled;
    }

    int DispatchCullingAndDrawGroup(Camera cam, RenderGroup group, Bounds worldBounds)
    {
        int submittedInstanceCount = group.instances.Count;

        // Reset AppendBuffer counter before dispatch.
        group.visibleGrassBuffer.SetCounterValue(0);

        // Bind per-group buffers and count.
        CullingComputeShader.SetBuffer(_kernelIndex, _idAllGrass, group.allGrassBuffer);
        CullingComputeShader.SetBuffer(_kernelIndex, _idVisibleGrass, group.visibleGrassBuffer);
        CullingComputeShader.SetInt(_idGrassCount, submittedInstanceCount);

        int threadGroups = Mathf.Max(1, Mathf.CeilToInt(submittedInstanceCount / 64.0f));
        CullingComputeShader.Dispatch(_kernelIndex, threadGroups, 1, 1);

        // Copy AppendBuffer count to IndirectDrawIndexedArgs.instanceCount.
        GraphicsBuffer.CopyCount(group.visibleGrassBuffer, group.indirectCommandBuffer, sizeof(uint));
        group.mpb.SetBuffer(_idVisibleBuffer, group.visibleGrassBuffer);
        group.mpb.SetFloat(_idReceiveShadows, receiveShadows ? 1f : 0f);

        Bounds drawBounds = worldBounds;
        drawBounds.Expand(2f);

        // RenderParams is a struct; reuse the cached template per group to avoid churn.
        var rp = group.rpTemplate;
        rp.camera = cam;
        rp.layer = renderLayer;
        rp.worldBounds = drawBounds;
        // Runtime tree shadows are supplied by the source renderers in ShadowsOnly mode.
        // Keeping indirect draws non-casting avoids duplicate heavy shadow submission.
        rp.shadowCastingMode = ShadowCastingMode.Off;
        rp.receiveShadows = receiveShadows;
        rp.matProps = group.mpb;
        Graphics.RenderMeshIndirect(rp, group.mesh, group.indirectCommandBuffer, 1, 0);

        return submittedInstanceCount;
    }

    void ApplyOriginalRendererRuntimeState()
    {
        if (!Application.isPlaying || debugUseOriginalRenderer)
        {
            RestoreOriginalRenderers();
            return;
        }

        if (castShadows && _cam != null && _cam.enabled)
        {
            UpdateShadowOnlyRenderersForCamera(_cam);
            return;
        }

        DisableOriginalRenderers();
    }

    void UpdateShadowOnlyRenderersForCamera(Camera cullCamera)
    {
        if (!Application.isPlaying || debugUseOriginalRenderer)
            return;

        if (!castShadows || cullCamera == null || !cullCamera.enabled)
        {
            DisableOriginalRenderers();
            return;
        }

        CameraCullingContext cullingContext = BuildCameraCullingContext(cullCamera);
        int renderLayerMask = 1 << Mathf.Clamp(renderLayer, 0, 31);
        bool shadowLayerVisibleToCamera = (cullCamera.cullingMask & renderLayerMask) != 0;

        DisableOriginalRenderers();
        if (!shadowLayerVisibleToCamera)
            return;

        foreach (var entry in _shadowCasterEntries)
        {
            MeshRenderer renderer = entry.renderer;
            if (renderer == null)
                continue;

            if (!PassBoundsCulling(entry.bounds, entry.bounds.center, cullingContext))
                continue;

            renderer.gameObject.layer = renderLayer;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            renderer.receiveShadows = false;
        }
    }

    void DisableOriginalRenderers()
    {
        foreach (var renderer in _cachedRenderers)
        {
            if (renderer != null)
                renderer.enabled = false;
        }
    }

    void RestoreOriginalRenderers()
    {
        foreach (var renderer in _cachedRenderers)
        {
            if (renderer == null)
                continue;

            if (_originalRendererStates.TryGetValue(renderer, out RendererState state))
            {
                renderer.enabled = state.enabled;
                renderer.shadowCastingMode = state.shadowCastingMode;
                renderer.receiveShadows = state.receiveShadows;
                renderer.gameObject.layer = state.layer;
            }
            else
            {
                renderer.enabled = true;
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (debugUseOriginalRenderer || !drawChunkGizmos)
            return;

        foreach (var chunk in _chunks.Values)
        {
            Color fill = new Color(0.45f, 0.45f, 0.45f, 0.05f);
            Color wire = new Color(0.45f, 0.45f, 0.45f, 0.35f);

            if (_cpuVisibleChunkCoords.Contains(chunk.coord))
            {
                fill = new Color(1.0f, 0.75f, 0.1f, 0.12f);
                wire = new Color(1.0f, 0.75f, 0.1f, 0.85f);
            }

            if (_submittedChunkCoords.Contains(chunk.coord))
            {
                fill = new Color(0.15f, 0.9f, 0.35f, 0.16f);
                wire = new Color(0.15f, 0.9f, 0.35f, 0.95f);
            }

            Gizmos.color = fill;
            Gizmos.DrawCube(chunk.bounds.center, chunk.bounds.size);

            Gizmos.color = wire;
            Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(chunk.center, 0.4f);

            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                chunk.bounds.center + Vector3.up * (chunk.bounds.extents.y + 0.5f),
                $"[{chunk.coord.x},{chunk.coord.y}]\n{TotalInstancesInChunk(chunk)} inst");
        }

        DrawCullingGizmos();
        DrawChunkLegend();
    }

    void DrawCullingGizmos()
    {
        Camera drawCam = targetCamera != null ? targetCamera : Camera.main;
        if (drawCam == null)
            return;

        if (cullMode == CullMode.CylinderFrustum)
        {
            DrawFrustumCylinderGizmo(
                drawCam.transform.position,
                drawCam.transform.forward,
                drawCam.transform.up,
                cylinderNearRadius,
                cylinderFarRadius,
                cylinderMaxDistance,
                new Color(0f, 0.85f, 1f, 0.18f));
            return;
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.12f);
        Gizmos.DrawSphere(drawCam.transform.position, cullDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(drawCam.transform.position, cullDistance);
    }

    void DrawFrustumCylinderGizmo(
        Vector3 origin,
        Vector3 forward,
        Vector3 up,
        float nearRadius,
        float farRadius,
        float length,
        Color color)
    {
        Gizmos.color = color;

        const int segments = 32;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        up = Vector3.Cross(forward, right).normalized;

        Vector3 nearCenter = origin;
        Vector3 farCenter = origin + forward * length;

        Vector3 prevNear = Vector3.zero;
        Vector3 prevFar = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            Vector3 nearPoint = nearCenter + right * cos * nearRadius + up * sin * nearRadius;
            Vector3 farPoint = farCenter + right * cos * farRadius + up * sin * farRadius;

            if (i > 0)
            {
                Gizmos.DrawLine(prevNear, nearPoint);
                Gizmos.DrawLine(prevFar, farPoint);
                Gizmos.DrawLine(prevNear, prevFar);
            }

            prevNear = nearPoint;
            prevFar = farPoint;
        }
    }

    void DrawChunkLegend()
    {
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            "Chunk Gizmos\nGray = All\nYellow = CPU Coarse Pass\nGreen = Submitted To GPU Draw");
    }

    int TotalInstancesInChunk(VegetationChunk chunk)
    {
        int count = 0;
        foreach (var group in chunk.groups)
            count += group.instances.Count;
        return count;
    }
#endif
}
