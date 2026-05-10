# Phase21 CopyDepth Y Flip / ScaleBias

日期：`2026-05-10`

## 概要

本阶段修复 NWRP `_CameraDepthTexture` 在 Frame Debugger / 调试采样中上下颠倒的问题。

Phase20 已经完成 Depth Texture / CopyDepth 的基础能力，并修过 fullscreen triangle `z` 导致 `_CameraDepthTexture` 纯黑的问题。本阶段不再改 depth copy 的资源模型，也不新增 pass；重点是对齐 URP 14 的 CopyDepth 方向处理：根据 source depth attachment 和 destination depth texture 的实际 Y 方向，给 CopyDepth shader 显式传入 `_BlitScaleBias`。

根因是 NWRP 之前只按 source RTHandle viewport scale 生成正向 scaleBias，默认假设 source 和 destination 的 Y 方向一致。但当前项目在 D3D11 / Metal / Vulkan 这类 `graphicsUVStartsAtTop` 平台上，Game camera 输出到 backbuffer 时和离屏 `_CameraDepthTexture` 的 Y 方向并不总是一致。尤其是 `EnableOpaqueTexture = true` 后启用 intermediate color/depth，深度 attachment 的内容方向仍应按当前 camera color 输出路径判断，不能简单把 `usesIntermediateColor` 当成 source depth 已翻转。

最终行为：Game camera 且 `camera.targetTexture == null` 时，source depth 按 backbuffer 方向视为未 flipped；`_CameraDepthTexture` 作为离屏 RT 视为 flipped。因此当前 D3D11 Game camera 路径会得到 `_BlitScaleBias = (1, -1, 0, 1)`，CopyDepth 产出的 `_CameraDepthTexture` 不再上下颠倒。

## 修改文件

- `Assets/NWRP/Runtime/Passes/CopyDepthPass.cs`
- `Assets/NWRP/ShaderLibrary/Passes/CopyDepthPass.hlsl`

## 问题根因

### 方向判断缺失

旧实现的 CopyDepth scaleBias 只保留 RTHandle viewport scale：

- `x/y` 来自 source RTHandle scale。
- `z/w` 固定为 `0`。
- 没有比较 source 和 destination 的 Y 方向。

这会让 R32 color path 和 depth target fallback path 都默认按正向采样 source depth。只要 source depth attachment 的内容方向和 `_CameraDepthTexture` 的采样方向不同，copy 后的深度图就会倒置。

### `usesIntermediateColor` 不能作为 source depth 方向依据

排查中曾尝试把 `targets.usesIntermediateColor` 作为 source depth flipped 的依据，但这会误判当前项目配置：

- `EnableOpaqueTexture = true` 会启用 intermediate color/depth。
- NWRP 当前只调用 `SetupCameraProperties(camera)`，没有像 URP 内部那样按 RTHandle target 重新设置 flipped projection matrix。
- Game camera 输出到屏幕时，source depth 内容仍应按 backbuffer 方向处理。

如果把 intermediate color/depth 直接视为 flipped，会让 source 和 destination 都被判为 flipped，最终 `yFlip = false`，等价于没有做额外 Y 翻转，Frame Debugger 里 `_CameraDepthTexture` 仍然上下颠倒。

## 关键实现

### CopyDepthPass.cs

- 新增 `GetCopyDepthScaleBias(camera, source, destination)`：
  - 保留 source RTHandle viewport scale。
  - 比较 `IsSourceDepthYFlipped(camera)` 和 `IsHandleYFlipped(camera, destination)`。
  - 方向不同则输出 `(scale.x, -scale.y, 0, scale.y)`。
  - 方向相同则输出 `(scale.x, scale.y, 0, 0)`。
- `IsSourceDepthYFlipped(camera)` 按实际相机输出路径判断：
  - `!SystemInfo.graphicsUVStartsAtTop`：不翻转。
  - `SceneView / Preview`：视为 flipped。
  - `camera.targetTexture != null`：视为 flipped。
  - 普通 Game camera 输出屏幕：视为未 flipped。
- `IsHandleYFlipped(camera, handle)` 按 URP `IsHandleYFlipped` 语义处理：
  - `!SystemInfo.graphicsUVStartsAtTop`：不翻转。
  - `SceneView / Preview`：视为 flipped。
  - 非 backbuffer RTHandle：视为 flipped。
