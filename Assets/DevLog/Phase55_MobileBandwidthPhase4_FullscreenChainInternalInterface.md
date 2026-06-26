# Phase55 Mobile Bandwidth Phase4 Fullscreen Chain 内部接口落地

日期: `2026-06-25`

## 概要

本阶段把 Phase54 预留的 `INWRPFullscreenEffectNode` 从“接口占位”推进为实际可执行的 NWRP 内部 fullscreen chain。目标不是新增视觉效果，也不是迁移到 URP RenderFeature，而是在现有 custom SRP 里收敛以下重复路径：

- `cameraColor -> temp -> cameraColor`
- `cameraColor -> temp -> backbuffer`
- final fullscreen pass 和 `FinalBlit` 的重复 present
- 每个 fullscreen pass 私有 copy material / temp RT / restore camera target 逻辑

本阶段继续保持移动端优先：

- 不引入 Unity RenderGraph。
- 不引入 URP `ScriptableRendererFeature` / `ScriptableRenderPass`。
- 不新增 shader keyword。
- 不新增 MRT / GBuffer。
- 不合并 Valley Fog / Cloud Shadow / Screen Blur shader。
- 不把多个功能塞进“超级 Feature”。

核心变化是新增一个内部 chain 执行器，由节点声明自己要执行的 material pass，chain 统一负责临时 RT、A/B ping-pong、final-present、backbuffer 输出、camera target restore 和 debug stats。

## 修改文件

- `Assets/NWRP/Runtime/Passes/INWRPFullscreenEffectNode.cs`
- `Assets/NWRP/Runtime/Passes/NWRPFullscreenChain.cs`
- `Assets/NWRP/Runtime/NWRPFrameDebugStats.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Passes/ScreenBlurPass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Tests/EditMode/FullscreenChainContractTests.cs`

## 问题背景

### 1. Fullscreen pass 的重复实现已经影响后续收口

Phase52 / Phase53 已经把 fullscreen blit 的入口统一到 `NWRPFullscreenPassUtils`，并通过轻量 frame graph 判断最后一个 camera color 使用者是否可以直接写 backbuffer。

但每个 pass 仍然自己维护类似逻辑：

```text
Ensure effect material
Create color descriptor
Allocate temp
Blit cameraColor -> temp
Blit temp -> cameraColor 或 backbuffer
Release temp
Restore camera target
```

这会导致几个问题：

- final-present 规则虽然一致，但分散在多个 pass 内。
- 单步效果需要 copy material 做 `temp -> cameraColor`。
- 多步效果很难共享 A/B ping-pong 规则。
- debug stats 只能看到普通 fullscreen blit / temp RT，看不到 chain 维度的节点数量。
- 后续想做更严格的 transient alias 时，必须逐个 pass 改。

### 2. ScreenBlur 的多 iteration 路径仍有不必要写回

旧的 ScreenBlur 每个 iteration 都会执行：

```text
cameraColor -> temp horizontal
temp -> cameraColor vertical
```

当 iterations > 1 时，中间结果反复写回 cameraColor，不利于后续做 A/B 临时 RT 复用，也会增加移动端带宽压力。

本阶段把 ScreenBlur 改为 chain 驱动：

```text
cameraColor -> tempA horizontal
tempA -> tempB/cameraColor/backbuffer vertical
```

多 iteration 时由 chain 在 temp slot 之间 ping-pong，最后一步才写回 cameraColor 或融合到 backbuffer。

### 3. PostProcess final composite 和 ScreenBlur 有特殊耦合

旧 `PostProcessPass` 会显式判断：

```csharp
ScreenBlurFeature.IsAfterPostProcessActive(ref frameData)
```

如果 AfterPostProcess ScreenBlur active，PostProcess 会走一条特殊路径，把 final composite 写回 cameraColor，而不是直接写 backbuffer。

这条分支本质上是在手工补偿“后面还有 camera color 使用者”。现在 frame graph 已经能判断 last camera color use，chain 也能按 final-present 合法性决定输出目标，因此 PostProcess 不应该继续硬编码 ScreenBlur 这个具体 feature。

