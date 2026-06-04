# Phase46 Depth Texture ForcePrepass UV Contract

日期：`2026-06-04`

## 概要

本阶段修正并记录 NWRP `ForcePrepass` 模式下 `_CameraDepthTexture` 的 UV 采样约定。

此前 `AfterOpaques` / `AfterTransparents` 走 `CopyDepthPass`，会在 copy 阶段通过 `_BlitScaleBias` 把 source depth attachment 写成对 shader 友好的 `_CameraDepthTexture` 方向；而 `ForcePrepass` 直接把 `DepthOnly` pass 渲染到 `_CameraDepthTexture` depth target，没有经过 copy 阶段，因此在 D3D11 这类 `graphicsUVStartsAtTop` 平台的 Game Camera 下，调试预览会出现上下颠倒。

本阶段不改变 `ForcePrepass` 的资源路径，也不新增 `CopyDepthPass`。修复点是让 renderer 在设置 `_CameraDepthTextureScaleBias` 时知道当前 `_CameraDepthTexture` 是否由 prepass 直接写入：copy 路径保持现有采样约定，prepass 路径在需要时由采样侧补 Y flip。这样 shader 侧继续使用统一的 `SampleSceneDepth(uv)`，不需要根据 depth texture mode 手动判断是否翻转。

## 修改文件

- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`

## 问题背景

NWRP 当前 Depth Texture 支持三种模式：

- `AfterOpaques`：不透明物体后、透明物体前执行 `CopyDepth`，适合透明水体、软粒子、折射等透明材质读取 scene depth。
- `AfterTransparents`：透明物体后执行 `CopyDepth`，适合后处理、调试 overlay 或帧末 pass 读取 depth，不适合透明材质自身采样。
- `ForcePrepass`：在 `NWRPPassEvent.DepthPrepass` 额外绘制 opaque renderer 的 `DepthOnly` pass，直接生成 `_CameraDepthTexture`。

Frame Debugger 中容易产生两个误解：

1. `Depth Prepass` 和 `Draw Opaque Objects` 看起来绘制了同一批物体。
2. `ForcePrepass` 没有像 URP 某些路径那样出现后续 `Copy Depth Pass`。

这两点在 NWRP 当前实现中都是预期行为。`DepthPrepass` 与 opaque pass 使用相同的 opaque render queue 和 layer mask，只是 shader tag 不同：

```text
DepthPrepass       -> LightMode = DepthOnly
Draw Opaque Objects -> NewWorldUnlit / SRPDefaultUnlit / NewWorldForward
```

`DepthOnly` pass 通常 `ZWrite On`、`ColorMask 0`，只写 depth target，不写 color。因此 Frame Debugger 的颜色预览不一定会显示成 URP copy 后的灰度深度图；真正有效的数据在 depth attachment 内。NWRP `ForcePrepass` 直接把这个 depth attachment 作为 `_CameraDepthTexture` 绑定，所以后面即使没有 `CopyDepthPass`，`_CameraDepthTexture` 仍然有内容。

## 关键实现

### 1. 标记 `_CameraDepthTexture` 来源

`NWRPFrameTargets` 新增：

```csharp
public bool cameraDepthTextureWrittenByPrepass;
```

当 frame target requirements 表示本帧需要 depth texture prepass 时，renderer 在分配 `_CameraDepthTexture` 后记录：

```csharp
frameData.targets.cameraDepthTextureWrittenByPrepass =
    requirements.requiresDepthTexturePrepass;
