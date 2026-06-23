# Phase53 Mobile Bandwidth Phase3 Depth Consumer 与 Blit Orientation 收口

日期：`2026-06-23`

## 概要

本阶段接在 Phase51 / Phase52 之后，继续收口 NWRP 面向移动端 tile-based GPU 的低带宽基线，但重点从“减少资源成本”转向“保证资源需求驱动后的功能正确性”。

Phase52 已经把 Valley Height Fog、Cloud Shadow Projector、Screen Blur、PostProcess 等 fullscreen pass 接入统一 helper 和轻量 frame graph 语义，并将 `_CameraDepthTexture` 改成由 renderer setting 或 feature requirement 驱动。落地后暴露出几个契约问题：

- `Depth Texture` 从全局强制开关改成 `Off / AutoFeatureOnly / Force` 后，ValleyFog / CloudShadow 在不同 policy、SceneView、GameView、PlayMode / EditMode 下行为不一致。
- 当 `copyDepthMode = AfterTransparents` 且 CloudShadow / ValleyFog 也是 `AfterTransparent` 附近的 consumer 时，serialized feature 顺序可能让 consumer 先于 `CopyDepth` 入队。
- CloudShadow 与 ValleyFog 同时启用时，二者顺序必须稳定，否则后一个效果会覆盖或读到错误的 camera color 状态。
- depth-driven fullscreen shader 不能再直接用 Blitter source UV 做 depth/world reconstruction；但 GameView final backbuffer 路径又必须跟随 `_ScaleBiasRt` 的 source orientation，否则会出现上下颠倒。

本阶段的目标不是继续扩大 feature fusion，也不是引入 Unity RenderGraph，而是把上述资源和方向契约固定下来，让移动端低带宽 baseline 在功能上可用、可测、可追踪。