本阶段改为：

- `PostProcessPass` final composite 也实现 `INWRPFullscreenEffectNode`。
- 是否写 backbuffer 由 `frameData.frameGraph.IsCameraColorFinalPresentPass(this)` 决定。
- Bloom pyramid 仍然保留在 `PostProcessPass` 内部，不在本阶段拆散。

## 关键实现

### 1. `INWRPFullscreenEffectNode` 扩展为执行合约

接口从原来的占位式 `Execute(...)` 调整为节点声明式合约：

```csharp
internal interface INWRPFullscreenEffectNode
{
    NWRPPassEvent PassEvent { get; }
    bool RequiresDepthTexture { get; }
    bool RequiresOpaqueTexture { get; }

    bool IsActive(ref NWRPFrameData frameData);
    bool CanPresentToBackBuffer(ref NWRPFrameData frameData);
    bool Prepare(ref NWRPFrameData frameData);
    int GetPassCount(ref NWRPFrameData frameData);

    bool TryGetPass(
        ref NWRPFrameData frameData,
        int passIndex,
        bool isFinalPass,
        out NWRPFullscreenEffectPass fullscreenPass);
}
```

设计重点：

- 节点只声明自己需要执行几个 material pass。
- 节点负责上传自己的 shader globals。
- chain 负责 RT 生命周期、目标选择和 final-present。
- depth / opaque 依赖是显式字段，后续可继续接入 frame target requirements 或更严格的调度校验。
- 接口保持 `internal`，不作为公开插件 API。

### 2. 新增 `NWRPFullscreenChain`

`NWRPFullscreenChain` 是本阶段的核心执行器，职责集中在 runtime pass 层：

- 校验 node active 状态。
- 校验 camera color、depth texture、opaque texture 等必要 target。
- 调用 node `Prepare(...)` 上传常量。
- 缓存本次需要执行的 material/pass index。
- 根据 frame graph 判断是否可以 final-present。
- 统一分配 fullscreen temp slot A/B。
- 单步效果非 final 时自动做 effect pass + copy back。
- 多步效果使用 temp A/B ping-pong，最后一步写回 cameraColor 或 backbuffer。
- 释放临时 RT。
- 非 backbuffer 输出时恢复 camera render target。

这个类不改变 pass queue，也不新增 pass event；它只替代每个 fullscreen pass 内部重复的 blit 编排。

### 3. CloudShadowProjector 接入 chain

`CloudShadowProjectorPass` 保持原 Feature / Pass 边界不变：

```text
CloudShadowProjectorFeature
CloudShadowProjectorPass
NWRPCloudShadowProjector VolumeComponent
```

变化点：

- 删除 pass 私有 `_copyMaterial`。
- 删除手写 temp 分配 / copy back / restore 逻辑。
- pass 实现 `INWRPFullscreenEffectNode`。
- `Prepare(...)` 中继续上传 cloud shadow layer、distortion、depth texture。
- `RequiresDepthTexture = true`。
- final-present 仍然要求 Game Camera、intermediate color、depth texture 可用。

视觉 shader 和 variant 完全不变。

### 4. ValleyHeightFog 接入 chain

`ValleyHeightFogPass` 同样保留原本职责：

- SingleLayer / ThreeLayer shader pass index 不变。
- Volume 参数上传不变。
- depth texture 依赖不变。

变化点：

- 删除 pass 私有 `_copyMaterial`。
- 删除手写 `cameraColor -> temp -> cameraColor/backbuffer`。
- `TryGetPass(...)` 根据当前 fog mode 返回 `SingleLayerShaderPass` 或 `ThreeLayerShaderPass`。
- `RequiresDepthTexture = true`。

这一步只收敛 RT 与输出路径，不改变高度雾算法。

### 5. ScreenBlur 接入 chain

`ScreenBlurPass` 改为把每个 iteration 拆成两个 chain material pass：

```text
Horizontal
Vertical
```

pass count 为：

