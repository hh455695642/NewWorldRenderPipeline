# Phase52 Mobile Bandwidth Phase2 Fullscreen Helper 与轻量 FrameGraph

日期：`2026-06-22`

## 概要

本阶段继续推进 NWRP 面向移动端 tile-based GPU 的带宽治理。目标不是“实现 TBDR 硬件”，而是在现有 custom SRP / forward-first 架构内，把 fullscreen pass、临时 RT、final present 和 camera texture 依赖收束到更可分析、更容易融合的路径。

Phase52 接在 Phase51 之后。Phase51 先解决 camera attachment load/store、重复 camera target bind、debug stats 以及 Valley Height Fog 隐式 depth copy 的问题；本阶段进一步把 Valley Height Fog、Cloud Shadow Projector、Screen Blur、PostProcess 这些 fullscreen 路径统一到同一套 helper 和轻量 frame graph 语义下，为后续真正的 pass fusion / native render pass 生命周期优化铺路。

本阶段保持以下边界：

- 不引入 Unity RenderGraph。
- 不迁移到 URP RendererFeature / ScriptableRenderPass。
- 不新增 shader keyword。
- 不新增 MRT / GBuffer。
- 不重写 renderer 主流程。
- 不做 ValleyFog + CloudShadow shader 级融合，这类高风险项延后到 profiling 证明瓶颈后再执行。

## 修改文件

- `Assets/NWRP/Runtime/Passes/NWRPFullscreenPassUtils.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPFrameDebugStats.cs`
- `Assets/NWRP/Runtime/NWRPPass.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPFeatureScheduler.cs`
- `Assets/NWRP/Runtime/CameraTextures/DepthTextureFeature.cs`
- `Assets/NWRP/Runtime/Passes/CopyColorPass.cs`
- `Assets/NWRP/Runtime/Passes/CopyDepthPass.cs`
- `Assets/NWRP/Runtime/Passes/DepthPrepass.cs`
- `Assets/NWRP/Runtime/Passes/DrawOpaquePass.cs`
- `Assets/NWRP/Runtime/Passes/DrawSkyboxPass.cs`
- `Assets/NWRP/Runtime/Passes/DrawTransparentPass.cs`
- `Assets/NWRP/Runtime/Passes/FinalBlitPass.cs`
- `Assets/NWRP/Runtime/Outlines/Passes/DrawOutlinePass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Passes/ScreenBlurPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/ValleyHeightFogFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/CloudShadowProjectorFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Tests/Editor/ValleyHeightFogOverlayFeatureTests.cs`

## 当前管线判断

NWRP 当前的主要移动端带宽风险不在单个 shader ALU，而在以下 SRP 层行为：

- 多个独立 fullscreen pass 各自管理 temp RT。
- ValleyFog / CloudShadow / ScreenBlur 都有类似的 `cameraColor -> temp -> cameraColor` 路径，但实现分散。
- 最后一个 fullscreen pass 已经拥有最终画面时，仍可能再走一次 `cameraColor -> backbuffer` FinalBlit。
- depth texture / opaque texture 的创建仍容易被 renderer setting 或 feature 行为混在一起，难以判断真实消费者。
- pass 之间的 camera color/depth 读写关系没有统一声明，后续很难安全做 pass fusion。

本阶段的核心不是立刻减少所有 pass，而是先让这些 pass 的资源行为可声明、可统计、可复用。只有先把入口统一起来，后续才能判断哪些 pass 的生命周期不重叠、哪些 final copy 可跳过、哪些 camera texture 根本没有消费者。

## 关键实现

### 1. Fullscreen Pass Helper

新增 `NWRPFullscreenPassUtils`，作为 NWRP 内部 fullscreen pass 的统一入口。

它集中处理：

- `CreateColorDescriptor(...)`
- fullscreen temp color RT 分配与释放
- `BlitToTarget(...)`
- `BlitToBackBuffer(...)`
- viewport 与 render scale scale/bias
- camera target invalidation
- `RenderBufferLoadAction.DontCare` / `RenderBufferStoreAction.Store`
- fullscreen blit / final blit / temp RT / final fusion debug stats

新增两个共享 fullscreen transient slot：

```text
_NWRPFullscreenTempColorA
_NWRPFullscreenTempColorB
```

当前 ValleyFog、CloudShadow、ScreenBlur 都使用 `FullscreenTempA`。这些 pass 是顺序执行的 fullscreen effect，生命周期不重叠，因此可以先以显式 alias 的方式复用同一类 temp slot，避免每个 pass 私有一套 temp RT 管理代码。

