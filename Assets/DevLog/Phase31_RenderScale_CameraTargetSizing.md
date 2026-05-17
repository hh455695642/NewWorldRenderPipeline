# Phase31 Render Scale / Camera Target Sizing

日期：`2026-05-17`

## 概要

本阶段为 NWRP 增加 asset 级 Render Scale v1。目标是降低主 3D 相机的 color / depth / opaque texture / depth texture 带宽，同时保持现有 UICamera + Screen Space Camera + UI layer filtering 的输出结果不变。

实现策略是：符合条件的 Game Camera 在 `renderScale < 1` 时先渲染到缩放后的中间 color/depth RT，再通过现有 `FinalBlitPass` 放大回 backbuffer；UICamera 通过 `NWRPCameraData` 显式标记为 `ForceNative`，继续按原生分辨率叠加 UI，不把文字、图标、mask、sorting 结果画进低分辨率 RT。

本阶段不引入 FSR、动态分辨率、XR render scale，也不引入 URP `ScriptableRendererFeature`。这是 NWRP 自定义 SRP 内部的 camera target sizing 能力。

## 本阶段目标

- 在 `NewWorldRenderPipelineAsset` 的 General 区增加可控的 Render Scale 开关。
- 为每个 Camera 增加可显式覆盖的 render scale 模式，避免按名字或 layer 自动猜 UI camera。
- 让 frame data 统一记录 resolved render scale 与 scaled target size。
- 让 camera color/depth、`_CameraOpaqueTexture`、`_CameraDepthTexture` 的 descriptor 跟随 scaled size。
- 保持 `_ScreenParams` 表示输出相机尺寸，`_ScaledScreenParams` 表示内部渲染尺寸。
- 复用现有 `FinalBlitPass` 完成 upscale，不额外增加新的全屏 pass。
- 保持 SceneView、Preview、Reflection camera 与 `camera.targetTexture` 的 native 行为。

## URP 行为参考

本阶段参考 URP 14 的 render scale 基本语义，但只取 NWRP v1 需要的部分：

- URP asset 级 `renderScale` 默认 `1.0`，范围支持 `0.1 - 2.0`。
- URP 在相机初始化时会把接近 `1.0` 的 scale 折回 `1.0`，避免无意义的中间 RT。
- SceneView、Preview、Reflection camera 不应用 render scale。
- URP 使用 `camera.pixelWidth/Height * renderScale` 作为内部 `cameraTargetDescriptor` 尺寸。
- `_ScreenParams` 仍代表输出尺寸，`_ScaledScreenParams` 代表内部渲染尺寸。
- 最终通过 final blit 根据 viewport scale/bias 与平台 Y flip 写回 backbuffer。

NWRP v1 没有接入 URP 的 upscaling filter 体系、FSR、XR render scale 或 dynamic resolution。移动端 baseline 先保持简单、可验证、可回退。

## Pipeline Asset 设置

`NewWorldRenderPipelineAsset` 新增 General 设置：

- `enableRenderScale`
- `renderScale`
- `renderScaleFilterMode`

当前范围约束为：

- `renderScale`: `0.5 - 1.0`
- 默认值：`1.0`
- 默认 upscale filter：`Bilinear`
- 可选 filter：`Point / Bilinear`

这里有意不支持 `> 1.0` 的 supersampling。移动端目标是降低带宽，而不是把 render scale 变成高画质抗锯齿入口。若未来需要高端档位 supersampling，应作为独立质量策略评估，并与 MSAA、TAA 或后处理锐化一起做成本建模。

`NewWorldRenderPipelineAssetEditor` 也同步增加了 `General / Render Scale` Inspector 区域。由于 NewWorldRP asset 使用自定义 Inspector，新增 serialized field 不会自动显示；本阶段已显式绑定：

- `Enable Render Scale`
- `Render Scale`
- `Upscale Filter`

`Render Scale` 与 `Upscale Filter` 在开关关闭时置灰，避免误以为设置已经生效。

## Camera 级策略

`NWRPCameraData` 新增 per-camera render scale 模式：

- `PipelineDefault`
- `ForceNative`
- `Override`

