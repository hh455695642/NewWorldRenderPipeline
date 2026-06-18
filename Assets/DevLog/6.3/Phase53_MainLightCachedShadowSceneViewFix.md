# Phase53 Main Light Cached Shadow SceneView 缓存路径修复

日期：`2026-06-18`

## 概要

本阶段处理 main light cached shadow 在 GameView 与 SceneView 行为不一致的问题。

现象是：GameView 中 cached main light shadow 表现符合预期，静态投影物移动后不会自动刷新缓存；但在 SceneView 中移动同一个静态投影物时，阴影会实时跟随移动。按当前 cached shadow 的设计，如果没有显式 dirty / clear cache，静态投影区域不应实时更新。

根因不在 shadow atlas 本身，也不在 shadow caster shader。当前实现明确把 cached main light shadow 限制在 `CameraType.Game`，SceneView 会直接回退到 realtime main light shadow atlas。所以 SceneView 中看到的“静态阴影实时移动”不是缓存被错误刷新，而是 SceneView 从未进入缓存路径。

本阶段将 cached main light shadow 的相机策略调整为：

```text
Player / Game Camera:
    使用 cached main light shadow

Editor SceneView:
    使用独立的 cached main light shadow

Preview Camera:
    保持 realtime main light shadow
```

为了避免 SceneView 编辑器预览污染 GameView 的运行时缓存，本阶段没有把 Game 和 SceneView 共用同一个 cache state，而是为两类相机维护独立的 `MainLightShadowCacheState`、static cache pass 和 dynamic overlay pass。

## 修改文件

- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/Tests/EditMode/NWRPAdditionalShadowLayoutTests.cs`

## 解决的问题

### 1. SceneView 被硬编码排除在 cached shadow 外

旧逻辑中，cached shadow 的入口判断为：

```csharp
camera != null && camera.cameraType == CameraType.Game
```

因此只要当前 camera 是 SceneView，即使 pipeline asset 开启了 cached main light shadow，也会在 `MainLightShadowFeature.AddPasses(...)` 中走 realtime shadow pass。

这解释了用户侧观察到的现象：

```text
GameView:
    CachedStatic / CachedStaticPlusDynamicOverlay
    静态投影物移动后不自动刷新缓存

SceneView:
    RealtimeAtlas
    静态投影物移动后每帧重新渲染 shadow atlas
```

本阶段新增 `CameraType` 级别的判断入口：

```csharp
public static bool ShouldUseCachedMainLightShadow(CameraType cameraType)
```

当前策略为：

```text
CameraType.Game      -> true
CameraType.SceneView -> true, only in UNITY_EDITOR
CameraType.Preview   -> false
```

这样 SceneView 可以预览与 GameView 一致的 cached static shadow 行为，同时不会改变 Player / 移动端构建中的相机策略。

### 2. GameView 与 SceneView cache state 必须隔离

如果只把 SceneView 放进 cached shadow 判断，但继续复用原来的单一 `_cacheState`，会产生新的编辑器污染风险：

- SceneView 移动、旋转、缩放视角时可能改写 GameView 使用的 cascade matrix。
- SceneView 的 static atlas rebuild 会覆盖 GameView 当前 cache。
- GameView / SceneView 同时刷新时，`LastExecutionPath`、receiver globals 和 indirect shadow target 更难排查。

本阶段将 `MainLightShadowFeature` 中原来的单 cache：

```text
_cacheState
_staticCachePass
_dynamicOverlayPass
```

拆为：

```text
_gameCacheState
_gameStaticCachePass
_gameDynamicOverlayPass

UNITY_EDITOR:
_sceneViewCacheState
_sceneViewStaticCachePass
_sceneViewDynamicOverlayPass
```

`AddPasses(...)` 根据当前 camera type 选择对应 pass。Preview camera 不会获得 cache pass，仍然走 realtime atlas。

### 3. Dirty / Clear 操作同步影响两套缓存

项目已有显式缓存控制入口：

```csharp
NewWorldRenderPipelineAsset.MarkMainLightShadowCacheDirty()
NewWorldRenderPipelineAsset.ClearMainLightShadowCache()
```

材质 GUI 中修改 realtime shadow caster 开关时，也会调用 dirty 入口。

本阶段保留这些入口的 public surface，不新增 API。内部行为改为同时作用于 Game cache 和 SceneView cache：

```text
MarkCacheDirty:
    game cache dirty
    scene view cache dirty, only in editor

