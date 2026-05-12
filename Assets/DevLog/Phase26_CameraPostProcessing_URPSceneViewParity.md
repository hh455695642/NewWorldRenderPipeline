# Phase26 Camera PostProcessing URP Naming / SceneView Preview Alignment

日期：`2026-05-12`

## 概要

本阶段围绕 Phase25 新增的 NWRP 后处理框架继续收口，重点不是新增新的后处理效果，而是对齐 URP 用户习惯中的 Camera 后处理开关语义，并排查 Scene View 后处理预览链路。

Phase25 中 `NWRPCameraData` 已经承担 NWRP 自己的相机级后处理入口，但早期实现中存在两个体验差异：

1. 相机组件上的后处理开关命名与 URP 的 `renderPostProcessing` 不完全一致。
2. Scene View 预览后处理时，既不能稳定依赖临时 SceneView Camera 上存在 `NWRPCameraData`，又需要尊重 Scene 窗口顶部工具栏的 Effects / Post Processing 开关。

本阶段参考 URP 14.0.12 的 CameraData / AdditionalCameraData 关系，将“相机组件上的用户开关”和“每帧运行时解析后的后处理状态”区分开：

- `NWRPCameraData.renderPostProcessing`：对齐 URP 命名，是用户在相机组件上看到/控制的开关。
- `NWRPFrameData.postProcessingEnabled`：NWRP 当前帧真正执行后处理的 runtime 状态。

同时针对 Scene View 增加 Editor-only 判断，尝试接入 Unity Scene 窗口顶部工具栏的 Image Effects / Post Processing 状态，并继续保持 Player 构建不包含这些编辑器依赖。

> 当前状态说明：Game Camera 的相机开关语义已按 URP 风格整理；Scene View 侧已实现工具栏开关判断和 Volume 采样 fallback，但用户在最新 v3 覆盖后仍反馈 Scene 视图没有实际效果。因此本阶段日志同时记录已完成改动与后续需要继续定位的 SceneView 预览问题。

## 修改文件

- `Assets/NWRP/Runtime/NWRPCameraData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`

## 解决的问题

### NWRPCameraData 的开关命名与 URP 使用习惯不一致

URP 中相机组件上的后处理开关位于 `UniversalAdditionalCameraData.renderPostProcessing`，而运行时真正传入 renderer 的状态是 `CameraData.postProcessEnabled`。也就是说，URP 将“用户配置项”和“当前帧解析结果”分开处理。

NWRP 早期实现中公开的是：

```csharp
public bool RenderPostProcessing => renderPostProcessing;
```

这对 NWRP 自身可用，但不利于从 URP 迁移项目或阅读代码时保持直觉一致。

本阶段将相机组件改为 URP 风格命名：

```csharp
public bool renderPostProcessing
{
    get => m_RenderPostProcessing;
    set => m_RenderPostProcessing = value;
}
```

并保留旧入口作为兼容 alias：

```csharp
public bool RenderPostProcessing => renderPostProcessing;
```

这样后续 NWRP 内部代码优先使用 `cameraData.renderPostProcessing`，而已有调用 `RenderPostProcessing` 的代码不会立刻断裂。

### 序列化字段需要避免破坏已有相机配置

为了后续更接近 URP 的字段风格，本阶段把内部字段整理为：

```csharp
[SerializeField]
[FormerlySerializedAs("renderPostProcessing")]
private bool m_RenderPostProcessing = true;
```

这样旧版本中已经序列化为 `renderPostProcessing` 的相机组件，升级到新字段后仍能迁移原值，避免场景里的相机后处理开关丢失。

当前默认值仍保持 `true`，原因是 Phase25 的 Editor AutoAdd 流程会给 Game Camera 自动添加 `NWRPCameraData`，如果新组件默认 `false`，会导致测试场景或用户现有场景升级后 Game View 后处理突然关闭。

### Scene View 不能依赖 NWRPCameraData

Scene View Camera 是 Unity Editor 管理的临时相机，不适合要求其挂载 `NWRPCameraData`。Phase25 已经做过 SceneView fallback：当 cameraType 为 `SceneView` 时，直接使用 SceneView Camera 的 transform 和 cullingMask 采样 Volume。

本阶段在此基础上补充 URP 风格的 SceneView 后处理开关判断：

```csharp
if (camera.cameraType == CameraType.SceneView)
{
    if (!IsSceneViewPostProcessingEnabled(camera))
    {
        return;
    }

    ConfigurePostProcessingFromVolume(
        ref frameData,
        camera.transform,
        camera.cullingMask,
        null);
    return;
}
```

