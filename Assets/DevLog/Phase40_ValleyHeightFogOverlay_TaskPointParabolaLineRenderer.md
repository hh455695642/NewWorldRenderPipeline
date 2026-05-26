# Phase40 ValleyHeightFogOverlay 与 TaskPoint 抛物线连线渲染修复

日期: `2026-05-26`

## 概要

本阶段串联了旧项目 `ParabolaLine` 特效迁移、`ValleyHeightFogOverlay` 可插拔化，以及场景中 `Task Line` 抛物线连线的实际运行时修复。相关连续会话包括 `019e634d-3808-7912-b01c-d26611f71090` 中的 Overlay Feature 拆分，以及随后对 `TaskPointParabolaGenerator`、prefab、材质和 shader 的检查。

整体问题链路如下:

- 旧项目的 `ParabolaLine.shader` 使用 `LightMode="AfterFog"`，依赖一个在 Valley Height Fog 之后、后处理之前执行的透明补画 pass。
- 当前 NWRP 需要把该逻辑拆成可插拔 `NWRPFeature / NWRPPass`，不能把旧 URP `ScriptableRendererFeature` 直接搬进主渲染流程。
- 实际场景里的 `Task Line` 又暴露了两个运行时问题: clone 出来的 `LineRenderer` prefab 被脚本默认隐藏，`progress` 在 Inspector 中改值后不再实时写入材质属性。
- 最终对当前任务线采用雾后 overlay 渲染路径: `ParabolaLine.shader` 使用 `AfterFog` pass，由 `ValleyHeightFogOverlayPass` 在 Valley Height Fog 之后、PostProcess 之前绘制。

本阶段没有修改 `CameraRenderer` / `NWRPRenderer` 主流程，遵守 NWRP 的 Feature / Pass 扩展边界。

## ValleyHeightFogOverlay 拆分

旧项目中的 `ValleyHeightFogOverlayPass` 是 URP `ScriptableRenderPass`，核心行为很轻:

- 执行在 Valley Height Fog 之后、Post Processing 之前。
- 通过 `ShaderTagId("AfterFog")` 筛选 shader pass。
- 只绘制 `RenderQueueRange.transparent`。
- 使用 `SortingCriteria.CommonTransparent`。
- 使用 `RenderStateBlock(RenderStateMask.Nothing)`，不覆盖材质自身 render state。
- 不申请额外 RenderTexture，不做 fullscreen blit。

NWRP 中按同样职责拆成独立功能:

- `Assets/NWRP/Runtime/PostProcessing/ValleyHeightFogOverlayFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/ValleyHeightFogOverlayPass.cs`
- `Assets/NWRP/Runtime/NWRPPassEvent.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`

接入点:

```text
NWRPPassEvent.AfterValleyHeightFog = 575
```

顺序位于:

```text
AfterTransparent -> AfterValleyHeightFog -> PostProcess
```

Overlay pass 支持两个 tag:

```text
AfterFog
NWRPAfterFog
```

`AfterFog` 用于兼容旧项目 shader；`NWRPAfterFog` 作为后续 NWRP 自有命名扩展点。该 pass 只负责“雾后透明补画层”，不绑定具体 `ParabolaLine` 资产，也不依赖 Valley Height Fog Volume 当前是否激活。

移动端取舍:

- 增加的是一次透明对象 `DrawRenderers`，不是全屏 blit。
- 不新增 RT，避免额外带宽。
- 不引入 URP runtime 依赖。
- Feature 通过 renderer data 显式挂载，可按项目需求启用或移除。

## ParabolaLine 资产检查

本阶段检查了旧项目拷入的两个 prefab:

- `Assets/NewWorld/ArtResources/Effects/Prefabs/P_FX_ParabolaLine.prefab`
- `Assets/NewWorld/ArtResources/Effects/Prefabs/P_FX_ParabolaLine_Big.prefab`

检查结果:

