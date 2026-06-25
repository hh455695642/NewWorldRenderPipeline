# Phase54 Mobile Bandwidth Phase1 收口与 ScreenBlur / Overlay 回归修复

日期: `2026-06-24`

## 概要

本阶段把前几轮 TBDR / TBR-friendly 移动端带宽优化从“局部可用”推进到 Phase 1 可验收状态，重点不再继续扩大渲染架构范围，而是修正几个会直接影响落地判断的闭环问题：

- final fullscreen pass 直写 backbuffer 的判定必须严格绑定“最后一个 camera color 使用者”。
- Valley Height Fog Overlay 必须声明 camera color / camera depth 资源使用，避免前一个 fullscreen pass 被误判为最终直出。
- Screen Blur 作为独立 pluggable fullscreen feature，不应被全局 `supportsPostProcessing` 错误屏蔽。
- Bloom 移动预算路径优先使用低带宽 HDR 格式，减少 fullscreen RT 链路成本。
- debug stats 补充 camera color last-use / final-present pass index，方便 Frame Debugger 对照。
- Phase 2 预留内部 fullscreen chain 接口，但不在本阶段引入公开 API 或超级 Feature。

本阶段继续保持 NWRP 自定义 SRP 架构，不迁移 URP，不引入 `ScriptableRendererFeature` / `ScriptableRenderPass`，不新增 shader keyword，不引入 MRT / GBuffer / tiled deferred。优化重心仍然是移动端 tile-based GPU 上的 RT 数量、load/store action、fullscreen blit 数量和最终 present 路径。

## 修改文件

- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPFrameDebugStats.cs`
- `Assets/NWRP/Runtime/NWRPFrameResources.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/Passes/INWRPFullscreenEffectNode.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/ScreenBlurFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay/ValleyHeightFogOverlayFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay/Passes/ValleyHeightFogOverlayPass.cs`
- `Assets/NWRP/Tests/EditMode/NWRP.Tests.EditMode.asmdef`
- `Assets/NWRP/Tests/EditMode/TBDRFrameGraphTests.cs`
- `Assets/NWRP/Tests/EditMode/TBDRSettingsTests.cs`
- `Assets/NWRP/Tests/EditMode/TBDRTargetRequirementTests.cs`
- `Assets/NWRP/Tests/EditMode/DepthDrivenBlitShaderContractTests.cs`

## 问题背景

### 1. final presenter 判定过早

Phase52 / Phase53 已经让 Valley Fog、Cloud Shadow、Screen Blur 等 fullscreen pass 可以在自己是最后一个 fullscreen color pass 时直接写 backbuffer，从而跳过多余 `FinalBlit`。但原先 frame graph 的判定只记录“最后一个声明 can present 的 pass”，没有确认它后面是否还有其它 pass 继续读写 camera color。

这会产生一个典型错误：

```text
CloudShadow 可以 present -> 被选为 final presenter
ValleyHeightFogOverlay 后续仍然读写 camera color/depth
CloudShadow 直接写 backbuffer
Overlay 仍然以 camera color 为目标继续执行
```

在 Frame Debugger 中表现为 fullscreen pass 的输出路径和后续 pass 资源关系不一致；在移动端则可能变成额外 backbuffer / camera color 往返，甚至视觉顺序错误。

### 2. Valley Height Fog Overlay 未声明资源使用

Valley Height Fog Overlay 是透明 overlay draw pass，本质上仍然读写 camera color，并读取 camera depth 做正确的覆盖关系。但它之前没有覆写 `GetFrameResourceUsage(...)`，轻量 frame graph 看不到它对 camera color 的后续使用。

因此只要 Overlay 排在某个 fullscreen effect 后面，前面的 effect 就有机会被误判为最终直出 pass。

### 3. Screen Blur 被全局 PostProcess capability 错误屏蔽

Screen Blur 在 Phase43 被设计为独立的 pluggable feature：

```text
ScreenBlurFeature + ScreenBlurPass + NWRPScreenBlur VolumeComponent
```

但实际激活路径中仍然检查了：

```csharp
PostProcessFeature.IsPostProcessingEnabled(ref frameData)
```

当 `NewWorldRP.asset` 里 `supportsPostProcessing = false` 时，即使 Renderer Data 中已经启用 `ScreenBlurFeature`，Volume 中 `NWRPScreenBlur` active 且 radius > 0，Screen Blur 也不会解析、不会请求 intermediate color、不会入队。

这和 NWRP 当前的模块化策略冲突：Screen Blur 是独立 fullscreen Feature，不是 `PostProcessPass` 内建效果，不应被 asset 级内建 postprocess capability 直接关闭。

### 4. Valley Fog Overlay “只在运行时渲染”的真实原因

排查后确认 Overlay pass 本身不是通过 `Application.isPlaying` 限制运行时。当前看起来“只有运行时才渲染”的主要原因是示例中的抛物线 overlay 对象由 `TaskPointParabolaGenerator.Start()` 生成。

也就是说：

```text
Overlay pass 存在
ShaderTagId("AfterFog") / ShaderTagId("NWRPAfterFog") 匹配存在
但编辑态没有运行 Start()，场景里没有实际 LineRenderer overlay 对象
```

如果要在编辑态稳定预览，需要把 overlay 渲染对象做成场景持久对象，或增加 editor-time generation / OnValidate 路径，而不是在渲染管线里额外加运行时特判。

### 5. Bloom HDR RT 格式仍偏高带宽

移动预算开启后，Bloom mip 数量和 base size 已经被限制，但 bloom RT 格式仍主要沿用半精度 RGBA 路径。对移动端 fullscreen blur / upsample 链来说，RT 格式带宽同样重要。

本阶段在不增加 shader variant、不改变 bloom pass 结构的前提下，让移动预算路径优先使用 `B10G11R11_UFloatPack32`；不支持时再回退到 `R16G16B16A16_SFloat`，最后再使用 Unity HDR 默认图形格式。

## 关键实现

### 1. camera color last-use contract

`NWRPFrameGraphData` 新增：

```csharp
public int cameraColorLastUsePassIndex;
```

`NWRPFrameGraphAnalyzer.Analyze(...)` 改为同时追踪：

```text
lastCameraColorUseIndex
lastPresentCandidateIndex
```

最终直出判定收口为：

```text
cameraColorFinalPresentPassIndex =
    lastPresentCandidateIndex == lastCameraColorUseIndex
        ? lastPresentCandidateIndex
        : -1