该 helper 不是通用“万能 Blit 工具”。它只服务 NWRP 内部 fullscreen pass，目的是把移动端敏感的 RT / load-store / final present 语义收束到一处，后续才能在这里继续加入更严格的 transient alias 或 native render pass 规则。

### 2. ValleyFog / CloudShadow / ScreenBlur 接入统一路径

以下 pass 已改为使用 `NWRPFullscreenPassUtils`：

- `NWRPScreenBlurPass`
- `ValleyHeightFogPass`
- `CloudShadowProjectorPass`

原先这些 pass 各自包含：

```text
CreateTempDescriptor
GetTemporaryRT
Blit cameraColor -> temp
Blit temp -> cameraColor
ReleaseTemporaryRT
```

现在统一改为：

```text
CreateColorDescriptor
AllocateTempColor
BlitToTarget
BlitToBackBuffer / BlitToTarget
ReleaseTempColor
```

视觉路径保持不变：仍是读取 camera color，写入临时 color，再写回 camera color 或最终 backbuffer。变化在于资源管理和统计入口统一了。

对移动端的意义：

- 减少 pass 私有 RT 管理分叉。
- 统一 load/store action，避免每个 pass 自己遗漏 discard 语义。
- 后续要把多个 fullscreen pass 融合时，可以从统一 helper 和 frame graph usage 处做判断。
- debug stats 中能稳定看到 fullscreen blit、final blit、final fusion、temp color RT 计数。

### 3. 轻量 FrameGraph 资源声明

`NWRPFrameData` 扩展了 NWRP 自有轻量 frame graph 数据，不引入 Unity RenderGraph。

新增资源访问枚举：

```text
NWRPFrameResourceAccess.None
NWRPFrameResourceAccess.Read
NWRPFrameResourceAccess.Write
NWRPFrameResourceAccess.ReadWrite
```

新增 pass usage 描述：

```text
NWRPFramePassResourceUsage
cameraColor
cameraDepth
cameraDepthTexture
opaqueTexture
canPresentCameraColorToBackBuffer
writesBackBuffer
```

`NWRPPass` 增加：

```csharp
public virtual NWRPFramePassResourceUsage GetFrameResourceUsage(ref NWRPFrameData frameData)
```

默认实现保持兼容，只把已有 `CanPresentCameraColorToBackBuffer(...)` 暴露进 usage。具体 pass 再按职责覆写资源读写关系。

已补充 usage 的关键 pass 包括：

- `CopyColorPass`
- `CopyDepthPass`
- `DepthPrepass`
- `DrawOpaquePass`
- `DrawSkyboxPass`
- `DrawTransparentPass`
- `DrawOutlinePass`
- `FinalBlitPass`
- `PostProcessPass`
- `ScreenBlurPass`
- `ValleyHeightFogPass`
- `CloudShadowProjectorPass`

这一步不会自动改变 pass 顺序，也不会自动合并 pass。它的价值在于让 renderer 能回答几个移动端关键问题：

```text
谁读 camera color？
谁写 camera color？
谁读 _CameraDepthTexture？
谁写 _CameraOpaqueTexture？
谁是 DebugOverlay 前最后一个可 present 的 color pass？
本帧是否已有 backbuffer writer？
```

### 4. Fullscreen Final Present Fusion

`NWRPRenderer` 在 pass queue 排序后执行轻量分析：

```text
AnalyzeFrameGraph
```

它会跳过 `DebugOverlay`，从后往前查找最后一个可直接 present camera color 的 pass。如果该 pass 声明：

```text
canPresentCameraColorToBackBuffer = true
```

则记录为：

```text
cameraColorFinalPresentPass
```

ValleyFog、CloudShadow、ScreenBlur 在执行最后一次 fullscreen write 时会查询：

```csharp
frameData.frameGraph.IsCameraColorFinalPresentPass(this)
```

如果当前 pass 是 DebugOverlay 前最后一个 color pass，则直接：

```text
temp -> backbuffer
```

否则仍然：

```text
temp -> cameraColor
```

这样当 ValleyFog / CloudShadow / ScreenBlur 位于最终输出前时，可以跳过后续多余的 `FinalBlit`。这不是 shader fusion，而是 present fusion：保留 pass 自身效果，只消除最后一跳冗余 copy。

`NWRPFrameDebugStats` 新增：

```text
cameraColorFinalPassFusionCount
```

日志中对应字段：

```text
finalFusion
```

用于和 `finalBlit` 一起观察最后一跳是否被融合。

### 5. PostProcess 接入统一 temp / blit 入口

`PostProcessPass` 保留自己的 bloom pyramid 生命周期，但临时 RT 分配、释放和 blit 入口改为复用 `NWRPFullscreenPassUtils`。

