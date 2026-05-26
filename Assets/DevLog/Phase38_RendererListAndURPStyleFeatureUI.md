# Phase38 Renderer List and URP-Style Feature UI

日期：`2026-05-26`

## 概要

本阶段为 NWRP 增加了接近 URP 语义的 Renderer List，并同步整理 Explicit Feature 的创建入口和 Inspector 体验。

核心目标不是引入 URP 依赖，也不是把 NWRP 改造成多 renderer 实例架构，而是在现有自定义 SRP 的 `NWRPFeature / NWRPPass` 模型上补齐 renderer data 层：

- `NewWorldRenderPipelineAsset` 持有 `NWRPRendererData[] rendererDataList` 和 `defaultRendererIndex`。
- `NWRPCameraData` 持有 per-camera `RendererIndex`，`-1` 表示使用默认 renderer。
- `NWRPRendererData` 承载 renderer-local 的 feature/pass 组合，包括内置 feature toggle、explicit feature list 和 runtime feature cache。
- General、Lighting、Shadow、Platform 这类全局预算仍保留在 pipeline asset 上，避免移动端全局成本被拆散到多个 renderer data。
- 运行时仍复用一个 `NWRPRenderer` 调度器，只在每个 camera 渲染前解析当前使用的 `NWRPRendererData` 并写入 `NWRPFrameData`。

本阶段同时调整了 Feature 创建逻辑：

- Project Create 菜单不再暴露 `Rendering/NWRP Features/...`。
- `ValleyHeightFogFeature` 不再作为独立 project asset 手动创建。
- 需要 explicit feature 时，在 `NWRP Renderer Data` Inspector 里通过 `Add Feature > Valley Height Fog` 创建 renderer-local sub-asset。
- Explicit Features 列表改为 URP-style 行 UI：拖拽排序、展开、启用 toggle、object field、Select、删除。

最后对 UI 细节做了收口：feature 行保留 `ReorderableList` 默认拖拽手柄，展开三角和 enable toggle 向右错开；去掉重复的 name label，由 object field 显示 feature asset 名称。

## 参考背景

本阶段延续以下 DevLog 已建立的边界：

- `Phase5_PassFeatureFramework.md`：NWRP 功能通过 `NWRPFeature / NWRPPass` 扩展，不回写成主流程里的特殊分支。
- `Phase24_URPCompatibleNamingAndDependencyClosure.md`：NWRP runtime/editor 不依赖 `UnityEngine.Rendering.Universal`。
- `Phase35_ValleyHeightFog_PostTransparentFeature.md`：Valley Height Fog 是 explicit feature，运行参数由 Volume 控制，pass event 保持 `NWRPPassEvent.AfterTransparent`。
- `Phase37_ValleyHeightFogAssetCleanup.md`：清理历史重复 sub-asset 后，本阶段进一步把 Valley feature 生命周期收敛到 renderer data 内部。

## 修改范围

### Runtime

新增：

- `Assets/NWRP/Runtime/NWRPRendererData.cs`

修改：

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Runtime/NWRPCameraData.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipeline.cs`
- `Assets/NWRP/Runtime/CameraRenderer.cs`

同步调整 built-in feature create menu：

- `Assets/NWRP/Runtime/CameraTextures/DepthTextureFeature.cs`
- `Assets/NWRP/Runtime/CameraTextures/OpaqueTextureFeature.cs`
- `Assets/NWRP/Runtime/Fog/NWRPFogFeature.cs`
- `Assets/NWRP/Runtime/Outlines/OutlineFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/PostProcessFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/ValleyHeightFogFeature.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowFeature.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/VegetationIndirectShadowFeature.cs`

### Editor

新增：

- `Assets/NWRP/Editor/Cameras/NWRPCameraDataEditor.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPFeatureEditor.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`

修改：

- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`

### Tests

新增：

- `Assets/NWRP/Tests/EditMode/RendererListTests.cs`
- `Assets/NWRP/Tests/EditMode/RendererDataFeatureEditorTests.cs`

### Assets

修改：

- `Assets/Settings/NewWorldRP.asset`

