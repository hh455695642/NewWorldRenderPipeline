# Phase51 Mobile Bandwidth Phase1 与深度纹理契约修正

日期：`2026-06-22`

## 概要

本阶段落地 NWRP 面向移动端 tile-based GPU 的第一批带宽治理改造，并修正 Valley Height Fog 对 `_CameraDepthTexture` 的隐式依赖问题。

Phase1 的核心目标不是把管线改造成桌面 GPU 风格的 deferred renderer，而是在现有 custom SRP / forward-first 架构下，优先减少以下成本：

- 不必要的 camera color / depth store-load。
- 重复 `SetRenderTarget`。
- 不受控的 fullscreen blit。
- Bloom 等后处理链路的高分辨率临时 RT。
- Feature 私自生成 depth texture 导致的隐藏 `CopyDepth`。

本阶段仍保持 NWRP 的模块化 Feature / Pass 架构，不引入 RenderGraph，不增加 shader variant，不新增桌面向 MRT/GBuffer 路径。

## 修改文件

- `Assets/NWRP/Runtime/NWRPCameraAttachmentPolicy.cs`
- `Assets/NWRP/Runtime/NWRPFrameDebugStats.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/Runtime/Passes/CopyColorPass.cs`
- `Assets/NWRP/Runtime/Passes/CopyDepthPass.cs`
- `Assets/NWRP/Runtime/Passes/DepthPrepass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/Passes/VegetationIndirectShadowPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Passes/ScreenBlurPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/ValleyHeightFogFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay/Passes/ValleyHeightFogOverlayPass.cs`

## 当前管线判断

NWRP 当前仍是 forward-first custom SRP：

- 主路径是 shadow、opaque、skybox、transparent、少量 screen-space feature、post process、final blit。
- depth texture / opaque texture 是按 renderer data 开关显式生成的 camera texture。
- Valley Height Fog、Cloud Shadow Projector、ScreenBlur 这类效果是独立 NWRPFeature，不应把依赖的 camera texture 生成逻辑隐藏在自身内部。

对移动端 TBDR/TBR GPU 来说，本阶段优先优化 Unity SRP 层可控的部分：RT 生命周期、load/store action、重复 target bind、临时 RT 数量、fullscreen pass 预算。硬件 tile binning / native render pass subpass 仍依赖底层 API、driver 和 Unity 渲染后端，本阶段不伪装成引擎层 tiled deferred。

## 关键实现

### 1. Camera Attachment Load/Store 策略集中化

新增 `NWRPCameraAttachmentPolicy`，将 camera attachment 的 load/store 选择从零散 pass 代码中收束到统一入口。

核心语义：

- camera setup 且本帧会 clear 时，color/depth 使用 `DontCare` load。
- 回到 camera target 继续绘制时，只有确实需要保留已有内容才 `Load`。
- 离开 camera target 进入 shadow atlas、copy、post temp RT 等目标前，显式标记 camera target 状态失效。

`NWRPFrameData` 增加 `cameraAttachmentState`，`NWRPRenderer` 增加：

```csharp
RestoreCameraRenderTarget(...)
InvalidateCameraRenderTarget(...)
```

后续 pass 不再各自猜测 camera target 当前是否仍然有效，而是通过 renderer 统一恢复。这样可以减少重复 `SetRenderTarget`，并为后续 native render pass / 更细的 discard 策略预留入口。

### 2. 跳过重复 Camera Target Bind

`NWRPRenderer` 现在会记录 camera color/depth 是否已经处于当前绑定状态。连续 opaque、skybox、transparent 或 overlay pass 需要回到同一 camera target 时，可跳过无意义的重复 bind。

这对移动端的意义是降低 render target state 切换概率，减少驱动层可能触发的 tile flush 风险。该优化不改变 pass 顺序，也不改变任何 shader 输入输出。

### 3. Fullscreen / RT 调试计数

新增 `NWRPFrameDebugStats`，用于轻量记录每帧关键带宽事件：

```text
camera target bind / skip
fullscreen blit
final blit
camera color copy
camera depth copy
temporary color RT
temporary depth RT
shadow atlas copy
```

`NewWorldRenderPipelineAsset` 增加 `logFrameDebugStats` 开关。默认关闭，避免在正式移动端运行中产生日志成本。

该统计不是替代 RenderDoc、Xcode GPU Frame Capture、AGI 或 Mali 工具，而是给 Frame Debugger 前的快速巡检提供低成本信号。

### 4. Mobile Fullscreen Budget

`NewWorldRenderPipelineAsset` 新增 `MobileBandwidthSettings`：

```text
enableMobileFullscreenBudget = true
bloomMaxMipCount = 4
bloomMaxBaseSize = 512
logFrameDebugStats = false
```

`PostProcessPass` 会根据该预算限制 bloom mip 数量和 base size。默认策略压低高分辨率 bloom pyramid 对临时 RT 和 fullscreen blit 的消耗。

当预算 mip 数不足以覆盖原有固定 6 mip compose 路径时，跳过 custom bloom compose 分支，避免 shader 采样未分配 mip。该选择偏保守：先保证移动端 RT 数量与带宽可控，再逐步恢复更复杂的 bloom 组合。

### 5. Pass 离屏写入后统一恢复 Camera Target

以下 pass 在写入非 camera target 或临时 RT 后，统一调用 renderer helper 恢复 camera target 状态：