本阶段没有把 bloom 改写为新的低分辨率算法，也没有合并 tonemap / bloom / FXAA 之外的新效果。改动重点是：

- bloom down/up/compose 的 temp RT 统计统一进入 `RecordTemporaryColorRT`。
- final composite 直接写 backbuffer 时使用统一 `BlitToBackBuffer(...)`。
- 当存在 `AfterPostProcess` 的 ScreenBlur 时，PostProcess 仍走兼容路径，把最终 composite 写回 camera color，供 blur 读取。
- 现有 tonemap、color adjustment、vignette、FXAA 仍在 final composite 内完成。

PostProcess 的 direct backbuffer 是既有路径，因此本阶段没有把它计入新增 `finalFusion`，避免 debug stats 含义混乱。`finalFusion` 主要用于记录 fullscreen feature 取代 FinalBlit 的情况。

### 6. Depth Texture Demand Graph

Phase51 曾经先把 Valley Height Fog 私自 enqueue `CopyDepthPass` 的行为砍掉，避免 Feature 绕过 Renderer Data 产生隐藏 depth copy。

Phase52 在此基础上进一步调整为“显式消费者驱动”的模型：

- `EnableDepthTexture` 保留为兼容强制开关。
- ValleyFog / CloudShadow active 时，通过 `TryGetFrameTargetRequirements(...)` 声明需要 camera depth texture。
- `DepthTextureFeature` 在 renderer setting 开启或 `frameData.targets.hasCameraDepthTexture` 为 true 时入队。
- depth copy mode 仍复用 renderer data 配置；没有 renderer data 时使用保守默认。

这意味着：

```text
没有消费者 -> 不创建 depth texture
renderer 强制开启 -> 创建 depth texture
ValleyFog / CloudShadow active -> 声明 depth texture 消费，由统一 DepthTextureFeature 创建
```

关键点是 Feature 不再自己持有私有 `CopyDepthPass` / `DepthPrepass`，而是通过统一 target requirements 让 scheduler 处理。这和 Phase51 的“禁止私自 copy”并不冲突：Phase52 把依赖升级成可见、统一、可统计的声明式请求。

`_CameraOpaqueTexture` 当前仍主要保留 renderer setting 强制路径。原因是项目里存在材质/shader 直接采样 opaque texture 的可能，例如通过 include 或水面 shader 使用，运行时仅靠 feature list 无法可靠推导所有材质消费者。后续如果要完全消费者驱动 opaque texture，需要补充材质/Shader 使用分析或显式 renderer feature 声明，不应在本阶段硬猜。

### 7. Attachment Lifetime / Memoryless Depth

`NWRPRenderer` 增加了保守的 intermediate depth memoryless 判断：

```text
CanUseMemorylessIntermediateDepth(...)
```

只有满足以下条件时，intermediate camera depth 才允许使用 `RenderTextureMemoryless.Depth`：

- 需要 intermediate depth/color attachment。
- 不需要 camera depth texture。
- 不需要 CopyDepth。
- 不需要 DepthPrepass。
- 不需要 opaque texture。
- depth 只作为 attachment 使用，不被 sampled/copy。

这条路径非常保守，目标是先避免移动端把纯 attachment depth 写回外部内存；一旦本帧需要 `_CameraDepthTexture` 或后续 copy/sample，就不启用 memoryless，避免破坏功能。

对 tile-based GPU 来说，这属于低风险优化：attachment-only depth 更适合留在 tile memory 内，减少不必要的 depth store/load。

## 性能与移动端策略

### CPU

- 新增 frame graph 分析是线性扫描 pass queue，成本很低。
- 不引入 per-object / per-instance CPU loop。
- 不增加大型 scheduler 重构。
- pass usage 由 pass 自身声明，避免 renderer 主流程继续堆条件判断。
- Debug stats 仍然只在显式开启时输出。

### GPU

- 不新增 shader keyword。
- 不新增 fullscreen shader pass。
- 不新增 MRT。
- 不新增 compute shader。
- 不改变 ValleyFog / CloudShadow / ScreenBlur 的视觉采样逻辑。
- 当 fullscreen feature 是最终 color pass 时，可跳过一次 `cameraColor -> backbuffer` FinalBlit。
- 顺序 fullscreen pass 复用统一 temp slot，降低重复 temp RT 管理和后续 alias 难度。
- attachment-only depth 可走 memoryless，降低 tile depth store/load 风险。

### Tile-Based GPU 取舍

本阶段选择“先统一生命周期，再做融合”：

- 先统一 fullscreen blit / temp RT / final present 入口。
- 先让 pass 声明资源读写关系。
- 先把 depth texture 请求收束到统一 Feature。
- 暂不把多个效果塞进一个超级 shader。
- 暂不做 native render pass/subpass 级改写。