```text
iterations * 2
```

chain 负责在 temp A/B 之间切换，避免每个 iteration 都写回 cameraColor。最后一步根据 frame graph 写回 cameraColor 或 backbuffer。

保留的约束：

- radius clamp 仍然使用 `NWRPScreenBlur.MaxRadius`。
- iterations clamp 仍然使用 `NWRPScreenBlur.MaxIterations`。
- shader 不变。
- injection point 仍然由 `NWRPScreenBlurInjectionPoint` 决定。

### 6. PostProcess final composite 接入 chain

`PostProcessPass` 本阶段只迁移 final composite：

- Bloom pyramid 仍由 `ExecuteBloom(...)` 内部执行。
- Bloom downsample / upsample / custom compose 的临时 RT 逻辑不拆。
- final composite 的 tonemap / bloom combine / color adjustment / vignette / FXAA 作为一个 chain node 执行。

旧路径中针对 ScreenBlur 的特殊分支被移除：

```text
PostProcess 不再直接查询 ScreenBlurFeature.IsAfterPostProcessActive
```

现在行为由 frame graph 决定：

```text
PostProcess 是最后一个 camera color 使用者 -> 可直接写 backbuffer
PostProcess 后面还有 ScreenBlur 等 camera color 使用者 -> 写回 cameraColor
```

这让 PostProcess 不再知道具体后续 fullscreen feature，解耦程度更高。

### 7. Debug stats 增加 chain 维度

`NWRPFrameDebugStats` 新增：

```text
fullscreenChainNodeCount
fullscreenChainTempRTCount
```

`LogFrameDebugStats(...)` 输出新增字段：

```text
fullscreenChainNode
fullscreenChainTempRT
```

用途：

- `fullscreenChainNode` 用于观察本帧有多少 fullscreen effect 通过 chain 执行。
- `fullscreenChainTempRT` 用于观察 chain 自己分配的 temp slot 数量。
- 仍保留已有 `fullscreenBlit`、`finalBlit`、`finalFusion`、`tempColorRT`，便于和 Frame Debugger 对齐。

## 行为结果

### 单步 fullscreen effect

CloudShadow / ValleyFog 这类单步效果现在由 chain 执行：

```text
不是 final presenter:
cameraColor -> tempA -> cameraColor

是 final presenter:
cameraColor -> backbuffer
```

非 final 时仍需要一次 copy back，这是为了保证后续 pass 继续读写 cameraColor；但 copy material 不再由每个 pass 私有持有。

### 多步 ScreenBlur

ScreenBlur 多 iteration 现在由 chain ping-pong：

```text
cameraColor -> tempA
tempA -> tempB
tempB -> tempA
...
last -> cameraColor 或 backbuffer
```

这比每轮 vertical 都写回 cameraColor 更适合后续做 transient alias / native render pass 优化。

### PostProcess + ScreenBlur

当 `PostProcess` 后面还有 `AfterPostProcess` ScreenBlur：

```text
PostProcess final composite -> cameraColor
ScreenBlur -> cameraColor/backbuffer
```

当 PostProcess 是最后一个 camera color 使用者：

```text
PostProcess final composite -> backbuffer
```

该行为由 frame graph last-use 决定，而不是 PostProcess 硬编码 ScreenBlur。

## 性能与移动端策略

### CPU

- 新增 chain 执行器只在 pass 执行时处理当前 node，不做全局复杂调度。
- pass count 缓存使用小数组，按需扩容，避免每帧 List 分配。
- 没有新增 per-object / per-instance CPU loop。
- 没有新增材质扫描或场景对象扫描。
- 没有新增复杂调试系统。

### GPU / 带宽

- 不新增 fullscreen shader pass。
- 不新增 shader keyword。
- 不新增 MRT。
- 不新增 depth / opaque copy。
- 单步非 final effect 的 blit 次数与旧路径保持等价。
- 单步 final effect 继续保留 backbuffer 直写能力。
- ScreenBlur 多 iteration 的中间写回从 cameraColor 收敛到 temp A/B ping-pong。
- PostProcess final composite 的输出目标由 frame graph 决定，避免对后续 ScreenBlur 的硬耦合。