```

该标记只描述当前 `_CameraDepthTexture` 的生成方式，不改变 depth target / color target 分配策略。

### 2. 统一 `_CameraDepthTextureScaleBias`

`SetCameraScreenGlobals` 设置 `_CameraDepthTextureScaleBias` 时，传入 `cameraDepthTextureWrittenByPrepass`。

copy 路径：

- `CopyDepthPass` 已经在写入 `_CameraDepthTexture` 时处理 source/destination Y 方向。
- shader 采样侧继续使用 identity scale/bias。

prepass 路径：

- `DepthPrepass` 直接写 `_CameraDepthTexture`。
- 在 top-origin 平台的普通 Game Camera + backbuffer 输出路径下，采样侧使用 `(1, -1, 0, 1)` 补齐 Y flip。

当前逻辑：

```text
!graphicsUVStartsAtTop 或 camera == null -> (1, 1, 0, 0)
ForcePrepass + Game Camera + targetTexture == null -> (1, -1, 0, 1)
SceneView / Preview -> (1, -1, 0, 1)
其他路径 -> (1, 1, 0, 0)
```

这样对外 shader 接口仍然稳定：

```hlsl
float rawDepth = SampleSceneDepth(uv);
```

材质不需要知道当前 depth texture 来自 `CopyDepthPass` 还是 `DepthPrepass`。

## 与 URP 路径的差异

URP 中常见的 Frame Debugger 顺序可能是：

```text
Depth Prepass
Copy Depth
Draw Opaque Objects
```

这通常意味着 URP 先把深度写入某个 depth attachment，再 copy/resolve 成可采样的 `_CameraDepthTexture`。copy 后的目标往往更容易在 Frame Debugger 中以灰度图形式预览。

NWRP 当前 `ForcePrepass` 是更直接的路径：

```text
Depth Prepass -> 直接写 _CameraDepthTexture depth target
Draw Opaque Objects
```

优点：

- 少一次 full-screen depth copy。
- 少一次中间 RT 读写。
- 对移动端 tile-based GPU 更友好。

代价：

- opaque renderer 会额外执行一次 depth-only draw。
- Frame Debugger 的颜色预览不如 copy 到 R32 texture 直观。
- 所有需要进入 prepass 的材质必须提供 `DepthOnly` pass。

## 性能与移动端策略

`ForcePrepass` 不是 NWRP 移动端默认推荐路径。

默认建议仍然是：

- 透明材质需要 scene depth：使用 `AfterOpaques`。
- 只在后处理或调试阶段读取 depth：可使用 `AfterTransparents`。
- depth copy 在目标平台不可用或不可靠，或确实需要更早的 depth texture：才考虑 `ForcePrepass`。

移动端取舍：

- `AfterOpaques` 成本是一次 full-screen depth copy 和对应带宽。
- `ForcePrepass` 成本是 opaque depth-only 重绘，draw call / vertex cost 会增加。
- 在 tile-based GPU 上，避免额外全屏读写通常有价值，但如果场景 opaque 几何复杂，prepass 也可能更贵。

因此该模式应以目标机 GPU profiling 为准，不应仅凭 Frame Debugger draw 数量判断优劣。

## 验证记录

- Unity `AssetDatabase.Refresh()` 成功，C# 编译刷新通过。
- Runtime 探针验证当前 D3D11 / `graphicsUVStartsAtTop=True` 环境下：
  - copy 路径 `_CameraDepthTextureScaleBias = (1, 1, 0, 0)`。
  - ForcePrepass 路径 `_CameraDepthTextureScaleBias = (1, -1, 0, 1)`。
- `git diff --check` 通过，仅有工作区 CRLF 提示。
- 未修改 shader keyword，未新增 RenderPass，未改变 `AfterOpaques` / `AfterTransparents` 语义。

## 当前限制与后续方向

- 本阶段只统一 `_CameraDepthTexture` 的采样方向，不把 `ForcePrepass` 改造成 URP 风格的 prepass + copy depth 双阶段路径。
- Frame Debugger 中 `Depth Prepass` 不显示灰度深度图是当前设计的可接受现象；如需可视化，应通过 debug shader、RenderDoc depth attachment inspection 或后续专用 DebugOverlay pass 观察。
- `ForcePrepass` 依赖材质存在 `DepthOnly` pass。没有 `DepthOnly` pass 的 opaque shader 会参与 forward opaque，但不会写入 prepass 生成的 `_CameraDepthTexture`。
- 若后续引入 camera stacking、XR 或更完整的 RTHandle-aware projection setup，需要重新核对 `cameraDepthTextureWrittenByPrepass` 与 `_CameraDepthTextureScaleBias` 的方向规则。