- 两个 prefab 都是单个 `LineRenderer`。
- 材质分别为 `M_FX_ParabolaLine_Small` 和 `M_FX_ParabolaLine_Big`。
- 运行时 shader 可解析到 `NewWorld/Env/ParabolaLine`。
- 原始旧项目 shader pass 为 `AfterFog`，可被 `ValleyHeightFogOverlayPass` 捕获。
- 两个 prefab 的 `sortingOrder = 4`，`widthMultiplier = 0.4`。

同时记录了两个资产风险:

- 材质 YAML 中曾保留旧项目 shader GUID，干净导入环境可能出现 shader 引用丢失，需要以当前项目 shader GUID 为准。
- 透明 LineRenderer 作为特效线，`Cast Shadows` / `Receive Shadows` 不应作为默认开启项；移动端建议关闭，减少无意义阴影状态和潜在渲染成本。

## Shader 渲染路径决策

`ParabolaLine.shader` 当前路径:

- `Assets/NewWorld/ArtResources/Shaders/Effects/ParabolaLine.shader`

旧项目版本依赖:

```hlsl
Tags { "LightMode"="AfterFog" }
```

这条路径正是当前 `Task Line` 需要的结果: 抛物线 LineRenderer 应该作为雾后 overlay 特效绘制，保证它出现在 Valley Height Fog 之后、PostProcess 之前。旧项目 `MainMap` layer 仍不参与，本项目只依赖默认 layer 与 shader pass tag。

因此当前最终实现让任务线 shader 走 `ValleyHeightFogOverlayPass`:

```hlsl
Name "AfterFogParabolaLine"
Tags { "LightMode"="AfterFog" }
```

透明队列保持:

```hlsl
"RenderType"="Transparent"
"Queue"="Transparent"
```

这样 `LineRenderer` 不会被 NWRP 常规 `Draw Transparent Objects` 阶段捕获，而是由 `ValleyHeightFogOverlayPass` 通过 `ShaderTagId("AfterFog")` 绘制。

保留规则:

- 当前 `ParabolaLine` / `Task Line` 使用 `AfterFog`，保持旧项目兼容语义。
- 后续新增 NWRP 自有雾后 overlay shader 时，可使用 `NWRPAfterFog`。
- 普通无雾后需求的透明特效仍可使用 `NewWorldUnlit`。

## TaskPointParabolaGenerator 修复

实际场景中的 `Task Line` 使用:

- `Assets/Scripts/TaskPointParabolaGenerator.cs`

问题集中在脚本运行时逻辑:

- clone 出来的 prefab 被默认隐藏。
- 旧项目 `MainMap` layer 赋值仍留在脚本中。
- `progress` 只在 `Start()` 写入一次，运行时 Inspector 拖动不会继续刷新材质属性。

本阶段调整:

```csharp
void Update()
{
    SetProgress(progress);
}
```

这让 Inspector 中修改 `progress` 时，每帧都会重新写入 `_Progress`。

旧项目 layer 逻辑已注释，clone 保持 prefab 默认 layer:

```csharp
// lineObj.layer = LayerMask.NameToLayer("MainMap");
```

默认隐藏逻辑已注释，生成后是否显示交给 prefab active 状态和业务逻辑控制:

```csharp
// line.gameObject.SetActive(false);
```

当前场景验证中，clone 出来的 `P_FX_ParabolaLine(Clone)` 为:

```text
activeInHierarchy = True
LineRenderer.enabled = True
layer = Default(0)
```

## Progress 逻辑

`SetProgress(float p)` 按整条路径长度分配到每一段线:

```text
global progress -> current path length -> per-line localProgress -> _Progress
```

每段 `LineRenderer` 使用 `MaterialPropertyBlock` 写入:

```csharp
mpb.SetFloat(ProgressID, localProgress);
line.SetPropertyBlock(mpb);
```

shader 中 `_Progress` 控制未完成纹理和完成纹理之间的切换:

```hlsl
half progressMask = step(1.0h - _Progress, 1.0h - (half)uv.x);
return lerp(colorDefault, colorProgress, progressMask);
```