ClearCache:
    game cache clear
    scene view cache clear, only in editor
```

这样编辑器中修改静态投影相关资源后，GameView 与 SceneView 都会在下一次 cached shadow 执行时重建各自缓存，不会出现一个视图刷新、另一个视图还拿旧 atlas 的情况。

### 4. SceneView vegetation indirect shadow 兼容保留

Phase52 中 SceneView 的 realtime shadow path 对 vegetation indirect shadow 有 editor-only 兼容逻辑：即使 renderer data 没有开启 vegetation indirect tree shadows，SceneView 仍允许 indirect shadow provider 参与预览。

SceneView 改走 cached static shadow 后，如果 static cache pass 不同步这条 editor-only 规则，可能出现：

```text
Realtime SceneView path:
    vegetation indirect shadow 可预览

Cached SceneView path:
    vegetation indirect shadow 丢失
```

本阶段在 `MainLightShadowStaticCachePass.HasIndirectShadowCasters(...)` 中补齐 SceneView 的 editor-only allow path，避免切换 cached path 后丢失已有编辑器预览能力。

## 关键实现

### MainLightShadowPassUtils

新增 `CameraType` overload：

```csharp
public static bool ShouldUseCachedMainLightShadow(CameraType cameraType)
```

原有 camera overload 改为转发：

```csharp
public static bool ShouldUseCachedMainLightShadow(Camera camera)
{
    return camera != null && ShouldUseCachedMainLightShadow(camera.cameraType);
}
```

该 helper 是后续扩展 cached shadow camera policy 的集中入口，避免在 feature / pass / editor 文案中继续散落 `CameraType.Game` 判断。

### MainLightShadowFeature

`Create()` 阶段创建两套 cache-owned pass：

```text
Game:
    MainLightShadowCacheState
    MainLightShadowStaticCachePass
    MainLightShadowDynamicOverlayPass

SceneView, editor only:
    MainLightShadowCacheState
    MainLightShadowStaticCachePass
    MainLightShadowDynamicOverlayPass
```

`AddPasses(...)` 中先解析当前 camera 可用的 cached pass：

```text
TryGetCachedPasses(camera, out staticCachePass, out dynamicOverlayPass)
```

然后按原有执行路径 enqueue：

```text
EnableMainLightShadows == false:
    disabled pass

EnableCachedMainLightShadows == false:
    realtime atlas pass

cached camera:
    static cache pass
    optional dynamic overlay pass

non-cached camera:
    realtime atlas pass
