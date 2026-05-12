# Phase25 HDR Color Buffer / PostProcess Framework v1 / Tonemapping

日期：`2026-05-12`

## 概要

本阶段基于 `main` 当前 Phase24 收口后的代码继续推进。Phase24 的重点是解除 NWRP 自有 runtime / shader 对 URP package source 的依赖；本阶段在这个基础上补齐两条能力：

1. 之前工作区中尚未单独提交的 HDR camera color buffer 基础能力。
2. NWRP 自己的后处理框架 v1，并以 Tonemapping 作为第一版实际效果。

本阶段仍保持移动端优先原则：默认路径不因为后处理框架存在而新增中间 RT 或 fullscreen blit；只有 Camera 显式开启 NWRP 后处理、Volume 中存在 active 的 NWRP Tonemapping 时，才请求 intermediate color。Tonemapping 作为当前最后一个 post-process operator，直接写回 camera/backbuffer target，并标记本帧已经 present，让 `FinalBlitPass` 跳过，避免 Tonemap 后再额外做一次 FinalBlit。

同时，本阶段将后处理对外调度收敛为统一的 `NWRP PostProcess` pass。Frame Debugger 中不再以 `NWRP Tonemapping` 作为独立外部 pass 暴露，Tonemapping 只是 `PostProcessPass` 内部当前最后一步。后续 Bloom、LUT、Sharpen、DOF 等效果应继续接入统一 PostProcess 阶段，而不是每个效果都独立暴露为一个顶层 `NWRPPass`。

## 修改文件

### HDR / pipeline asset / renderer

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/Settings/NewWorldRP.asset`

### PostProcessing runtime

- `Assets/NWRP/Runtime/NWRPCameraData.cs`
- `Assets/NWRP/Runtime/NWRPCameraData.cs.meta`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPPassEvent.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/PostProcessing.meta`
- `Assets/NWRP/Runtime/PostProcessing/NWRPTonemapping.cs`
- `Assets/NWRP/Runtime/PostProcessing/NWRPTonemapping.cs.meta`
- `Assets/NWRP/Runtime/PostProcessing/PostProcessFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/PostProcessFeature.cs.meta`
- `Assets/NWRP/Runtime/PostProcessing/Passes.meta`
- `Assets/NWRP/Runtime/PostProcessing/Passes/TonemappingPass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/TonemappingPass.cs.meta`

> 当前文件名仍为 `TonemappingPass.cs`，但修正后内部类已经收敛为 `PostProcessPass`。后续可以单独重命名为 `PostProcessPass.cs`，避免和当前职责不完全一致。

### Editor / sample scene / shader

- `Assets/NWRP/Editor/NWRPCameraDataAutoAdd.cs`
- `Assets/NWRP/Editor/NWRPCameraDataAutoAdd.cs.meta`
- `Assets/NWRP/Shaders/PostProcess.meta`
- `Assets/NWRP/Shaders/PostProcess/NWRP_Tonemapping.shader`
- `Assets/NWRP/Shaders/PostProcess/NWRP_Tonemapping.shader.meta`
- `Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity`
- `Assets/NWRP/Tests/Scenes/MaterialSampleScene/NWRP_TonemappingProfile.asset`
- `Assets/NWRP/Tests/Scenes/MaterialSampleScene/NWRP_TonemappingProfile.asset.meta`

## 解决的问题

### NWRP 之前没有独立的后处理控制入口

旧状态下，NWRP 只有基础渲染、阴影、OpaqueTexture、DepthTexture、FinalBlit 等路径，没有自己的 Camera / Volume 后处理入口。如果直接复用 URP 的 `UniversalAdditionalCameraData` 或 URP Tonemapping override，会重新把当前自定义 SRP 和 URP 的相机/Volume 模型绑在一起，不符合 Phase24 的依赖边界。

本阶段新增 `NWRPCameraData` 作为 NWRP 相机级后处理入口：

- `renderPostProcessing`：相机是否执行 NWRP 后处理。
- `volumeLayerMask`：该相机采样的 Volume layer。
- `volumeTrigger`：Volume 采样位置，未指定时回退到 Camera transform。

