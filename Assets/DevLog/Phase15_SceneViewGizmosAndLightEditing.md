# Phase15 SceneView Gizmos 与灯光多选编辑 UI

Date: `2026-04-24`

## 概要

本阶段补齐 NWRP 在 Unity SceneView 下的编辑器可视化支持，重点解决美术编辑灯光时看不到 Gizmo / 3D Icon，以及多选多个灯光后范围 UI 丢失的问题。

完成后的能力边界：

- SceneView 支持 Unity 官方 Gizmo 绘制链路，包括普通 Gizmo、3D Icon 与 Image Effect 前后 Gizmo。
- `Directional / Point / Spot` 灯光在单选和多选时都使用 Unity 官方灯光范围 UI。
- 所有新增逻辑均为 Editor-only，不进入 Player 构建。
- 不新增 RenderTexture、fullscreen blit、shader keyword 或 shader variant。

## 核心实现

### 1. SceneView Gizmo 绘制链路

修改文件：

- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/Passes/DrawGizmosPass.cs`

完成内容：

- 在 SceneView camera culling 前调用 `ScriptableRenderContext.EmitWorldGeometryForSceneView(camera)`。
- 新增 Editor-only `DrawGizmosPass`：
  - `GizmoSubset.PreImageEffects` 接入 `NWRPPassEvent.AfterTransparent`。
  - `GizmoSubset.PostImageEffects` 接入 `NWRPPassEvent.DebugOverlay`。
- `DrawGizmos` 执行时尊重 Unity 官方开关：
  - `Handles.ShouldRenderGizmos()`
  - `camera.sceneViewFilterMode != Camera.SceneViewFilterMode.ShowFiltered`

设计取舍：

- Gizmo pass 只在 `UNITY_EDITOR` 下编译，运行时移动端构建不包含该 pass。
- pass 入队放在 Editor 条件块内，不走 `NWRPFeature` 序列化配置，避免把纯编辑器辅助能力暴露成移动端 runtime feature。

### 2. 灯光 Scene GUI 改为官方实现

修改文件：

- `Assets/NWRP/Editor/NWRPLightEditor.cs`
- `Assets/NWRP/Editor/NWRP.Editor.asmdef`

完成内容：

- `NWRPLightEditor` 改为：
  - `[CustomEditorForRenderPipeline(typeof(Light), typeof(NewWorldRenderPipelineAsset))]`
  - 继承 `UnityEditor.LightEditor`
- Scene GUI 不再反射调用内置 `LightEditor.OnSceneGUI`。
- 对当前 `target` 按灯光类型调用 Unity 官方实现：
  - `CoreLightEditorUtilities.DrawDirectionalLightGizmo`
  - `CoreLightEditorUtilities.DrawPointLightGizmo`
  - `CoreLightEditorUtilities.DrawSpotLightGizmo`
- 多选灯光时由 Unity Editor 对每个 selected target 分发 `OnSceneGUI`，避免在 `OnSceneGUI` 中访问 `targets` 触发 Unity Editor 错误。
- `NWRP.Editor.asmdef` 增加 `Unity.RenderPipelines.Core.Editor` 引用，用于访问官方灯光 Gizmo 工具。

保留内容：

- Inspector 仍保留 NWRP 简化面板，只暴露当前移动端实时路径消费的字段。
- 混合类型或非 NWRP 支持灯光类型仍走 fallback Inspector。

## 代码检查与优化

本阶段完成实现后做了一轮检查，并做了两处收口优化：

- `NWRPLightEditor` 的 fallback editor 改为 lazy 创建，不再每次 `OnEnable` 都创建内置 `LightEditor` 实例。
- `DrawGizmos` 判断补齐 URP 官方的 SceneView filter 保护，避免隔离过滤模式下绘制无效 Gizmo。

移动端影响：

- Player 构建不编译 `UnityEditor` 引用。
- 不增加 runtime pass 数量。
- 不增加 RT / blit / MRT。
- 不新增 shader keyword，variant 数量不变。

## 验证记录

静态检查：

- `git diff --check`
  - 无 whitespace error。
- 检查 runtime 目录中的 `UnityEditor` 引用：
  - 新增引用均包裹在 `#if UNITY_EDITOR` 内。
  - `DrawGizmosPass.cs` 整个文件为 Editor-only。

Unity 验证：

- `AssetDatabase.Refresh`
  - Unity 编译完成。
  - Console 无 C# / NWRP Error。
- 多选验证：
  - 选择 `Point Light`
  - 选择 `Point Light (1)`
  - 选择 `Spot Light`
  - 选择 `Directional Light`
  - SceneView repaint 后无 `OnSceneGUI` error。

已知无关 warning：

- Console 中仍可能出现 `com.ivanmurzak.unity.mcp` 的 NuGet restore warning。
- 该 warning 与本阶段 NWRP SceneView / LightEditor 改动无关。

## 当前限制与后续方向

当前限制：

- 灯光范围 UI 直接沿用 Unity 官方 `CoreLightEditorUtilities`，不实现 NWRP 自定义批量缩放行为。
- Gizmo 显示仍受 SceneView Gizmos 开关控制；关闭 Gizmos 时不强制绘制。

后续可选方向：

- 若美术需要，可在 NWRP Light Inspector 中增加轻量状态提示，例如当前 SceneView Gizmos 是否关闭。
- 若后续增加自定义 light 类型，再为该类型单独补充 Editor-only Scene GUI，不把逻辑塞进现有三种灯光路径。
