# Phase27 Beautify Core Bloom / PostProcess Inspector Cleanup

日期：`2026-05-13`

## 概要

本阶段在 Phase25/Phase26 已经搭好的 NWRP 后处理框架上，移植 Beautify 的核心 Bloom 算法，并继续保持“一个外部 `NWRP PostProcess` pass，内部执行子流程”的架构约束。

本阶段没有引入 URP Runtime 依赖，也没有新增 shader keyword / variant。Bloom 的开关与参数走 NWRP 自己的 Volume 组件；管线资产只提供粗粒度的 Bloom 硬开关，用于移动端性能兜底。

同时，本阶段补齐了后处理 Volume 的 Editor 体验：Bloom 的 custom layer 参数只在 `Customize` 真正生效时展示；Tonemapping 的参数根据 `Mode` 展示，避免美术或调参人员看到与当前模式无关的参数。

## 修改文件

### Runtime / Renderer

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/PostProcessing/NWRPBloom.cs`
- `Assets/NWRP/Runtime/PostProcessing/PostProcessFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/TonemappingPass.cs`

### Shaders

- `Assets/NWRP/Shaders/PostProcess/NWRP_Bloom.shader`
- `Assets/NWRP/Shaders/PostProcess/NWRP_Tonemapping.shader`

### Editor

- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/Editor/NWRPBloomEditor.cs`
- `Assets/NWRP/Editor/NWRPTonemappingEditor.cs`

## 解决的问题

### NWRP 后处理需要第一个真实 Bloom 效果

Phase25 中已经把 Tonemapping 收敛进统一 `PostProcessPass`，但当时还没有 Bloom / LUT / Sharpen 等实际扩展效果。本阶段把 Beautify 的核心 Bloom 作为 NWRP PostProcess 的第一个内部子流程：

```text
HDR Camera Color
    -> Bloom bright-pass / pyramid blur / upsample combine
    -> Bloom composite before tonemapping
    -> Tonemapping or Linear final composite
    -> camera/backbuffer target
```

Renderer 侧仍然只看到一个 `NWRP PostProcess` pass。这样 Frame Debugger 不会因为 Bloom 的多级内部 blit 而出现多个 NWRP 顶层 pass，也避免后续每新增一个后处理效果就扩张外部 pass 调度。

### Bloom 需要 Volume 参数，而不是管线资产承载所有美术配置

新增 `NWRPBloom : VolumeComponent`，菜单归属：

```text
NWRP/Post-processing/Bloom
```

主要参数包括：

- 基础 Bloom：`intensity`、`threshold`、`conservativeThreshold`、`spread`、`maxBrightness`、`tint`
- 质量与路径：`antiflicker`、`resolution`、`quickerBlur`
- 分层自定义：`customize`、`weight0..5`、`boost0..5`、`tint0..5`
- Lens Dirt：`lensDirtIntensity`、`lensDirtThreshold`、`lensDirtTexture`、`lensDirtSpread`

管线资产只新增：

```csharp
FeatureSettings.bloom.enableBloom
```

该开关用于项目级禁用 Bloom。默认开启，但只有 Volume 中 `NWRPBloom` active 且 `intensity` 或 `lensDirtIntensity` 大于 0 时，才会产生运行时成本。

### Bloom active 时需要请求 intermediate color

`NWRPFrameData` 新增缓存：

```csharp
public bool bloomActive;
public NWRPBloom bloom;
```

`NWRPRenderer.ConfigurePostProcessingFromVolume` 从 VolumeStack 读取 `NWRPBloom`，并结合管线资产开关写入 `bloomActive`。

`PostProcessFeature.HasAnyActivePostProcess` 从只检查 Tonemapping 扩展为：

```text
tonemappingActive || bloomActive
```

因此 Bloom-only 时也会请求 HDR intermediate color，并由 `PostProcessPass` 直接输出到 backbuffer；Tonemapping-only 维持原行为；Bloom + Tonemapping 时 Bloom 在 tonemapping 前叠加。

## 关键实现

### Beautify Core Bloom 移植范围

本阶段只移植核心 Bloom，不移植 Beautify 的 Anamorphic Flares、Sun Flares、Depth Attenuation、Layer Include/Exclude。

已移植的核心行为：

- max-rgb brightness 提取。
- conservative threshold knee。
- tint alpha 混合。
- 可选 anti-flicker bright-pass。
- 6 层 Bloom pyramid，默认 5 个 mip step。
- quicker blur 路径：downsample 与 blur 合并，移动端更便宜。
- 非 quicker blur 路径：resample 后执行水平/垂直 5-tap Gaussian blur。
- `Lerp3` spread upsample combine。
- custom layer weights / boosts / tints。
- Lens Dirt：最终 composite 中使用指定 pyramid 层作为 dirt luminance source。