```

Preview camera 因为不是 cached camera，继续走 realtime atlas。

### Pipeline asset 与 Editor 文案

旧文案明确写着 cached main light shadows only apply to Game Cameras。本阶段更新为：

```text
Cached main light shadows apply to Game Cameras and SceneView cameras.
Preview cameras still render realtime main light shadows.
```

同时将 cached setting tooltip 中的 Game Camera 字样收敛为 cached camera，避免后续误判 SceneView 不受 dirty / motion invalidation 等设置影响。

## 性能与移动端策略

本阶段不会改变移动端 Player 的渲染成本：

- `UNITY_EDITOR` 外只有 `CameraType.Game` 可以使用 cached shadow。
- SceneView cache state、SceneView cache pass 只在 Editor 编译中存在。
- Preview camera 继续 realtime，不为材质预览等小相机分配 cached static atlas。
- 不新增 shader、RenderTexture chain、fullscreen blit、compute dispatch 或 MRT。
- 不新增 CPU per-object shadow loop。

Editor 中的额外成本是 SceneView 多持有一套 cached static shadow atlas / combined atlas / empty atlas。该成本只在编辑器预览中存在，换来的是 SceneView 与 GameView 对 cached static shadow 的一致行为，并避免 SceneView 改写 GameView cache。

## Shader Variant 风险

本阶段没有修改 shader 文件，也没有新增 shader keyword。

```text
新增 shader keyword: 0
新增 multi_compile: 0
新增 shader_feature_local: 0
新增 shader 文件: 0
修改 shader 文件: 0
```

Cached shadow 的执行路径通过 C# pass enqueue、RenderTexture / global 参数上传和已有 shader uniform 控制，不引入新的 variant 组合。

## 验证记录

### TDD / 回归测试

新增 EditMode 覆盖：

```text
NWRP.Tests.NWRPAdditionalShadowLayoutTests.CachedMainLightShadowCameraTypePolicyKeepsPreviewRealtime
NWRP.Tests.NWRPAdditionalShadowLayoutTests.MainLightShadowFeatureKeepsGameAndSceneViewCachesSeparate
```

覆盖点：

- `CameraType.Game` 使用 cached main light shadow。
- `CameraType.SceneView` 在 Editor 下使用 cached main light shadow。
- `CameraType.Preview` 不使用 cached main light shadow。
- `MainLightShadowFeature` 中 Game cache 与 SceneView cache 是不同实例。
- Preview camera 不分配 cached shadow state。

### dotnet 编译

已完成：

```text
dotnet build NWRP.Runtime.Tests.csproj --no-restore -v:minimal
0 warnings / 0 errors
```

已完成：

```text
dotnet build NWRP.Editor.csproj --no-restore -v:minimal
0 errors
3 existing MSB reference conflict warnings
```

`NWRP.Editor` 的 warning 是项目已有的 `System.*` / NuGet 引用版本冲突，非本阶段新增错误。

### Unity Test Runner

本阶段未能完成 Unity batchmode EditMode Test Runner 自动运行。原因是当前项目已经被一个 Unity Editor 实例打开，batchmode 启动时报错：

```text
It looks like another Unity instance is running with this project open.
Multiple Unity instances cannot open the same project.
```

因此本阶段已完成 C# 编译验证，但 SceneView / GameView 视觉 smoke 仍需要在当前打开的 Unity Editor 中手动执行。

## 手动验证清单

建议在复现场景中验证：

1. 开启 `Enable Cached Main Light Shadows`。
2. 在 GameView 中移动 static caster，不调用 dirty / clear cache，确认投射阴影不实时更新。
3. 在 SceneView 中移动同一个 static caster，不调用 dirty / clear cache，确认投射阴影同样不实时更新。
4. 调用 `NewWorldRenderPipelineAsset.MarkMainLightShadowCacheDirty()` 后，确认 GameView 与 SceneView 各自重建缓存。
5. 开启 dynamic overlay 后，确认 dynamic caster 仍可每帧叠加，static caster 仍走缓存。
6. 打开材质 Preview 或其他 Preview camera，不应触发 cached static shadow atlas 路径。
7. Frame Debugger 中确认 SceneView main light shadow path 显示为 cached static 或 cached static plus dynamic overlay，而不是 realtime atlas。

## 与旧阶段的关系

Phase52 主要修复 SceneView / GameView additional shadow 稳定性和 Unity 6.3 shadow caster culling 生命周期问题。该阶段同时确认 cached shadow 与 realtime shadow 的 culling context 边界。

Phase53 是对 main light cached shadow camera policy 的补齐：

```text
Phase52:
    修复 SceneView / GameView shadow culling 稳定性
    修复 additional light shadow slice 稳定性
    收敛 Unity 6.3 renderer-list / RTHandle API

Phase53:
    修复 SceneView 没有进入 cached main light shadow path 的行为差异
    保证 GameView cache 与 SceneView cache 隔离
    保持 Preview camera realtime
```

该阶段不新增渲染功能，不改变移动端 Player 默认能力，只让 Editor SceneView 能正确预览 cached main light shadow 的静态缓存语义。

## 当前注意事项

- SceneView cache 是 editor-only，不应被后续移动端性能评估计入 Player 成本。
- 如果后续新增 cached shadow debug UI，需要同时显示 Game cache 与 SceneView cache 的状态，避免把两套 atlas 混在一起解释。
- `Final Shadow Source Tint` 仍保持 Game Camera only，本阶段没有扩展 SceneView debug tint。
- 如果后续新增更多 editor camera 类型，应继续让 Preview camera 保持 realtime，避免材质预览或小型工具相机持有 cached atlas。
- 本阶段没有做 Android / iOS 真机验证，因为 runtime Player 策略没有改变。

## 后续方向

- 在当前打开的 Unity Editor 中运行 `NWRPAdditionalShadowLayoutTests` 和现有 EditMode suite。
- 在复现场景中做一次 SceneView + GameView 同开 smoke，重点确认 static caster 阴影缓存不随 SceneView 移动实时刷新。
- 如需更直观调试，可后续增加轻量 debug 输出：当前 camera type、main light shadow execution path、cache dirty state、cache atlas size。该输出应保持 editor-only 或显式 debug toggle。
