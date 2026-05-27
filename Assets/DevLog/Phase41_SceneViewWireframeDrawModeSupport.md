# Phase41 SceneView Wireframe 与 Shaded Wireframe 支持

日期: `2026-05-27`

## 概要

本阶段补齐 NWRP 在 Unity SceneView 顶部 Shading Mode 工具栏中的 `Wireframe` 与 `Shaded Wireframe` 支持。

Phase15 已经接入 SceneView Gizmo 绘制链路，Phase26 已经处理 SceneView 后处理开关与 editor camera fallback。本阶段继续收口 SceneView 编辑器预览能力，重点解决自定义 SRP 下 SceneView 线框模式不可用的问题。

最终能力边界:

- SceneView `Shaded Wireframe` 可显示 shaded scene 与 wire overlay。
- SceneView `Wireframe` 可显示纯线框结果。
- SceneView draw mode 校验对齐 URP 14.0.12 的支持范围。
- 所有新增逻辑均为 `UNITY_EDITOR` 路径，不进入移动端 Player。
- 不新增 shader、shader keyword、RenderTexture、MRT 或移动端 runtime pass。

## 问题背景

NWRP 原有 SceneView 支持主要覆盖:

- `ScriptableRenderContext.EmitWorldGeometryForSceneView(camera)`
- `GizmoSubset.PreImageEffects`
- `GizmoSubset.PostImageEffects`
- SceneView 后处理工具栏开关判断

但 SceneView 工具栏中的线框模式仍缺少两类关键处理:

1. 没有注册 SceneView draw mode validation，Unity SceneView 不知道当前 SRP 支持哪些 Shading Mode。
2. 没有处理 `DrawWireOverlay` 与 `GL.wireframe` 下的 final blit 特殊路径。

第一次接入 `DrawWireOverlay` 后，`Shaded Wireframe` 已经可以显示，但用户反馈 `Wireframe` 仍没有渲染出来。后续通过 MCP 排查确认:

```text
actual=Wireframe
try=True
resolved=Wireframe
isWire=True
cameraType=SceneView
pipeline=NWRP.NewWorldRenderPipelineAsset
```

这说明问题不是 draw mode 识别失败，而是纯 `Wireframe` 下的 final target 写回路径不正确。

## 修改文件

- `Assets/NWRP/Runtime/NewWorldRenderPipeline.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/Passes/DrawSceneViewWireOverlayPass.cs`
- `Assets/NWRP/Runtime/SceneView/NWRPSceneViewDrawMode.cs`
- `Assets/NWRP/Tests/Editor/ValleyHeightFogOverlayFeatureTests.cs`

## 核心实现

### 1. SceneView DrawMode 注册

新增 Editor-only 工具类:

```text
NWRPSceneViewDrawMode
```

职责:

- 在 `NewWorldRenderPipeline` 构造时调用 `SetupDrawMode()`。
- 在 pipeline dispose 时调用 `ResetDrawMode()`。
- 为每个 SceneView 注册 `sceneView.onValidateCameraMode`。
- 允许 `Wireframe` 与 `TexturedWire`。
- 拒绝当前 NWRP 未实现的 debug draw mode，例如 `Overdraw`、`Mipmaps`、`ValidateAlbedo` 等。

该逻辑参考 URP 的 `SceneViewDrawMode`，但放在 NWRP 自有命名空间，不引入 `UnityEngine.Rendering.Universal` 依赖。

### 2. SceneView Wire Overlay Pass

新增:

```text
DrawSceneViewWireOverlayPass
```

该 pass 为 Editor-only，挂在 `NWRPPassEvent.DebugOverlay`，执行:

```csharp
frameData.context.DrawWireOverlay(frameData.camera);
```

入队顺序调整为:

```text
FinalBlit -> Draw Scene View Wire Overlay -> Draw Gizmos Post Image Effects
```

原因:

- wire overlay 必须绘制到最终 SceneView target。
- 如果在 `FinalBlit` 前绘制，后续 present 可能覆盖纯线框结果。
- Post Image Gizmos 保持最后绘制，继续匹配 Unity SceneView 交互预期。

### 3. Wireframe 下的 FinalBlit 特殊路径

纯 `Wireframe` 仍不显示的根因在 final blit。

URP 14.0.12 在 `RenderingUtils.FinalBlit` 中有专门分支:

- 当 `GL.wireframe == true`
- 且当前是 SceneView camera
- 不能使用 SRP `Blitter.BlitTexture`
- 必须走 legacy `cmd.Blit`

原因是 `Blitter` 使用全屏三角形绘制。SceneView 纯 Wireframe 模式下 `GL.wireframe` 会影响该全屏三角形，导致最终拷贝只画出三角形边，无法得到完整 SceneView 图像。

NWRP 在 `PresentIntermediateColor` 中补齐同类判断:

```csharp
private static bool ShouldUseSceneViewWireframeBlit(Camera camera)
{
    return camera != null
        && camera.cameraType == CameraType.SceneView
        && GL.wireframe;
}
```

满足条件时使用:

```csharp
frameData.cmd.Blit(source, frameData.targets.backBufferColor);
```

