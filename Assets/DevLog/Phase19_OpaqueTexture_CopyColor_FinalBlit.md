# Phase19 Opaque Texture / CopyColor / Final Blit

日期：`2026-04-30`

## 概要

本阶段为 NWRP 增加 URP-style opaque texture 支持。开启 `Enable Opaque Texture` 后，管线在不透明物体和 Skybox 渲染完成后、透明物体渲染前执行 `CopyColor`，将当前 camera color 复制到 `_CameraOpaqueTexture`，供透明材质、折射、热扭曲或调试 shader 采样。

实现过程中同步修复了启用 intermediate color 后 Game View 上下倒置的问题，并将最终输出 pass 命名和结构调整为 `FinalBlitPass`，对齐 URP 的 `Packages/com.unity.render-pipelines.universal/Runtime/Passes/FinalBlitPass.cs` 语义。Frame Debugger 也清理为更接近 URP 的实际 pass 展示，避免 `Main Rendering Opaque / Transparent / CopyColor / Final Output` 这类外层 scope 造成误读。

## 修改文件

- `Assets/NWRP/Runtime/OpaqueTextureFeature.cs`
- `Assets/NWRP/Runtime/Passes/CopyColorPass.cs`
- `Assets/NWRP/Runtime/Passes/FinalBlitPass.cs`
- `Assets/NWRP/Runtime/NWRPBlitterResources.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPProfiling.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/ShaderLibrary/DeclareOpaqueTexture.hlsl`
- `Assets/NWRP/Shaders/Utils/CoreBlit.shader`
- `Assets/NWRP/Shaders/Utils/CoreBlitColorAndDepth.shader`
- `Assets/NWRP/Shaders/Debug/NewWorld_Debug_OpaqueTexturePreview.shader`
- `Assets/NWRP/Tests/Materials/M_OpaqueTexturePreview.mat`
- `Assets/NWRP/Tests/Scenes/LakeShaderTestScene.unity`
- `Assets/Settings/NewWorldRP.asset`

## 关键实现

### Opaque Texture Feature

- 新增 `OpaqueTextureFeature`，通过 `NewWorldRenderPipelineAsset.FeatureSettings.opaqueTexture.enableOpaqueTexture` 控制，默认关闭。
- `CopyColorPass` 使用 `NWRPPassEvent.BeforeTransparent`，也就是 Opaque + Skybox 之后、Transparent 之前。
- `CopyColorPass` 使用 RTHandle source / destination 调用 `Blitter.BlitCameraTexture`，source 为当前 camera color，destination 为 `_CameraOpaqueTexture`。
- copy 完成后显式恢复 camera color/depth render target，保证后续透明渲染继续写回正常 camera target。

### Frame Targets

- `NWRPFrameTargets` 增加 camera color/depth RTHandle、opaque texture RTHandle 和相关状态字段。
- opaque texture 开启时强制 intermediate color/depth，并分配 `_CameraOpaqueTexture`。
- opaque texture 关闭时释放对应 RTHandle，并将 `_CameraOpaqueTexture` 绑定到黑纹理，避免 shader 采样到旧帧内容。
- intermediate color 开启后，最终输出由 `FinalBlitPass` 负责 present 到 backbuffer。

### CoreBlit 与 Final Blit

- `CoreBlit.shader` 和 `CoreBlitColorAndDepth.shader` 从 URP 14.0.12 对应 utility shader 迁入 NWRP 的 `Assets/NWRP/Shaders/Utils`，shader name 改为 `Hidden/NWRP/CoreBlit` 和 `Hidden/NWRP/CoreBlitColorAndDepth`。
- 新增 `NWRPBlitterResources` 负责初始化 SRP Core `Blitter`，避免 copy path 手写 `_BlitTexture`。
- 原 `SubmitPass` 改名为 `FinalBlitPass`，文件名、class 名和 renderer 字段都与 URP `FinalBlitPass` 语义对齐。
- `FinalBlitPass` 不创建自己的 pass profiling scope，实际 Frame Debugger 节点来自内部 `Final Blit` scope。
- final blit 使用 URP `RenderingUtils.FinalBlit` 同款 Y-flip scale/bias，修复 RT 到 backbuffer 时 Game View 倒置的问题。

