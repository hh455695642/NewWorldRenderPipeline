# Phase45 NWRP Feature 模块化与可插拔目录收敛

日期: `2026-05-28`

## 概要

本阶段整理 NWRP 的 feature 架构边界，目标不是新增渲染效果，而是让内置必要 feature 与可插拔 feature 的职责、文件位置和查找规则更清楚。

最终约定:

- 只有 `CloudShadowProjector`、`ScreenBlur`、`ValleyHeightFog`、`ValleyHeightFogOverlay` 属于当前可插拔 feature。
- 可插拔 feature 统一放在 `Assets/NWRP/Runtime/PluggableFeatures/<FeatureName>`。
- `PluggableFeatures` 下不再拆 `Environment`、`PostProcessing` 等分类目录，避免功能归类争议和查找成本。
- 其他 renderer 必要 feature 仍然保留在原有 runtime domain 文件夹，例如 `CameraTextures`、`Fog`、`MainLightShadows`、`PostProcessing`、`VegetationIndirectShadows`。
- feature 文件名与 feature 类型名保持一致，减少 `NWRPScreenBlurFeature` / `ScreenBlurFeature` 这类查找偏差。

本阶段同时引入了 feature metadata、runtime feature store、built-in feature catalog 和 feature scheduler，使 `NWRPRenderer` 不再直接硬编码大量具体 feature 类型。

## 问题背景

此前 NWRP 已经逐步接近 URP 风格的 renderer feature 列表，但存在两个结构问题:

1. 可插拔 feature 和内置必要 feature 混在同一套目录迁移逻辑里，容易让所有 `NWRPFeature` 都看起来像“插件”。
2. 部分文件名、类型名和功能名不一致，例如 Screen Blur 的新 feature 语义已经是 `ScreenBlurFeature`，但旧文件名仍带 `NWRP` 前缀，检索和维护成本较高。

本阶段将“可插拔”严格收敛为当前四个明确功能:

```text
CloudShadowProjector
ScreenBlur
ValleyHeightFog
ValleyHeightFogOverlay
```

这些功能由 renderer data 显式挂载，属于可增删、可开关、可按项目需要组合的扩展模块。其他 feature 是 NWRP renderer 的基础能力或运行时必要调度点，继续按原 domain 管理，避免把核心渲染链路伪装成插件系统。

## 修改文件

核心架构:

- `Assets/NWRP/Runtime/NWRPFeatureMetadata.cs`
- `Assets/NWRP/Runtime/NWRPRuntimeFeatureStore.cs`
- `Assets/NWRP/Runtime/NWRPBuiltInFeatureCatalog.cs`
- `Assets/NWRP/Runtime/NWRPFeatureScheduler.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPRendererData.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`

可插拔 feature 目录:

- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/CloudShadowProjectorFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/NWRPCloudShadowProjector.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/ScreenBlurFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/NWRPScreenBlur.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Passes/ScreenBlurPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Compatibility/NWRPScreenBlurFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/ValleyHeightFogFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/NWRPValleyHeightFog.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay/ValleyHeightFogOverlayFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay/Passes/ValleyHeightFogOverlayPass.cs`

内置 feature 保持 domain 目录:

- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowFeature.cs`
- `Assets/NWRP/Runtime/CameraTextures/DepthTextureFeature.cs`
- `Assets/NWRP/Runtime/CameraTextures/OpaqueTextureFeature.cs`
- `Assets/NWRP/Runtime/Fog/FogFeature.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs`
- `Assets/NWRP/Runtime/Outlines/OutlineFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/PostProcessFeature.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/VegetationIndirectShadowFeature.cs`

规则与测试:

- `AGENTS.md`
- `Assets/NWRP/Runtime/AGENTS.md`
- `Assets/NWRP/Tests/EditMode/NWRPFeatureArchitectureTests.cs`

## 核心实现

### 1. Feature Metadata

新增 `NWRPFeatureMetadataAttribute`，让 feature 自己声明:

```text
DisplayName
MenuPath
AllowMultiple
VolumeDriven
ShowInAddMenu
```

Renderer Data Inspector 的 Add Feature 菜单不再依赖一组硬编码按钮，而是读取 metadata 生成显示名称、菜单路径和重复添加策略。

这样做的直接收益:

- 新 feature 增加菜单入口时，只需要在 feature 类型上声明 metadata。
- editor 侧不用持续追加 `AddXxxFeature` 分支。
- 可插拔 feature 与内置 feature 的显示策略可以由类型自己表达。

### 2. Runtime Feature Store

新增 `NWRPRuntimeFeatureStore`，用于复用运行时内置 feature 实例。

