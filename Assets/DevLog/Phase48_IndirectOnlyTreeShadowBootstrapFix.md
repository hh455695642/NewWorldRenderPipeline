# Phase48 Indirect-Only 树阴影引导漏洞修复

日期：`2026-06-11`

## 概要

本阶段修复 `Map_LoopForest` 中运行时实例化树木在删除场景 Cube 后不再投射 / 接收主光阴影的问题。

现象非常明确：

- 场景里新建一个普通 `Cube` 后运行，树木相关阴影链路会出现。
- 删除这个 `Cube` 后重新运行，树木没有投影，也不接收主光阴影。
- 问题集中在 GPU indirect 植被路径，而不是普通 `MeshRenderer` 的 `ShadowCaster` pass。

根因是主光阴影系统此前以 Unity 常规 culling 结果作为是否创建 shadow atlas 的前置条件：

```csharp
CullingResults.GetShadowCasterBounds(...)
```

当场景中没有任何常规 shadow caster 时，该调用会返回 `false`。但是 GPU indirect 树木并不在 Unity 的 `CullingResults` renderer 集合里，因此虽然 `VegetationIndirectRenderer` 后续可以提供 indirect shadow draw，主光阴影路径已经提前判定为“无 caster”，并上传 disabled globals：

```text
_MainLightShadowParams = 0
_MainLightShadowmapTexture = NWRP_MainLightShadows_EmptyShadowmap / black fallback
```

普通 Cube 之所以能“救活”树阴影，是因为 Cube 作为常规 caster 让 `GetShadowCasterBounds()` 返回 true，主光 shadow atlas 和 receiver globals 被正常创建并保留下来。Cube 本身不是正确修复方案，只是暴露了 indirect-only 场景缺少 shadow bootstrap 的逻辑漏洞。

本阶段正式修复该漏洞：当存在 `VegetationIndirectRenderer` 提供的 indirect shadow caster 时，即使 Unity 常规 caster bounds 为空，也允许主光阴影路径建立有效 atlas、cascade 数据和 receiver globals，并让 `VegetationIndirectShadowPass` 写入该 atlas。

## 修改文件

- `Assets/NWRP/Runtime/VegetationIndirectShadows/VegetationIndirectShadowRegistry.cs`
- `Assets/NWRP/Runtime/VegetationIndirectRendering/VegetationIndirectRenderer.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs`
- `Assets/NWRP/Runtime/NWRPFeatureScheduler.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/VegetationIndirectShadowFeature.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/Passes/VegetationIndirectShadowPass.cs`
- `Assets/NWRP/Tests/ShadowBootstrapEditor/NWRP.ShadowBootstrap.Editor.Tests.asmdef`
- `Assets/NWRP/Tests/ShadowBootstrapEditor/VegetationIndirectShadowBootstrapTests.cs`

## 关键修复

### 1. Registry 增加轻量 caster 查询

`VegetationIndirectShadowRegistry` 新增 `IVegetationIndirectShadowCasterQuery`：

```csharp
bool HasIndirectShadowCasters(
    bool includeStaticCasters,
    bool includeDynamicCasters);
```

该接口只回答“当前是否存在可提交的 indirect shadow caster”，不创建 draw list，也不触发额外 buffer 绑定。

对于未来没有实现该接口的 provider，registry 仍保留兼容路径：临时调用 `TryCollectIndirectShadowDraws(...)` 到 scratch list，以保证扩展接口不会破坏旧 provider。

### 2. VegetationIndirectRenderer 复用提交条件

`VegetationIndirectRenderer` 实现 `IVegetationIndirectShadowCasterQuery`。

查询逻辑复用现有 shadow draw gating：

- `Application.isPlaying`
- 未启用 `debugUseOriginalRenderer`
- 未进入 original renderer fallback
- compute / indirect path 已准备好
- `castShadows = true`
- 当前 renderer data 开启 indirect rendering
- group 支持 tree indirect shadow
- group 具有 `ShadowCaster` pass
- `_CastShadows` 未关闭

这样主光阴影 pass 在建立 atlas 前可以先知道是否存在 indirect-only caster，而不需要提前构建完整 draw 列表。

### 3. Realtime 主光阴影支持 empty regular caster atlas

`MainLightShadowCasterPass` 现在区分两类 caster：

