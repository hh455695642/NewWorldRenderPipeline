# Phase52 Scene/Game Additional Shadow 稳定性与 Unity 6.3 API 收敛

日期：`2026-06-18`

## 概要

本阶段整理并修复 Unity 6.3 分支中 NWRP 阴影稳定性与部分渲染 API 迁移问题。

用户侧现象主要集中在 SceneView / GameView 同场景预览时：

- SceneView 拉远、拉近、旋转后，main light shadow 有时消失。
- `Point Light` 开启后有阴影，但再开启 `Point Light (1)` 时，第一盏 point light 的阴影会消失。
- `Point Light`、`Point Light (1)`、`Spot Light` 同时开启时，GameView / SceneView 只能稳定看到 1 个 additional light shadow。
- GameView 相对 SceneView 更稳定，但 additional shadow 数量仍与预期不一致。

本阶段按 Unity 6.3 / URP 当前 shadow renderer-list 思路重新梳理 NWRP 的 realtime shadow caster culling 生命周期。核心问题不是单盏灯的 bias 或 shader receiver，而是 shadow caster culling 的 frame-level 数据被 main light 与 additional light 分散、重复提交，导致后一次 shadow culling 状态可能覆盖前一次状态。

修复后，NWRP 在每个 camera frame 中先统一准备 main directional cascades 与 selected additional punctual light slices，再由 main shadow pass 和 additional shadow pass 共同消费这份 per-camera shadow culling context。SceneView 的 additional shadow 候选也不再直接以 SceneView 相机距离作为唯一筛选依据，避免编辑器镜头移动导致候选灯和 atlas slice 随机跳变。

同时，本阶段继续收敛 Unity 6.3 迁移中的旧 API 和临时 RT 使用：

- 普通 renderer 绘制路径统一走 renderer list helper。
- Skybox 改为 `CreateSkyboxRendererList(...)`。
- Shadow 绘制保持 `CreateShadowRendererList(...)`，不回退旧 `DrawShadows(...)`。
- fullscreen 临时 RT 改为 feature / pass 持有的 RTHandle 复用路径。
- `FormatUsage` 改为 `GraphicsFormatUsage`。
- runtime 内不再保留 `GetTemporaryRT/ReleaseTemporaryRT`。

本阶段没有新增 Editor Window、菜单项或用户可见诊断 UI。

## 修改文件

### Shadow / Lighting

- `Assets/NWRP/Runtime/Lighting/NWRPShadowCullingContext.cs`
- `Assets/NWRP/Runtime/Lighting/NWRPShadowCullingContext.cs.meta`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterDebugOverlayPass.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/Passes/AdditionalLightShadowCasterPass.cs`

### Unity 6.3 API / RTHandle 收敛

- `Assets/NWRP/Runtime/NWRPTransientRTHandles.cs`
- `Assets/NWRP/Runtime/NWRPTransientRTHandles.cs.meta`
- `Assets/NWRP/Runtime/Passes/DepthPrepass.cs`
- `Assets/NWRP/Runtime/Outlines/Passes/DrawOutlinePass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Passes/ScreenBlurPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay/Passes/ValleyHeightFogOverlayPass.cs`

### Vegetation indirect shadow 相关同步

- `Assets/NWRP/Runtime/VegetationIndirectRendering/VegetationIndirectRenderer.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/VegetationIndirectShadowRegistry.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/Passes/VegetationIndirectShadowPass.cs`

### 测试

- `Assets/NWRP/Tests/EditMode/NWRPAdditionalShadowLayoutTests.cs`
- `Assets/NWRP/Tests/EditMode/NWRPAdditionalShadowLayoutTests.cs.meta`

## 解决的问题

### 1. Shadow caster culling 被逐灯重复提交

Unity 6 的 shadow renderer-list 路径需要先准备 `ShadowCastersCullingInfos`：

```text
CullingResults.visibleLights
    -> LightShadowCasterCullingInfo per light
    -> ShadowSplitData split buffer
    -> ScriptableRenderContext.CullShadowCasters(...)
    -> CreateShadowRendererList(...)