当前采用 Inspector 实时调试模式，每帧调用 `SetProgress(progress)`。场景中只有少量任务线，CPU 侧 `SetPropertyBlock` 成本可接受。

后续如果该脚本用于大量路线，应改为 dirty-value 刷新，避免无变化时每帧写 `MaterialPropertyBlock`。

## 性能与 Variant

CPU:

- `ValleyHeightFogOverlayPass` 仅在启用 Feature 时增加一次透明对象绘制调度。
- `TaskPointParabolaGenerator` 当前每帧按线段数量写 `MaterialPropertyBlock`。
- 当前验证为 2 条线，不构成 CPU 大规模循环渲染问题。

GPU:

- 当前 Task Line 依赖已启用的 `ValleyHeightFogOverlayPass`。
- 当前 Task Line 不新增 RenderTexture。
- 当前 Task Line 不做 fullscreen blit。
- `LineRenderer` 仍在透明队列中，主要成本来自透明 overdraw 和 2 次纹理采样。

Shader Variant:

- 没有新增自定义 keyword。
- 保留 `#pragma multi_compile_instancing`。
- 无 URP keyword 叠加。
- 当前 variant 增长可控。

后续如果任务线、战斗特效线、UI 线需求分化，应拆分 shader family，不应在同一个 `ParabolaLine.shader` 中堆叠大量 keyword。

## 验证记录

Overlay Feature 阶段验证过:

- `AfterValleyHeightFog` 位于 `AfterTransparent` 与 `PostProcess` 之间。
- `ValleyHeightFogOverlayFeature` 可入队一个 `ValleyHeightFogOverlayPass`。
- Overlay pass 使用 `AfterFog` / `NWRPAfterFog` tag 绘制透明队列。
- 未引入 `UnityEngine.Rendering.Universal`、`ScriptableRendererFeature`、`ScriptableRenderPass` 到 NWRP runtime。

最终 Task Line 阶段执行:

```text
AssetDatabase.Refresh
```

结果:

```text
Success
Unity Console Error: 0
Unity Console Warning: 0
```

Play Mode 验证:

```text
isPlaying=True
taskLineFound=True
lineCount=2
line[0] name=P_FX_ParabolaLine_Big(Clone), active=True/True, enabled=True, layer=Default(0), shader=NewWorld/Env/ParabolaLine, lightMode=AfterFog, queue=3000, mpbProgress=0.5278745
line[1] name=P_FX_ParabolaLine_Big(Clone), active=True/True, enabled=True, layer=Default(0), shader=NewWorld/Env/ParabolaLine, lightMode=AfterFog, queue=3000, mpbProgress=0
```

运行时将 `TaskPointParabolaGenerator.progress` 设置为 `1` 后再次验证:

```text
lineCount=2
line[0] active=True, enabled=True, lightMode=AfterFog, mpbProgress=1
line[1] active=True, enabled=True, lightMode=AfterFog, mpbProgress=0.9999999
```

结论:

- `P_FX_ParabolaLine(Clone)` 不再被脚本默认隐藏。
- clone 对象保持 `Default(0)` layer。
- 当前任务线材质进入 NWRP 可识别的 `AfterFog` pass，并由 `ValleyHeightFogOverlayPass` 绘制。
- `_Progress` 能通过 `MaterialPropertyBlock` 写入并更新。
- 验证后已退出 Play Mode。

## 后续建议

- 如果任务线需要业务显隐控制，应提供明确接口，不要在生成阶段默认隐藏所有 clone。
- 如果 `progress` 只由业务系统驱动，可后续把每帧刷新改为外部 dirty 调用。
- 如果未来还需要其他“雾后透明补画”的特效层，继续复用 `ValleyHeightFogOverlayFeature`，对应 shader 使用 `AfterFog` 或 `NWRPAfterFog`。
- 如果后续路线数量增多，应考虑批处理或 GPU-driven 方案，不要扩展成大量独立 `LineRenderer`。