- `CopyColorPass`
- `CopyDepthPass`
- `DepthPrepass`
- `PostProcessPass`
- `CloudShadowProjectorPass`
- `ScreenBlurPass`
- `ValleyHeightFogPass`
- `ValleyHeightFogOverlayPass`
- `MainLightShadowPassUtils`
- `VegetationIndirectShadowPass`

这样后续 draw pass 不需要依赖隐式状态，Frame Debugger 中 target 切换也更容易追踪。

## Valley Height Fog 深度纹理契约修正

### 问题现象

当 Renderer Data 中 `Enable Camera Depth Texture` 关闭时，场景里的 `NWRP_DepthTexture_RawPreviewQuad` 仍然能看到深度图；Frame Debugger 中仍有一个 opaque 后的 `CopyDepth` pass。进一步开启 `Enable Camera Depth Texture` 后，又出现新的 `CopyDepth`。

这说明有 Feature 在绕过 renderer data 的显式开关，自己创建了 hidden depth texture path。

### 根因

`ValleyHeightFogFeature` 原先持有：

```csharp
CopyDepthPass
DepthPrepass
```

并通过 `NeedsOwnEarlyDepthTexture(...)` 判断：

```text
!EnableDepthTexture -> 自己请求 depth texture
DepthTextureCopyMode.AfterTransparents -> 自己补一个 AfterOpaques depth
```

这会导致 Valley Height Fog 一旦激活，即使用户关闭 `Enable Camera Depth Texture`，也会自动追加 `CopyDepthPass` 或 `DepthPrepass`。

这个行为违背当前 NWRP 的资源契约：camera texture 的生成应由 Renderer Data 显式开关控制，Feature 只声明和消费依赖，不应私自制造全局 depth texture。

### 修正策略

`ValleyHeightFogFeature` 现在只做三件事：

1. 判断后处理和 Valley Fog volume 是否 active。
2. 判断 active renderer data 是否启用 `Enable Camera Depth Texture`。
3. 条件满足时只 enqueue `ValleyHeightFogPass`。

如果 Valley Fog active 但没有开启 renderer depth texture，则跳过效果并输出一次警告：

```text
NWRP Valley Height Fog is active but Renderer Data has Enable Camera Depth Texture disabled.
```

`ValleyHeightFogFeature` 不再创建或 enqueue：

```text
CopyDepthPass
DepthPrepass
DepthTextureFeature.GetFrameTargetRequirements(...)
```

因此 `CopyDepth` 的出现重新回到唯一来源：Renderer Data 的 `Enable Camera Depth Texture` 开关，以及内置 `DepthTextureFeature` 的 copy/prepass 模式。

## 性能与移动端策略

### CPU

- 不增加 per-object / per-instance CPU loop。
- 不引入新的 pass scheduler 分支复杂度。
- Debug stats 默认关闭，避免正式运行时日志成本。
- Feature 深度依赖通过 renderer data 控制，减少隐式状态排查成本。

### GPU

- 不新增 shader keyword。
- 不新增 MRT。
- 不新增 depth copy 路径。
- Bloom 默认预算减少高分辨率 fullscreen RT 链。
- Valley Height Fog 关闭 depth texture 时直接跳过，不再隐式产生一次 full-screen depth copy。

### Tile-Based GPU 取舍

本阶段选择保守落地：

- 优先减少 load/store、RT 切换和 fullscreen blit。
- 保留现有 pass graph，不做大范围 native render pass/subpass 重构。
- 对后处理链路先做预算控制，而不是一次性重写为复杂的 pass fusion。
- 对 depth texture 坚持显式开关，避免某个后处理 Feature 让移动端多出隐藏带宽成本。

## Shader Variant 影响

本阶段没有新增 shader keyword：

```text
新增 multi_compile: 0
新增 shader_feature_local: 0
新增全局 keyword: 0
```

所有新增控制均在 C# renderer / asset setting 层完成。Bloom 预算、frame debug stats 和 Valley Fog 深度依赖都不需要 shader variant。

## 验证记录

- `ValleyHeightFogFeature.cs` 已确认不再包含 `NeedsOwnEarlyDepthTexture`、`new CopyDepthPass`、`new DepthPrepass`。
- 相关 C# 文件执行 `git diff --check` 通过，仅有工作区 CRLF 提示。
- Unity `Editor.log` 显示脚本重新导入和 domain reload 完成，最新导入段未出现新的 C# 编译错误。
- 本阶段早期本地 `NWRP.Editor.Tests` 曾通过 `47/47`。
- 深度纹理契约补丁后，MCP `tests_run` 返回空响应，未拿到新的有效 Test Runner 报告；因此最终测试状态以静态校验和 Editor.log 编译刷新为准。

## 当前限制与后续方向

- `NWRPFrameDebugStats` 是轻量诊断，不等同于真实 GPU bandwidth counter。最终仍需在 AGI、Xcode GPU Frame Capture、Mali Graphics Debugger 或 Snapdragon Profiler 中验证 external memory bandwidth。
- `PostProcessPass` 的 bloom 预算目前采用保守 clamp；后续可以设计移动端专用低 mip bloom compose，避免在视觉效果和 RT 数量之间二选一。
- `RestoreCameraRenderTarget` / `InvalidateCameraRenderTarget` 为后续更严格的 native render pass 生命周期提供基础，但当前仍运行在现有 command buffer / RTHandle 模型上。
- Valley Height Fog 现在严格要求 renderer depth texture。后续如果需要自动依赖提示，可以在 Renderer Data Inspector 中做配置校验，但不应恢复 Feature 私自 enqueue `CopyDepthPass` 的行为。
