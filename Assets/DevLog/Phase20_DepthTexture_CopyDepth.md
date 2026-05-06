# Phase20 Depth Texture / CopyDepth

日期：`2026-05-06`

## 概要

本阶段为 NWRP 增加 URP-style camera depth texture 支持。开启 `Enable Camera Depth Texture` 后，管线会生成并绑定 `_CameraDepthTexture`，供水体、透明特效、软粒子、屏幕空间调试 shader 等透明阶段或后续 pass 采样。

默认模式为 `AfterOpaques`，也就是在不透明物体和 Skybox 渲染完成后、透明物体渲染前执行 `CopyDepth`。这与 URP 在透明材质需要 scene depth 时提前 copy 的路径一致，优先满足透明 shader 使用深度的需求。

实现过程中修复了 `_CameraDepthTexture` 在 Frame Debugger 中纯黑的问题。根因是 CopyDepth fullscreen triangle 的 clip-space z 使用了 NWRP 本地 `UNITY_NEAR_CLIP_VALUE`，在 D3D 编辑器下实际落到裁剪范围外，fragment 没有执行。最终改为固定 `z = 0.0`，并补齐稳定 sampler、R32 color 输出和 texel size 上传后，`_CameraDepthTexture` 已能正确写入非零深度。

## 修改文件

- `Assets/NWRP/Runtime/DepthTextureFeature.cs`
- `Assets/NWRP/Runtime/Passes/CopyDepthPass.cs`
- `Assets/NWRP/Runtime/Passes/DepthPrepass.cs`
- `Assets/NWRP/Runtime/NWRPBlitterResources.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/ShaderLibrary/DeclareDepthTexture.hlsl`
- `Assets/NWRP/ShaderLibrary/Passes/CopyDepthPass.hlsl`
- `Assets/NWRP/Shaders/Utils/CopyDepth.shader`
- `Assets/Settings/NewWorldRP.asset`

## 关键实现

### Depth Texture Feature

- 新增 `DepthTextureFeature`，通过 `NewWorldRenderPipelineAsset.FeatureSettings.depthTexture.enableDepthTexture` 控制，默认开启。
- `copyDepthMode` 暴露为 `Camera Depth Texture Mode`，支持：
  - `AfterOpaques`
  - `AfterTransparents`
  - `ForcePrepass`
- `AfterOpaques` 映射到 `NWRPPassEvent.BeforeTransparent`，用于透明材质采样深度。
- `AfterTransparents` 映射到 `NWRPPassEvent.AfterTransparent`，用于只在帧末或调试阶段读取深度的场景。
- `ForcePrepass` 走 `DepthPrepass`，依赖材质提供 `DepthOnly` pass。

### Frame Targets

- camera depth attachment 继续作为当前相机渲染使用的 depth target。
- 新增独立 `_CameraDepthTexture` RTHandle，避免 shader 直接读取 active depth attachment。
- 支持两种输出路径：
  - `GraphicsFormat.R32_SFloat` color target，用 fullscreen CopyDepth 写入 raw depth。
  - depth target fallback，用 `_OUTPUT_DEPTH` 写入 `SV_Depth`。
- R32 路径只在 `SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.Render)` 为 true 时使用。
- 不支持 R32 render target 或强制 prepass 时，分配 depth target fallback。

### CopyDepth Pass

- `CopyDepthPass` 使用 `Blitter.BlitCameraTexture(cmd, source, destination, DontCare, Store, material, 0)` 写入 R32 target。
- 不再手动 clear R32 color target，避免 Frame Debugger 只看到 clear 且内容保持黑。
- `_CameraDepthAttachment` 通过 source `nameID` 绑定，降低 RTHandle 隐式绑定风险。
- 每次 copy 前上传 `_CameraDepthAttachment_TexelSize`，供 MSAA resolve 与 texel coord fallback 使用。
- copy 完成后显式绑定 `_CameraDepthTexture`，并恢复 camera color/depth render target。

### CopyDepth Shader

