# Phase22 Depth Sampling / Depth To World Reconstruction

日期：`2026-05-11`

## 概要

本阶段修复 NWRP 在 `_CameraDepthTexture` 采样、屏幕空间 UV 以及 Depth to World 重建链路上的基础契约问题。

Phase20 已经完成 `_CameraDepthTexture` 的生成与 CopyDepth 基础能力，Phase21 继续处理 CopyDepth 写入方向。本阶段不再改动 CopyDepth pass，也不引入新的渲染 pass；重点转到 shader 读取侧：让 NWRP shader 具备与 URP 14 接近的 `_ScaledScreenParams`、`_ScaleBiasRt`、normalized screen UV、GPU projection inverse VP 以及 `ComputeWorldSpacePosition(positionNDC, deviceDepth, UNITY_MATRIX_I_VP)` 路径。

修复后，透明水面、depth debug shader、后续软粒子或屏幕空间效果可以用统一的 `SampleSceneDepth(screenUV)` 与 Depth to World helper 读取 scene depth，避免各 shader 自己手写 Y flip、屏幕尺寸和 inverse VP 逻辑。

## 修改文件

- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/ShaderLibrary/DeclareDepthTexture.hlsl`
- `Assets/NWRP/ShaderLibrary/SpaceTransforms.hlsl`
- `Assets/NWRP/ShaderLibrary/UnityInput.hlsl`

## 修复的问题

### 屏幕空间 UV 缺少 URP-style 全局参数

旧路径里 shader 主要依赖 `_ScreenParams` 或手写 `SV_POSITION.xy / size` 来生成 screen UV，没有完整对齐 URP 的 `_ScaledScreenParams` 和 `_ScaleBiasRt` 语义。

这会导致以下问题：

- 使用 intermediate color、SceneView、targetTexture 或不同平台 RT 方向时，screen UV 与实际 scene texture 方向不一致。
- `_CameraDepthTexture` 明明已经由 CopyDepth 正确写入，但 shader 采样后的 linear eye depth 与 Draw Opaque 画面上下不一致。
- 基于 depth 重建的 world grid 会表现为随屏幕或相机漂移，而不是稳定锚定到 opaque world position。

### Depth to World 缺少统一入口

Depth to World 重建需要同时满足三个条件：

- 输入 UV 是经过当前 camera RT 方向处理后的 normalized screen UV。
- 输入 depth 是 device depth，而不是已经 linearized 的 depth。
- inverse VP 必须来自 GPU projection matrix，而不是未处理平台翻转的 CPU projection matrix。

旧实现缺少集中 helper，不同 shader 容易混用 raw UV、linear depth 或不匹配的 inverse matrix，导致 Game View 和 Scene View 结果不一致。

### SceneView 采样方向与 Game View 不一致

Game View 修正后，SceneView 仍可能出现 depth texture 读取方向与 opaque pass 不一致的问题。根因在于 SceneView / Preview 在 `graphicsUVStartsAtTop` 平台下有独立的 handle Y 方向规则。

本阶段新增 `_CameraDepthTextureScaleBias`，让默认 Game 路径保持 identity depth sampling，同时只对 SceneView / Preview 提供 per-camera depth texture UV bias，避免再把全局 `SampleSceneDepth` 写成硬编码 `1 - y`。

## 关键实现

### NWRPRenderer.cs

- 新增 `SetCameraScreenGlobals(ref NWRPFrameData frameData)`。
- 每相机上传：
  - `_ScreenParams`
  - `_ScaledScreenParams`
  - `_ScaleBiasRt`
  - `_CameraDepthTextureScaleBias`
- `_ScaledScreenParams` 使用当前 camera target 的实际渲染尺寸：
  - 优先使用 `cameraColorHandle.rt.width / height`
  - fallback 到 `camera.pixelWidth / pixelHeight`
- `_ScaleBiasRt` 按 URP-style 规则区分：
  - Game camera 直出 backbuffer 且无 `targetTexture`：`(1, 0, 1, 1)`
  - RT / SceneView / Preview / targetTexture / intermediate target：`(-1, 1, -1, 1)`
- `_CameraDepthTextureScaleBias` 用于 scene depth 读取侧：
  - 默认：`(1, 1, 0, 0)`
  - SceneView / Preview：`(1, -1, 0, 1)`
- 新增 `SetCameraMatrices(ref NWRPFrameData frameData)`，统一上传：
  - `unity_MatrixInvV`
  - `unity_MatrixInvP`
  - `unity_MatrixInvVP`
- inverse projection 使用 `GL.GetGPUProjectionMatrix(projectionMatrix, projectionFlipped)` 后再求逆，保证 shader 侧 inverse VP 与 GPU clip-space 一致。

### NWRPShaderIds.cs

新增 shader property id，避免运行时反复字符串查找：

- `_CameraDepthTextureScaleBias`
- `_ScreenParams`
- `_ScaledScreenParams`
- `_ScaleBiasRt`
- `unity_MatrixInvV`
- `unity_MatrixInvP`
- `unity_MatrixInvVP`

### UnityInput.hlsl

补齐 NWRP shader 全局输入：

- `_ScaledScreenParams`
- `_ScaleBiasRt`

这些变量保持 URP-compatible 命名，后续迁移 URP shader 或复用 URP 风格 helper 时不需要每个 shader 单独适配。

### SpaceTransforms.hlsl

新增 URP-compatible 屏幕与重建 helper：

- `GetScaledScreenParams()`
- `TransformScreenUV(inout float2 uv, float screenHeight)`
- `TransformScreenUV(inout float2 uv)`
- `TransformNormalizedScreenUV(inout float2 uv)`
- `GetNormalizedScreenSpaceUV(float2 positionCS)`
- `GetNormalizedScreenSpaceUV(float4 positionCS)`
- `ComputeClipSpacePosition(float2 positionNDC, float deviceDepth)`
- `ComputeWorldSpacePosition(float2 positionNDC, float deviceDepth, float4x4 invViewProjMatrix)`

Depth to World 的标准路径变为：

```hlsl
float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
float deviceDepth = SampleSceneDepth(screenUV);
float3 sceneWS = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
```

### DeclareDepthTexture.hlsl

- 保留 `_CameraDepthTexture` 与 `sampler_CameraDepthTexture`。
- 新增 `_CameraDepthTextureScaleBias`。
- 新增 `TransformSceneDepthUV(screenUV)`。
- `SampleSceneDepth(screenUV)` 统一走：

```hlsl
SampleSceneDepthRawTextureUV(TransformSceneDepthUV(screenUV))
```

- 保留 raw helper：
  - `SampleSceneDepthRawTextureUV(textureUV)`
  - `LoadSceneDepthRawTextureCoord(pixelCoord)`
- `LoadSceneDepth(pixelCoord)` 不做额外 Y flip，避免 pixel coord 读取路径和 texture UV 采样路径混在一起。

## 路径行为

### Game View

普通 Game camera 直出 backbuffer、无 `targetTexture` 时：

- `_ScaleBiasRt = (1, 0, 1, 1)`
- `_CameraDepthTextureScaleBias = (1, 1, 0, 0)`
- `SampleSceneDepth(screenUV)` 不额外翻转 depth UV

这保持 CopyDepth 已经写好的 `_CameraDepthTexture` 方向，不在 shader 采样侧再补一次全屏 Y flip。

### SceneView / Preview

SceneView / Preview 在 `SystemInfo.graphicsUVStartsAtTop == true` 平台下：

- `_ScaleBiasRt` 按 flipped handle 处理 screen UV
- `_CameraDepthTextureScaleBias = (1, -1, 0, 1)`

这样可以让 debug shader 中的 linear eye depth 与 opaque 结果方向一致，同时不影响 Game View 当前正确路径。

### targetTexture / intermediate target

`camera.targetTexture != null` 或 camera color handle 不是 backbuffer 时，projection flip 和 `_ScaleBiasRt` 按 RT 路径处理。后续屏幕空间效果应优先使用 `GetNormalizedScreenSpaceUV(positionCS)`，不要直接手写 `positionCS.xy / _ScreenParams.xy`。

## 性能与移动端策略

- 不新增 RenderPass。
- 不新增 RenderTexture。
- 不修改 CopyDepth 资源模型。
- 不增加 fullscreen blit。
- 每相机只增加少量 CPU 侧矩阵 / `Vector4` 上传。
- shader 侧新增的是 uniform scale/bias 和矩阵重建 helper，不引入额外纹理采样。
- Depth to World 使用 `float`，这是屏幕空间重建的精度必要成本；移动端材质颜色、mask 强度等仍可继续用 `half`。

## Shader Variant 风险

本阶段没有新增 shader keyword：

- 不新增 `multi_compile`
- 不新增 `shader_feature`
- 不新增 local keyword
- variant 数量不变

所有切换都通过 per-camera global uniform 或 shader helper 完成，符合移动端 variant 控制要求。

## 验证记录

- Unity `AssetDatabase.Refresh()` 后 Console：无 Error。
- `SampleSceneDepth(screenUV)` 不再在 sampler 内写死全局 `1 - y`。
- Game View debug output `4` 的 linear eye depth 与 Draw Opaque 方向一致。
- Scene View debug output `4` 的 linear eye depth 与 Draw Opaque 方向一致。
- depth reconstructed world grid 使用 scene depth 重建的 `sceneWS.xz`，移动相机时应锚定 opaque world position，而不是随屏幕滑动。
- 对照 debug path 使用相反 depth UV 时会重新出现方向错误，用于确认正常路径不是偶然对齐。
- 保护文件未纳入本阶段修改：
  - `CopyDepthPass.cs`
  - `CopyDepthPass.hlsl`
  - `ShorelineTest_NWRP.shader`
  - `shoreline_test`

## 当前限制与后续方向

- 本阶段只修复 depth sampling、screen UV、Depth to World 基础链路，不扩展完整水体渲染。
- `_CameraDepthTextureScaleBias` 是 NWRP 当前 SceneView / Preview 路径下的读取侧兼容处理；如果后续重构 RTHandle projection setup 或 CopyDepth SceneView 写入规则，需要重新验证这条契约。
- 不处理 XR / camera stacking。
- 不新增 DepthNormalsTexture。
- 后续水体交互、岸线遮罩、软粒子等功能应复用本阶段 helper，不再各 shader 内自行实现 screen UV 和 depth Y flip。