运行时如果 Camera 缺少 `NWRPCameraData`，默认不执行后处理，避免所有旧相机隐式付费。Editor 下如果当前 Render Pipeline 是 NWRP，会自动给场景里的 Game Camera 添加该组件，并默认 `renderPostProcessing = true`，方便测试场景直接使用。

### NWRP 需要自己的 Volume override，而不是复用 URP Tonemapping

新增 `NWRPTonemapping : VolumeComponent`，菜单归属：

```text
NWRP/Post-processing/Tonemapping
```

该组件只依赖 Unity Core Volume 系统，不依赖 `UnityEngine.Rendering.Universal`。公开参数包括：

- `Mode`：`None / Linear / ACES / ACESFitted / AGX`
- `preExposure`
- `postBrightness`
- `maxInputBrightness`
- `agxGamma`

`None` 作为禁用状态；只有 mode 非 `None` 且 Volume component active 时，才认为 Tonemapping active。

### HDR camera color buffer 需要独立 capability 和格式选择

本阶段把之前尚未单独提交的 HDR 基础能力整理进 Phase25 记录。

`NewWorldRenderPipelineAsset` 新增：

- `supportsHDR`
- `hdrColorBufferPrecision`
- `HDRColorBufferPrecision._32Bits`
- `HDRColorBufferPrecision._64Bits`

Renderer 侧通过 Camera 和 Asset 双重判断启用 HDR：

```text
camera.allowHDR && asset.SupportsHDR
```

当相机启用 HDR 且直接面向 backbuffer 渲染时，NWRP 会请求 intermediate color，避免 HDR 内容直接落到 LDR backbuffer 造成高亮被提前截断。颜色格式策略为：

- 移动端默认优先尝试 `B10G11R11_UFloatPack32`，减少带宽。
- 如果需要 alpha 或选择 64-bit precision，则优先尝试 `R16G16B16A16_SFloat`。
- 不支持目标格式时回退到 Unity 的 `DefaultFormat.HDR`。

该能力是 Tonemapping 的基础，因为 Tonemapping 需要从 HDR camera color 采样，再输出到 LDR camera/backbuffer target。

### Tonemapping 不应导致 Tonemap + FinalBlit 两次全屏写

`PostProcessFeature` 在检测到 active post-process 后，请求 intermediate color。统一 `PostProcessPass` 从 HDR camera color 采样，并在当前 v1 中执行 Tonemapping final：

```text
HDR camera color -> NWRP PostProcess / Tonemapping -> camera/backbuffer target
```

完成后设置：

```text
frameData.targets.cameraColorPresented = true
```

`FinalBlitPass` 检测到该标记后跳过实际 blit，只执行必要的 command buffer flush。这样 Tonemapping 既承担色彩映射，也承担最终 present，避免：

```text
HDR color -> Tonemap intermediate -> FinalBlit -> backbuffer
```

这种多一次 fullscreen write 的路径。

### Frame Debugger 中后处理不应按单效果暴露为多个顶层 pass

第一次接入时 Frame Debugger 中可以看到 `NWRP Tonemapping` pass。这个设计对 v1 可用，但不适合扩展：后续 Bloom、LUT、Sharpen、DOF 等如果都变成独立外部 `NWRPPass`，Frame Debugger 和 pass 调度会越来越碎，也容易打破“最后一个 post-process 直接 present”的规则。

本阶段将外部 pass 名称和职责统一为：

```text
NWRP PostProcess
```

当前 Tonemapping 只是 `PostProcessPass` 内部的最后一步。后续扩展建议保持：

```text
Transparent
 -> NWRP PostProcess
    -> Bloom Prefilter / Downsample / Upsample
    -> Color Adjustments / LUT
    -> Sharpen
    -> Tonemapping Final
 -> DebugOverlay / FinalBlit skip-or-present
```

对外仍只有一个 post-process stage；内部根据 active effects 决定需要的临时 RT、执行顺序和最后 present 点。

### Scene View 需要能预览后处理，但不能破坏运行时零成本规则

Game Camera 可以通过 `NWRPCameraData` 控制后处理；但 Scene View Camera 是 Unity Editor 临时相机，不能稳定依赖自动添加组件。因此最初版本中 Game View 可以看到 Tonemapping，Scene View 看不到后处理效果。