- R32 color path 不再依赖 `Blitter.BlitCameraTexture` 的默认正向 scaleBias，改为：
  - 显式 `CoreUtils.SetRenderTarget(destination, DontCare, Store, ClearFlag.None, Color.clear)`。
  - 再调用 `Blitter.BlitTexture(cmd, source, scaleBias, material, 0)`。
- depth target fallback path 和 R32 color path 复用同一个 `GetCopyDepthScaleBias`，避免两条路径方向行为分叉。

### CopyDepthPass.hlsl

- fullscreen vertex 继续使用 NWRP 本地实现，不引入 URP/Core Blit include，避免宏和 TEXTURE2D_X 依赖扩散。
- `GetCopyDepthFullScreenTexCoord()` 只生成平台基础 UV；最终方向由 C# 传入的 `_BlitScaleBias` 统一决定。
- 保持 fullscreen position 的 `z = 0.0`，不回退到 `UNITY_NEAR_CLIP_VALUE`，避免再次触发 Phase20 中 D3D 编辑器 CopyDepth 纯黑问题。
- 不新增 shader keyword；仍只保留现有 local variants：
  - `_DEPTH_MSAA_2`
  - `_DEPTH_MSAA_4`
  - `_DEPTH_MSAA_8`
  - `_OUTPUT_DEPTH`

## 路径行为

### 当前项目默认路径

当前 `Assets/Settings/NewWorldRP.asset` 中：

- `EnableOpaqueTexture = true`
- `EnableDepthTexture = true`
- `copyDepthMode = AfterOpaques`

在 D3D11 Game camera 且 `camera.targetTexture == null` 时：

- source depth attachment：未 flipped。
- destination `_CameraDepthTexture`：离屏 RT，flipped。
- `yFlip = true`。
- `_BlitScaleBias = (1, -1, 0, 1)`。

这会在 copy 阶段补上一次 Y 翻转，使 Frame Debugger 中 `_CameraDepthTexture` 的方向与场景一致。

### targetTexture / SceneView / Preview

- `camera.targetTexture != null`：source depth 按 targetTexture 路径视为 flipped。
- `SceneView / Preview`：source 和 destination 都按 editor view 规则视为 flipped。
- 这些路径保留可预测的方向判断，避免为 Game camera 修复引入 SceneView / Preview 回归。

## 性能与移动端策略

- 本次修复不新增 RenderPass，不改变 `RenderPassEvent`。
- 不新增 RT，不改变 R32 color target / depth target fallback 的资源分配策略。
- 不新增 shader keyword，CopyDepth variant 数量仍为：
  - MSAA 4 档 × `_OUTPUT_DEPTH` 2 档，最多 8 个 local variant。
- 每帧只新增少量 CPU 侧 bool 判断和一个 `Vector4 scaleBias` 计算，GPU 成本不变。
- 移动端重点仍是控制 `AfterOpaques` full-screen depth copy 的带宽成本；若只在帧末调试或后处理读取 depth，可继续使用 `AfterTransparents` 降低透明前 target 切换压力。

## 验证记录

- Unity `AssetDatabase.Refresh()` 后，`Hidden/NWRP/CopyDepth` 无 shader error。
- `CopyDepthPass.cs` 无 C# 编译错误。
- 在当前 D3D11 Game camera / `_CameraDepthTexture` 路径中，预期 scaleBias 为 `(1, -1, 0, 1)`。
- Frame Debugger 中 `CopyDepth` pass 输出的 `_CameraDepthTexture` 不应再上下颠倒。
- 保持 `CopyDepthPass.hlsl` fullscreen position `z = 0.0`，避免 Phase20 纯黑问题回归。

## 当前限制与后续方向

- 本阶段只修 `_CameraDepthTexture` 生成方向，不处理 debug preview material 中手动 `_FlipY` 补偿值。
- 不新增 DepthNormalsTexture。
- 不处理 XR / camera stacking。
- 如果后续为 NWRP 增加真正的后处理链或 RTHandle-aware projection setup，需要重新对齐 `IsSourceDepthYFlipped` 与新的 camera target 绑定规则。