当前 pipeline asset 已包含默认 renderer data sub-asset：`NWRP Default Renderer`。它是 renderer list 的 index 0，也是默认 renderer。历史上容易误解的 `NWRP Renderer Data 1` 不再作为强制默认项保留。

## Renderer Data 设计

`NWRPRendererData` 是 renderer-local 配置资产，职责是描述某个 camera 应使用哪组 feature/pass 组合。

当前它承载：

- `FeatureSettings featureSettings`
- `List<NWRPFeature> Features`
- `EnableOpaqueTexture`
- `EnableDepthTexture`
- `DepthTextureCopyModeSetting`
- `EnableOutline`
- `EnableVegetationIndirectTreeShadows`
- renderer-local runtime feature cache

这不是一个新的渲染调度器实例。实际调度仍由同一个 `NWRPRenderer` 完成，renderer data 只提供当前 camera 的 feature 数据源。

这样做的取舍：

- 避免多个 `NWRPRenderer` 实例各自维护 RTHandle cache 和中间 RT，降低移动端内存膨胀风险。
- 保持 camera 之间可以切换不同 feature/pass 组合。
- 不把全局 HDR、render scale、shadow atlas、主光预算、附加光预算拆成 per-renderer 配置，避免项目后期预算失控。

## Pipeline Asset Renderer List

`NewWorldRenderPipelineAsset` 新增 renderer list API：

```text
int DefaultRendererIndex
int RendererDataCount
NWRPRendererData GetRendererData(int index)
NWRPRendererData GetRendererDataForCamera(Camera camera, out int resolvedIndex)
bool ValidateRendererData(int index)
bool ValidateRendererDataList(bool requireAllValid = false)
```

索引语义：

- `-1`：使用默认 renderer。
- 有效索引：使用 `rendererDataList[index]`。
- 无效索引：fallback 到默认 renderer。
- 默认 renderer 缺失或 index 越界：尝试使用第一个有效 renderer data。
- renderer list 为空的 legacy asset：运行时生成 `HideAndDontSave` fallback renderer data，并从旧 `featureSettings` 复制设置。

这保证旧资产不会因为没有 renderer list 而中断渲染。

## Camera Renderer 选择

`NWRPCameraData` 新增：

```text
int RendererIndex
void SetRenderer(int index)
```

`RendererIndex = -1` 是默认值，表示跟随 pipeline asset 的 default renderer。

`NWRPCameraDataEditor` 在 Camera 的 NWRP 数据组件上显示 renderer 下拉：

```text
Default Renderer (0: NWRP Default Renderer)
0: NWRP Default Renderer
1: ...
```

如果 camera 指向缺失 renderer，会在运行时 fallback default；Editor 侧也会提示 renderer 缺失。

## FrameData 与 Runtime 调度

`NWRPFrameData` 新增当前 camera resolved renderer data。

渲染入口大致变为：

```text
Camera
  -> NewWorldRenderPipelineAsset.GetRendererDataForCamera(camera, out index)
  -> frameData.rendererData = resolved renderer data
  -> NWRPRenderer.Render(...)
```

`NWRPRenderer` 中以下逻辑改为从 `frameData.rendererData` 读取：

- 内置 feature toggle
- explicit feature list
- target requirement 收集
- feature pass enqueue

因此同一帧内不同 camera 可以使用不同 renderer data，而不会创建多个 renderer 调度器。

## Explicit Feature 去重

运行时对 singleton explicit feature 做轻量防重。

当前主要针对 `ValleyHeightFogFeature`：

- 同一个 renderer data 中正常 UI 不允许添加重复 Valley feature。
- 如果旧 YAML 或手动编辑造成重复，runtime enqueue 阶段会跳过后续重复项。
- target requirement 收集也做同样防重，避免重复 feature 造成重复 depth/intermediate color 请求。

这属于兼容性保护，不是鼓励同类 feature 多实例化。Valley Height Fog 的参数仍由 Volume 控制，一个 renderer data 中保留一个开关入口即可。

## Shadow Cache 兼容

`MarkMainLightShadowCacheDirty()` 和 `ClearMainLightShadowCache()` 不再只看旧 pipeline asset explicit feature list。

当前逻辑会遍历所有有效 renderer data 中的 explicit shadow feature：