```

这意味着只有“最后一个使用 camera color 的 pass”才能成为 final presenter。前面任意 fullscreen pass 即使具备直写 backbuffer 能力，只要后面还有 camera color 使用者，就必须写回 camera color。

移动端收益是避免错误的 backbuffer 写入和后续 camera color 读写断裂；架构收益是让 Phase 2 fullscreen chain 可以基于明确的 last-use 信息继续演进。

### 2. debug stats 输出 frame graph pass index

`NWRPFrameDebugStats` 新增：

```text
cameraColorLastUsePassIndex
cameraColorFinalPresentPassIndex
```

`NWRPRenderer.AnalyzeFrameGraph(...)` 会把 usage index 映射回真实 queued pass index，再写入 debug stats。`LogFrameDebugStats(...)` 输出新增：

```text
colorLastUsePass
colorFinalPresentPass
```

这两个字段用于和 Frame Debugger 对齐：

- `colorLastUsePass`：本帧最后一个 camera color 使用者。
- `colorFinalPresentPass`：本帧实际允许直接写 backbuffer 的 camera color pass；为 `-1` 时表示不能跳过 FinalBlit 或已有其它 backbuffer writer。

### 3. ValleyHeightFogOverlay 资源声明

`ValleyHeightFogOverlayPass` 新增：

```csharp
public override NWRPFramePassResourceUsage GetFrameResourceUsage(
    ref NWRPFrameData frameData)
{
    return new NWRPFramePassResourceUsage
    {
        cameraColor = NWRPFrameResourceAccess.ReadWrite,
        cameraDepth = NWRPFrameResourceAccess.Read
    };
}
```

Overlay draw pass 继续保持独立 feature / pass，不并入 Valley Height Fog fullscreen pass，也不和 PostProcess 合并。它只把自己的资源关系显式告诉 frame graph，保证前序 fullscreen effect 不会误判最终直出。

当前调试分支中 `ValleyHeightFogOverlayFeature` 已确认没有 `Application.isPlaying` 限制。若后续要正式支持编辑态 overlay 预览，应优先处理对象生成路径，而不是让渲染管线承担场景对象生命周期。

### 4. ScreenBlur 从内建 PostProcess capability 中解耦

`ScreenBlurFeature.IsActive(...)` 从：

```text
PostProcessFeature.IsPostProcessingEnabled
&& screenBlurActive
&& screenBlur != null
```

调整为：

```text
screenBlurActive
&& screenBlur != null
```

同时 `NWRPRenderer.ConfigureCameraData(...)` 把 Screen Blur Volume 解析从 `ResolvePostProcessingFromVolume(...)` 中拆出：

```text
ResolvePostProcessingFromVolume -> 只解析 Tonemapping / Bloom / ColorAdjustments / Vignette / FXAA
ResolveScreenBlurFromVolume     -> 单独解析 NWRPScreenBlur
```

新的能力边界：

- `supportsPostProcessing = false` 不再屏蔽 Screen Blur。
- `NWRPCameraData.renderPostProcessing` 仍然作为 camera 级 fullscreen effect 开关。
- SceneView 仍然尊重 Effects/Post Processing toolbar。
- OpenGLES2 仍然被禁用，避免低端兼容性风险。
- Screen Blur 仍必须作为 renderer data feature 显式存在，Volume active 且 radius / iterations 有效时才入队。

这符合 Phase43 的原始设计：Screen Blur 是可插拔 fullscreen feature，而不是内建 PostProcessPass 的子效果。

### 5. Bloom descriptor 移动低带宽格式

`PostProcessPass.CreateBloomDescriptor(...)` 增加 asset 参数，并通过 `ResolveBloomGraphicsFormat(...)` 选择格式：

```text
EnableMobileFullscreenBudget && 支持 B10G11R11_UFloatPack32
    -> B10G11R11_UFloatPack32