### Shader Sampling

- 新增 `DeclareOpaqueTexture.hlsl`，提供：
  - `SampleSceneColor(uv)`
  - `SampleSceneColorAlpha(uv)`
  - `LoadSceneColor(pixelCoord)`
- 新增 `NewWorld/Debug/OpaqueTexturePreview` 透明队列调试 shader，用 `_CameraOpaqueTexture` 显示 copy 后的 opaque scene color。
- 测试场景中新增 `NWRP_OpaqueTexture_PreviewQuad`，用于验证透明阶段采样的是 copy 后的 opaque texture，而不是当前透明物体自身。

### Frame Debugger Scope Cleanup

- 普通 render stage 不再创建外层 `ProfilingScope`。
- Frame Debugger 中实际渲染节点直接显示为 pass scope：
  - `Camera Properties`
  - `Draw Opaque Objects`
  - `Draw Skybox`
  - `CopyColor`
  - `Draw Transparent Objects`
  - `Final Blit`
- 主光和附加光 shadow 分组保留，因为它们内部有 atlas、cache、dynamic overlay 等更细阶段，仍需要分组诊断。

## 性能与移动端策略

- Opaque texture 默认关闭，移动端 baseline 不增加 RT、不增加 full-screen copy。
- 开启 opaque texture 后固定新增：
  - 1 张 full-resolution `_CameraOpaqueTexture`
  - 1 次 full-screen CopyColor
  - camera color/depth intermediate RT 路径
  - 1 次 final blit present
- 当前 v1 不做 downsampling，优先保证行为与 URP `Downsampling.None` 对齐。
- CopyColor 不新增 runtime feature keyword。
- preview shader 只使用 `multi_compile_instancing`，没有 opaque texture keyword。
- CoreBlit utility shader 保持 URP 工具 shader 结构，variant 成本不叠加到业务材质。

## Review 与优化建议

本阶段复查后没有发现必须立即修复的渲染正确性问题。当前实现满足：

- CopyColor 位于 Opaque + Skybox 后、Transparent 前。
- `_BlitTexture` 不再黑，`_CameraOpaqueTexture` 能采样到 opaque scene color。
- Game View final present 方向正常。
- Frame Debugger scope 不再出现 `CopyColor/CopyColor` 或 `Final Output/Final Blit` 这种重复层级。

后续建议：

- `ConfigureFrameTargets` 当前每帧创建并释放 backbuffer RTHandle wrapper，功能正确，但后续可改为缓存 alias 或仅在需要时创建，以减少 GC 压力。
- 后续如果水面、热扭曲等大量使用 scene color，可增加 opaque texture downsampling 选项。
- 未来 PostProcessFeature 也会请求 intermediate color，应统一 post-process present 与 `FinalBlitPass`，避免多次 RT 到 backbuffer 的 full-screen blit。

## 验证记录

- Unity `AssetDatabase.Refresh()` 后编译通过。
- Console 最终检查：0 error / 0 warning。
- Game View 截图验证：
  - 画面方向正常。
  - `NWRP_OpaqueTexture_PreviewQuad` 能采样 `_CameraOpaqueTexture`。
  - preview quad 不包含自身，说明 copy 发生在透明渲染之前。
- Frame Debugger MCP 在部分 Editor 状态下会返回 0 events；最终结构以手动 Frame Debugger 和代码 scope 为准。
- 测试场景中的 Lake shader 已删除，湖面粉色属于已知 out-of-scope 状态，不作为本阶段问题。

## 当前限制与后续方向

- v1 不支持 opaque texture downsampling。
- v1 不单独处理 camera stacking / XR。
- v1 不实现完整后处理链，只提供 CoreBlit utility 和 `FinalBlitPass` present。
- Frame Debugger cleanup 只处理普通渲染阶段，不改 shadow 分组。