这样每个子步骤都可回退，不会一次性把 renderer 主流程改成难以定位问题的大系统。

## Shader Variant 影响

本阶段没有新增 shader keyword：

```text
新增 multi_compile: 0
新增 shader_feature_local: 0
新增全局 keyword: 0
```

ValleyFog、CloudShadow、ScreenBlur、PostProcess 的 shader 文件没有为了 Phase52 增加新 variant。所有控制都在 C# pass / frame data / scheduler 层完成。

这符合移动端 variant 控制原则：final present、RT alias、depth demand 都是运行时资源调度问题，不应该通过 shader keyword 表达。

## 测试覆盖

新增 Phase52 回归测试集中在 `ValleyHeightFogOverlayFeatureTests.cs` 内，覆盖：

- `NWRPFullscreenPassUtils` 暴露共享 temp slot、descriptor 创建、blit 入口。
- 内置 pass 可以声明 frame resource usage。
- ScreenBlur / ValleyFog / CloudShadow / PostProcess 覆写资源读写关系。
- final present capability 能被 frame graph 查询。
- attachment-only depth descriptor 可以使用 memoryless depth。
- ValleyFog / CloudShadow 的 depth texture 需求由 feature target requirements 声明，而不是私有 copy pass。

测试的重点是资源契约，不是像素视觉对比。视觉一致性仍需要通过 Frame Debugger 和典型场景截图继续确认。

## 验证记录

已完成 C# 编译验证：

```text
dotnet build NWRP.Runtime.csproj -v:minimal
0 warnings
0 errors
```

已完成 Unity EditMode 测试：

```text
TotalTests: 55
PassedTests: 55
FailedTests: 0
SkippedTests: 0
```

已完成静态检查：

```text
git diff --check -- Assets/NWRP/Runtime Assets/NWRP/Tests/Editor/ValleyHeightFogOverlayFeatureTests.cs
```

结果无 whitespace error，仅有当前工作区 CRLF 提示。

已完成 NWRP Runtime 依赖边界扫描：

```text
UnityEngine.Rendering.Universal
ScriptableRendererFeature
ScriptableRenderPass
```

Runtime 代码中没有新增上述 URP 依赖。

## Frame Debugger 建议复查路径

建议继续用 Frame Debugger 对比以下典型路径：

```text
无后处理 baseline
ValleyFog only
CloudShadow only
ScreenBlur BeforePostProcess
ScreenBlur AfterPostProcess
PostProcess + ScreenBlur
DepthTexture on/off
OpaqueTexture on/off
```

重点看 debug stats：

```text
fullscreenBlit
finalBlit
finalFusion
tempColorRT
opaqueCopy
depthCopy
cameraBind
cameraSkip
```

预期行为：

- 当最后一个 fullscreen feature 可直接 present 时，`finalFusion` 增加，额外 `finalBlit` 减少。
- 没有 depth texture 消费者时，不应出现无意义 `depthCopy`。
- ValleyFog / CloudShadow active 时，depth texture 应由统一 `DepthTextureFeature` 产生，而不是 feature 私有 copy。
- ScreenBlur / ValleyFog / CloudShadow 的 temp RT 行为应进入统一 `tempColorRT` 统计。

## 当前限制与后续方向

本阶段没有完成也不应该急着完成以下高风险融合项：

- ValleyFog + CloudShadow 合并进单一 final composite shader。
- CloudShadow 从 screen-space pass 改为 forward lighting modulation。
- Forward+ / clustered additional lights。
- 用材质扫描自动推导所有 `_CameraOpaqueTexture` 消费者。
- native render pass / subpass 级重构。

后续推荐顺序：

1. 用 Frame Debugger 和 `logFrameDebugStats` 确认 finalFusion 是否按预期触发。
2. 在 Android / iOS 真机上用 RenderDoc、AGI 或 Xcode GPU Capture 看 render target load/store、tile store/load、外部带宽和 RT peak memory。
3. 如果 ValleyFog + CloudShadow 连续 fullscreen pass 成为真实瓶颈，再设计单独 composite shader，而不是提前把两个功能耦合。
4. 如果 opaque texture 成为无消费者也被 copy 的主要成本，再补充显式材质/feature 级声明机制。
5. 如果 memoryless depth 在目标设备上收益稳定，再扩大 attachment lifetime 分析范围。

Phase52 的价值是把 NWRP 从“多个 fullscreen pass 各自管理资源”推进到“fullscreen pass 可声明、可统计、可 present fusion、可 transient alias”的状态。它仍然是 custom SRP 内部的轻量调度改造，不是一次大而全的 RenderGraph 迁移。