行为如下：

- `PipelineDefault`：使用 pipeline asset 的 `enableRenderScale / renderScale`。
- `ForceNative`：强制使用 camera native pixel size，适合 UICamera。
- `Override`：使用相机组件上的 `renderScaleOverride`，仍限制在 `0.5 - 1.0`。

本阶段明确不做自动 UI camera 判断。按相机名、layer、Canvas 类型或 clear flags 推断 UI camera 都容易形成隐藏规则，后续场景复杂后很难维护。因此 UICamera 需要显式挂载或保留 `NWRPCameraData`，并把模式设为 `ForceNative`。

以下相机始终不应用 render scale：

- `SceneView`
- `Preview`
- `Reflection`
- `camera.targetTexture != null`

`camera.targetTexture` 暂不缩放，是为了避免破坏外部 RT 语义。外部 RT 通常由调用方明确指定尺寸，NWRP 不应在 v1 中隐式改写。

## FrameData 与 Target Descriptor

`NWRPFrameData` 新增 resolved 字段：

- `resolvedRenderScale`
- `cameraTargetWidth`
- `cameraTargetHeight`
- `renderScaleFilterMode`
- `renderScaleActive`

这些字段是本阶段 target size 的单一数据来源。`NWRPRenderer` 在配置相机数据后解析 render scale：

1. 获取 camera native target size。
2. 判断 asset 开关与 camera eligibility。
3. 读取 camera mode 与 override。
4. 对接近 `1.0` 的 scale 使用阈值折回 native。
5. 写入 scaled width/height。

随后 `ConfigureFrameTargets` 使用 `cameraTargetWidth / cameraTargetHeight` 创建：

- camera color RT
- camera depth RT
- `_CameraDepthTexture`
- `_CameraOpaqueTexture`

如果 `renderScaleActive == true`，主相机会强制需要 intermediate color 与 depth，并在最终通过现有 final blit 输出到 backbuffer。UICamera 或 native camera 仍可以沿用原生尺寸和现有相机顺序。

## Screen Globals 与采样契约

本阶段保持 URP-style screen global 语义：

- `_ScreenParams`：camera 输出尺寸，即 native pixel width/height。
- `_ScaledScreenParams`：内部渲染尺寸，即 scaled RT width/height。

这对 Lake shader、depth sampling、opaque texture sampling 和后处理都很关键。业务 shader 如果使用 normalized screen UV，应继续基于 `_ScaledScreenParams` 或 NWRP shader library helper 获取内部 RT 坐标，不应自己用 `camera.pixelWidth` 推导采样比例。

`_CameraDepthTexture` 与 `_CameraOpaqueTexture` 的尺寸跟随 scaled descriptor，因此主 3D 相机在 `0.75` scale 下会得到缩小后的 depth/opaque texture。UICamera `ForceNative` 时这些资源仍保持 native 尺寸。

## Pass 与 Viewport 修正

为了避免 scaled target 写入后 viewport 没有恢复，本阶段增加了两个明确的 renderer helper：

- `GetCameraRenderViewport`
- `GetCameraTargetViewport`

用途区分：

- render viewport：当前实际渲染 RT 的 viewport。scaled intermediate 使用 scaled size，native/backbuffer 使用 camera pixel rect。
- target viewport：最终输出 camera viewport。final blit 写回 backbuffer 时使用 native camera viewport。

同步修正的 pass：

- `CopyColorPass`
- `CopyDepthPass`
- `DepthPrepass`

这些 pass 在执行 fullscreen copy 或 depth prepass 后会恢复正确 viewport，避免后续 opaque/transparent/postprocess 出现缩放错位、采样偏移或只画局部区域。

## Final Blit 策略

本阶段不新增新的全屏 pass。Render scale 激活时，仍由现有 `FinalBlitPass` 负责把 camera color RT 写回 backbuffer。

移动端取舍：

- 额外成本：一次 final blit，以及 scaled intermediate color/depth RT。
- 节省成本：主 3D 场景的 opaque、transparent 前的 depth/opaque texture、post-process 输入以及 depth 带宽下降。
- UI 清晰度：UICamera `ForceNative` 后仍按 native 分辨率渲染，不吃主相机 render scale 的模糊。