## 修改文件

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Runtime/NWRPRendererData.cs`
- `Assets/NWRP/Runtime/NWRPFeatureScheduler.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPFrameResources.cs`
- `Assets/NWRP/Runtime/NWRPFrameDebugStats.cs`
- `Assets/NWRP/Runtime/NWRPCameraAttachmentPolicy.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/Passes/NWRPFullscreenPassUtils.cs`
- `Assets/NWRP/Runtime/CameraTextures/OpaqueTextureFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/CloudShadowProjectorFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/ValleyHeightFogFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/Lighting/AdditionalLightUtils.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowIndirectCasterContext.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDynamicOverlayPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs`
- `Assets/NWRP/ShaderLibrary/DepthWorldReconstructionBlit.hlsl`
- `Assets/NWRP/Shaders/PostProcess/NWRP_ValleyHeightFog.shader`
- `Assets/NWRP/Shaders/Environment/CloudShadowProjector.shader`
- `Assets/NWRP/Shaders/Utils/CoreBlit.shader`
- `Assets/NWRP/Shaders/Utils/CoreBlitColorAndDepth.shader`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`
- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/Settings/NewWorldRP.asset`
- `Assets/NWRP/Tests/EditMode/TBDRSettingsTests.cs`
- `Assets/NWRP/Tests/EditMode/TBDRFrameGraphTests.cs`
- `Assets/NWRP/Tests/EditMode/TBDRTargetRequirementTests.cs`
- `Assets/NWRP/Tests/EditMode/DepthDrivenBlitShaderContractTests.cs`

## 问题背景

### 1. `Depth Texture Off` 必须绝对禁止 `_CameraDepthTexture`

Phase52 把 depth texture 需求变成 feature requirement 后，理论路径应为：

```text
无 depth consumer -> 不创建 _CameraDepthTexture
ValleyFog / CloudShadow active + AutoFeatureOnly -> 声明 depth texture consumer
ValleyFog / CloudShadow active + Off -> 不声明 depth texture，效果跳过
Force -> 每帧强制创建 _CameraDepthTexture
```

实际问题是，旧的 boolean 语义和新的 policy 语义混在一起后，`Off` 一度被解释成“不强制生成，但 active feature 仍可请求”。这会导致用户在 Inspector 中选择 `Off` 后，Frame Debugger 里仍然出现 `CopyDepth`，ValleyFog / CloudShadow 也仍然渲染，语义不直观。

本阶段将 policy 语义收口为：

- `OpaqueTexture Off`：不强制 copy `_CameraOpaqueTexture`。
- `DepthTexture Off`：绝对禁止 `_CameraDepthTexture`，active depth consumer 不得触发 `CopyDepth`。
- `DepthTexture AutoFeatureOnly`：默认移动端路径，仅在 active feature 需要时创建。
- `Force`：兼容旧行为，每个 camera 强制创建对应 camera texture。

这样 `Off` 就是功能禁用和带宽禁用；需要 ValleyFog / CloudShadow 自动工作时，应使用 `AutoFeatureOnly`。

### 2. 后处理 capability 不能屏蔽 depth consumer volume 解析

Valley Height Fog 和 Cloud Shadow Projector 当前是 NWRP pluggable fullscreen feature，不应被 `supportsPostProcessing` 直接判死。否则资产关闭全局 PostProcessing 后，volume stack 里的 fog / cloud 仍存在，但 frame data 不会解析出 active consumer，scheduler 也无法请求 depth texture。

本阶段把 depth consumer volume 解析从 post process capability 路径中拆出。即使 `supportsPostProcessing = false`，也会解析：

```text
NWRPValleyHeightFog
NWRPCloudShadowProjector
```

这样低带宽 baseline 可以关闭后处理总开关，同时仍允许显式 feature 独立运行。

### 3. 同一 `passEvent` 下需要稳定 feature 顺序

`copyDepthMode = AfterTransparents` 时，`DepthTextureFeature` 产生的 `CopyDepthPass` 和 CloudShadow / ValleyFog consumer 可能处于相近甚至相同的 event 区间。只靠 serialized feature list 顺序会带来不稳定行为：

```text
CloudShadow / ValleyFog 先执行 -> 采样旧 depth 或空 depth
CopyDepth 后执行 -> 本帧 consumer 已错过正确数据
```

本阶段让 scheduler 对可处理 feature 使用 `NWRPFeatureMetadata.sortOrder` 排序，并在需要 depth texture 时优先 enqueue serialized `DepthTextureFeature`。如果 renderer data 中没有 serialized depth feature，则 fallback 到 runtime feature。

同时锁定 CloudShadow 在 ValleyFog 前执行，保证两者同时启用时，ValleyFog 基于已经写回 camera color 的 cloud shadow 结果继续叠加。

### 4. Source UV 与 Screen UV 必须分离

ValleyFog / CloudShadow 都是读取 camera color、采样 depth、重建 world position 的 fullscreen shader。Blitter 传入的 `input.texcoord` 表示 source color UV，已经包含 source scale/bias；而 `input.positionCS` 表示 destination pixel position。

旧路径直接用同一个 `uv` 同时处理：

```hlsl
sceneColor = SampleSource(uv);
rawDepth = SampleSceneDepth(uv);
positionWS = ComputeSceneWorldSpacePosition(uv, rawDepth);
```

在 final backbuffer 或 SceneView / GameView 方向不一致时，这会让 color source 与 depth/world reconstruction 对不上，表现为 ValleyFog / CloudShadow 上下颠倒、只在某个视图正确、PlayMode 和非 PlayMode 行为不同。

本阶段把 shader 契约改为：

```hlsl
float2 sourceUV = input.texcoord.xy;
float2 screenUV = GetBlitScreenUV(input.positionCS);

