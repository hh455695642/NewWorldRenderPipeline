# NewWorld Render Pipeline (NWRP)

NewWorld Render Pipeline, 简称 NWRP，是一个基于 Unity SRP 架构实现的自定义轻量化渲染管线。当前主线项目使用 Unity `2022.3.62f2`，目标平台优先面向 Android / iOS，并以移动端 Tile-Based GPU 的带宽、RenderTarget 切换和 shader variant 成本作为主要约束。

仓库另有 Unity `6.3` 维护分支 `origin/codex/unity-6.3-migration`。该分支与当前 Unity 2022.3 版本保持功能对齐，主要差异应限制在 Unity 版本升级、API 兼容、包版本和必要的平台适配上；渲染架构、Feature/Pass 边界、移动端性能策略和 shader variant 控制原则应保持一致。

NWRP 不是 Built-in Render Pipeline，也不是 URP `ScriptableRendererFeature` / `ScriptableRenderPass` 实现。仓库中保留 URP 包主要用于测试、参考和 shader 迁移工作；NWRP 自身的 runtime、shader 和 feature 系统保持自研 custom SRP 边界。

## 项目定位

- 渲染路径：custom Scriptable Render Pipeline。
- 当前主线引擎版本：Unity `2022.3.62f2`。
- Unity 6.3 对齐分支：`origin/codex/unity-6.3-migration`，功能与当前版本对齐。
- 主要平台：Android、iOS。
- 优先级：移动端性能、跨 GPU 兼容性、长期可扩展性、复杂度可控。
- 核心策略：Feature / Pass 模块化，所有高成本能力显式开关，避免把渲染流程集中进单体 renderer。
- 移动端基线：优先减少 DrawCall、SetPass、overdraw、fullscreen blit、RenderTexture 分配和高分辨率中间 RT。

## 目录结构

```text
Assets/NWRP/Runtime
```

管线 runtime。根目录保留核心类型，例如 `NewWorldRenderPipeline`、`NWRPRenderer`、`NWRPFeature`、`NWRPPass`、`NWRPFrameData`、`NWRPShaderIds`、`NewWorldRenderPipelineAsset`。具体功能按 domain 分组，例如 `MainLightShadows`、`AdditionalLightShadows`、`CameraTextures`、`PostProcessing`、`VegetationIndirectRendering`。

```text
Assets/NWRP/ShaderLibrary
```

NWRP 共享 HLSL 库。包含空间变换、输入常量、光照、阴影、BRDF、全局光照、雾、深度重建、相机纹理声明和可复用 pass include。

```text
Assets/NWRP/Shaders
```

NWRP 自有 material-facing shaders、post-process shaders、debug shaders、utility blit shaders 和 compute shaders。环境、植被、Lit、NPR、Unlit、Effect 等 shader family 分开维护，避免做跨场景类型的超级 shader。

```text
Assets/NWRP/Editor
```

Inspector、材质工具、shader GUI、camera/light editor 扩展。Editor 代码按 Pipeline、Shaders、PostProcessing、Lighting、Cameras、Materials 分组。

```text
Assets/NWRP/Tests
```

测试场景、测试材质、测试资源和 EditMode 测试。重点覆盖 fullscreen chain、tile-based frame graph、target requirement、depth-driven blit shader contract 等管线约束。

```text
Assets/Settings/NewWorldRP.asset
```

项目当前使用的 NWRP pipeline asset。

## 整体渲染框架

NWRP 的主链路是：

```text
NewWorldRenderPipeline
  -> NWRPRenderer
    -> NWRPFeatureScheduler
      -> NWRPPass
```

关键文件：

- `Assets/NWRP/Runtime/NewWorldRenderPipeline.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPFeatureScheduler.cs`

每个 camera 的主要流程：

```text
BeginCameraRendering
GetRendererDataForCamera
Cull
ConfigureCameraData
ResolveCameraRenderScale
ConfigureFrameTargets
BuildPassQueue
AnalyzeFrameGraph
ExecutePassQueue
ReleaseFrameTargets
Submit
EndCameraRendering
```

`NewWorldRenderPipeline` 负责 Unity SRP 入口、逐相机调用和生命周期管理。`NWRPRenderer` 负责 culling、相机目标、内置 pass、pass queue、frame graph 分析和提交。`NWRPFeatureScheduler` 负责收集 feature target requirements，并把序列化 feature 与 runtime 内置 feature 统一入队。`NWRPPass` 是实际渲染工作单元，长期复用，避免每帧临时创建对象。

`CameraRenderer` 目前仅作为兼容 facade 保留，实际渲染由 `NWRPRenderer` 执行。

## Pass 顺序契约