```text
hasRegularShadowCasters
hasIndirectShadowCasters
```

只有二者都不存在时才上传 disabled globals。

当常规 caster 为空但 indirect caster 存在时：

- 仍创建 / 清空 realtime shadow atlas。
- 仍计算 cascade view / projection / split sphere。
- 仍上传 receiver globals。
- 向 `MainLightShadowIndirectCasterContext` 注册 atlas target。
- 跳过 `ScriptableRenderContext.DrawShadows(...)` 的常规绘制。

这避免了 indirect-only 场景被错误判定为“无阴影”。

### 4. Cached Static + Dynamic Overlay 支持 indirect-only bootstrap

`MainLightShadowStaticCachePass` 现在会分别查询：

```text
hasIndirectStaticCasters
hasIndirectDynamicCasters
```

当常规 static caster bounds 为空，但存在 static 或 dynamic indirect caster 时，允许建立一个有效的空 static cache：

```csharp
allowEmptyAtlas: hasIndirectStaticCasters || hasIndirectDynamicCasters
```

这样 dynamic overlay 后续可以把 static atlas copy 到 combined atlas，再让 indirect dynamic caster 写入 combined atlas。

该修复覆盖了树叶这类动态 indirect caster：即使没有普通 Cube、角色或其他常规 caster，也不会因为 static cache 初始化失败而导致整条主光阴影链路 disabled。

### 5. Shadow atlas 工具增加 allowEmptyAtlas

`MainLightShadowPassUtils.RenderMainLightShadowAtlas(...)` 增加：

```csharp
bool allowEmptyAtlas = false
```

默认仍保持旧行为：没有常规 caster 就返回 `false`。

只有主光阴影调用方明确传入 `allowEmptyAtlas = true` 时，才允许在无常规 caster 的情况下返回成功，并依赖已有 cascade 数据让 indirect shadow pass 后续写入。

这个设计避免了把“空 atlas 也算有效”的语义扩散到所有 shadow 调用点。

### 6. SceneView / realtime 相机路径补洞

初次修复后，仍发现一个二次漏洞：Game Camera cached path 可以被 indirect caster 引导，但 SceneView 或非 cached realtime 相机路径仍可能因为没有常规 caster 而把主光阴影 globals 写成 disabled。

本阶段补齐三处：

- `MainLightShadowCasterPass` 的 indirect caster 查询允许 SceneView 使用已注册的 indirect caster。
- `VegetationIndirectShadowFeature` / `VegetationIndirectShadowPass` 允许 SceneView 在编辑器内运行 indirect shadow pass。
- `NWRPFeatureScheduler` 的 runtime `VegetationIndirectShadowFeature` 入队条件也认识 SceneView，避免 pass 内部条件写对但 feature 根本没有入队。

这部分只在 `UNITY_EDITOR` 下影响 SceneView，不改变 Player 运行时平台策略。

## 不采用的方案

本阶段明确不采用以下 workaround：

- 不在场景里保留隐藏 Cube。
- 不恢复 CPU `ShadowsOnly` source renderer fallback 作为正式方案。
- 不通过创建不可见常规 `MeshRenderer` 来欺骗 `CullingResults`。
- 不新增 shader keyword。
- 不改 Tree / TreeLeaf shader variant。
- 不把草 / 灌木默认纳入投影。

正式方案仍是主光 shadow atlas 生命周期识别 indirect caster，并由 `VegetationIndirectShadowPass` 写入 atlas。

## 性能与移动端策略

CPU：

- 新增查询是 provider 级轻量遍历，只判断是否存在可提交 group。
- 不引入大规模 CPU per-instance shadow loop。
- 不恢复源 `MeshRenderer` 的逐帧 ShadowsOnly fallback。
- 不新增 renderer 主流程中的 monolithic 逻辑。

GPU：

- 不新增额外 shadow RT，继续写入已有 main-light atlas / combined atlas。
- 不新增 shader variant。
- 额外成本只在存在 indirect caster 且主光阴影启用时发生。
- 每个 cascade 仍需要 vegetation shadow culling dispatch 和 indirect shadow draw。
- TreeLeaf alpha clip 仍是 shadow atlas overdraw 的主要风险点。

移动端取舍：