所有 Bloom 临时 RT 使用 ARGBHalf、无 depth、MSAA=1、bilinear 过滤，并在 pass 结束释放。

### NWRP_Bloom.shader

新增 hidden shader：

```text
Hidden/NWRP/PostProcess/Bloom
```

Pass 组织：

- `Luminance`
- `Luminance AntiFlicker`
- `Blur Horizontal`
- `Blur Vertical`
- `Resample`
- `Resample And Combine`
- `Bloom Compose`

该 shader 只 include NWRP/Core 兼容层和 SRP Core Blit 工具，不 include URP shader library。开关通过 C# 选择 pass 或 uniform 控制，不使用 `multi_compile` / `shader_feature`。

### Tonemapping shader 扩展

`NWRP_Tonemapping.shader` 在 `FetchTonemapInput` 阶段先执行：

```text
cameraColor + bloom + lensDirt
```

然后再进入 Linear / ACES / ACESFitted / AGX pass。

这保证 Bloom 在 Tonemapping 前叠加，HDR 高亮先参与 Bloom，再由 tone curve 压回 LDR。Bloom-only 时 C# 选择 Linear pass，作为最终 composite 输出。

### Editor Inspector 收口

新增 `NWRPBloomEditor`：

- 默认只显示基础 Bloom 与 Lens Dirt 参数。
- 只有 `Customize` 的 override 勾选且 value 为 true 时，才显示 `weight0..5 / boost0..5 / tint0..5`。
- 避免 Volume 的 override checkbox 关闭后，旧 value 仍为 true 导致 custom 参数继续展示。

新增 `NWRPTonemappingEditor`：

- `Mode` 未 override 时，只显示 Mode。
- `Mode = None` 时隐藏附属参数。
- `Linear / ACES / ACESFitted` 显示 `preExposure`、`postBrightness`、`maxInputBrightness`。
- `AGX` 额外显示 `agxGamma`。

这两个 Editor 改动只影响 Inspector 展示，不改变序列化字段名，也不影响运行时逻辑。

## 性能与兼容性

- 不新增 shader keyword，Bloom shader variant 数量保持可控。
- Bloom 仍在单个外部 `NWRP PostProcess` pass 内部执行，避免扩张 NWRP 主流程。
- Bloom active 才请求 intermediate color；未启用 Volume 或强度为 0 时不分配 Bloom pyramid。
- `quickerBlur` 提供更便宜的移动端路径。
- Custom layer compose 会额外采样 6 张 mip，并多一次 compose blit，默认不建议开启。
- Lens Dirt 只在 `lensDirtIntensity > 0` 时产生最终采样成本。
- 当前未接入 Depth Attenuation，因此不会因为 Bloom 默认请求 depth texture。

## 验证记录

- 执行 `codex mcp add ai-game-developer --url http://localhost:26048`，MCP server 添加成功。
- `dotnet build .\NWRP.Runtime.csproj --no-restore`：0 errors。
- `dotnet build .\NWRP.Editor.csproj --no-restore`：0 errors；仍存在项目已有 NuGet / Unity assembly 版本冲突 warning。
- 最新 `NWRP_Bloom.shader` shader compiler block 无 error / warning。
- 定向检查新增 Bloom shader 与相关 runtime 文件，无 `UnityEngine.Rendering.Universal`、无 URP shader include、无新增 `multi_compile` / `shader_feature`。
- Unity batchmode 全量验证被已打开的 Unity Editor 实例阻止，日志提示同一项目不能被多个 Unity 实例同时打开。

## 未纳入范围与后续建议

- `Depth Attenuation / Near Attenuation` 本阶段只完成原理确认，没有实装。Beautify 在 Bloom bright-pass 阶段采样 `_CameraDepthTexture`，用线性 0-1 深度分别衰减远处和近处 Bloom。NWRP 后续可以在 `NWRPBloom` 中追加两个参数，并在 Bloom active 且 attenuation 非 0 时请求 depth texture。
- Layer Include/Exclude 不建议和 Depth Attenuation 一起顺手移植。它需要额外 mask/depth 渲染路径，带宽和复杂度都明显更高，应作为单独 Phase 评估。
- `TonemappingPass.cs` 文件名与内部 `PostProcessPass` 职责仍不完全一致，后续可单独重命名为 `PostProcessPass.cs`，同步 `.meta`，减少长期维护混淆。