NWRP 使用 `NWRPPassEvent` 作为全局 pass 顺序契约。新 pass 必须进入这个顺序表，除非存在明确的硬渲染依赖。

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
AfterValleyHeightFog
BeforePostProcess
PostProcess
AfterPostProcess
DebugOverlay
```

同一事件内按 enqueue 顺序保持稳定。不要在 feature 内使用 ad-hoc 排序规则，也不要通过隐藏耦合假定其他 feature 的内部执行细节。Pass 间通信应显式使用 `NWRPFrameData`、全局 shader 参数、命名 RT 或 feature 自己声明的 frame target requirement。

## Renderer Data 与 Pipeline Asset

`NewWorldRenderPipelineAsset` 是全局 pipeline asset，负责创建 pipeline、持有 renderer data 列表、全局渲染能力开关和共享设置。典型设置包括 SRP Batcher、GPU Instancing、HDR、Render Scale、主光阴影、额外光阴影、诊断统计等。

`NWRPRendererData` 是 renderer 级配置。它管理 layer filtering、内置 feature settings、pluggable feature 列表，以及 renderer 级 runtime feature store。相机可通过 `NWRPCameraData.RendererIndex` 选择 renderer data；未指定时使用 pipeline 默认 renderer。

`NWRPCameraData` 是相机扩展组件，负责：

- 是否启用 NWRP post-processing。
- render scale 模式：pipeline default、force native、override。
- volume layer mask 和 volume trigger。
- renderer data index。

这个分层避免把所有设置堆进 pipeline asset，同时允许不同 camera 使用不同 renderer 配置。

## Feature / Pass 扩展模型

`NWRPFeature` 是 ScriptableObject 级 feature 抽象。一个 feature 可以创建并入队一个或多个 `NWRPPass`，并可通过 `TryGetFrameTargetRequirements` 提前声明对 camera color、camera depth、depth texture、opaque texture 或 intermediate target 的需求。

`NWRPPass` 是实际执行单元，包含 `passEvent`、debug name、profiling sampler 和资源使用声明。Pass 需要尽量长期复用，避免每帧分配。

Feature 来源分为两类：

- 内置 runtime feature：由 pipeline asset 或 renderer data 自动创建，例如 main light shadows、additional light shadows、depth texture、opaque texture、fog、post process。
- 序列化 feature list：挂在 `NWRPRendererData.Features` 上，适合可插拔功能，例如 `CloudShadowProjectorFeature`、`ScreenBlurFeature`、`ValleyHeightFogFeature`、`ValleyHeightFogOverlayFeature`。

`NWRPRuntimeFeatureStore` 按类型复用 runtime feature 实例，并使用 `HideFlags.HideAndDontSave` 管理生命周期。`NWRPFeatureMetadata` 为 editor add menu、排序、显示名、多实例规则和 volume-driven 标记提供元数据。`NWRPBuiltInFeatureCatalog` 记录序列化 feature 是否已经覆盖某个内置能力，避免重复调度。

当前可插拔 feature 目录限定为：

```text
Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector
Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur
Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog
Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay
```

## 核心功能

### 相机目标、Depth Texture 与 Opaque Texture

NWRP 会根据 camera、renderer data、feature requirement 和 render scale 决定是否使用 intermediate color/depth。`DepthTextureFeature` 可在 opaques 后、transparents 后或 force prepass 模式下生成 `_CameraDepthTexture`。`OpaqueTextureFeature` 在需要时复制 camera color 到 `_CameraOpaqueTexture`。

这两个资源都有显式策略：`Off`、`AutoFeatureOnly`、`Force`。默认应避免强制开启，因为它们通常意味着额外 RT、copy 或 prepass 成本。

### 主光阴影

`MainLightShadowFeature` 支持一个主方向光阴影路径，移动端基线为稳定、可控的 directional shadow。

- 支持 1-2 cascades。
- 支持 `Hard` 与显式选择的 `MediumPCF`。
- 支持 realtime atlas。
- 支持 cached static shadow atlas，加可选 dynamic overlay。
- SceneView / Preview camera 会走兼容路径，Game camera 可使用 cached static + dynamic overlay。
- Debug view 可在 receiver 上显示最终主光 shadow source tint，不额外增加 fullscreen pass。

主光阴影 receiver 数据通过 `Shadows.hlsl` 与 `Lighting.hlsl` 消费。Bias 区分 caster depth/normal bias 与 receiver-side bias，避免把内部 raster baseline 暴露成公共调参入口。

### 额外光阴影

`AdditionalLightShadowFeature` 负责小预算 punctual light 实时阴影。

- 支持 spot light 和 point light。
- Spot 占一个 atlas slice，point 占六个 cubemap-face slices。
- 按 camera 距离、light 类型和 shadow caster bounds 筛选候选光。
- 共享 shadow atlas，tile resolution 和 atlas max size 由 asset 控制。
- 支持 `Hard` 与 `MediumPCF`。

额外光阴影是显式高成本功能，不应成为移动端默认多光源阴影路径。

### Fog

`FogFeature` 在 `BeforeOpaque` 上传雾参数。材质 shader 通过共享库计算 fog factor，并在 fragment 末尾混合。Fog 由 volume/component 状态驱动，避免在每个 shader 中堆叠 keyword。

### Outline

`OutlineFeature` 在 `Opaque` 阶段绘制 `NewWorldOutline` pass。移动端默认应保持关闭，只在项目材质明确需要 shell outline 时启用。

### PostProcess

`PostProcessFeature` 使用统一 `PostProcessPass`，对外保持一个 pass，内部处理多个效果：

- Bloom：内部 pyramid，下采样/模糊/上采样/compose。
- Tonemapping：Linear、ACES、ACES Fitted、AGX。
- Color Adjustments。
- Vignette。
- FXAA。

`PostProcessPass` 使用 `NWRPFullscreenChain` 执行最终 composite。若它是 camera color 的最后使用者，并且目标是 Game camera backbuffer，可直接 present 到 backbuffer，减少一次 copy-back。

### Screen Blur

`ScreenBlurFeature` 是 pluggable volume-driven feature，可注入 `BeforePostProcess` 或 `AfterPostProcess`。它走 fullscreen chain，并提前声明 intermediate color 需求。Blur 使用横向和纵向 pass，适合明确画面需求，不应作为常驻默认效果。

### Valley Height Fog

`ValleyHeightFogFeature` 是 depth-driven fullscreen effect，通常在 `AfterTransparent` 执行。它需要 `_CameraDepthTexture`，用于根据深度重建世界空间位置并叠加山谷高度雾。`ValleyHeightFogOverlayFeature` 在 `AfterValleyHeightFog` 提供对应 overlay pass。

### Cloud Shadow Projector

`CloudShadowProjectorFeature` 是 pluggable 屏幕空间投影效果，在 `AfterTransparent` 执行。它根据深度和投影参数把云影叠加到场景中，需要明确声明 depth texture 和 intermediate color 成本。

### Vegetation Indirect Rendering

`VegetationIndirectRenderer` 是大规模植被 GPU-driven 路径：

- 使用 chunk/group 组织实例。
- 使用 compute shader 做 GPU culling。
- 使用 `ComputeBuffer` / append buffer 管理可见实例。
- 使用 `Graphics.RenderMeshIndirect` 提交 indirect draw。
- Shader 通过 procedural instancing 读取 instance matrix 和数据。

该路径避免 CPU per-instance for-loop 驱动大规模绘制。关闭时保留源 MeshRenderer 作为 fallback。

### Vegetation Indirect Shadows

`VegetationIndirectShadowFeature` 延迟到主光 shadow target 建立后接入 `ShadowMap` 阶段。主光 shadow pass 会把 cascade target 注册给 `MainLightShadowIndirectCasterContext`，植被 shadow pass 再用 indirect draw 写入主光 shadow atlas。这样可复用主光 cascade、atlas 和 receiver 数据，不为植被单独引入另一套阴影系统。

## Shader 与材质体系

NWRP shader 使用自有 ShaderLibrary，不应包含 URP package shader include。标准 pass/tag 包括：

```text
NewWorldForward
ShadowCaster
DepthOnly
NewWorldOutline
NewWorldUnlit
```

`NewWorld/Lit/StandardLit` 是主要 lit shader 示例，包含 forward、shadow caster 和 depth only pass，并复用 shared pass include。环境 shader 独立维护 grass、tree、tree leaf、shrub、lake 等材质族，避免植被、角色、特效共用一个大而全 shader。

Shader 编写原则：

- 默认支持 GPU Instancing，常规材质 pass 使用 `#pragma multi_compile_instancing`。
- 大规模植被使用 procedural instancing，例如 `#pragma instancing_options procedural:SetupInstancing`。
- 移动端颜色和 lighting math 优先使用 `half`；世界空间位置、矩阵、深度重建保留 `float`。
- runtime 强度、阈值、开关优先使用 uniform。
- 可选功能优先使用 `shader_feature_local`，避免无约束 `multi_compile`。
- 高成本且低频的模式应拆成独立 shader 或独立 pass，而不是叠加 keyword 组合。