也就是说，Scene View 不走相机组件上的 `renderPostProcessing`，而是走 Scene 窗口顶部工具栏的 Effects / Post Processing 状态。

### Scene 窗口顶部工具栏后处理开关需要接入

URP 的 Scene View 后处理不是只看 Camera 上的组件开关，还会读取 SceneView 自身的 `imageEffectsEnabled` 状态，并在 Wireframe 模式下禁用后处理。

本阶段增加 `IsSceneViewPostProcessingEnabled`：

```csharp
#if UNITY_EDITOR
private static bool IsSceneViewPostProcessingEnabled(Camera camera)
{
    if (camera == null || camera.cameraType != CameraType.SceneView)
    {
        return false;
    }

    if (CoreUtils.ArePostProcessesEnabled(camera))
    {
        return true;
    }

    SceneView currentSceneView = SceneView.currentDrawingSceneView;
    return currentSceneView != null
        && currentSceneView.sceneViewState.imageEffectsEnabled
        && currentSceneView.cameraMode.drawMode != DrawCameraMode.Wireframe;
}
#endif
```

该函数先走 Unity Core 的 `CoreUtils.ArePostProcessesEnabled(camera)`，用于匹配 URP 常规行为；如果 SceneView 临时相机引用无法与当前绘制 SceneView 完全匹配，则 fallback 到 `SceneView.currentDrawingSceneView`，继续使用同样的工具栏开关和 Wireframe 规则。

### Volume Stack 需要按相机刷新，避免 Game / SceneView 状态串扰

`VolumeManager.instance.stack` 是全局管理的 Volume stack。Game Camera 与 Scene View Camera 交替渲染时，如果不重置主 stack，容易让上一个相机的 Volume 状态影响当前相机的后处理判断。

本阶段在每次采样 Volume 前增加：

```csharp
VolumeManager.instance.ResetMainStack();
VolumeManager.instance.Update(volumeTrigger, volumeLayerMask);
```

让 Game Camera 和 Scene View 都从当前相机自己的 trigger / layer mask 重新解析 Volume。

## 关键实现

### NWRPCameraData

本阶段后的相机数据职责如下：

- `renderPostProcessing`：URP 风格的用户开关。
- `RenderPostProcessing`：旧 NWRP 代码兼容入口，后续不建议新增调用。
- `VolumeLayerMask`：Volume 采样层。
- `GetVolumeTrigger(Camera camera)`：Volume trigger 优先使用手动指定 transform，否则 fallback 到 Camera transform。

该类仍然只是 MonoBehaviour 配置组件，不承担 per-frame runtime 状态。运行时状态继续保存在 `NWRPFrameData`。

### NWRPRenderer.ConfigureCameraData

本阶段将后处理状态解析顺序整理为：

1. 清空上一帧状态：`cameraData / volumeStack / postProcessingEnabled / tonemappingActive / tonemapping`。
2. 检查 Camera / Asset / `SupportsPostProcessing`。
3. GLES2 直接禁用后处理。
4. Editor SceneView 分支：走 SceneView 工具栏开关，使用 SceneView camera transform / cullingMask 采样 Volume。
5. Game / 普通 Camera 分支：要求存在 `NWRPCameraData` 且 `renderPostProcessing = true`。
6. 更新 Volume stack，读取 `NWRPTonemapping`，写入 `NWRPFrameData`。

这样可以保持：

```text
Camera component setting
    -> NWRPFrameData.postProcessingEnabled
        -> PostProcessFeature target requirement
            -> NWRP PostProcess pass enqueue / execution
```

### SceneView 分支边界

SceneView 逻辑全部包裹在 `#if UNITY_EDITOR` 下，避免 Player 构建引入 `UnityEditor.SceneView`、`DrawCameraMode` 等编辑器类型。

SceneView 不要求 `NWRPCameraData`，也不使用 Game Camera 的 Volume LayerMask，而是使用：

```csharp
camera.transform
camera.cullingMask
```

这与编辑器预览需求更匹配：Scene View 能看到当前场景视锥和可见 layer 下的 Volume 效果，但不改变运行时相机的零成本规则。

## 性能与移动端策略

- 不新增 shader。
- 不新增 shader keyword。
- 不新增业务材质 variant。
- 不新增 RenderTexture 类型。
- 不新增新的顶层 NWRP pass。
- Game Camera 缺少 `NWRPCameraData` 时仍不执行后处理。
- GLES2 继续禁用后处理。
- SceneView 相关逻辑仅在 Editor 编译，不进入移动端 Player。

