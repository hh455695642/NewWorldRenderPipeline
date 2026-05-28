# Phase44 NWRP Renderer Data Layer Filtering

日期: `2026-05-28`

## 概要

本阶段为 NWRP Renderer Data 增加 Filtering 能力，用于控制当前 renderer 绘制哪些不透明层和透明层，对齐常见 Renderer Data 中的以下配置语义：

- `Filtering`
- `Opaque Layer Mask`
- `Transparent Layer Mask`

默认值保持为 `Everything`，确保旧资产升级后渲染结果不变。该功能只影响 `DrawRenderers` 的 layer 过滤，不新增 RenderPass、不分配 RenderTexture、不改 shader、不增加 keyword 或 variant。

## 问题背景

此前 NWRP 的主渲染路径只按 `RenderQueueRange.opaque` / `RenderQueueRange.transparent` 区分对象，renderer data 本身没有独立的 layer 过滤入口。

项目需要在不侵入主 SRP 架构、不改变 camera culling mask 语义的前提下，让不同 Renderer Data 可以控制：

- 当前 renderer 绘制哪些 opaque layer。
- 当前 renderer 绘制哪些 transparent layer。
- 深度预通道、描边、雾后透明叠加等依赖 draw filtering 的 pass 与主渲染保持一致。

该能力属于 renderer 数据层面的绘制筛选，不是新的渲染效果，因此没有拆成新的 `NWRPFeature` / `NWRPPass`。

## 修改文件

- `Assets/NWRP/Runtime/NWRPRendererData.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/Passes/DepthPrepass.cs`
- `Assets/NWRP/Runtime/Outlines/Passes/DrawOutlinePass.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/ValleyHeightFogOverlayPass.cs`
- `Assets/Settings/NewWorldRP.asset`
- `Assets/NWRP/Tests/Editor/ValleyHeightFogOverlayFeatureTests.cs`

## 核心实现

### 1. Renderer Data 配置

在 `NWRPRendererData` 中新增 `RendererFilteringSettings`：

```text
opaqueLayerMask = Everything
transparentLayerMask = Everything
```

并暴露只读访问入口：

```text
OpaqueLayerMask
TransparentLayerMask
```

`OnValidate` 会确保旧资产缺失 filtering 数据时自动补齐默认配置，避免已有 renderer data 升级后出现空引用或错误过滤。

### 2. Inspector 面板

`NWRPRendererDataEditor` 新增 `Filtering` 区块，字段名保持为：

```text
Opaque Layer Mask
Transparent Layer Mask
```

Tooltip 语义：

- `Controls which opaque layers this renderer draws.`
- `Controls which transparent layers this renderer draws.`

该区块放在 Feature Settings 之前，属于 renderer 的基础绘制过滤配置。

### 3. 主绘制路径

`NWRPRenderer` 的 opaque / transparent 主绘制改为显式传入 layer mask：

```text
FilteringSettings(RenderQueueRange.opaque, opaqueLayerMask)
FilteringSettings(RenderQueueRange.transparent, transparentLayerMask)
```

当 `frameData.rendererData` 为空时，回退为 `~0`，保持全部 layer 可绘制。

### 4. 相关 pass 对齐

为避免不同 pass 之间出现 layer 语义不一致，本阶段同步调整：

- `DepthPrepass` 使用 opaque layer mask。
- `DrawOutlinePass` 使用 opaque layer mask。
- `ValleyHeightFogOverlayPass` 的 transparent draw 使用 transparent layer mask。

这样 opaque 相关的深度、描边与主 opaque draw 保持一致；透明雾后叠加只处理 renderer data 允许的 transparent layer。

## 设计取舍

### 不修改 Camera Culling Mask

Renderer Data layer mask 是 `DrawRenderers` 阶段的附加过滤，不替代 `Camera.cullingMask`。

这意味着：

- Camera culling mask 仍决定相机可见对象集合。
- Renderer Data mask 决定当前 renderer 实际提交哪些 layer 的 draw。
- 如果目标是减少 culling 本身的 CPU 工作量，仍应调整 camera culling mask 或引入更前置的 culling 策略。

### 不影响阴影投射

本阶段没有改 shadow caster layer 规则。

主光阴影、额外灯光阴影和 shadow receiver 策略仍由现有 shadow settings / shadow pass 控制。Renderer Data 的 opaque layer mask 不会静默改变 shadow caster 集合，避免 renderer 可见性与阴影可见性发生隐式耦合。

### 不新增 Pass 或 Feature

该能力是 renderer data 的基础过滤参数，不是独立效果模块。新增 pass 会增加调度复杂度，但不会带来实际收益。

### 不改 Shader

过滤发生在 SRP draw filtering 层，不需要 shader 分支、keyword、material property 或 pass tag 变更。

## 性能与 Variant

CPU:

- 无 per-object CPU for-loop。
- 无额外 culling 系统。
- 只是在构造 `FilteringSettings` 时传入 layer mask。

GPU:

- 被过滤 layer 不会进入对应 draw 提交。
- 不新增 fullscreen pass。
- 不新增临时 RT。
- 不新增 blit / MRT。

Shader Variant:

- 新增 keyword 数量：`0`
- 新增 `shader_feature`：`0`
- 新增 `multi_compile`：`0`
- Variant 风险：无

移动端风险较低。该改动主要减少不需要提交的 renderer draw 候选，不引入 tile memory flush 或额外带宽读写。

## 验证记录

TDD 红灯检查：

```text
Missing NWRPRendererData.OpaqueLayerMask
```

实现后验证：

```text
RendererDataLayerFilteringTests
3 passed / 0 failed
```

覆盖内容：

- 默认 opaque / transparent layer mask 为 `Everything`。
- opaque 与 transparent mask 可独立序列化。
- renderer data 为空时 helper 回退为 `Everything`。
- 自定义 mask 能正确传入 helper。

Editor 测试：

```text
NWRP.Editor.Tests
35 passed / 0 failed
```

编译验证：

```text
dotnet build NWRP.Runtime.csproj --no-restore
0 warnings / 0 errors
```

```text
dotnet build NWRP.Editor.Tests.csproj --no-restore
0 errors
3 existing Unity/NuGet reference warnings
```

## 当前限制与后续方向

- Renderer Data layer mask 不会减少 camera culling mask 覆盖范围内的 culling 输入；如果需要降低 culling 成本，应从 camera 或更前置的可见性系统处理。
- 阴影投射 layer 仍保持独立策略，后续如需 renderer-local shadow filtering，应作为明确 shadow setting 扩展，避免与主可见性隐式绑定。
- GPU-driven vegetation / indirect draw 若需要类似 layer filtering，应在 chunk / cluster / visibility buffer 层设计 GPU 过滤，而不是回退到 CPU 实例循环。