当前 keyword 风险主要来自 instancing variant、utility blit local keywords、少量环境 shader local feature。新增 shader 时必须评估 variant 数量，移动端目标应保持可预测且受控。

## 移动端性能策略

NWRP 按 Tile-Based GPU 优先设计。通常带宽和 RT 切换比 ALU 更敏感。

关键策略：

- 尽量减少 fullscreen pass 数量。
- 避免不必要的 `_CameraDepthTexture` 和 `_CameraOpaqueTexture`。
- 避免高分辨率中间 RT 链。
- 避免 repeated blit 和无意义 copy-back。
- 尽量让最后一个 fullscreen pass 直接写 backbuffer。
- HDR color 优先使用低带宽格式，例如支持时使用 `B10G11R11_UFloatPack32`。
- render scale 支持 pipeline default 和 per-camera override，UI camera 可 force native。
- dynamic batching 已移除；SRP Batcher 和 GPU Instancing 是默认方向。

`NWRPFrameGraphAnalyzer` 会根据 pass resource usage 统计 camera attachment cluster、camera color last use、final present pass、depth last use 等信息。`NWRPFullscreenChain` 复用少量临时 RT，并在最后 pass 可 present 时减少中间写回。`Log Frame Debug Stats` 可输出每相机 RT bind、fullscreen blit、copy、temp RT、final fusion 等诊断数据。