否则支持 R16G16B16A16_SFloat
    -> R16G16B16A16_SFloat
否则
    -> SystemInfo.GetGraphicsFormat(DefaultFormat.HDR)
```

该改动不增加 keyword，不增加 pass，不改变 bloom shader。移动预算开启时允许轻微视觉差异，优先减少 bloom pyramid 的外部内存带宽。

### 6. Phase 2 fullscreen chain 内部接口预留

新增内部接口：

```csharp
internal interface INWRPFullscreenEffectNode
{
    NWRPPassEvent PassEvent { get; }
    bool RequiresDepthTexture { get; }

    bool IsActive(ref NWRPFrameData frameData);

    void Execute(
        ref NWRPFrameData frameData,
        RenderTargetIdentifier source,
        RenderTargetIdentifier destination,
        bool destinationIsBackBuffer);
}
```

本阶段只预留接口，不接入公开 API，不把 Valley / Cloud / ScreenBlur / PostProcess 合并成超级 Feature。Phase 2 可以在内部 scheduler 中把同一注入阶段的 fullscreen effects 组织成 A/B ping-pong chain，并只让最后一个 node 写 backbuffer。

## 行为结果

### 默认无 HDR / 无 post / 无 depth texture / 无 opaque texture

目标状态：

```text
finalBlit = 0 或仅在确实需要 present intermediate color 时出现
opaqueCopy = 0
depthCopy = 0
tempColorRT = 0
```

本阶段没有恢复任何默认 depth / opaque copy，也没有新增全局 fullscreen pass。

### 单个最终 fullscreen effect

当最后一个 camera color 使用者本身支持 present：

```text
cameraColorFinalPresentPassIndex == cameraColorLastUsePassIndex
```

该 pass 可以直接把最终结果写入 backbuffer，跳过后续 FinalBlit。

### 多个 fullscreen / overlay effect

当后面仍有 camera color 使用者：

```text
cameraColorFinalPresentPassIndex = -1
```

前面的 fullscreen effect 必须写回 camera color，最后一个真正的 camera color 使用者才有资格直写 backbuffer。

### Screen Blur 独立运行

现在以下组合可以正常成立：

```text
supportsPostProcessing = false
ScreenBlurFeature enabled
NWRPScreenBlur active
radius > 0
iterations > 0
camera renderPostProcessing = true
```

注意：当前 `MaterialSampleScene` 的 Volume Profile 中 `NWRPScreenBlur.active` 仍为 `0`，如果要在场景里直接观察 blur，需要在 Volume 中启用该组件并保持 radius / iterations override。

## 性能与移动端策略

### CPU

- 没有新增 per-object / per-instance CPU loop。
- 没有新增运行时材质扫描。
- 没有新增复杂调试系统；debug stats 仍由 asset 开关控制。
- Screen Blur 解耦只改变 frame data 解析路径，不增加 scheduler 的高成本分支。

### GPU

- 没有新增 shader keyword。
- 没有新增 MRT。
- 没有新增 depth / opaque copy。
- Bloom 移动预算路径减少 HDR RT 格式带宽。
- final presenter 判定更严格，避免错误直写 backbuffer 后又继续使用 camera color。
- Overlay 显式声明资源使用，避免 fullscreen pass 误判导致额外 copy 或视觉顺序问题。

### Tile-Based GPU 取舍

本阶段仍然选择保守落地：

- 继续使用现有 `CoreUtils.SetRenderTarget` load/store 路径。
- 不引入 Unity RenderGraph / Native RenderPass 大改。
- 不融合 shader，只修正 pass 级资源声明和最终 present 合法性。
- 不对 opaque texture 做材质扫描；需要 opaque texture 的材质仍应通过 `Force` 或后续显式 feature request 控制。

## Shader Variant 影响

本阶段没有新增 shader keyword：

```text
新增 multi_compile: 0
新增 shader_feature_local: 0
新增全局 keyword: 0
```

Bloom 格式选择、ScreenBlur 激活解耦、frame graph last-use 判定、debug stats 都在 C# 运行时层完成，不增加 shader variant 组合。

## 测试与验证

新增或修复的关键 EditMode 测试包括：

- `AnalyzePassUsages_DoesNotSelectPresenterBeforeLaterCameraColorUser`
- `AnalyzePassUsages_SelectsPresenterOnlyWhenPresenterIsLastCameraColorUse`
- `ValleyHeightFogOverlay_DeclaresCameraColorAndDepthUsage`
- `ConfigureCameraData_ResolvesDepthConsumers_WhenPostProcessingCapabilityIsDisabled`
- `ConfigureCameraData_ResolvesScreenBlur_WhenPostProcessingCapabilityIsDisabled`
- `ScreenBlur_RequestsIntermediateColor_WhenPostProcessingCapabilityIsDisabled`
- `Scheduler_EnqueuesScreenBlur_WhenPostProcessingCapabilityIsDisabled`

已执行验证：

```text
tests_run testMode=EditMode testNamespace=NWRP.Tests includePassingTests=false includeMessages=true
```

结果：

```text
Status: Passed
TotalTests: 81
FailedTests: 0
```

同时执行：

```text
git diff --check
```

结果无 whitespace error，仅有工作区 LF/CRLF 提示。

## 当前限制与后续方向

- 本阶段的 frame graph 仍是轻量分析器，不负责完整 RT 生命周期管理。
- `INWRPFullscreenEffectNode` 只是 Phase 2 内部接口预留，尚未接入实际 chain 调度。
- Screen Blur 仍是 full-resolution separable blur；半分辨率、mask、区域 blur、UI 排除应作为后续独立演进。
- Valley Height Fog Overlay 编辑态可见性取决于实际 overlay 对象是否存在；如果对象只在 `Start()` 生成，编辑态不会自然显示。
- 真机带宽收益仍需在 Mali / Adreno / Apple GPU 上用 AGI、Snapdragon Profiler、Xcode GPU Frame Capture 或 RenderDoc 验证。

Phase 2 建议继续围绕 fullscreen chain 收口：

```text
同一注入阶段 fullscreen nodes -> shared A/B ping-pong
最后一个 node -> backbuffer
中间 node -> A/B transient color slots
Depth texture -> 仍由 FeatureScheduler 统一按需申请
```

这样可以在不制造超级 Feature 的前提下，把 CloudShadow + ValleyFog + ScreenBlur + PostProcess 的 RT 峰值和 fullscreen copy 数量继续压低。
