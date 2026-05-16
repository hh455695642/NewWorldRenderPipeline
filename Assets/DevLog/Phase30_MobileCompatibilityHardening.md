# Phase30 移动端兼容性加固

日期：`2026-05-16`

## 概要

本阶段围绕 NWRP 当前移动端兼容性风险做一次收口。重点不是扩展新渲染效果，而是保证 Android Vulkan / GLES fallback、iOS Metal、不同移动 GPU 能在能力不足时走可控退路，避免 GPU-driven 植被、后处理、阴影默认预算在低端设备上引入黑屏、植被消失或明显带宽压力。

本阶段保持自定义 SRP 边界：不引入 URP Runtime 依赖，不把新逻辑塞回主渲染流程，不新增 monolithic feature。GPU-driven 植被仍保留高性能主路径；移动端低能力设备只在运行时能力不满足时恢复原 MeshRenderer 路径。

## 本阶段目标

- 为 vegetation indirect path 增加运行时能力门控。
- 为 GLES / 无 compute / 无 indirect arguments buffer 设备提供安全 fallback。
- 收敛默认 pipeline asset 到移动端 baseline，避免默认开启高带宽功能。
- 修正样例场景中树木阴影距离与 vegetation culling 距离不一致的问题。
- 降低已知 shader keyword 污染点。
- 保持 NWRP 自有 runtime / shader 不依赖 URP package source。

## 植被 GPU Driven 兼容性

`VegetationIndirectRenderer` 原路径默认假设 compute shader、append buffer、indirect arguments buffer 与 `Graphics.RenderMeshIndirect` 可用。Android 项目配置中仍保留 GLES3 fallback，而当前环境植被 shader 使用 SM 4.5 + procedural instancing + StructuredBuffer；如果低端设备或 GLES 路径继续尝试 indirect path，最危险的结果是原 MeshRenderer 已经被关闭，但 GPU 路径无法提交，导致植被直接消失。

本阶段新增运行时能力门控：

- `SystemInfo.graphicsDeviceType` 为 `OpenGLES2 / OpenGLES3` 时不进入 indirect path。
- `SystemInfo.supportsComputeShaders == false` 时不进入 indirect path。
- `SystemInfo.supportsInstancing == false` 时不进入 indirect path。
- `SystemInfo.supportsIndirectArgumentsBuffer == false` 时不进入 indirect path。
- `CullingComputeShader` 缺失或 `CSVegetationCulling` kernel 不存在时不进入 indirect path。
- indirect buffer 分配失败时释放已分配资源并退回 MeshRenderer。

fallback 行为：

- 恢复原始 MeshRenderer 的 enabled、shadowCastingMode、receiveShadows、layer。
- 不再执行 shadow-only 源 renderer 切换，避免 fallback 路径被每帧覆盖成 ShadowsOnly。
- 只打印一次 fallback warning，便于真机日志定位能力缺口。
- 保留 `debugUseOriginalRenderer` 作为人工调试开关。

## 移动端 fallback shader

本阶段新增 `Assets/NWRP/Shaders/Environment/MobileFallback`，为当前 vegetation shader 提供低能力设备可用的 target 3.0 版本：

- `NewWorld/Env/MobileFallback/WorldGrass`
- `NewWorld/Env/MobileFallback/Tree`
- `NewWorld/Env/MobileFallback/TreeLeaf`
- `NewWorld/Env/MobileFallback/Shrub`

这些 shader 的策略：

- `#pragma target 3.0`
- 保留 NWRP pass tag：`NewWorldForward`、必要时 `ShadowCaster`、`DepthOnly`
- 保留 `#pragma multi_compile_instancing`
- 不使用 procedural instancing
- 不使用 StructuredBuffer
- 不使用 compute/indirect 数据
- 不新增 fog keyword
- 不新增材质功能 keyword

`VegetationIndirectRenderer` 在 fallback 时会根据源 shader 名称创建运行时材质副本，并切换到对应 MobileFallback shader。运行时副本使用 `HideFlags.DontSave`，释放组件时销毁，避免污染资源文件。

## Pipeline Asset 移动端 baseline

`Assets/Settings/NewWorldRP.asset` 从高保真样例配置收敛为移动端默认基线：

- `supportsHDR = false`
- `supportsPostProcessing = false`
- `Enable Opaque Texture = false`
- `Enable Depth Texture = false`
- 主光阴影距离从 `300` 收敛到 `80`
- 主光阴影分辨率从 `2048` 收敛到 `1024`
- 主光阴影过滤从 `MediumPCF` 收敛到 `Hard`
- cached main light shadow 默认关闭
- dynamic shadow overlay 默认关闭
- additional punctual light shadows 默认关闭
- additional shadow atlas max size 从 `2048` 收敛到 `1024`
- additional shadowed light budget 收敛到 `1`