### Tile-Based GPU 取舍

本阶段仍然是保守收口：

- 不把多个视觉效果合成一个超级 shader。
- 不引入 subpass / native render pass 重构。
- 不改变 RenderPassEvent 序列。
- 不改变 shader 采样逻辑。
- 先把 fullscreen effect 的资源生命周期集中，后续再根据真机 profiling 判断是否继续做 pass fusion。

## Shader Variant 影响

本阶段没有修改 shader 文件。

```text
新增 multi_compile: 0
新增 shader_feature_local: 0
新增全局 keyword: 0
新增材质 shader: 0
```

所有控制都发生在 C# runtime pass / frame graph / chain 层，不增加移动端 shader variant 风险。

## 测试与验证

新增 EditMode 合约测试：

- `SimpleFullscreenPasses_ImplementFullscreenEffectNode`
- `FullscreenEffectNode_ExposesChainExecutionContract`
- `PostProcess_DeclaresCameraColorReadWrite_WhenScreenBlurRunsAfter`
- `FrameDebugStats_TracksFullscreenChainWork`

测试重点不是像素对比，而是确认：

- 简单 fullscreen pass 已接入 node 合约。
- node 合约包含 chain 执行所需的 prepare / pass count / pass query / final-present 能力。
- PostProcess 不再在资源声明上被 ScreenBlur 特判牵制，而是声明 cameraColor read/write。
- debug stats 能记录 chain 节点和 chain temp RT。

已执行 Unity EditMode 全量验证：

```text
testMode: EditMode
Status: Passed
TotalTests: 85
PassedTests: 85
FailedTests: 0
SkippedTests: 0
```

已执行静态检查：

```text
git diff --check
```

结果无 whitespace error；仅有当前工作区 LF/CRLF 提示。

已扫描依赖边界：

```text
UnityEngine.Rendering.Universal
ScriptableRendererFeature
ScriptableRenderPass
```

NWRP runtime 没有新增 URP 依赖。

## 当前限制与后续方向

- `NWRPFullscreenChain` 当前是每个 pass 内部持有并执行的轻量 chain，不是全局 fullscreen scheduler。
- 还没有把同一个 `NWRPPassEvent` 下的多个独立 pass 合并为单个外层 pass。
- Bloom pyramid 仍保留在 `PostProcessPass` 内部，未拆成 chain 子节点。
- 单步非 final effect 仍需要 `effect -> temp -> cameraColor` 两段 blit，这是正确性优先的保守路径。
- 真机带宽收益仍需要在 Mali / Adreno / Apple GPU 上通过 RenderDoc、AGI、Snapdragon Profiler 或 Xcode GPU Frame Capture 验证。

后续推荐顺序：

1. 用 Frame Debugger 对比 `CloudShadow -> ValleyFog -> PostProcess -> ScreenBlur` 的 pass 顺序和 backbuffer 输出点。
2. 在 `logFrameDebugStats` 打开时观察 `fullscreenChainNode`、`fullscreenChainTempRT`、`fullscreenBlit`、`finalBlit`、`finalFusion`。
3. 若多 fullscreen effect 连续出现且真机 profiling 证明带宽瓶颈明显，再设计事件级 fullscreen chain scheduler。
4. 若 Bloom 仍是峰值 RT 成本来源，再单独设计 Bloom pyramid budget / low-res chain，而不是把 Bloom 硬塞进当前简单 node。
5. 若需要进一步减少 copy back，可考虑把连续 fullscreen nodes 合并到一个 pass 内统一 A/B ping-pong，但必须保持 feature toggle 和 RenderPassEvent 顺序可解释。

Phase55 的价值是把 fullscreen effect 从“各 pass 私有编排”推进到“节点声明 + chain 统一执行”。它仍然是 NWRP custom SRP 内部的轻量化移动端带宽收口，不是大规模 RenderGraph 迁移。
