# Phase6 主光阴影 Hard-Only 稳定化

Date: `2026-04-14`

## 概要

这个阶段有意将主方向光阴影路径回退到一个最小化的硬阴影实现。

此前的软阴影迭代叠加了多组互相影响的变量：

- Reversed-Z 平台上的阴影比较方向问题
- 软过滤对图集 texel 的解释不正确
- Shadow Caster 缺少 normal bias 应用
- 在 caster 路径尚未正确前就加入了 receiver 侧补偿

那一套实现已经不再适合作为可靠基线。本阶段的目标是恢复一个更简单、更容易通过 Frame Debugger 验证、也更适合作为兜底分支保留的版本。

## 本阶段改动

- 从 `NewWorldRenderPipelineAsset` 中移除了软阴影运行时设置
  - 移除 `MainLightShadowFilterMode`
  - 移除 `mainLightShadowSoftRadius`
  - 移除 `mainLightShadowReceiverBias`
- 移除了 soft-shadow receiver globals
  - 移除 `_MainLightShadowFilterParams`
- 移除了软阴影相关 shader helper 代码
  - 移除 tent/PCF helper include
  - 移除 receiver 侧 soft filter 逻辑
  - receiver 采样回到单次硬件比较采样
- 保留并修正了 caster 侧 bias 应用
  - runtime 现在会上传 `_ShadowBias`
  - `mainLightShadowBias` 继续作为面向用户的 URP 风格 depth bias 控制，`mainLightShadowNormalBias` 继续作为 normal bias 控制
  - 共享 `ShadowCaster` pass 现在会在 vertex 阶段基于位置、法线和主光方向应用 bias
  - shadow caster 路径现在使用独立的 `_ShadowLightDirection`，不再隐式复用 `_MainLightPosition`
  - `_ShadowBias` 会根据每个 cascade 的 shadow texel size 缩放，而不是直接把 asset 数值当世界单位使用
  - 阴影图渲染恢复固定 raster depth bias 基线 `SetGlobalDepthBias(1.0, 2.5)`，用于降低大面积平面 acne
  - 阴影关闭或无 cascade 的路径会在返回前显式重置 raster bias 和 shadow globals
  - Frame Debugger / profiling label 统一命名为 `Main Light Shadows`，为未来额外灯光阴影预留空间
- 移除了 pipeline asset 对 `maxAdditionalLights` 的暴露
  - runtime 只按照 renderer 侧常量上限裁剪 additional lights

## 默认稳定化参数

当前 `NewWorldRP.asset` 中的备用基线配置为：

```yaml
mainLightShadowResolution: 2048
mainLightShadowDistance: 40
mainLightShadowCascadeCount: 2
mainLightShadowCascadeSplit: 0.2
mainLightShadowBias: 0.35
mainLightShadowNormalBias: 1.2
```

这些默认值是刻意偏保守的，优先服务于正确性调试。

## 验证目标

这个阶段不是质量提升阶段，而是一次正确性重置。

预期验证内容：

- 确认 `ShadowMap` 阶段仍然只有一个主光阴影 pass
- 确认 `StandardLit` 使用的是 `ShadowCaster` pass
- 确认硬阴影不再依赖 receiver 侧 soft-shadow 补偿
- 在关闭 soft filter 的前提下，对比投影和非投影物体表现

## 重新引入软阴影前的前置门槛

在以下条件全部确认之前，不要重新引入软阴影：

- 当前目标平台上的硬阴影比较是正确的
- shadow caster bias 在不引入大面积全屏 acne 的情况下工作正常
- cascade 选择与 atlas addressing 稳定
- 硬阴影基线在样例场景中视觉上可接受

只有在这些条件都成立后，才应该重新引入 soft filter，而且必须从这个 hard-shadow baseline 出发，而不是继续在一个已经失稳的 receiver 路径上叠补丁。
