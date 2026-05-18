# Phase34 FXAA PostProcess Anti-Aliasing

日期：`2026-05-18`

## 概要

本阶段为 NWRP 增加第一版轻量级后处理抗锯齿能力：`FXAA`。

实现目标不是建立一套完整抗锯齿矩阵，而是在当前移动端优先的自定义 SRP 中，先提供一个低带宽、低接入复杂度、可通过 Volume 控制的 baseline 方案。FXAA 被合并到现有 `NWRP PostProcess` 的最终 composite 阶段，不新增顶层 `NWRPPass`，不新增额外中间 LDR RenderTexture，也不新增 shader keyword / variant。

当前阶段明确不实现 MSAA / SMAA / TAA：
- MSAA 需要重做 camera color/depth RT 的 `msaaSamples`、resolve、depth copy、post-process 输入和 `targetTexture` 兼容，移动端带宽成本较高。
- SMAA 通常需要多 pass 和额外纹理资源，不适合作为本阶段移动端 baseline。
- TAA 需要 jitter、history、motion vector 和 ghosting 控制，当前 NWRP 基础设施还不适合直接接入。

## 修改文件

### Runtime / Renderer

- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/PostProcessing/NWRPAntiAliasing.cs`
- `Assets/NWRP/Runtime/PostProcessing/PostProcessFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`

### Shaders

- `Assets/NWRP/Shaders/PostProcess/NWRP_Tonemapping.shader`

### Editor

- `Assets/NWRP/Editor/NWRPAntiAliasingEditor.cs`

## 解决的问题

### NWRP 缺少移动端友好的屏幕空间抗锯齿入口

当前 NWRP camera color/depth RT 仍固定 `msaaSamples = 1`。虽然项目 `QualitySettings` 中存在 MSAA 设置，但自定义 SRP 内部并未真正走 MSAA resolve 路径。

如果直接接 MSAA，需要处理：
- color/depth attachment 多采样描述符
- 后处理前的 color resolve
- depth copy 与 depth texture 的 MSAA 兼容
- GLES / Metal / Vulkan 差异
- render scale 与 targetTexture 的组合路径

这些改动会扩大渲染目标管理复杂度，并在 Tile-Based GPU 上引入更高带宽压力。因此本阶段优先接入 FXAA，覆盖几何边缘、透明边缘、alpha test 和 shader 高频边缘，作为低成本 baseline。

### 抗锯齿不应增加外部 RenderPass

NWRP 从 Phase25 起已经把后处理收敛到统一 `PostProcessPass`：

```text
Transparent
    -> NWRP PostProcess
    -> DebugOverlay / FinalBlit
```

本阶段延续该约束。FXAA 不作为独立 `NWRPPass`，而是成为 `NWRP_Tonemapping.shader` 的最终输出 pass 版本：

```text
HDR Camera Color
    -> Bloom / Lens Dirt
    -> Tonemapping or Linear
    -> Color Adjustments
    -> Vignette
    -> FXAA
    -> camera/backbuffer target
```

这样 Frame Debugger 的顶层 pass 结构不会扩张，也避免了移动端额外一次全屏 RT 读写。

### FXAA-only 也必须触发 PostProcess

新增 `NWRPAntiAliasing` 后，`PostProcessFeature.HasAnyActivePostProcess` 扩展为：

```text
tonemappingActive
|| bloomActive
|| colorAdjustmentsActive
|| vignetteActive
|| antiAliasingActive
```

因此只开启 FXAA，而不开 Bloom / Tonemapping / Color Adjustments / Vignette 时，也会正确请求 intermediate color 并 enqueue `NWRP PostProcess`。

## 关键实现

### NWRPAntiAliasing Volume 组件

新增 Volume 组件：

```text
NWRP/Post-processing/Anti Aliasing
```

参数：
- `mode`：`None / FXAA`
- `fixedThreshold`：默认 `0.0833`
- `relativeThreshold`：默认 `0.166`
- `subpixelBlending`：默认 `0.75`

`IsActive()` 只在组件 active 且 `mode == FXAA` 时返回 true。参数通过 Volume override 控制，未 override 时使用 NWRP 默认值，避免添加组件但未显式调参时产生不可预期的参数漂移。

### NWRPFrameData / NWRPRenderer

`NWRPFrameData` 新增：

```csharp
public bool antiAliasingActive;
public NWRPAntiAliasing antiAliasing;
```

`NWRPRenderer.ConfigureCameraData` 初始化默认关闭状态，`ConfigurePostProcessingFromVolume` 在更新 Volume stack 后读取 `NWRPAntiAliasing` 并缓存 active 状态。

SceneView 行为继续沿用现有后处理规则：SceneView 由 Unity Scene 视图 Effects/Post Processing toggle 控制，Game camera 由 `NWRPCameraData.renderPostProcessing` 与 Volume 共同控制。

### PostProcessPass pass offset

`PostProcessPass` 保留原有 4 个 tonemapping pass：

- Linear
- ACES
- ACES Fitted
- AGX

新增 FXAA pass offset：

```csharp
private const int k_FxaaPassOffset = 4;
```

当 FXAA active 时，在原 tonemapping pass index 基础上加 offset，选择对应的 FXAA 版本 pass。这样不需要 keyword，也不需要在 shader 内做 FXAA 开关分支。

Runtime 上传：

- `_NWRPFxaaParams`
- `_NWRPFxaaTexelSize`

`_NWRPFxaaTexelSize` 基于 post-process source RT 实际尺寸计算，能够覆盖 render scale 下的内部渲染尺寸。

### NWRP_Tonemapping.shader

`NWRP_Tonemapping.shader` 新增 4 个 FXAA pass：

- `Linear FXAA`
- `ACES FXAA`
- `ACES Fitted FXAA`
- `AGX FXAA`

FXAA 版本 pass 会复用同一套最终颜色解析函数：

```text
Fetch source
    -> Bloom
    -> Tonemap
    -> Color Adjustments
    -> Vignette