同步调整 `NewWorldRenderPipelineAsset` 中新建 asset 的默认值，避免后续新建 NWRP asset 时重新回到高带宽默认配置。

## 样例场景修正

`MaterialSampleScene` 中已经拆成两组 vegetation renderer：

- 树木组：`castShadows = true`
- 草/灌木组：`castShadows = false`

本阶段只调整树木组：

- `cullDistance: 50 -> 100`

原因是主光阴影 baseline 为 80m，树木组需要覆盖阴影距离外加安全余量，避免树木主体或 shadow-only 源 renderer 过早被 vegetation culling 排除。草组保持 50m，不扩大草的提交范围。

## Android / iOS 平台配置

Android 仍保留 Vulkan + GLES fallback 策略。为了避免现代 Android 设备只产出 32-bit ARMv7 包，本阶段将：

- `AndroidTargetArchitectures: 1 -> 3`

即同时启用 ARMv7 与 ARM64。iOS 仍以 Metal 为主路径。

## Variant 与移动端成本

本阶段新增的 MobileFallback shader 不使用新的 `shader_feature`，只保留 instancing multi_compile。

本阶段同时将 NPR Outline 中的：

```hlsl
#pragma shader_feature __ _PIXELWIDTH_ON
```

收敛为：

```hlsl
#pragma shader_feature_local _PIXELWIDTH_ON
```

这样 `_PIXELWIDTH_ON` 不再作为全局 keyword 污染其它 shader variant 空间。

仍需继续关注的既有 variant 成本：

- 原 vegetation indirect shader 仍有 `#pragma multi_compile_fog`。
- 原 vegetation indirect shader 仍是 SM 4.5 高能力路径。
- Core blit / CopyDepth 仍有平台兼容 multi_compile，属于已有基础设施成本。

## 性能与移动端策略

- 高能力设备：继续使用 compute culling + indirect draw，保持大规模植被 GPU-driven 主路径。
- GLES / 低能力设备：退回普通 MeshRenderer + target 3.0 fallback shader，优先保证可见性和稳定性。
- 默认 pipeline asset 不再自动请求 HDR color、post-process intermediate、opaque texture、depth texture。
- 主光阴影默认使用 Hard，避免 3x3 PCF 在移动端成为隐性 baseline 成本。
- additional punctual shadow 继续作为显式高成本功能，不默认进入移动端路径。

CPU vs GPU 取舍：

- GPU 主路径适合大规模实例，CPU 只做 chunk / group 粗粒度管理。
- fallback 路径会回到 Unity MeshRenderer culling / batching 体系，CPU 与 draw call 成本更高，但只用于低能力设备或错误兜底，不作为性能主路径。

## 验证

本阶段已执行：

- `AssetDatabase.Refresh(ForceUpdate)`：工具调用等待超时，但 `Editor.log` 显示刷新实际完成。
- 新增 fallback shader 与 `.meta` 已导入。
- 脚本编译完成，未在本轮刷新日志中发现新的 `error CS`。
- 未在本轮刷新日志中发现新的 `Shader error`。
- `git diff --check` 通过。
- 静态确认 fallback shader 均为 `#pragma target 3.0`。
- 静态确认 NPR Outline 使用 `shader_feature_local`。

未完成项：

- EditMode tests 未运行成功，因为当前打开的 `MaterialSampleScene` 处于 dirty 状态，Unity Test Runner 要求所有打开场景先保存。
- 尚未在 Android Vulkan / GLES3 / iOS Metal 真机上做 RenderDoc 或 GPU profiler 验证。

## 已知边界与后续建议

- MobileFallback shader 是低能力兜底，不追求完全复刻 indirect shader 的风动画、噪声和所有视觉细节。
- 如果后续需要在 GLES3 上保持大规模植被性能，应单独设计不依赖 compute/indirect 的分层 LOD 或烘焙合批方案，而不是把当前 GPU-driven path 硬降级。
- 原 vegetation shader 的 `multi_compile_fog` 可以作为后续 variant cleanup 阶段处理。
- 树木阴影长期仍建议迁入独立 NWRP vegetation shadow pass，减少对源 MeshRenderer ShadowsOnly fallback 的依赖。
- 移动端默认配置已经收敛；高画质配置应作为单独 asset 或 quality override 管理，避免重新污染 baseline。