- 仍建议控制 cascade count 和 atlas resolution。
- 仍不将草默认纳入投影，避免 alpha clip + 大面积 overdraw 推高成本。
- additional punctual light shadow 不接入 vegetation indirect shadow。
- SceneView 特例只存在于编辑器，不影响 Android / iOS Player。

## 当前场景边界

`Map_LoopForest` 当前实际 indirect shadow provider 中观察到的有效 shadow draw 主要是：

```text
Dynamic | NewWorld/Env/TreeLeaf
```

这意味着：

- 树叶可以作为 dynamic indirect caster 写入 dynamic overlay / realtime atlas。
- 树干静态组当前没有进入 indirect shadow provider；如果后续希望树干也走 static indirect cache，需要让树干 renderer / material 被 `VegetationIndirectRenderer` 正确采集，并满足 `NewWorld/Env/Tree` + `ShadowCaster` pass 条件。
- `Shrub` 当前不在 indirect shadow allow-list，也没有默认纳入本阶段投影范围。

所以本阶段解决的是“删 Cube 后主光阴影系统被错误关闭”的逻辑漏洞，不等同于把所有植被类型自动纳入阴影 caster。

## 测试与验证

新增 focused EditMode 测试 assembly：

```text
Assets/NWRP/Tests/ShadowBootstrapEditor/NWRP.ShadowBootstrap.Editor.Tests.asmdef
```

覆盖内容：

- registry 能报告已注册 indirect caster。
- realtime main-light path 不会在 indirect-only caster 存在时上传 disabled globals。
- cached static/dynamic path 能为 indirect-only caster 建立 empty static cache。
- no-caster / no-indirect 的默认路径仍保持 disabled。
- SceneView realtime path、vegetation shadow feature、vegetation shadow pass 和 scheduler 均具备 indirect shadow 入队 / 执行合同。

已完成静态验证：

```text
git diff --check
通过
```

```text
dotnet build NWRP.Runtime.csproj --no-restore
0 warnings / 0 errors
```

```text
dotnet build NWRP.ShadowBootstrap.Editor.Tests.csproj --no-restore
0 warnings / 0 errors
```

运行期探针曾确认过以下状态：

```text
providers = 2
draws = 117
targets = 1
shadowmap = NWRP_MainLightShadows_CombinedShadowmap
cascadeCount = 2
_MainLightShadowParams.x > 0
```

未完成项：

- Unity MCP 在本轮后半段开始返回 `Response data is null`，因此最后的 Play Mode 自动验证未能重新执行。
- 新增 SceneView 合同测试已通过 C# 编译，但 Unity Test Runner 未能在 MCP 恢复前重新跑完整 EditMode suite。
- 仍需要在 Unity Editor 恢复后手动验证 `Map_LoopForest` 删除 Cube 的冷启动场景。

## 手动验证清单

恢复 Unity Editor / MCP 后建议验证：

1. 打开 `Assets/NewWorld/ArtResources/Scenes_3.0/Map_LoopForest.unity`。
2. 确认场景中没有临时 Cube caster。
3. 进入 Play Mode。
4. 检查主光 shadow globals：
   - `_MainLightShadowParams.x > 0`
   - `_MainLightShadowCascadeCount > 0`
   - `_MainLightShadowmapTexture` 不是 `NWRP_MainLightShadows_EmptyShadowmap`
5. Frame Debugger 中确认：
   - `Main Light Shadows`
   - `Render Vegetation Indirect Shadows`
6. 添加 / 删除 Cube 后，indirect tree shadow 不应再随 Cube 是否存在而开关。
7. 分别观察 Game View 与 SceneView，避免 SceneView realtime path 再次覆盖 disabled globals。

## 后续建议

- 为 `VegetationIndirectRenderer` 增加轻量 debug counter，统计 provider count、target count、draw count、visible instance count，但默认关闭。
- 如果需要树干静态阴影，优先整理树木 source prefab / renderer 分组，让 trunk 使用 `NewWorld/Env/Tree` 并进入 static indirect cache，而不是扩大草 / 灌木投影范围。
- 后续可以把 shadow cascade culling 做成更贴合 chunk / cluster 的 GPU 预剔除，减少每 cascade 每 group 的 dispatch 压力。
- 不建议恢复 ShadowsOnly fallback；它会重新把大规模植被投影带回 CPU renderer/culling 路径，不符合当前 GPU-driven 方向。