```

旧 NWRP 在 main light 和 additional light 内部分别调用 shadow caster culling。main cascade、spot light、point light face 都在各自 pass 里临时创建 culling buffer，并在本 pass 或本灯处理结束后释放。

在 Unity 6.3 的 renderer-list 模式下，这种“逐 pass / 逐灯重新 cull”的生命周期风险较高：

- main light culling 结果可能被 additional light 的 culling 结果覆盖。
- additional light 之间也可能互相覆盖 shadow caster culling 状态。
- SceneView / GameView 交替渲染时，上一段 shadow culling 和矩阵状态更容易泄漏到后续 forward pass。

本阶段新增内部 helper：

```csharp
internal sealed class NWRPShadowCullingContext : System.IDisposable
```

它挂在 `NWRPFrameData` 上，由 `NWRPRenderer` 每帧在 `SetupLights` 之后、shadow pass 执行前统一准备：

```csharp
ExecuteStage(ref frameData, IsSetupLightsPass);
PrepareShadowCullingContext(ref frameData);
ExecuteShadowStage(ref frameData, NWRPProfiling.MainLightShadow, IsMainLightShadowPass);
ExecuteShadowStage(ref frameData, NWRPProfiling.AdditionalLightShadow, IsAdditionalLightShadowPass);
RestoreCameraStateAfterShadowStages(ref frameData);
```

`NWRPShadowCullingContext` 内部持有：

```text
NativeArray<ShadowSplitData>
NativeArray<LightShadowCasterCullingInfo>
MainLightShadowCascadeData[]
NWRPAdditionalShadowLightEntry[]
NWRPAdditionalShadowSlice[]
additional world-to-shadow / params / atlas rect arrays
```

main light 与 additional light 的 split data 会被放进同一份 culling buffer，之后通过一次统一入口调用：

```csharp
frameData.context.CullShadowCasters(frameData.cullingResults, shadowCullingInfos);
```

这样 realtime main shadow 和 additional shadow 不再互相覆盖 culling 状态。buffer 生命周期覆盖 renderer list 创建与绘制，直到本 camera frame 结束后由 renderer 释放。

### 2. Main light cascade renderer list 改为消费统一 context

`MainLightShadowCasterPass` 不再为 realtime main light 自己创建一份临时 shadow caster culling buffer。它改为读取 frame data 中的：

```text
frameData.shadowCullingContext.MainCascadeData
frameData.shadowCullingContext.MainCascadeValid
```

当 regular caster 存在时，先调用：

```csharp
shadowCullingContext.Apply(ref frameData);
```

然后每个 cascade 创建 shadow renderer list：

```csharp
ShadowDrawingSettings shadowDrawingSettings =
    MainLightShadowPassUtils.CreateShadowDrawingSettings(
        frameData.cullingResults,
        mainLightIndex,
        useRenderingLayerMaskTest: true,
        splitIndex: cascadeIndex);
```

关键点是 `splitIndex` 显式等于 `cascadeIndex`，不依赖 Unity 默认 `-1` 的隐式递增行为。

已有的 vegetation indirect-only fallback 仍然保留。没有普通 caster 但存在 indirect caster 时，main light 仍可建立 atlas、cascade matrix 和 receiver globals，由 `VegetationIndirectShadowPass` 后续写入 shadow atlas。

### 3. Additional punctual shadow atlas 与 slice index 固定

旧 additional shadow pass 自己负责候选灯筛选、atlas layout、per-light culling 和绘制。point light 会消耗 6 个 cubemap face slice，spot light 消耗 1 个 slice；如果候选灯在绘制过程中被跳过，后续灯的 slice index 容易发生变化，表现为“开第二盏灯后第一盏灯阴影消失”。

本阶段把 additional light 的选择和 atlas 分配提前到 `NWRPShadowCullingContext.PrepareAdditionalLights(...)`：

```text
collect additional lights
    -> collect shadow candidates
        -> sort candidates
            -> compute atlas layout
                -> assign firstSliceIndex
                    -> prepare spot / point split data
