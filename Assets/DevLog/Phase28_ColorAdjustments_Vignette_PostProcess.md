# Phase28 Color Adjustments / Vignette 后处理扩展

日期：`2026-05-14`

## 概要

本阶段继续沿用 Phase25 到 Phase27 建立的统一后处理框架，在现有 `NWRP PostProcess` 外部 pass 内部扩展 Color Adjustments 与轻量 Vignette。目标不是增加新的顶层 RenderPass，而是在最终 composite 阶段补齐常用调色能力：

- Saturate
- Brightness
- Contrast
- Daltonize
- Sepia
- Tint Color
- White Balance：Blend / Temperature
- Vignette

本阶段保持移动端优先约束：不新增 RenderTexture，不新增外部 full-screen pass，不新增 shader keyword / variant。所有新增效果通过 Volume 组件控制，运行时只在 Volume active 时请求已有 post-process intermediate color。

同时根据调参反馈修正了 Vignette 的方向与边缘过硬问题：最终行为为中心保留原色，边缘柔和混入暗角颜色。

## 修改文件

### Runtime / Renderer

- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Runtime/PostProcessing/NWRPColorAdjustments.cs`
- `Assets/NWRP/Runtime/PostProcessing/NWRPVignette.cs`
- `Assets/NWRP/Runtime/PostProcessing/PostProcessFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/TonemappingPass.cs`

### Shaders

- `Assets/NWRP/Shaders/PostProcess/NWRP_Tonemapping.shader`

### Editor

- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/Editor/NWRPColorAdjustmentsEditor.cs`
- `Assets/NWRP/Editor/NWRPVignetteEditor.cs`

## 解决的问题

### 后处理调色能力需要继续收敛到统一 PostProcess pass

Phase27 已经把 Bloom 接入 `PostProcessPass` 内部子流程，但基础调色能力仍然缺失。如果为每个效果新增独立 pass，会快速增加 full-screen 写回次数和 Frame Debugger 顶层 pass 数量，不适合移动端。

本阶段将 Color Adjustments 和 Vignette 合入 `NWRP_Tonemapping.shader` 的最终输出阶段：

```text
HDR Camera Color
    -> Bloom / Lens Dirt
    -> Tonemapping or Linear
    -> Color Adjustments
    -> Vignette
    -> camera/backbuffer target
```

Renderer 侧仍然只看到一个 `NWRP PostProcess`。Bloom-only、Tonemapping-only、Color-only、Vignette-only 都走同一外部 post-process stage。

### 具体效果开关不应该放在 Pipeline Asset 中

初始计划中曾考虑在 `NewWorldRenderPipelineAsset.FeatureSettings` 增加 Bloom / ColorAdjustments / Vignette 的硬开关。但后续确认：具体后处理效果不应由管线资产承载，否则会出现同一个项目级资产影响所有相机和所有 Volume 的问题，也容易让调参人员误判 Volume 为什么不生效。

最终规则收敛为：

- `supportsPostProcessing` 保留为管线级总能力开关。
- Camera 上的 `NWRPCameraData.renderPostProcessing` 控制相机是否执行 NWRP 后处理。
- 具体效果只由 Volume 组件的 active 状态和参数决定。
- Pipeline Asset 的 Feature Settings 不再出现 `Enable NWRP Bloom`、`Enable NWRP Color Adjustments`、`Enable NWRP Vignette`。

因此本阶段同步移除了已加入过的 per-effect pipeline asset toggle，并从 Bloom active 判断中移除了 asset 级 Bloom 开关。

### Volume 默认值不能让未覆盖参数产生隐式效果

`NWRPColorAdjustments` 需要兼容 Beautify 参数语义，其中 `saturate` 默认值为 `1`，但中性值是 `0`。如果直接读取默认 Volume stack 值，会导致未覆盖参数也影响画面。

本阶段在运行时上传参数时统一遵守 `overrideState`：

- 参数未 override 时使用 NWRP 自己的中性 fallback。
- 参数 override 后才使用 Volume 中的实际值。
- `IsActive()` 同样只把 override 且非中性的参数视为 active。

这样添加组件但不勾 override 不会改变已有画面。

### Vignette 初版方向反了

第一次接入 Vignette 后，实际画面出现中心变暗，边缘反而不符合暗角预期。原因是 shader 端把中心区域映射到了 vignette color 端。

修正后逻辑改为：

```text
center -> edgeBlend = 0 -> 保留原色
edge   -> edgeBlend = 1 -> 混入 vignette color
```

同时 `innerRing` 未覆盖时使用清晰中心区 fallback，避免只调 `outerRing` 时中心被过早压暗。

### Vignette 边缘过实

方向修正后，暗角边缘仍然偏硬，尤其在黑色 vignette color 和较高 alpha 下边缘实感明显。

本阶段继续调整 falloff：

```hlsl
edgeBlend = smoothstep(0.0h, 1.0h, edgeBlend);
edgeBlend *= edgeBlend;
```

这让暗角边缘从线性过渡改为更柔和的 S 曲线，并在中间段稍微减弱混合强度。该修正只增加少量 ALU，不增加采样、RT 或 variant。

## 关键实现

### NWRPColorAdjustments

新增 Volume 组件：

```text
NWRP/Post-processing/Color Adjustments
```

参数范围按 Beautify 兼容语义：