half4 sceneColor = SampleSource(sourceUV);
float rawDepth = SampleSceneDepth(screenUV);
float3 positionWS = ComputeSceneWorldSpacePosition(screenUV, rawDepth);
```

这能避免直接拿 Blitter source UV 做 depth reconstruction。

### 5. GameView final backbuffer 仍必须跟随 `_ScaleBiasRt`

排查过程中曾尝试让 `GetBlitScreenUV` 完全绕开 `_ScaleBiasRt`，只使用 raw `positionCS.xy / _ScaledScreenParams.xy`。这个判断是不完整的。

在 Unity Blitter 语义里：

- `input.texcoord` 是 source UV。
- `positionCS` 是 destination pixel。
- GameView final backbuffer 在 top-left 平台上会通过 `_ScaleBiasRt` 让 source orientation 与最终 present 对齐。

因此 depth/world reconstruction 不能直接使用 raw source UV，但也不能完全忽略 `_ScaleBiasRt`。正确做法是：由 `positionCS` 建立 screen UV，再在 `UNITY_UV_STARTS_AT_TOP` 平台按 `_ScaleBiasRt` 修正 Y，使 depth/world reconstruction 与最终 color source 采样同一个 camera-space pixel。

## 关键实现

### 1. Camera Texture Policy

新增：

```csharp
public enum CameraTexturePolicy
{
    Off = 0,
    AutoFeatureOnly = 1,
    Force = 2
}
```

`OpaqueTextureSettings` 和 `DepthTextureSettings` 都接入该 policy。旧的 `enableOpaqueTexture` / `enableDepthTexture` 字段保留为 hidden legacy 字段，运行时 `EnableOpaqueTexture` / `EnableDepthTexture` 现在对应 `ShouldForceTexture`。

默认移动端语义：

```text
OpaqueTexture = Off
DepthTexture = AutoFeatureOnly
MobileFullscreenBudget = On
```

这样默认不再因为 asset 全局开关产生 `_CameraOpaqueTexture` 或 `_CameraDepthTexture` copy；只有 feature requirement 或显式 `Force` 才进入高带宽路径。

### 2. Feature Requirement 驱动 depth texture

ValleyFog / CloudShadow 在 active 时通过 `TryGetFrameTargetRequirements(...)` 声明：

```text
requiresDepthTexture = true
requiresDepthTextureCopy = true
requiresIntermediateColor = true
```

Scheduler 收集所有 feature requirements 后，决定是否入队 `DepthTextureFeature`。这让 depth texture 的来源回到统一系统，而不是由 feature 私自持有 `CopyDepthPass` 或 `DepthPrepass`。

本阶段特别修正了 `supportsPostProcessing = false` 时的行为：depth consumer volume 仍会被解析；但只有 depth policy 不是 `Off` 时，feature 才能声明 requirement。

### 3. Scheduler 排序和 serialized DepthTextureFeature 优先

`NWRPFeatureScheduler` 增加 sorted processable feature indices：

```text
NWRPFeatureMetadata.sortOrder
serialized index as tie-breaker
```

需要 depth texture 时，scheduler 会先尝试从 renderer data 的 serialized feature list 中找到 `DepthTextureFeature` 并入队；找不到时才创建 runtime `DepthTextureFeature`。

这样可以保证：

- serialized depth feature 的配置仍被尊重。
- `AfterTransparents` depth copy 在同 event consumer 前执行。
- CloudShadow 按 metadata 排在 ValleyFog 前。
- `DepthTexture Off + active consumer` 不再触发 depth requirement，consumer pass 也不入队。

### 4. 轻量 FrameGraph 从统计推进到 lifetime contract

新增 `NWRPFrameResources.cs`，包含：

- `NWRPFrameResourceDesc`
- `NWRPFrameResourceHandle`
- `NWRPTransientResourceAllocator`
- `NWRPFrameGraphAnalyzer`

当前不是 Unity RenderGraph，也不接管所有 RT 分配。它的职责是先建立可测试的生命周期模型：

```text
logical transient color count
physical transient color count
camera color final present pass
camera depth last use pass
render pass cluster count
can discard camera depth after last use
```

`NWRPFullscreenPassUtils.AllocateTempColor(...)` 会把 fullscreen temp color 分配记录到 transient allocator，debug stats 输出逻辑/物理 transient 数量。

`NWRPRenderer.AnalyzeFrameGraph(...)` 改成收集 pass usage 后交给 `NWRPFrameGraphAnalyzer`，并把 usage index 映射回真实 queued pass index。这样 `currentPassIndex` 可以用于判断 depth 最后一次使用点。

### 5. Camera depth last-use store action

`NWRPCameraAttachmentPolicy` 扩展：

```text
BeginCameraColor
ContinueCameraColor
LastCameraDepthUse
FinalBackBufferWrite
```

当 `RestoreCameraRenderTarget(...)` 发现当前 pass 是 camera depth 最后使用点，且 frame graph 允许 discard 时，depth store 使用 `DontCare`，并记录：

```text
discardedDepthStore
```

这仍是保守策略，只在 frame graph 明确有 last-use 信息时触发。目标是让 tile-based GPU 上的 depth attachment 不再无条件写回外部内存。

### 6. Fullscreen `_ScaleBiasRt` 下沉到 target 绑定点

`NWRPFullscreenPassUtils` 在写不同目标前显式设置 `_ScaleBiasRt`：

- 写临时 RT / camera color：`isGameBackBufferTarget = false`
- 写 Game camera backbuffer：`isGameBackBufferTarget = true`

`DepthWorldReconstructionBlit.hlsl` 新增：

```hlsl
float2 GetBlitScreenUV(float4 positionCS)
{
    float2 uv = positionCS.xy * rcp(_ScaledScreenParams.xy);
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - (uv.y * _ScaleBiasRt.x + _ScaleBiasRt.y);
#endif
    return uv;
}
```

ValleyFog / CloudShadow shader 统一改成 source UV 与 screen UV 分离。最终行为：

- color 采样继续使用 Blitter source UV。
- depth 采样和 world reconstruction 使用 `GetBlitScreenUV(input.positionCS)`。
- GameView final backbuffer 的 Y 方向通过 `_ScaleBiasRt` 对齐。
- SceneView / temp RT 路径保持各自的 orientation。

### 7. 默认资产进入移动低带宽 baseline

`Assets/Settings/NewWorldRP.asset` 调整为移动端保守默认：

```text
supportsHDR = false
supportsPostProcessing = false
enableRenderScale = false
OpaqueTexture = Off
DepthTexture = AutoFeatureOnly
MobileFullscreenBudget = On
Bloom max mip = 4
Bloom base size = 512
Mobile max additional lights = 4
MainLightShadowFilter = Hard
AdditionalLightShadows = Off
AdditionalLightShadowFilter = Hard
```

Lookdev 能力仍保留，但需要显式开启，避免默认 runtime baseline 带着 HDR、PostProcess、RenderScale、OpaqueTexture、DepthTexture 和额外光阴影一起跑。

### 8. 额外光与主光阴影带宽收口

`AdditionalLightUtils` 增加移动端上传上限和重要性排序：

- `EnableMobileFullscreenBudget` 开启时使用 `MobileMaxAdditionalLights`。
- 默认上限为 4。
- 按 camera 到 light 的距离和 luminance 估算重要性，优先上传近且亮的 punctual lights。
- 不增加 per-object lighting CPU loop，只收敛已有 visible light upload。

主光 cached shadow dynamic overlay 调整为只有存在真实 dynamic caster 或 pending indirect dynamic overlay 时才 copy static atlas 到 combined atlas。没有 dynamic overlay 消费者时，不再每帧无条件 static atlas copy。

## 性能与移动端策略

### CPU

- Feature 排序只处理 renderer data 中可处理 feature 列表，数量很小。
- FrameGraph analyzer 是线性扫描 pass usage。
- Transient allocator 只记录 fullscreen temp lifetime，不扫描材质或场景对象。
- 额外光排序发生在已有 visible light upload 阶段，且被 `MaxAdditionalLights` / mobile asset limit 约束。
- 没有新增 per-instance / per-object CPU 大循环。

### GPU

- 默认资产关闭 HDR、PostProcess、RenderScale、OpaqueTexture 和额外光阴影，减少 baseline 外部带宽。
- 无 depth consumer 时不创建 `_CameraDepthTexture`，避免无意义 `CopyDepth`。
- Active ValleyFog / CloudShadow 通过统一 depth feature 获取 depth，不产生私有 hidden copy。
- fullscreen temp color 进入 transient lifetime 统计，为后续真实 alias / pass fusion 提供数据。
- camera depth last-use 后允许 `StoreAction.DontCare`，降低 tile depth store 风险。
- dynamic overlay 阴影 atlas copy 改为按需触发。

### Tile-Based GPU 取舍

本阶段优先保证低带宽路径的正确性，不把多个 screen-space effect 提前合并成一个超级 shader。这样可以避免在未完成视觉验证和真机 profiling 前，把 ValleyFog、CloudShadow、PostProcess、ScreenBlur 的维护边界耦合在一起。

当前状态仍然是 custom SRP 内部轻量资源图与调度约束，不是完整 native render pass/subpass 系统。

## Shader Variant 影响

本阶段没有新增业务 shader keyword：

```text
新增 multi_compile: 0
新增 shader_feature_local: 0
新增全局 keyword: 0
```

ValleyFog / CloudShadow 的修复通过 uniform `_ScaleBiasRt` 和 shared HLSL helper 完成。Camera texture policy、feature order、transient lifetime、depth last-use 都在 C# 调度层表达，不通过 shader variant 表达。

`CoreBlit` / `CoreBlitColorAndDepth` 仍保持工具 shader 职责，没有为本阶段增加新的业务功能组合。

## 测试覆盖

新增 EditMode 测试目录：

```text
Assets/NWRP/Tests/EditMode
```

### `TBDRSettingsTests`

覆盖：

- OpaqueTexture 默认 `Off`。
- DepthTexture 默认 `AutoFeatureOnly`。
- Bloom mobile budget 固定 `mipCount = 4`、`baseSize = 512`。
- mobile additional light upload 默认上限为 4。

### `TBDRFrameGraphTests`

覆盖：

- frame graph 能选出最后一个可 present pass。
- camera depth last-use 能被识别。
- render pass cluster count 可统计。
- transient allocator 在 lifetime 不重叠时复用同一 physical resource。

### `TBDRTargetRequirementTests`

覆盖：

- ValleyFog 在 `supportsPostProcessing = false` 时仍声明 depth texture requirement。
- CloudShadow 在 `AutoFeatureOnly` 下声明 depth texture requirement。
- CloudShadow / ValleyFog active 且 depth policy 为 `Off` 时，不请求 depth texture。
- CloudShadow / ValleyFog active 且 depth policy 为 `AutoFeatureOnly` 时，才通过 feature requirement 请求 depth texture。
- 无 depth consumer 且 policy 为 `Off` 时，不请求 depth texture。
- `AfterTransparents` copy depth 必须排在 `AfterTransparent` consumer 前。
- CloudShadow 必须按 feature sort order 排在 ValleyFog 前。
- ConfigureCameraData 在关闭 post process capability 时仍能解析 ValleyFog / CloudShadow volume active 状态。

### `DepthDrivenBlitShaderContractTests`

覆盖：

- ValleyFog / CloudShadow shader 必须使用 `GetBlitScreenUV(input.positionCS)` 做 depth/world reconstruction。
- 不允许重新用 raw source `uv` 直接采样 depth 或重建 world。
- `GetBlitScreenUV` 必须保留 `_ScaleBiasRt` 修正，避免 GameView final backbuffer 路径再次上下颠倒。

## 验证记录

已完成 Unity EditMode 测试：

```text
TotalTests: 71
PassedTests: 71
FailedTests: 0
SkippedTests: 0
```

已完成 Runtime 编译验证：

```text
dotnet build NWRP.Runtime.csproj -nologo --no-restore
0 warnings
0 errors
```

已完成 Editor 编译验证：

```text
dotnet build NWRP.Editor.csproj -nologo --no-restore
0 errors
```

Editor build 仍有 Unity 引用解析相关 `MSB3277` warning，属于当前工程既有 warning，不是本阶段新增错误。

已完成 shader 支持检查：

```text
Hidden/NWRP/PostProcess/ValleyHeightFog
IsSupported = true
HasErrors = false

