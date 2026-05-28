# Phase43 NWRP Screen Blur Renderer Feature

日期: `2026-05-28`

## 概要

本阶段新增 NWRP 的屏幕空间模糊能力，形态保持为可插拔 renderer feature，而不是并入 `PostProcessPass` 的内置效果栈：

- 新增 `NWRPScreenBlurFeature`，负责按 Volume 状态入队。
- 新增 `NWRPScreenBlurPass`，执行 separable fullscreen blur。
- 新增 `NWRPScreenBlur` VolumeComponent，参数只暴露 `radius`、`iterations`、`injectionPoint`。
- 新增 hidden shader `Hidden/NWRP/PostProcess/ScreenBlur`，包含 Horizontal / Vertical 两个 pass。
- 默认插入点为 `AfterFogOverlay`，即 Valley Height Fog Overlay 之后、NWRP PostProcess 之前。
- 用户追加调整后，原计划中的 `AfterTransparent` 不再暴露，改为 `AfterPostProcess`。

整体目标是提供一个足够窄、可移除、可按 Volume 控制的屏幕模糊模块。该功能不会自动创建 runtime blur feature，必须显式添加到 Renderer Data 的 feature list 中。

## 问题背景

屏幕模糊属于典型 fullscreen 后处理型效果，但本阶段刻意没有把它塞进统一 `PostProcessPass`：

- 项目需要 renderer feature 级别的可插拔能力，便于按 renderer data 控制启停。
- Blur 的执行时机需要可选，既要支持雾后、后处理前，也要支持后处理后。
- 移动端需要清楚暴露 fullscreen blit 数量和临时 RT 成本。
- 后续如果要扩展 mask、半分辨率、区域模糊或 UI 排除，应从独立 feature 演进，而不是继续膨胀主后处理 pass。

因此本阶段采用独立 `NWRPFeature + NWRPPass + VolumeComponent`，只通过 frame data 与 renderer pass queue 交互。

## 修改文件

- `Assets/NWRP/Runtime/PostProcessing/NWRPScreenBlur.cs`
- `Assets/NWRP/Runtime/PostProcessing/NWRPScreenBlurFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/NWRPScreenBlurPass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/NWRPPassEvent.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Editor/PostProcessing/NWRPScreenBlurEditor.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`
- `Assets/NWRP/Shaders/PostProcess/NWRP_ScreenBlur.shader`
- `Assets/Settings/NewWorldRP.asset`
- `Assets/NWRP/Tests/Scenes/MaterialSampleScene/NWRP Volume Profile.asset`
- `Assets/NWRP/Tests/Editor/ValleyHeightFogOverlayFeatureTests.cs`

## 核心实现

### 1. Volume 作为唯一运行时配置源

新增:

```text
NWRPScreenBlur
```

菜单路径:

```text
NWRP/Post-processing/Screen Blur
```

暴露参数:

- `radius`: 模糊采样半径，范围 `0 - 8`。
- `iterations`: 迭代次数，范围 `0 - 4`。
- `injectionPoint`: 插入时机。

默认值:

```text
radius = 0
iterations = 1
injectionPoint = AfterFogOverlay
```

激活条件:

```text
Volume component active
radius > 0
iterations > 0
pipeline / camera post-processing enabled
```

`radius = 0` 是默认关闭状态，避免项目只挂载 Volume 后立刻增加 fullscreen pass 成本。

### 2. 插入时机

新增 pass event:

```text
NWRPPassEvent.BeforePostProcess = 590
NWRPPassEvent.AfterPostProcess = 650
```

当前可选时机:

- `AfterFogOverlay` -> `NWRPPassEvent.BeforePostProcess`
- `AfterPostProcess` -> `NWRPPassEvent.AfterPostProcess`

默认顺序:

```text
Valley Height Fog Overlay -> NWRP Screen Blur -> NWRP PostProcess
```

选择 `AfterPostProcess` 时顺序为:

```text
NWRP PostProcess -> NWRP Screen Blur -> FinalBlit / DebugOverlay
```

`AfterTransparent` 没有继续作为 Screen Blur 的公开选项。原因是该时机早于 Valley Height Fog Overlay，和当前“默认雾后、后处理前”的需求不一致；如果需要真正的透明后预模糊，后续应作为新插入点单独评估，而不是复用当前 Volume 枚举。

### 3. Feature 入队与 target requirements

`NWRPScreenBlurFeature` 职责保持很窄：

- 检查 `PostProcessFeature.IsPostProcessingEnabled(ref frameData)`。
- 检查 `frameData.screenBlurActive`。
- active 时请求 `requiresIntermediateColor = true`。
- active 时根据 Volume 的 `injectionPoint` 设置 pass event。
- Preview camera 不入队。

该 feature 没有 renderer-local 参数。启停路径为:

```text
Renderer Data feature toggle / remove
Volume active
radius
iterations
camera post-processing
```

Renderer Data Editor 的 Add Feature 菜单新增 `Screen Blur`，并复用已有 `NWRPScreenBlurFeature`，避免重复添加同类 feature。

### 4. Pass 执行

`NWRPScreenBlurPass` 使用一个 full-resolution 临时 RT：