本阶段修正为：

- 运行时 / 普通 Game Camera：仍然遵守“缺少 `NWRPCameraData` 就不执行后处理”。
- Editor Scene View：在 `UNITY_EDITOR` 下允许 fallback，使用 Scene View camera transform 和 cullingMask 采样 Volume。

这样不会扩大运行时默认成本，又能让编辑器 Scene View 预览 NWRP Volume 后处理。

## 关键实现

### ConfigureCameraData

`NWRPRenderer` 在配置 frame targets 之前先解析 Camera 后处理状态：

- 清空上一帧的 `cameraData / volumeStack / postProcessingEnabled / tonemappingActive / tonemapping`。
- 检查 `asset.SupportsPostProcessing`。
- 优先读取 Camera 上的 `NWRPCameraData`。
- 通过 `VolumeManager.instance.Update(trigger, layerMask)` 更新 Core Volume stack。
- 从 stack 中读取 `NWRPTonemapping` 并缓存到 `NWRPFrameData`。

这样 target requirements 和 pass enqueue 都使用同一份 per-frame post-process 状态，不需要各 Feature/Pass 重复查 Volume。

### NWRPFrameData / NWRPFrameTargets

`NWRPFrameData` 新增：

- `cameraData`
- `volumeStack`
- `postProcessingEnabled`
- `tonemappingActive`
- `tonemapping`

`NWRPFrameTargets` 新增：

- `cameraColorPresented`

`cameraColorPresented` 是 FinalBlit 跳过的关键标记。PostProcess final 或普通 FinalBlit 只要已经把 camera color 写回 final target，就应该设置它，避免重复 present。

### PostProcessFeature

`PostProcessFeature` 只负责对外接入 NWRP feature/pass 系统：

- `TryGetFrameTargetRequirements`：当 `HasAnyActivePostProcess` 为 true 时请求 `requiresIntermediateColor`。
- `AddPasses`：只 enqueue 一个 `PostProcessPass`。
- `HasAnyActivePostProcess`：后续扩展 Bloom/LUT/Sharpen 时统一在这里 OR active 状态。

这保证外部调度始终以一个 `NWRP PostProcess` pass 为入口。

### PostProcessPass

`PostProcessPass` 当前只执行 final Tonemapping：

- 创建并缓存 `Hidden/NWRP/PostProcess/Tonemapping` material。
- 根据 `NWRPTonemappingMode` 选择 shader pass index，而不是 shader keyword。
- 设置 `_NWRPTonemapParams`。
- 使用与 FinalBlit 一致的 viewport / load action / Y flip scaleBias。
- 直接写入 `frameData.targets.backBufferColor`。
- 写完后设置 `cameraColorPresented = true`。

因为没有使用 tonemap keyword，所以不会增加业务材质的 variant 组合，也不会引入 post-process operator 的全局 keyword 交叉。

### NWRP_Tonemapping.shader

新增 shader：

```text
Hidden/NWRP/PostProcess/Tonemapping
```

实现方式：

- include `NWRPBlitCoreCompat.hlsl` 和 Unity Core `Blit.hlsl`。
- 不 include URP shader library。
- 不使用 shader keyword 切换 tonemap operator。
- 使用独立 shader pass index：
  - pass 0：Linear
  - pass 1：ACES
  - pass 2：ACES Fitted
  - pass 3：AGX

`ACESFitted` / `AGX` 参考 Beautify 的实现，并保留 MIT license 注释。Shader 侧对 HDR 输入做 `maxInputBrightness` clamp，对输出做 saturate，并保留输入 alpha。

### Editor auto-add

新增 `NWRPCameraDataAutoAdd`：

- 仅 Editor 生效。
- 仅当前 Render Pipeline 为 `NewWorldRenderPipelineAsset` 时生效。
- 只处理 Scene 中已加载的 Game Camera。
- 不处理 Project asset / prefab 持久对象。
- 使用 `Undo.AddComponent<NWRPCameraData>`，并标记场景 dirty。

这让测试场景更接近 URP 的使用体验，但运行时仍然不会因为缺少组件而隐式启用后处理。

## 性能与移动端策略