## 如何扩展 NWRP

新增 runtime 功能时按以下规则执行：

- 使用一个明确职责的 `NWRPFeature`。
- 使用一个或多个聚焦的 `NWRPPass`。
- 在 `NewWorldRenderPipelineAsset` 或 `NWRPRendererData` 上提供显式开关或配置。
- 通过 `TryGetFrameTargetRequirements` 提前声明 depth/opaque/intermediate 需求。
- 使用现有 `NWRPPassEvent`，不要发明私有排序。
- Pass 间通信使用 frame data、shader globals、命名 target 或明确上下文对象。
- 不把无关系统塞进一个超级 feature。
- 不把新功能直接侵入 `NWRPRenderer` 主流程，除非有清楚的管线级依赖。

新增 shader 时按以下规则执行：

- 使用 NWRP ShaderLibrary。
- 使用 NWRP pass tags。
- 支持 instancing。
- 优先使用 `half`，谨慎使用高精度。
- 避免 geometry shader 和移动端不通用特性。
- 标注 keyword 使用、variant 风险和移动端成本。
- 植被、角色、特效、UI 不共用一个超级 shader。

新增 screen-space effect 时：

- 优先接入 `NWRPFullscreenChain`。
- 不在 pass 内偷偷分配 camera depth / opaque 依赖。
- 尽量单 pass 或可融合最终输出。
- 只在视觉结果确实需要时请求 depth texture 或 opaque texture。

## 调试与验证

推荐验证路径：

- Unity Console：确认无 C# compile error 和 shader error。
- Frame Debugger：确认 pass 顺序符合 `NWRPPassEvent`，并检查是否出现额外 fullscreen blit / RT switch。
- RenderDoc：检查 shadow atlas、fullscreen pass、depth/opaque texture 和 tile flush 风险。
- Profiler：关注 DrawCall、SetPass、GC Alloc、RenderTexture allocation、GPU time。
- EditMode tests：运行 `NWRP.Tests.EditMode`，覆盖 frame graph、target requirements、fullscreen chain 等契约。
- Sample scenes：检查 `Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity` 与 `Assets/NWRP/Tests/Scenes/NWRPArtIntroLookDev/NWRPArtIntroLookDev.unity`。
- Frame debug log：临时开启 `Log Frame Debug Stats`，记录 per-camera fullscreen、copy、temp RT 和 final fusion 统计。

如果 Unity Editor 不可用，至少应做静态检查：

- Pass tag 与 renderer drawing settings 是否一致。
- Runtime shader global ID 与 shader property 是否一致。
- Serialized field 名称是否兼容现有 asset。
- NWRP runtime/shader 是否误引入 URP runtime 依赖。

## Public APIs / Interfaces

本文档变更不新增、不删除、不修改任何 public API、serialized field、shader property 或 asset schema。README 只描述当前 NWRP 架构和使用边界，不改变运行时行为。

## 当前边界与注意事项

- NWRP runtime 和 shader 不依赖 URP runtime；不要在 `Assets/NWRP` 中引入 `UnityEngine.Rendering.Universal`。
- 不要为 NWRP 功能实现 URP `ScriptableRendererFeature` 或 `ScriptableRenderPass`。
- 不要迁回 Built-in Render Pipeline。
- 不要重新引入 dynamic batching。
- 不要把多光源实时阴影作为移动端默认路径。
- 不要在 fullscreen effect 中隐藏 depth / opaque texture 成本。
- 不要新增无约束 shader keyword；新增 keyword 必须有明确理由和 variant 数量评估。
- 不要在大规模渲染中使用 CPU per-instance loop。
- 不要创建跨植被、角色、特效、UI 的超级 shader 或超级 feature。

NWRP 的长期方向是保持渲染能力模块化、成本显式、移动端可控，并让新功能以清晰 Feature / Pass 边界叠加，而不是让主渲染流程继续膨胀。