Vulkan 下额外按 URP 逻辑临时关闭 command buffer wireframe:

```csharp
frameData.cmd.SetWireframe(false);
frameData.cmd.Blit(source, frameData.targets.backBufferColor);
frameData.cmd.SetWireframe(true);
```

这部分仅在 `UNITY_EDITOR` 中编译，不影响 Game Camera 和移动端 Player。

## 设计取舍

### 不做 NWRP 自定义线框 Shader

本阶段支持的是 Unity SceneView 编辑器工具栏行为，不是 runtime debug rendering feature。

因此没有新增:

- wireframe replacement shader
- geometry shader
- barycentric wireframe shader
- material keyword
- runtime renderer feature

原因:

- Geometry Shader 不符合移动端兼容性要求。
- Shader keyword 会增加 variant 风险。
- SceneView 工具栏本身是 Editor-only 能力，不应扩展成移动端 runtime 成本。

### 不把逻辑做成 NWRPFeature

SceneView wireframe 是 Unity Editor 预览能力，不是项目渲染功能。

本阶段沿用 Phase15 的 editor overlay 思路:

- 直接由 `NWRPRenderer` 在 `UNITY_EDITOR` 下入队。
- 不暴露到 `NewWorldRenderPipelineAsset`。
- 不进入 serialized feature list。

这样可以避免编辑器辅助能力污染移动端 runtime feature 配置。

## 性能与 Variant

CPU:

- 仅 SceneView editor camera 参与。
- Game Camera 不新增任何 draw mode 检查成本。
- Player 构建不包含 `UnityEditor.SceneView`、`DrawCameraMode`、`GL.wireframe` 相关逻辑。

GPU:

- `Wireframe` / `Shaded Wireframe` 只影响 SceneView。
- 不新增移动端 RT。
- 不新增 MRT。
- 不新增移动端 fullscreen blit。
- SceneView final blit 在 `GL.wireframe` 下改用 `cmd.Blit` 是 editor-only 兼容路径。

Shader Variant:

- 没有新增 shader keyword。
- 没有新增 `multi_compile`。
- 没有修改 NWRP shader family。
- variant 数量不变。

## 验证记录

### MCP 排查

通过 Unity MCP 执行 SceneView 状态探针，确认当前 SRP 与 SceneView mode:

```text
requested=Textured actual=Textured validate=True try=True resolved=Textured isWire=False
requested=TexturedWire actual=TexturedWire validate=True try=True resolved=TexturedWire isWire=True
requested=Wireframe actual=Wireframe validate=True try=True resolved=Wireframe isWire=True
pipeline=NWRP.NewWorldRenderPipelineAsset
```

这说明:

- SceneView 工具栏已经正确进入 `Wireframe`。
- NWRP 自己的 mode 识别已经返回 `Wireframe`。
- `DrawWireOverlay` 判断链路不是失败点。

随后对比 URP 14.0.12，定位到 `GL.wireframe` 下 final blit 需要 legacy `cmd.Blit`。

### 单元测试

新增并通过:

```text
NWRPSceneViewDrawModeTests
6 passed / 0 failed
```

覆盖内容:

- `Wireframe` 与 `TexturedWire` 被 SceneView draw mode validation 接受。
- 不支持的 debug mode 仍被拒绝。
- `DrawSceneViewWireOverlayPass` 位于 `DebugOverlay` 阶段。
- editor overlay 顺序为 pre gizmo、wire overlay、post gizmo。
- wire overlay 在 `FinalBlit` 之后执行。
- `SceneView && GL.wireframe` 时走 legacy final blit 分支。

### 全量 Editor Tests 当前状态

```text
NWRP.Editor.Tests
16 passed / 8 failed
```

失败项均为既有 `CloudShadowProjectorFeatureTests`:

- `NWRP.CloudShadowProjectorFeature` 类型不存在。
- `NWRP.NWRPCloudShadowProjector` 类型不存在。
- `Assets/NWRP/Shaders/Environment/CloudShadowProjector.shader` 文件不存在。
- `NWRPRendererDataEditor.AddCloudShadowProjectorFeature` 不存在。

这些失败与本阶段 SceneView wireframe 改动无关。

### Console

MCP 切换当前 SceneView 到 Wireframe 后:

```text
NWRP_WIRE_APPLY actual=Wireframe name=Wireframe cameraType=SceneView
```

近期 Console Error:

```text
0
```

## 当前限制与后续方向

- 当前仅支持 Unity SceneView 内建 `Wireframe` / `Shaded Wireframe`，不提供 runtime 线框调试模式。
- 如果后续需要移动端 runtime wireframe debug，应独立设计专用 debug shader / debug feature，并严格控制平台开关和 variant。
- 如果后续需要支持 `Overdraw`、`Mipmaps`、`ValidateAlbedo` 等 SceneView debug mode，应逐项实现，不要一次性做成全能 SceneView debug feature。
- 若未来重构 final blit / RTHandle present 逻辑，需要保留 `SceneView && GL.wireframe` 的 legacy blit 兼容分支。