- 默认没有 active post-process 时，不请求 intermediate color，也不 enqueue runtime post-process feature。
- Camera 缺少 `NWRPCameraData` 时，运行时不执行后处理。
- Tonemapping 直接输出到 final target，避免 Tonemap 后再 FinalBlit 多一次 fullscreen write。
- HDR 默认优先 32-bit `B10G11R11_UFloatPack32`，更适合移动端带宽。
- 只有选择 64-bit 或需要 alpha 时才倾向 `R16G16B16A16_SFloat`。
- Tonemapping operator 使用 shader pass index，不使用 keyword，避免 variant 膨胀。
- Scene View fallback 仅 Editor 编译，不影响 Player runtime 路径。

## Shader Variant 风险

本阶段没有给业务 shader 新增 keyword。

Tonemapping shader 是独立 hidden utility shader，通过 pass index 选择 operator：

- Linear
- ACES
- ACES Fitted
- AGX

因此不会叠加到 Lit / Environment / Water / Vegetation 等业务 shader variant 矩阵中。

## 验证记录

已在本地覆盖补丁后进行基础验证：

- Game View 中启用 Tonemapping 后可以看到后处理效果。
- Frame Debugger 中可以看到 post-process 阶段。
- 初版暴露为 `NWRP Tonemapping`，后续已按扩展性反馈改为统一 `NWRP PostProcess`。
- 初版 Scene View 看不到后处理效果，后续已按编辑器 fallback 路径修正。

仍需在 Unity Editor 中复验：

- Console 无编译错误。
- Scene View / Game View 中 Tonemapping 方向一致，无 Y flip 问题。
- Camera 关闭 `renderPostProcessing` 时走原 FinalBlit。
- Camera 开启且 Volume Mode 为 `Linear / ACES / ACESFitted / AGX` 时走 `NWRP PostProcess`，并跳过 FinalBlit 的实际 blit。
- HDR 高亮能够通过 Tonemapping 压回 LDR。
- 非默认 viewport / targetTexture 路径没有被 `cameraColorPresented` 错误跳过。

## 当前需要复核的点

- `Assets/NWRP/Runtime/PostProcessing/Passes/TonemappingPass.cs` 文件名和类名职责已经不完全一致，建议后续重命名为 `PostProcessPass.cs`，并同步 `.meta`，避免长期混淆。
- 当前 v1 只有 Tonemapping，没有 Bloom/LUT/DOF。后续新增效果时，需要在 `PostProcessPass` 内部设计 ping-pong RT / temporary RT 生命周期，而不是继续新增外部顶层 pass。
- HDR intermediate color 现在在 `camera.allowHDR && asset.SupportsHDR && targetTexture == null` 时请求；如果后续 targetTexture 也需要 HDR resolve，需要单独复核 targetTexture descriptor 和最终输出路径。
- `supportsPostProcessing` 是 pipeline capability / kill switch，不应作为项目日常开关。日常开关应继续放在 Camera 的 `NWRPCameraData.renderPostProcessing` 和 Volume override 上。
- Scene View fallback 使用 Scene View camera 的 `cullingMask` 作为 Volume layer mask，适合编辑器预览；如果后续需要更精细的 Scene View 后处理控制，可以考虑 Editor-only preference 或 SceneView overlay，而不是把该逻辑带入 runtime。

## 后续方向

- 将 `TonemappingPass.cs` 正式重命名为 `PostProcessPass.cs`。
- 抽象内部 post-process chain，例如：
  - active effect 收集。
  - 临时 RT 分配策略。
  - ping-pong color buffer。
  - final operator 直接写回 backbuffer。
- 后续 Bloom 建议优先实现低成本移动版：prefilter + 固定层级 downsample / upsample，避免 URP Bloom 全量复杂度。
- 如果加入 Color Adjustments / LUT，建议继续使用 NWRP-owned Volume override，不复用 URP override 类型。
- 后处理 ShaderLibrary 可以保留少量 `PostProcess` 专用 include，但不要把 Beautify/URP 的整套后处理库直接搬入 NWRP。
- 继续保持 `Assets/NWRP` 无 `UnityEngine.Rendering.Universal` 和无 URP shader include 的边界。