```

point light 固定预留 6 个连续 slice：

```text
Point Light      firstSliceIndex = 0
Point Light (1)  firstSliceIndex = 6
Spot Light       firstSliceIndex = 12
```

如果某盏灯没有有效 caster、没有有效 slice、shadow strength 为 0 或超出 atlas 预算，它的 metadata 可以保持 disabled，但后续灯不会回退复用它的 slice index。这样 atlas 地址和 receiver 端 `_AdditionalLightShadowParams.w` 保持 deterministic。

新增 EditMode 测试覆盖该约束：

```text
NWRPAdditionalShadowLayoutTests.MixedPointAndSpotLightsKeepStableFirstSliceIndices
```

测试通过反射调用内部 test hook，验证 `Point + Point + Spot` 的 slice 起点固定为：

```text
0, 6, 12
```

### 4. SceneView additional shadow 候选不再随编辑器镜头跳变

SceneView 中拉远、拉近、旋转时，旧逻辑容易用 SceneView 相机位置直接参与 additional shadow distance gate 和 sort。这样编辑器预览镜头移动会改变：

- 哪几盏 additional light 被认为在 shadow distance 内。
- 哪几盏灯进入 `MaxShadowedAdditionalLights` 预算。
- point / spot light 的 atlas slice 分配顺序。

本阶段在 `NWRPShadowCullingContext.ResolveShadowCandidateReference(...)` 中收敛 SceneView 规则：

```text
SceneView:
    优先使用 active Game camera 作为 reference position
    没有 active Game camera 时关闭距离 gate / 距离排序
    仍保留 caster bounds、max light、atlas 预算限制

Game camera / Player:
    继续使用当前 camera position 做 distance gate / sort
```

该逻辑只在 `UNITY_EDITOR` 下改变 SceneView 预览稳定性，不改变移动端 Player 行为。

### 5. Shadow stage 后恢复 camera matrix / render target

Shadow pass 会设置 light view / projection matrix，并切换到 shadow atlas render target。如果 SceneView 和 GameView 都在刷新，后续 forward pass 可能继承错误的矩阵或目标状态。

本阶段在 main shadow stage 与 additional shadow stage 完成后统一恢复 camera state：

```csharp
frameData.context.SetupCameraProperties(frameData.camera);
SetCameraRenderTarget(ref frameData);
ExecuteBuffer(ref frameData);
```

这样 opaque / transparent / post-process 不会继续使用上一段 light-space matrix。additional shadow pass 自身也在绘制结束后重置：

```text
depth bias
shadow caster cull mode
shadow bias
shadow light direction
shadow light position
camera properties
```

### 6. Cached shadow 与统一 culling context 的边界

`MainLightShadowPassUtils.CullShadowCastersForCascades(...)` 仍保留，用于 cached static shadow 的隔离路径。它没有回到旧 `DrawShadows(...)`，只是为 cached shadow cache 维持自己的 culling scope。

为了避免 cached shadow 先执行后污染 realtime additional shadow，本阶段在 cached helper 调用 `CullShadowCasters(...)` 后标记：

```csharp
frameData.shadowCullingContext?.MarkDirty();
```

后续 realtime main / additional shadow 如果还要使用统一 context，会重新 apply 当前 camera frame 的 culling info。

## Unity 6.3 API 与 RT 生命周期收敛

### 1. RendererList helper

NWRP 新增统一 renderer list 绘制入口：

```csharp
internal static void DrawRendererList(
    ref NWRPFrameData frameData,
    ref DrawingSettings drawingSettings,
    ref FilteringSettings filteringSettings)
```

内部通过：

```csharp
RendererListParams rendererListParams =
    new RendererListParams(frameData.cullingResults, drawingSettings, filteringSettings);
RendererList rendererList = frameData.context.CreateRendererList(ref rendererListParams);
frameData.cmd.DrawRendererList(rendererList);
```

本阶段将以下路径收敛到 renderer list helper：

- Depth prepass
- Outline pass
- Valley height fog overlay
- Main light shadow debug overlay
- Opaque / transparent 主绘制路径

Skybox 使用 Unity 6 路径：

```csharp
RendererList skyboxRendererList =
    frameData.context.CreateSkyboxRendererList(frameData.camera);