- 新增 `Hidden/NWRP/CopyDepth`。
- `CopyDepthPass.hlsl` 使用 NWRP 本地 fullscreen vertex，不依赖 URP `Blit.hlsl` 的 `TEXTURE2D_X` 环境。
- fullscreen triangle 的 clip-space z 固定为 `0.0`，避免 D3D / Metal / GLES 平台 near clip 宏差异导致三角形被裁剪。
- 非 MSAA 路径使用 `sampler_PointClamp` 采样 `_CameraDepthAttachment`。
- R32 color path fragment 返回 `float4(depth, 0, 0, 1)`，避免 scalar `SV_Target` 在单通道 RT 上的兼容性风险。
- depth fallback path 使用 `_OUTPUT_DEPTH` 输出 `SV_Depth`。
- shader keyword 均为 local keyword：
  - `_DEPTH_MSAA_2`
  - `_DEPTH_MSAA_4`
  - `_DEPTH_MSAA_8`
  - `_OUTPUT_DEPTH`

### Shader Sampling

- 新增 `DeclareDepthTexture.hlsl`，声明 `_CameraDepthTexture`，并提供：
  - `SampleSceneDepth(uv)`
  - `LoadSceneDepth(pixelCoord)`
  - `SampleSceneDepthLinear01(uv)`
  - `SampleSceneDepthLinearEye(uv)`
- 对外 shader 接口使用 URP 同名 `_CameraDepthTexture`，方便后续材质和 shader 迁移。

## 性能与移动端策略

- Depth texture 支持移动端，但按平台能力选择路径，不强制所有设备走同一条 copy。
- Android / iOS 常见 Vulkan、Metal、GLES3 设备：
  - 支持 R32 render target 时走 R32 CopyDepth。
  - 不支持 R32 render target 时走 depth target fallback。
  - GLES + MSAA depth copy 判定为不安全时走 `ForcePrepass` fallback。
- 当前实现不使用 Geometry Shader、不使用 MRT、不新增全局 keyword。
- `AfterOpaques` 会在透明前新增一次 full-screen depth copy，并要求 intermediate depth；移动端有一次额外带宽成本。
- 默认选择 `AfterOpaques` 是为了透明水面、软粒子、折射等功能可直接采样深度；若项目只在后处理或调试阶段读取 depth，可切到 `AfterTransparents` 减少中途 target 切换压力。
- `ForcePrepass` 对移动端不是默认路径，因为它会增加不透明物体 depth-only draw；仅在 copy 不可靠或确实需要 prepass 时使用。

## Review 与优化建议

本阶段复查后，移动端兼容性路径是可接受的：

- Vulkan / Metal / D3D 编辑器 R32 CopyDepth 路径已验证能写入 `_CameraDepthTexture`。
- GLES + MSAA 不硬拷贝 depth，避免移动端深度 MSAA resolve 兼容性风险。
- shader variant 控制在 4 个 local keyword 轴内，没有污染全局 keyword。
- CopyDepth pass 独立于 OpaqueTextureFeature，仍通过 frame target requirements 显式声明资源需求。

后续建议：

- 在 Android 真机分别验证 Vulkan、GLES3、开启 MSAA 三种组合。
- 如果大量透明材质读取 scene depth，可增加 debug preview shader，直接显示 `_CameraDepthTexture` 和 linear eye depth。
- 如果 `ForcePrepass` 需要成为常用路径，应补齐更多 NWRP 材质的 `DepthOnly` pass 覆盖率。
- 后续可考虑按 camera 或 platform override 控制 depth texture 默认模式，避免低端移动设备无条件承担透明前 full-screen copy。

## 验证记录

- `dotnet build .\NWRP.Runtime.csproj --no-restore -v:minimal`：0 warning / 0 error。
- Unity `AssetDatabase.Refresh()` 后 Console：0 error / 0 warning。
- MCP shader 诊断：
  - `Hidden/NWRP/CopyDepth` found。
  - `Hidden/NWRP/CopyDepth` supported。
  - 绑定 `Texture2D.whiteTexture` 手动画 CopyDepth 到 R32 RT，读回 `1.000000`。
- MCP `_CameraDepthTexture` readback：
  - RT：`_CameraDepthTexture_1920x1080_R32_SFloat_Tex2D`
  - 采样点：`576 / 576` 非零
  - 范围：`min=0.011078 max=0.033621 avg=0.021090`
- Frame Debugger 顺序：
  - `Draw Opaque Objects`
  - `CopyDepth`
  - `Draw Transparent Objects`

## 当前限制与后续方向

- v1 不实现 DepthNormalsTexture。
- v1 不处理 camera stacking / XR。
- `ForcePrepass` 依赖 shader 存在 `DepthOnly` pass，当前不自动补齐所有材质。
- 移动端最终兼容性仍应以真机 RenderDoc / Frame Debugger / GPU profiler 结果为准。