内置 feature 不应该每帧临时创建，也不应该散落在 `NWRPRenderer` 的私有字段和 `GetOrCreateXxxFeature` 方法里。现在由 store 按类型复用实例，并统一设置:

```text
HideFlags.HideAndDontSave
Runtime feature name
DisposeAll
```

这让 renderer 生命周期更集中，也减少了内置 feature 后续扩展时对主 renderer 文件的侵入。

### 3. Built-in Feature Catalog

新增 `NWRPBuiltInFeatureCatalog`，负责组织 renderer 必要 feature。

每个内置 feature 通过 `INWRPSerializedFeatureStateProvider` 声明自己如何从 `NewWorldRenderPipelineAsset` 或 `NWRPRendererData` 读取启用状态。Catalog 只负责收集和调度，不再用 `feature is MainLightShadowFeature` 这类类型判断硬编码状态规则。

当前保持在内置路径的 feature 包括:

- Main Light Shadows
- Additional Light Shadows
- Depth Texture
- Opaque Texture
- Outline
- Fog
- Post Process
- Vegetation Indirect Shadows

这些是 renderer 基础链路、资源生成或默认渲染能力的一部分，不放入 `PluggableFeatures`。

### 4. Feature Scheduler

新增 `NWRPFeatureScheduler`，把 feature 的执行调度从 `NWRPRenderer` 主流程中拆出。

职责:

- 按 feature active 状态收集 target requirements。
- 按 pass event 对 pass 排序入队。
- 统一处理 renderer data feature list 与 runtime built-in feature list。

这样 `NWRPRenderer` 继续负责 camera render orchestration，而具体 feature 策略由 scheduler/catalog 处理，主流程不再持续膨胀。

### 5. 可插拔 Feature 目录收敛

最终目录采用扁平结构:

```text
Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector
Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur
Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog
Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay
```

没有继续保留:

```text
PluggableFeatures/Environment
PluggableFeatures/PostProcessing
```

原因是这四个功能本身就是一个明确的“可插拔集合”，再套一层分类会引入语义争议。例如 Valley Height Fog 可以被理解为环境效果，Cloud Shadow Projector 也可以被理解为屏幕空间后处理。直接按 feature 名管理，查找和移动都更稳定。

### 6. 命名兼容

Screen Blur 新类型名统一为:

```text
ScreenBlurFeature
ScreenBlurPass
```

保留兼容 shim:

```text
NWRPScreenBlurFeature : ScreenBlurFeature
```

该 shim 放在:

```text
Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Compatibility
```

用途是兼容旧序列化资产或 editor 反射路径。新代码和测试均指向 `ScreenBlurFeature` / `ScreenBlurPass`。

Fog 内置 feature 也收敛为:

```text
FogFeature
```

文件位于:

```text
Assets/NWRP/Runtime/Fog/FogFeature.cs
```

## 设计取舍

### 不把所有 NWRPFeature 都叫 Pluggable

`NWRPFeature` 是执行模型，不等于产品层面的“可插拔模块”。

内置 feature 仍然可以用 `NWRPFeature + NWRPPass` 形态实现，但它们属于 renderer 基础能力，不应该放在插件目录里。这样可以避免后续开发者误以为 Main Light Shadow、Depth Texture、PostProcess 等核心能力可以像视觉效果一样随意移除。

### 不按 Environment / PostProcessing 分类

本阶段明确取消可插拔 feature 下的分类目录。

原因:

- 分类会把讨论焦点从“feature 名是什么”转移到“它算哪类效果”。
- 一些效果天然跨域，例如 Cloud Shadow Projector 是环境表现，但执行方式是屏幕空间投影。
- 对维护者来说，`PluggableFeatures/ScreenBlur` 比 `PluggableFeatures/PostProcessing/ScreenBlur` 更直接。

### 不移动内置必要 feature

此前过宽的目录规划会让 Additional Light Shadows、Camera Textures、Fog、PostProcess 等也进入统一 feature 目录。最终改回原 domain 目录，符合当前 NWRP 的所有权边界:

- shadow 相关仍在 `MainLightShadows` / `AdditionalLightShadows`。
- camera texture 相关仍在 `CameraTextures`。
- 后处理主 pass 仍在 `PostProcessing`。
- 植被间接阴影仍在 `VegetationIndirectShadows`。

### 不引入 URP Feature 类型

本阶段只整理 NWRP 自己的 feature 系统，没有引入:

```text
UnityEngine.Rendering.Universal
ScriptableRendererFeature
ScriptableRenderPass
```

NWRP 继续保持 custom SRP 边界。

## 性能与 Variant

本阶段主要是架构和文件组织调整，不改变渲染算法。

CPU:

- `NWRPRenderer` 中具体 feature 类型判断减少。
- 内置 runtime feature 通过 store 复用，避免分散生命周期管理。
- Editor Add Feature 菜单通过 metadata 生成，减少手写分支。
- 不引入 per-object CPU loop。

GPU:

- 不新增 RenderPass。
- 不新增 fullscreen blit。
- 不新增 RenderTexture 分配。
- 不改变 Shadow / Depth / PostProcess 的实际渲染成本。

Shader Variant:

- 新增 keyword 数量: `0`
- 新增 `shader_feature`: `0`
- 新增 `multi_compile`: `0`
- 不改变现有 shader include 和 pass tag。

移动端风险较低。本阶段的主要风险来自 Unity asset 路径迁移和序列化引用，需要依赖 `.meta` GUID、兼容 shim 和编译验证兜底。

## 验证记录

静态结构检查:

```text
PluggableFeatures direct folders:
CloudShadowProjector, ScreenBlur, ValleyHeightFog, ValleyHeightFogOverlay
```

确认没有残留:

```text
PluggableFeatures/Environment
PluggableFeatures/PostProcessing
Assets/NWRP/Runtime/Features
NWRPScreenBlurPass
```

URP 边界检查:

```text
Assets/NWRP Runtime / ShaderLibrary / Shaders 源码中未新增
UnityEngine.Rendering.Universal
ScriptableRendererFeature
ScriptableRenderPass
Packages/com.unity.render-pipelines.universal shader include
```

编译验证:

```text
dotnet build NWRP.Runtime.csproj -nologo --no-restore
0 warnings / 0 errors
```

```text
dotnet build NWRP.Editor.csproj -nologo --no-restore
0 errors
4 warnings
```

Editor warnings 包含既有 Unity/NuGet assembly 冲突，以及 `NWRPScreenBlurFeature` compatibility shim 的 obsolete warning。

```text
dotnet build NWRP.Tests.EditMode.csproj -nologo --no-restore
0 errors
3 warnings
```

```text
dotnet build NWRP.Editor.Tests.csproj -nologo --no-restore
0 errors
3 warnings
```

Unity MCP:

```text
Cannot connect to Unity MCP server at 127.0.0.1:8080
```

因此本阶段没有跑到最新 Unity Test Runner。改用 `Editor.log` 检查最新扁平化导入后的编译状态，未发现新的 `error CS` / `Shader error`。日志中仍有既有 URP renderer asset 报错:

```text
URP-HighFidelity-Renderer is missing RendererFeatures
```

该报错来自 URP renderer asset 校验，不属于本阶段 NWRP feature 目录调整引入的运行时代码错误。

## 当前限制与后续方向

- `NWRPScreenBlurFeature` shim 仍会产生 obsolete warning；等旧序列化资产完成迁移后可以删除。
- 可插拔 feature 当前只锁定四个，后续新增 feature 时应先判断它是 renderer 基础能力还是项目可选效果，再决定是否进入 `PluggableFeatures`。
- 如果后续需要在可插拔集合内继续分组，优先考虑 editor 菜单 metadata，而不是恢复物理目录分类。
- Unity Test Runner 需要 MCP server 或 Editor 测试环境恢复后再跑一次完整回归。

## 后续优化补充

本阶段后续收尾继续保持“不改变渲染行为”的约束，主要处理 feature 架构改造后的 CPU/GC 与序列化清洁度问题:

- `NWRPFeatureMetadataUtility` 增加 metadata 缓存，避免 render loop 和 editor 菜单反复通过 reflection 读取 feature attribute。
- `NWRPBuiltInFeatureCatalog` / `NWRPFeatureScheduler` 移除每次扫描 serialized feature list 时创建的临时 `HashSet<Type>`，改为列表内小规模线性重复检查。
- `NWRPRendererDataEditor.AddScreenBlurFeature` 改为创建 `ScreenBlurFeature`，旧 `NWRPScreenBlurFeature` 只保留为序列化兼容 shim。
- `FeatureSettings` 增加 null feature 清理路径，并在 pipeline asset serialize / validate 与 renderer data validate 时清掉无效引用。
- `Assets/Settings/NewWorldRP.asset` 的 legacy pipeline-level `featureSettings.features` 清理为 `[]`，renderer data 下四个 pluggable feature sub-asset 引用保持不变。

新增测试覆盖:

- Add Feature 菜单只暴露 `CloudShadowProjectorFeature`、`ScreenBlurFeature`、`ValleyHeightFogFeature`、`ValleyHeightFogOverlayFeature`。
- `NWRPScreenBlurFeature` shim 继承当前 `ScreenBlurFeature`，但不再作为新增 feature 的创建目标。
- `NWRPRendererData.OnValidate` 和 `NewWorldRenderPipelineAsset.OnBeforeSerialize` 会移除 null feature entries。