- 如果 renderer data 内有显式 shadow feature，则对这些 feature 的 runtime cache 执行 dirty/clear。
- 如果没有显式 feature，则继续使用 pipeline asset 的全局 runtime shadow feature。

这保持了旧资产兼容，也允许未来某些 renderer data 显式配置 shadow feature。

## Create Menu 清理

Project Create 菜单最终保留：

```text
Assets > Create > Rendering > New World Render Pipeline Asset
Assets > Create > Rendering > NWRP Renderer Data
```

移除：

```text
Assets > Create > Rendering > NWRP Features > ...
```

涉及的 built-in feature 包括：

- Depth Texture
- Opaque Texture
- Outline
- Fog
- Post Process
- Main Light Shadow
- Additional Light Shadow
- Vegetation Indirect Shadow
- Valley Height Fog

原因：

- Depth/Opaque/Outline 等当前仍是 renderer data 上的内置 toggle，不应该作为 project asset 创建后让用户误以为会自动生效。
- Valley Height Fog 虽然仍是 `NWRPFeature` 类型，但 renderer feature 是否执行应由 renderer data 的 explicit list 决定。
- 独立创建一个 feature asset 不会自动进入任何 renderer data，容易产生“创建了但没效果”的误解。

静态检查结果：

```text
rg "Rendering/NWRP Features" Assets/NWRP/Runtime Assets/NWRP/Editor
无结果

rg "CreateAssetMenu|Rendering/NWRP Renderer Data|Rendering/New World Render Pipeline Asset" Assets/NWRP/Runtime Assets/NWRP/Editor
只剩 NWRPRendererData 和 NewWorldRenderPipelineAsset
```

## Renderer Data Inspector

`NWRPRendererDataEditor` 负责 renderer-local 配置。

Inspector 分区：

- Built-in Features
- Explicit Features

Built-in Features 仍显示现有内置 toggle：

- Opaque Texture
- Depth Texture
- Outline
- Vegetation Indirect Tree Shadows

本阶段没有把这些内置开关迁移成 explicit feature 行。这样可以避免一次性改变过多运行时语义。

Explicit Features 区域说明：

- 只有引用到该列表的 `NWRPFeature` 才会被这个 renderer data 执行。
- `Add Feature` v1 只提供 `Valley Height Fog`。
- 添加时创建为当前 renderer data 的 sub-asset，并自动加入 `featureSettings.features`。
- 同一个 renderer data 内只允许一个 `ValleyHeightFogFeature`。
- 删除 owned sub-asset 时同步销毁 sub-asset。
- 删除外部 feature asset 引用时只移除 list entry，不删除外部文件。

展开 feature 后：

- 跳过 `m_Script`。
- 跳过基类 `isEnabled`，因为 enable 已在行头显示。
- 如果没有 renderer-local 参数，显示提示：参数由 Volume 控制。

Valley Height Fog 当前就属于这种情况：feature asset 只决定该 renderer 是否允许 enqueue pass，具体雾效参数完全由 Volume 控制。

## URP-Style Feature 行 UI

Explicit Features 使用 `ReorderableList` 绘制。

每行包含：

```text
drag handle | foldout | enable toggle | feature object field | Select | remove
```

本阶段根据实际 Inspector 反馈做了两次细化：

- 为 `ReorderableList` 默认拖拽手柄预留 `18px`，避免拖拽手柄和 foldout 三角重叠。
- 去掉重复的 `nameRect` 文本列，避免与 object field 中的 feature 名称重复。

最终 object field 获得更多横向空间，Inspector 行结构更接近 URP 的 Renderer Feature 列表。

## Standalone Feature Inspector

`NWRPFeatureEditor` 用于处理历史上已经独立存在的 feature asset。

选中任意 `NWRPFeature` asset 时：

- 显示它是否被当前 active NWRP asset 的某个 renderer data 引用。
- 如果未被引用，显示 warning：该 feature 不会自动执行，需要添加到 Renderer Data。
- 如果被引用，显示引用它的 renderer data 名称和索引。

这个 editor 不改变 runtime 行为，只减少误操作和误解。

## Pipeline Asset Inspector

`NewWorldRenderPipelineAssetEditor` 增加 `Renderer List`。

列表行为：