本阶段属于 CameraData / SceneView 行为收口，不改变 Phase25 中 `NWRP PostProcess` 的统一 pass 设计。

## 验证记录

### 静态检查

完成源码级检查：

- `NWRPCameraData` 仍只依赖 `UnityEngine` 与 `UnityEngine.Serialization`。
- `NWRPRenderer` 中新增的 `SceneView` / `DrawCameraMode` 访问位于 `#if UNITY_EDITOR` 块内。
- Game Camera 后处理状态仍依赖 `NWRPCameraData`，没有扩大运行时默认后处理成本。
- `renderPostProcessing` 与 `NWRPFrameData.postProcessingEnabled` 的语义分层已经明确。

### 用户侧反馈

v1 / v2 / v3 迭代后，用户反馈：

```text
Scene 视图依旧没有效果。
```

因此当前不能把 SceneView 后处理预览标记为完成。已完成的是：

- URP 风格相机开关命名整理。
- SceneView 工具栏开关读取逻辑接入尝试。
- SceneView Volume fallback 路径保留。
- Volume stack 重置补齐。

仍待定位的是：SceneView 已满足 `postProcessingEnabled` 后，后处理 pass 是否真正 enqueue、执行，以及最终是否正确写回 SceneView target。

## 当前需要继续排查的点

### 1. Frame Debugger 中 SceneView 是否出现 NWRP PostProcess

首先确认 SceneView 渲染时是否存在：

```text
NWRP PostProcess
```

如果没有，问题仍在状态判断 / Feature target requirement / pass enqueue 链路。

重点检查：

- `IsSceneViewPostProcessingEnabled(camera)` 是否返回 true。
- `VolumeManager.instance.stack.GetComponent<NWRPTonemapping>()` 是否能拿到 active component。
- `PostProcessFeature.HasAnyActivePostProcess(ref frameData)` 是否为 true。
- SceneView 所在 camera.cullingMask 是否包含 Tonemapping Volume 所在 layer。

### 2. 如果 pass 出现但画面无效果，检查最终写回目标

如果 Frame Debugger 中有 `NWRP PostProcess`，但 SceneView 画面无变化，则问题更可能在目标和 blit 方向：

- `PostProcessPass` 是否写入 `frameData.targets.backBufferColor`。
- SceneView 的 backbuffer / camera target 是否需要不同的 final target 处理。
- `GetFinalBlitScaleBias` 对 SceneView 的 Y flip 是否正确。
- `frameData.targets.cameraColorPresented` 是否过早导致 FinalBlit 跳过了必要写回。

### 3. SceneView 工具栏开关名称差异

不同 Unity 版本或布局下，SceneView 顶部可能显示为：

- Effects
- Image Effects
- Post Processing

URP 底层判断核心仍是 `sceneViewState.imageEffectsEnabled`。如果 UI 已开启但代码仍判断为 false，需要在 Editor 下临时输出该状态确认。

### 4. Volume LayerMask 与 SceneView cullingMask

当前 SceneView fallback 使用 `camera.cullingMask` 作为 Volume layer mask。若 Volume 所在 layer 被 SceneView 过滤，SceneView 就不会采样到 Volume。

可临时验证：

```csharp
ConfigurePostProcessingFromVolume(
    ref frameData,
    camera.transform,
    ~0,
    null);
```

如果 `~0` 后 SceneView 有效果，则问题是 layer mask，不是 post-process pass。

## 后续方向

- 给 `ConfigureCameraData` 增加临时 Editor-only debug log 或 profiler marker，输出：
  - cameraType
  - SceneView imageEffectsEnabled
  - postProcessingEnabled
  - tonemappingActive
  - volumeLayerMask
- 在 Frame Debugger 中分别对比 Game Camera 与 SceneView：
  - 是否都请求 intermediate color。
  - 是否都执行 `NWRP PostProcess`。
  - 是否都设置 `cameraColorPresented`。
- 若确认 SceneView pass 已执行但无画面效果，下一阶段应专门处理 SceneView final target / blit path，而不是继续调整 CameraData 命名。
- 后续如果要完全贴近 URP，可以进一步引入类似 `postProcessEnabled` 的显式 Camera runtime data 字段，但当前 NWRP 已用 `NWRPFrameData.postProcessingEnabled` 承担该职责，不建议再新增重复字段。