- `saturate`：默认 `1`，范围 `[-2, 3]`，override 后按 Beautify saturation boost 公式执行。
- `brightness`：默认 `1`，范围 `[0, 2]`。
- `contrast`：默认 `1`，范围 `[0.5, 1.5]`。
- `daltonize`：默认 `0`，范围 `[0, 2]`。
- `sepia`：默认 `0`，范围 `[0, 1]`。
- `tintColor`：默认 `(1,1,1,0)`，alpha 控制混合。
- `colorTempBlend`：默认 `0`，范围 `[0,1]`。
- `colorTemp`：默认 `6550`，范围 `[1000,40000]`。

运行时上传到：

- `_NWRPColorAdjustParams`
- `_NWRPColorAdjustParams2`
- `_NWRPColorAdjustTint`

### NWRPVignette

新增 Volume 组件：

```text
NWRP/Post-processing/Vignette
```

本阶段只实现轻量数学版，不包含 mask texture / blink：

- `outerRing`
- `innerRing`
- `fade`
- `center`
- `color`
- `circularShape`
- `fitMode`
- `aspectRatio`

运行时上传到：

- `_NWRPVignetteColor`
- `_NWRPVignetteParams`
- `_NWRPVignetteParams2`

当 `circularShape` 开启时，C# 侧根据 camera aspect 计算横向或纵向缩放；shader 侧只做简单距离计算与 falloff 混合。

### NWRPRenderer / NWRPFrameData

`NWRPFrameData` 新增缓存：

```csharp
public bool colorAdjustmentsActive;
public bool vignetteActive;
public NWRPColorAdjustments colorAdjustments;
public NWRPVignette vignette;
```

`ConfigurePostProcessingFromVolume` 在更新 Volume stack 后读取：

- `NWRPTonemapping`
- `NWRPBloom`
- `NWRPColorAdjustments`
- `NWRPVignette`

`PostProcessFeature.HasAnyActivePostProcess` 扩展为：

```text
tonemappingActive
|| bloomActive
|| colorAdjustmentsActive
|| vignetteActive
```

因此单独使用 Color Adjustments 或 Vignette 时，也会正确请求 intermediate color 并 enqueue `NWRP PostProcess`。

### NWRP_Tonemapping.shader

本阶段没有新增 shader keyword。所有效果由 uniform 控制：

- `NWRP_COLOR_ADJUST_ACTIVE`
- `NWRP_VIGNETTE_ACTIVE`

Color Adjustments 顺序为：

```text
Daltonize
Sepia
Saturate
Tint
Contrast
Brightness
White Balance
```

Daltonize 使用 Beautify 风格的 luma-preserving correction，避免大幅改变整体亮度。Contrast 在 Linear 色彩空间中沿用 Beautify 的折算方式：

```csharp
contrast = 1f + (contrast - 1f) / 2.2f;
```

White Balance 使用 KelvinToRGB 近似函数，当前在 shader 中执行。若后续需要进一步压低移动端 ALU，可改为 C# 侧预计算 Kelvin RGB multiplier 后上传。

### Editor Drawer

新增：

- `NWRPColorAdjustmentsEditor`
- `NWRPVignetteEditor`

Color Adjustments editor 将 White Balance 单独分组，便于调参。

Vignette editor 根据 `circularShape` 的 override 和 value 决定显示：

- circular 时显示 `fitMode`
- 非 circular 时显示 `aspectRatio`

这只影响 Inspector 展示，不改变序列化字段名和运行时行为。

## 性能与 Variant 风险

- 不新增外部 `NWRPPass`。
- 不新增 RenderTexture。
- 不新增 full-screen Blit。
- 不新增 `multi_compile`。
- 不新增 `shader_feature`。
- Color Adjustments 与 Vignette 都是最终 composite 中的 uniform 分支和少量 ALU。
- Vignette 不采样 mask 纹理，避免额外带宽。
- Pipeline Asset 不提供 per-effect 后处理开关，减少全局状态复杂度。

移动端主要成本来自 final composite 中的额外 ALU。相对新增一次全屏 RT 读写，这个代价更可控。

## 验证记录

- `AssetDatabase.Refresh()` 后 Unity 编译完成。
- Unity Console 无 Error / Exception。
- `NWRP_Tonemapping.shader` 定向检查无新增 `multi_compile` / `shader_feature`。
- `Assets/NWRP` 运行时代码未引入 `UnityEngine.Rendering.Universal`。
- `git diff --check` 对本阶段代码文件无 whitespace error。
- EditMode 测试入口返回当前项目未发现测试。

调参反馈修正：

- 初版 Vignette 出现中心变暗，已修正为中心保留、边缘暗角。
- 第二轮反馈边缘过实，已将 falloff 调整为更柔和的 `smoothstep` 曲线。

## 当前状态与后续建议

- `NWRP_TonemappingProfile.asset` 在测试过程中被 Unity 添加过 ColorAdjustments / Vignette 组件，用于调试与观察效果；如果后续不希望 sample profile 带这些组件，可以单独整理样例配置。
- Vignette 当前是轻量数学版。如果未来需要美术 mask，应作为单独阶段评估，因为它会增加一次纹理采样并引入额外资源依赖。
- White Balance 当前在 shader 中执行 Kelvin 计算。若移动端 profiling 发现 ALU 压力，可以把 KelvinToRGB 移到 C# 上传阶段。
- `TonemappingPass.cs` 文件名仍与内部 `PostProcessPass` 职责不完全一致，后续可单独重命名为 `PostProcessPass.cs` 并同步 `.meta`。