Hidden/NWRP/Environment/CloudShadowProjector
IsSupported = true
HasErrors = false
```

CloudShadow shader 当前仍有一个 D3D warning：

```text
use of potentially uninitialized variable (ComputeCloudShadowDistortion)
```

该 warning 与本阶段 depth UV / `_ScaleBiasRt` contract 修复无关，后续可单独清理。

## Frame Debugger 建议复查路径

建议继续在 Frame Debugger 中复查以下组合：

```text
无 depth consumer baseline
ValleyFog only
CloudShadow only
ValleyFog + CloudShadow
DepthTexture Off
DepthTexture AutoFeatureOnly
DepthTexture Force
copyDepthMode AfterOpaques
copyDepthMode AfterTransparents
SceneView
GameView EditMode
GameView PlayMode
PostProcess disabled baseline
```

重点观察：

```text
depthCopy
opaqueCopy
finalBlit
finalFusion
tempColorRT
logicalTransientColorRT
physicalTransientColorRT
renderPassClusters
discardedDepthStore
forcedOpaqueCopy
forcedDepthCopy
```

预期行为：

- `DepthTexture Off` 时，即使 ValleyFog / CloudShadow active，`depthCopy = 0`。
- `DepthTexture AutoFeatureOnly` 且 ValleyFog / CloudShadow active 时，`DepthTextureFeature` 在 consumer 前提供 `_CameraDepthTexture`。
- `copyDepthMode = AfterTransparents` 时，CopyDepth 仍必须排在 AfterTransparent consumer 前。
- CloudShadow 与 ValleyFog 同时启用时，CloudShadow 先执行。
- ValleyFog / CloudShadow 在 SceneView 和 GameView 中都不应上下颠倒。
- GameView PlayMode 与非 PlayMode 的 CloudShadow 可见性应一致。

## 当前限制与后续方向

- 本阶段没有实现完整 Unity RenderGraph，也没有接 native render pass/subpass API。
- `NWRPTransientResourceAllocator` 当前先做 lifetime 建模和统计，不直接替代所有 `GetTemporaryRT`。
- ValleyFog / CloudShadow 仍是两个独立 fullscreen pass，没有合并进 mobile final composite shader。
- `_CameraOpaqueTexture` 还没有做到完整材质消费者分析；如果后续要彻底需求驱动，需要显式材质/feature 声明或构建时扫描。
- `DepthTexture Off` 当前语义是绝对禁止 `_CameraDepthTexture`。依赖 scene depth 的 ValleyFog / CloudShadow 会跳过；需要自动按 feature 生成 depth 时应使用 `AutoFeatureOnly`。
- 自动化测试覆盖了调度和 shader contract，但还不是像素级视觉回归。GameView / SceneView 的最终画面仍建议用 Frame Debugger、截图对比和真机 GPU capture 继续确认。
- 真机侧仍需在 Android Vulkan / GLES3、iOS Metal 上记录 GPU time、external bandwidth、tile store/load、RT peak memory 和 copy/blit count。

Phase53 的价值是把 Phase51/52 的低带宽资源调度从“理论上可省”收口到“功能上可用”：`Off / AutoFeatureOnly / Force` 的语义不再混淆，fullscreen depth reconstruction 不再和 Blitter source orientation 打架，feature 顺序不再依赖序列化偶然性，并且默认资产真正进入移动端低带宽 baseline。