```

随后在最终 LDR 输出空间上执行 FXAA。这样 FXAA 处理的是已经 tone mapped、已经完成调色和暗角的最终图像，更符合屏幕空间抗锯齿预期。

Shader 侧没有新增：
- `multi_compile`
- `shader_feature`
- `_FXAA` keyword
- URP shader include

### Editor Drawer

新增 `NWRPAntiAliasingEditor`，只在 `mode` override 且值为 `FXAA` 时显示 FXAA 参数。

这与现有 Bloom / Tonemapping / Vignette editor 的思路保持一致：Inspector 只显示当前真正相关的参数，减少调参噪音。

## 性能与移动端取舍

CPU：
- 只增加一次 Volume 组件读取。
- 只上传少量 global uniform。
- 不增加 culling、renderer list 或 per-object 逻辑。

GPU：
- 不新增外部 fullscreen pass。
- 不新增 RenderTexture。
- 不增加 MRT。
- FXAA active 时在最终 composite shader 内增加邻域采样和少量 ALU。

带宽：
- 相比新增独立 FXAA blit，本阶段避免额外一次全屏 source read + target write。
- FXAA 仍会增加同一 pass 内的纹理采样次数，因此默认应作为可选 Volume 效果，而不是强制全局开启。

移动端建议：
- 中低端机型优先使用 FXAA，而不是 MSAA。
- 对 UI 相机或需要像素锐利的 camera，可通过不启用该 camera 的 post-processing 或不在对应 Volume 中启用 FXAA 来避免软化。
- 若后续加入 MSAA，应作为高端档单独阶段处理，并优先建立 resolve / depth copy / render scale 的兼容矩阵。

## Variant 风险

本阶段没有新增 shader keyword，因此不会扩大 shader variant 组合。

FXAA 通过以下方式控制：
- C# 根据 Volume active 状态选择 shader pass。
- 参数通过 uniform 上传。
- shader 中没有 `_FXAA` keyword，也没有 `shader_feature_local`。

新增 pass 会增加同一个 hidden shader 的 pass 数，但不会产生 keyword 组合爆炸。相比在原 pass 内加入 keyword 分支，该方式更符合 NWRP 当前“严格控制 variant”的约束。

## 验证记录

已完成静态与编译验证：

- `git diff --check` 通过。
- `dotnet build NWRP.Runtime.csproj --no-restore` 使用临时输出目录通过，0 error / 0 warning。
- `dotnet build NWRP.Editor.csproj --no-restore` 通过，只有项目既有 NuGet / Unity assembly 版本冲突 warning。
- 定向检查新增 FXAA 相关 shader 与 runtime 代码，未发现 `UnityEngine.Rendering.Universal`、`ScriptableRendererFeature`、`ScriptableRenderPass` 或 URP shader include。
- 定向检查 `NWRP_Tonemapping.shader`，未新增 `multi_compile` / `shader_feature` / `_FXAA` keyword。
- Unity 当前活动 Editor 日志显示新增 `NWRPAntiAliasing.cs` 与 `NWRPAntiAliasingEditor.cs` 已导入并完成 domain reload，未看到 C# 编译错误。

未完成项：

- Unity batchmode 全量验证被当前已打开的 Unity Editor 实例阻止，日志为 `HandleProjectAlreadyOpenInAnotherInstance`。这不是代码编译错误，而是同一项目不能被多个 Unity 实例同时打开。
- 尚未进行 Frame Debugger 人工截图验证；预期顶层顺序仍为 `Transparent -> NWRP PostProcess -> DebugOverlay/FinalBlit`。

## 当前边界与后续建议

- 本阶段只实现 FXAA，不实现 MSAA / SMAA / TAA。
- FXAA 当前集成在后处理最终输出中，因此依赖 `supportsPostProcessing`、camera `renderPostProcessing` 和 Volume active 状态。
- 后续如需 MSAA，应单独建立 Phase，先处理 camera RT descriptor、resolve、depth copy、render scale、targetTexture 与移动平台兼容。
- 后续如需更高质量屏幕空间 AA，可评估 SMAA，但应明确其额外 pass、area/search texture 和带宽成本。
- 后续如需 TAA，应先补齐 motion vector / history RT / jitter / responsive mask 等基础设施，不能直接作为轻量功能塞入当前 PostProcessPass。