- 显示 index、renderer data object field、Set Default、Select。
- 禁止删除当前 default renderer。
- reorder 时同步修正 `defaultRendererIndex`。
- 删除非默认项后 clamp default index。
- 打开旧 asset 时，可自动迁移出默认 `NWRPRendererData` sub-asset，并复制旧 `featureSettings`。

`NewWorldRP.asset` 已迁移为：

```text
rendererDataList[0] -> NWRP Default Renderer
defaultRendererIndex = 0
```

这意味着默认 renderer 是 `NWRP Default Renderer`，而不是历史遗留的 `NWRP Renderer Data 1`。

## 使用方式

新增 renderer data：

```text
Assets > Create > Rendering > NWRP Renderer Data
```

添加到 pipeline asset：

```text
Project Settings / Graphics 使用的 NewWorldRP.asset
  -> Renderer List
  -> +
  -> 指定 NWRP Renderer Data
```

设置默认 renderer：

```text
Renderer List 中点击 Set Default
```

给相机指定 renderer：

```text
Camera
  -> NWRPCameraData
  -> Renderer
  -> Default Renderer (...) 或 0/1/2...
```

添加 Valley Height Fog：

```text
NWRP Renderer Data
  -> Explicit Features
  -> Add Feature
  -> Valley Height Fog
```

注意：Valley Height Fog 的浓度、高度、颜色、算法模式仍由 Volume 组件控制。Renderer Data 中的 feature 行只控制该 renderer 是否允许执行这条 pass。

## 兼容性与序列化

旧 `NewWorldRenderPipelineAsset.featureSettings` 没有删除。

它现在作为 legacy bridge：

- 旧资产没有 renderer list 时，用于创建 fallback renderer data。
- Editor migration 创建默认 renderer data 时，会复制旧设置。
- 保留字段名可以避免旧 YAML 直接丢失数据。

renderer data 缺失时的 fallback 规则：

```text
camera renderer index == -1 -> default renderer
camera renderer index invalid -> default renderer
default renderer invalid -> first valid renderer
no valid renderer data -> HideAndDontSave fallback renderer data
```

删除 explicit feature 时的资产规则：

- renderer data 自己拥有的 sub-asset：可以随 list entry 删除。
- 外部 `.asset` feature：只解除引用，不删除文件。

这样可以避免误删跨 renderer data 或跨 asset 复用的资源。

补充修复：

- 在 `NewWorldRP.asset` 的 Renderer List 中新增 renderer data sub-asset 后，立即 `SaveAssets + ImportAsset`，保证 Project 视图立刻显示新建的 renderer data。
- 删除 pipeline asset 拥有的 renderer data sub-asset 时，先清理它独占引用的 owned feature sub-asset，避免 `Valley Height Fog Feature` 这类 renderer-local feature 变成 Project 视图里的孤儿对象。
- 外部 feature asset 不会被删除；如果同一个 pipeline asset 里的其他 renderer data 仍引用某个 owned feature sub-asset，也不会销毁该 feature。
- 当前 `Assets/Settings/NewWorldRP.asset` 中由复现 bug 留下的未引用 `Valley Height Fog Feature` sub-asset 已清理，只保留默认 renderer data 引用的有效实例。

## 架构影响

本阶段没有改变 pass event 合约。

仍然遵守：

```text
BeforeShadowMap
ShadowMap
BeforeDepthPrepass
DepthPrepass
BeforeOpaque
Opaque
Skybox
BeforeTransparent
Transparent
AfterTransparent
PostProcess
DebugOverlay
```

Renderer Data 只决定哪些 feature/pass 被 enqueue，不引入新的排序体系。

本阶段也没有把 built-in feature 全部强制拆成 explicit feature 条目。Depth/Opaque/Outline/Vegetation 仍走现有内置 toggle，后续如需 URP-style 全量 feature 列表，可单独做迁移，避免这次改动扩大运行时风险。

## 性能与移动端影响

CPU：

- 每个 camera 增加一次 renderer data 解析。
- 无新增多 renderer 实例。
- 无新增 per-camera 大规模对象创建。
- fallback renderer data 只用于 legacy asset 兼容，不作为常规每帧创建路径。

GPU：