```

Shadow 使用：

```csharp
frameData.context.CreateShadowRendererList(ref shadowDrawingSettings);
frameData.cmd.DrawRendererList(rendererList);
```

没有恢复 `context.DrawRenderers(...)`、`context.DrawSkybox(...)` 或 `context.DrawShadows(...)`。

### 2. RTHandle 复用替代高频临时 RT

新增内部 helper：

```csharp
internal static class NWRPTransientRTHandles
```

职责很窄，只做 pass / feature 内部 transient RTHandle 的 descriptor 兼容检查、重分配和释放：

```text
ReAllocateIfNeeded(...)
Release(...)
```

本阶段改造的 fullscreen / post path 包括：

- `PostProcessPass`
- `ScreenBlurPass`
- `CloudShadowProjectorPass`
- `ValleyHeightFogPass`

这些 pass 不再每帧用 `GetTemporaryRT/ReleaseTemporaryRT` 申请释放 RT，而是复用 feature-owned / pass-owned RTHandle。descriptor 变化时才重分配，pass dispose 时释放。

这对移动端 tile GPU 的收益主要是降低临时 RT 管理抖动和渲染目标切换风险，不增加 fullscreen copy 数量。

### 3. GraphicsFormatUsage

格式支持检查改为 Unity 6 当前 API：

```csharp
SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Linear | GraphicsFormatUsage.Render)
SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.Render)
```

不再使用旧 `FormatUsage`。

### 4. OpenGLES2 gate 清理

Unity 6.3 目标平台不再把 OpenGLES2 作为有效渲染目标 gate。本阶段清理 runtime 内旧 GLES2 特判，Compute / indirect / post-process 等功能仍通过真实能力检查、asset setting 和平台支持路径控制。

## 性能与移动端策略

CPU：

- Additional shadow candidate 数量仍受 `AdditionalLightUtils.MaxAdditionalLights` 和 `MaxShadowedAdditionalLights` 控制。
- 不引入 CPU per-instance shadow loop。
- 不把 vegetation indirect shadow 退回普通 `MeshRenderer` fallback。
- Shadow caster culling buffer 每 camera frame 准备一次，避免逐灯重复构建和提交。

GPU：

- 不新增 shadow atlas 数量。
- 不新增 fullscreen pass。
- RTHandle 复用不增加带宽，只减少临时 RT 生命周期抖动。
- point light 仍按 6 个 slice 计入 atlas 预算，这是正确性成本，不做隐藏降级。

移动端取舍：

- `MaxShadowedAdditionalLights = 4` 不是强行显示 4 盏灯的承诺，最终仍受 atlas max size、tile resolution、caster bounds、shadow strength 和 light type 影响。
- `2 point + 1 spot` 需要 13 个 atlas slices，移动端要继续关注 additional shadow atlas 分辨率和 shadow caster overdraw。
- 不建议为了编辑器预览强行扩大 Player 的 additional shadow 预算。
- SceneView 稳定性修复是 editor-only 预览规则，不改变 Android / iOS runtime candidate gate。

## Shader Variant 风险

本阶段没有新增 shader keyword。

```text
新增 shader keyword: 0
新增 multi_compile: 0
新增 shader_feature_local: 0
修改 shader 文件: 0
```

当前确认：

- `CoreBlit.shader` / `CoreBlitColorAndDepth.shader` 中非全局共享 keyword 已使用 `multi_compile_local`。
- GPU instancing 相关 `multi_compile_instancing` 保持原状。
- additional shadow 数量、强度、shadow type、atlas rect 均走 C# metadata / uniform，不新增 shader variant。

继续需要关注的既有风险：

- Tree / TreeLeaf alpha clip shadow caster 在 point light 6 face 下可能扩大 overdraw。
- Additional point light shadow 对 atlas slice 与 draw cost 敏感，应避免移动端默认开启过多点光阴影。
- 后续如果要加入 soft shadow、PCF tier 或 light cookie，应优先通过独立 shader / feature 控制，避免叠加到通用 forward variant。

## 验证记录

### 静态检查

已确认 runtime 内没有恢复旧 shadow API：

```text
rg "context\.DrawSkybox|\.DrawRenderers\(|\.DrawShadows\(|CullShadowCastersForLight" Assets/NWRP/Runtime
无命中
```

已确认 runtime 内没有旧临时 RT API：

```text
rg "GetTemporaryRT|ReleaseTemporaryRT" Assets/NWRP/Runtime
无命中
```

已确认 NWRP runtime / shader 不依赖 URP runtime include。搜索命中只来自 `AGENTS.md` 规则说明，不是运行时代码或 shader include：

```text
UnityEngine.Rendering.Universal
ScriptableRendererFeature
ScriptableRenderPass
Packages/com.unity.render-pipelines.universal
```

已确认 Unity 6 格式检查使用：

```text
GraphicsFormatUsage
```

### dotnet 编译

已完成：

```text
dotnet build NWRP.Runtime.csproj --no-restore -v:minimal
0 warnings / 0 errors
```

已完成：

```text
dotnet build NWRP.Runtime.Tests.csproj --no-restore -v:minimal
0 warnings / 0 errors
```

已完成：

```text
dotnet build NWRP.Editor.csproj --no-restore -v:minimal
0 errors
3 existing MSB reference conflict warnings
```

`NWRP.Editor` 的 MSB warning 属于既有 assembly reference conflict，本阶段没有新增 Editor 代码或窗口。

### Unity Editor / Test Runner

本阶段未完成 Unity Test Runner 自动运行。原因是 batchmode Unity 启动时检测到当前工程已被另一个 Unity Editor 实例打开，MCP 当前也没有暴露可直接运行的 `tests-run` 工具。

因此本阶段已完成 C# 编译与静态检查，但 SceneView / GameView 视觉 smoke 仍需要在当前 Editor 中手动执行。

## 手动验证清单

建议在复现场景或 `NWRPArtIntroLookDev` 中验证：

1. 只开启 `Point Light`，GameView 中 additional shadow 稳定存在。
2. 再开启 `Point Light (1)`，第一盏 point light shadow 不应消失。
3. 同时开启 `Point Light`、`Point Light (1)`、`Spot Light`，在 caster、距离、shadow strength、atlas 预算允许时，应看到 3 盏 additional shadow。
4. SceneView 拉远、拉近、旋转时，main light shadow 不应随机消失。
5. SceneView additional shadow 候选不应随 SceneView 镜头距离随机变化。
6. Frame Debugger 中确认 main cascade renderer lists 与 additional spot / point face renderer lists 顺序稳定。
7. 关闭 compute / indirect 支持路径时，vegetation 仍回到原有 MeshRenderer fallback，不应因为 shadow context 改造而丢失 fallback。

如果第三盏灯仍未渲染，应按固定规则排查：

```text
shadow disabled
shadow strength = 0
无有效 caster bounds
超出 additionalLightShadowDistance + range
point light 6 slices 导致 atlas tile resolution 低于最小值
MaxShadowedAdditionalLights 预算不足
atlas max size 不足
```

## 与旧阶段的关系

Phase48 / Phase49 / Phase50 主要处理 vegetation indirect 主光阴影链路：

```text
Phase48: indirect-only 场景下 shadow atlas bootstrap
Phase49: indirect-only 场景下 cascade fallback
Phase50: indirect vegetation shadowed pixel 的 SH / normal matrix 正确性
```

Phase51 修复 VolumeManager 生命周期，解决 Volume 驱动效果整体不生效。

Phase52 不新增渲染功能，也不扩大移动端默认阴影能力。它修复的是 Unity 6.3 renderer-list / shadow caster culling 生命周期迁移后的稳定性问题，并把旧 API 和 RT 生命周期继续收敛到 NWRP 自己可控的内部 helper。

## 当前注意事项

- 本阶段没有新增 `Window/Rendering/NWRP Migration Diagnostics`，也没有新增任何用户可见调试窗口。
- `NWRPShadowCullingContext` 是 runtime 内部 helper，不是新的 feature toggle。
- cached static shadow 仍保留独立 culling scope；如果后续继续改 cached shadow，需要继续保证它不会覆盖 realtime main / additional shadow context。
- Additional point light shadow 在移动端成本很高，后续默认资产参数仍应偏保守。
- 本阶段没有做 Android Vulkan / GLES3 / iOS Metal 真机验证，真机验证仍应作为合入前检查项。

## 后续方向

- 在 Unity Editor 可用时运行 `NWRPAdditionalShadowLayoutTests` 和既有 EditMode suite。
- 用复现场景做一次 SceneView + GameView 同开 smoke，重点确认 main light shadow 与 2 point + 1 spot additional shadow。
- 在 Frame Debugger 中记录 additional shadow atlas slice 分配，确认 point light face 与 spot slice 对应 receiver metadata。
- 若移动端真机上 additional point light 成本过高，优先调低 atlas resolution、shadow distance 或默认 max shadowed additional lights，而不是增加 shader variant 或恢复 CPU fallback。