```text
cameraColor -> tempColor   Horizontal
tempColor   -> cameraColor Vertical
```

每次 iteration 执行一组 Horizontal + Vertical fullscreen pass。最终结果写回 `cameraColor`，供后续 pass 或 final blit 使用。

运行时 clamp:

```text
radius <= 8
iterations <= 4
```

这样 Volume YAML 或脚本写入异常值时，不会让移动端成本失控。

### 5. AfterPostProcess 路径

NWRP 原本的 `PostProcessPass` 会在最终 composite 阶段直接写到 back buffer，并把 `cameraColorPresented` 标记为 true。

如果 Screen Blur 选择 `AfterPostProcess`，blur 必须读取已经完成 tonemapping / bloom / color adjustments / FXAA 的结果。因此本阶段给 `PostProcessPass` 增加了一个窄分支：

```text
postprocess source -> temporary final composite
temporary final composite -> cameraColor
screen blur reads cameraColor
renderer final blit presents cameraColor
```

这个分支只在 `NWRPScreenBlurFeature.IsAfterPostProcessActive(ref frameData)` 为 true 时触发。默认 `AfterFogOverlay` 不走这条额外 copy 路径。

## Shader

新增 shader:

```text
Hidden/NWRP/PostProcess/ScreenBlur
```

Pass:

- `Blur Horizontal`
- `Blur Vertical`

采样结构为 5-tap separable Gaussian 近似：

```text
center
+/- 1.3846153846 * radius
+/- 3.2307692308 * radius
```

Shader include:

- `Assets/NWRP/ShaderLibrary/NWRPBlitCoreCompat.hlsl`
- `Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl`

没有引用 URP shader library。

## 设计取舍

### 不并入 PostProcessPass

Blur 与 Bloom / Tonemapping / FXAA 不同，它的插入时机本身就是功能需求的一部分。如果把它并入 `PostProcessPass`，会让主后处理 pass 持续增加分支和资源路径。

当前方案保持:

- `PostProcessPass` 只为 `AfterPostProcess` 读取结果提供最小兼容分支。
- Blur 的 shader、参数、临时 RT 和迭代逻辑全部留在独立 pass。
- 后续扩展不会继续推高主后处理复杂度。

### 不做半分辨率与 mask

全分辨率 blur 的带宽成本很明确，但本阶段没有加入 half-res、区域 mask、深度 mask 或 UI 排除参数。

原因:

- 当前用户接口只需要范围、迭代次数和插入时机。
- 半分辨率会引入额外上采样策略和边缘质量问题。
- mask 会扩大 Volume 参数面，并可能要求额外贴图或 stencil/depth 约束。

这些都应作为后续明确需求再拆分实现。

### 不自动创建 runtime feature

`NWRPScreenBlur` Volume 只描述效果参数，不负责修改 Renderer Data。

这样可以保持 NWRP 的 feature lifecycle 清晰:

```text
Renderer Data 决定是否存在能力
Volume 决定能力是否在当前 camera/frame 生效
```

示例 renderer data 可以显式添加 `Screen Blur Feature` 用于验证，但 runtime 不会偷偷补齐 feature。

## 性能与 Variant

CPU:

- 每帧只做 Volume 解析、feature active 判断、pass enqueue 和少量 uniform 上传。
- 无 per-object 遍历。
- 无 CPU blur 循环。

GPU:

- 成本为 `2 * iterations` 次 fullscreen blit。
- 使用 1 个 full-resolution temporary color RT。
- `AfterPostProcess` 激活时，额外需要一次 postprocess final composite 写回 `cameraColor` 的临时路径。
- 无 compute shader。
- 无 geometry shader。
- 无 MRT。

移动端建议:

- `iterations = 1 - 2`
- `radius <= 4` 作为常用范围
- 大半径或 after-post blur 必须结合目标机型带宽做 profiling

Shader Variant:

- 不新增 keyword。
- 不使用 `shader_feature`。
- 不使用 `multi_compile`。
- 不使用 `multi_compile_instancing`。

该 shader 是 fullscreen hidden blit shader，不是材质实例化渲染 pass；加入 instancing pragma 没有收益，只会增加无意义 variant 风险。

## 验证记录

Editor tests:

```text
NWRP.Editor.Tests
32 passed / 0 failed
```

覆盖内容:

- `BeforePostProcess` 位于 `AfterValleyHeightFog` 与 `PostProcess` 之间。
- `AfterPostProcess` 位于 `PostProcess` 之后、`DebugOverlay` 之前。
- 默认 Volume 不激活 blur，但默认 timing 为 `AfterFogOverlay`。
- active Volume 时 feature 请求 intermediate color。
- inactive Volume 时 feature 不请求 intermediate color。
- feature enqueue 的 pass event 随 Volume timing 改变。
- Renderer Data Editor 添加 Screen Blur 时复用已有 feature，避免重复。
- shader 不包含 URP include、`shader_feature` 或 `multi_compile`。

静态检查:

```text
git diff --check
```

结果为 clean。

Frame Debugger 建议继续手工确认两个路径:

```text
Valley Height Fog Overlay -> NWRP Screen Blur -> NWRP PostProcess
NWRP PostProcess -> NWRP Screen Blur -> FinalBlit
```