因此 v1 适合主 3D 场景像素成本高、UI 清晰度优先的移动端项目。若 UI 本身也成为瓶颈，应作为单独 UI 分辨率策略处理，不应和主 3D render scale 混在同一规则中。

## Variant 与 Shader 风险

本阶段没有新增 shader keyword。

Render scale 完全由 C# frame data、RT descriptor、viewport 与 shader global 控制：

- 不新增 `multi_compile`
- 不新增 `shader_feature`
- 不改变业务 shader pass tag
- 不引入 URP shader include

Variant 风险为 `0`。需要关注的是运行时采样契约：如果某些旧 shader 手写 `_ScreenParams` 推导内部 RT UV，在 render scale 下可能出现偏移，应迁移到 NWRP ShaderLibrary 的 screen/depth helper。

## 性能与移动端策略

CPU vs GPU 取舍：

- CPU 侧只在每个 camera 解析一次 scale 与 descriptor 尺寸，成本可以忽略。
- GPU 侧通过降低主 3D color/depth 像素数减少带宽与 fragment workload。
- render scale 不改变 draw call、SetPass 或 culling 数量。
- render scale 激活后会强制中间 color/depth，因此 camera 数量过多时需要谨慎使用。

移动端建议：

- 主 3D camera 可从 `0.75` 开始验证。
- UICamera 使用 `ForceNative`，保持 Screen Space Camera UI 清晰。
- 若开启 HDR、Opaque Texture、Depth Texture 或 PostProcess，优先检查这些 RT 是否都降到 scaled size。
- Tile-Based GPU 上重点看带宽、tile store/load 和 final blit 成本，不只看 ALU。

## 验证

本阶段已执行：

- `dotnet build NWRP.Runtime.csproj --no-restore`：通过，`0` error。
- `dotnet build NWRP.Editor.csproj --no-restore`：通过，`0` error；仍有项目已有 NuGet / Unity 引用版本 warning。
- `AssetDatabase.Refresh(ForceUpdate)`：通过。
- Unity Console Error：为空。
- `git diff --check`：通过。
- Unity EditMode tests：已尝试运行，但当前项目返回 `No tests found`。

静态检查结果：

- `Assets/Settings/NewWorldRP.asset` 当前没有场景内 UICamera 可直接配置。
- 已搜索 `UICamera` / `UI Camera`，当前打开场景和资产文本中没有匹配对象。
- 因此本阶段只实现 `ForceNative` 能力，没有修改具体 UI scene asset。

建议手动验证：

- `NewWorldRP` 开启 `Enable Render Scale`，设置 `Render Scale = 0.75`。
- Frame Debugger 检查主相机 camera color/depth RT 尺寸下降。
- 检查 `_CameraDepthTexture` 与 `_CameraOpaqueTexture` 尺寸跟随 scaled descriptor。
- 检查最终 backbuffer 铺满原 viewport。
- 给 UICamera 的 `NWRPCameraData` 设置 `ForceNative`，确认 UI 文字、图标、mask、Canvas sorting 与 render scale 关闭时一致。
- 在 Android Vulkan / GLES fallback 与 iOS Metal 真机上验证 Y flip、final blit、depth sampling 和 opaque sampling。

## 已知边界与后续建议

- v1 不支持 FSR、TAA upsample、CAS sharpen 或动态分辨率。
- v1 不支持 XR render scale。
- v1 不对 `camera.targetTexture` 做隐式缩放。
- SceneView / Preview / Reflection camera 保持 native，避免编辑器与反射路径出现不可预期变化。
- 如果后续需要 UI 也降分辨率，应增加独立 UI scale 策略，而不是复用主 3D render scale。
- 如果后续要支持 `renderScale > 1.0`，应与移动端 quality tier 明确绑定，默认仍不应进入 baseline。
- 旧 shader 若直接使用 `_ScreenParams` 构造内部 RT UV，应迁移到 NWRP screen/depth helper，避免 render scale 下采样偏移。