- 无新增 RenderPass。
- 无新增 RenderTexture。
- 无新增 fullscreen blit。
- 无新增 shader global。
- 无新增 shader keyword。

内存：

- 保持一个 `NWRPRenderer` 调度器，避免多个 renderer 实例带来 RTHandle cache 和中间 RT 膨胀。
- renderer data 只保存配置和 runtime feature cache，规模远小于独立 renderer 实例。

移动端：

- Android / iOS runtime pass 成本只由当前 renderer data 启用的 feature 决定。
- Renderer List 本身不会增加 tile GPU 带宽压力。
- Valley Height Fog 仍然只有在 renderer data 启用 feature 且 Volume active 时才进入实际 pass。

## Shader Variant

本阶段不修改 shader。

Variant 风险：

- 无新增 `multi_compile`。
- 无新增 `shader_feature_local`。
- 无新增 URP keyword。
- 无新增 shader include。
- 无新增 shader family。

Valley Height Fog 的算法选择仍由 C# 选择 hidden shader pass index 完成，不通过 keyword 组合控制。

## URP 依赖边界

本阶段没有新增 URP runtime/editor 依赖。

静态检查：

```text
rg "UnityEngine.Rendering.Universal" Assets/NWRP/Runtime Assets/NWRP/Editor
```

结果只命中 `Assets/NWRP/Runtime/AGENTS.md` 中的禁止规则说明，没有 NWRP runtime/editor 代码依赖 URP namespace。

Renderer List 的语义参考 URP，但实现完全使用 NWRP 自有类型：

- `NWRPRendererData`
- `NWRPFeature`
- `NWRPPass`
- `NWRPRenderer`
- `NWRPFrameData`

没有使用：

- `ScriptableRendererFeature`
- `ScriptableRenderPass`
- `UniversalRenderPipelineAsset`

## 验证记录

静态检查：

```text
rg "Rendering/NWRP Features" Assets/NWRP/Runtime Assets/NWRP/Editor
无结果

rg "UnityEngine.Rendering.Universal" Assets/NWRP/Runtime Assets/NWRP/Editor
只命中 AGENTS.md 规则说明
```

构建验证：

```text
dotnet build NWRP.Runtime.csproj --no-restore
dotnet build NWRP.Editor.csproj --no-restore
dotnet build NWRP.EditModeTests.csproj --no-restore
git diff --check
```

结果：

- Runtime build 通过。
- Editor build 通过。
- EditModeTests build 通过。
- `git diff --check` 通过，仅有 Git CRLF 提示。
- Editor build 中仍有项目已有的 Unity/NuGet assembly version warning，不是本阶段新增错误。

本阶段后续针对 Inspector UI 布局又重新执行：

```text
dotnet build NWRP.Editor.csproj --no-restore -v:minimal
git diff --check
```

结果同样通过。

针对 renderer data / feature sub-asset 生命周期补充执行：

```text
dotnet build NWRP.Editor.csproj --no-restore -v:minimal
dotnet build NWRP.EditModeTests.csproj --no-restore -v:minimal
Unity EditMode tests: NWRP.Tests
```

结果：

```text
TotalTests: 9
PassedTests: 9
FailedTests: 0
SkippedTests: 0
```

后续如进入合并阶段，建议再在 Unity Editor 内手动检查 `NWRP Renderer Data` Inspector 的行布局，以及 Project 视图中 renderer data / feature sub-asset 的显示与删除行为。

## 后续建议

- 如果要继续贴近 URP Renderer Feature 体验，可以后续把 Depth/Opaque/Outline 等 built-in toggle 逐步迁移为 renderer data 内的 feature 行，但需要单独设计兼容层，避免旧 asset 成本语义突变。
- 如果未来允许多个 explicit feature 类型，需要建立 feature type registry 或 Add Feature 类型菜单，而不是继续在 `NWRPRendererDataEditor` 中硬编码所有类型。
- 对 singleton feature 建议保留 runtime 防重，即使 Editor 已经限制，也能防止手写 YAML 或旧资产造成重复 enqueue。
- Renderer Data 不应承载全局移动端预算。Shadow atlas、render scale、HDR、主光策略仍应保持 pipeline-global，除非后续有明确的多平台配置需求和 profiling 数据支撑。
