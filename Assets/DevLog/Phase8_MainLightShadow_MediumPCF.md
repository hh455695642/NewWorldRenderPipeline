# Phase8 主光阴影 Medium PCF

Date: `2026-04-15`

## 概要

这个阶段不改变当前主光阴影的整体行为，只围绕 Medium PCF receiver 路径做低风险整理。

当前阴影系统仍然支持：

- `Hard`
- `MediumPCF`
- receiver 侧动态 depth / normal bias
- `shadowCoord.z` 越界保护
- 可选的主光 shadow caster cull override

这个检查点不会改变 tent kernel、sample 数量，也不会改变 static/dynamic shadow 的合并规则。它主要改善可维护性、Inspector 清晰度和 shader pass 一致性。

## 对外控制项

主光阴影控制项现在包括：

- `Main Light Shadow Filter Mode`
- `Main Light Shadow Filter Radius`
- `Main Light Shadow Receiver Depth Bias`
- `Main Light Shadow Receiver Normal Bias`
- `Shadow Caster Cull Mode`

控制项语义：

- `Main Light Shadow Filter Mode = Hard` 时使用单次比较采样
- `Main Light Shadow Filter Mode = MediumPCF` 时使用固定 `3x3` tent kernel
- `Main Light Shadow Filter Radius` 以 shadow texel 为单位
- `mainLightShadowFilterRadius = 1.0` 对应当前基线 footprint
- receiver bias 在 `Hard` 和 `MediumPCF` 下都会生效

本阶段的 Inspector 行为：

- 只有在选择 `MediumPCF` 时才显示 `Filter Radius`
- `Receiver Depth Bias` 和 `Receiver Normal Bias` 始终可见
- 当 `MediumPCF` 与 `Enable Dynamic Shadow` 同时开启时，Inspector 会显示一条移动端成本提醒，因为 receiver 可能要执行两次 `9-tap` compare filter

## 运行时说明

receiver 侧过滤仍然位于 `Shadows.hlsl`。

行为规则保持不变：

- static atlas 采样和 dynamic overlay 采样仍通过 `min(static, dynamic)` 合并
- `shadowCoord.z` 超出 `[0, 1]` 时返回 `1`，避免尾部无效阴影
- receiver 动态 bias 由 `1 - saturate(dot(normalWS, lightDirWS))` 计算
- `MainLightShadowCasterCullMode` 仍然只是兼容性 override，不是默认 acne 修复手段

本阶段的实现整理：

- static 与 dynamic 的 `MediumPCF` tent 路径现在共用一个 helper，不再维护两套重复的 `9-tap` 实现
- 兼容重载 `SampleMainLightShadow(float3 positionWS, float3 lightDirectionWS)` 现在直接转发到单参数路径，明确表示该重载不应用 receiver bias

## 本阶段修复的问题

1. Medium PCF tent 采样漂移风险下降

- 根因：static shadowmap 采样和 dynamic overlay 采样各自维护了一份重复的 `3x3` tent 逻辑
- 修复：两条路径现在都会调用同一个共享 tent helper，同时保持原有权重、半径语义和比较顺序不变

2. Inspector 不再在 `Hard` 模式下暴露误导性的 radius 控件

- 根因：`Filter Radius` 之前始终可见，但只有 `MediumPCF` 会真正读取它
- 修复：custom pipeline asset inspector 现在只在 `MediumPCF` 下显示 `Filter Radius`

3. 受影响的 crystal 环境 shader 的 instancing 支持得到对齐

- 根因：`MineralCrystal` 和 `ShardCrystal` 在多个 pass 中使用了 `UNITY_*INSTANCE*` 宏，但对应 pass 没有启用 instancing variants
- 修复：为相关 pass 补上 `#pragma multi_compile_instancing`，使 forward、shadow、depth-only 和 depth-normals 渲染都能正确参与 instancing

## 移动端成本说明

本阶段没有增加新的 render pass、render target 或 shadow keyword。

当前 receiver shadow filtering 成本仍然是：

- `Hard`：每个 atlas 一次比较采样，外加可选的 dynamic overlay compare
- `MediumPCF`：每个 atlas 九次比较采样，外加可选的 dynamic overlay tent compare

本阶段的 variant 影响被严格限制在很小范围内：

- 不新增 shadow filter keyword
- 不新增 runtime branching keyword
- 唯一有意增加的 variant 是 `MineralCrystal.shader` 和 `ShardCrystal.shader` 上的 `multi_compile_instancing`

这个取舍是可接受的，因为这些 pass 本身已经围绕 instancing 宏编写，这次改动是在恢复预期 batching 行为，而不是扩张阴影功能矩阵。

## 验证记录

这个检查点的验证目标：

- Unity Console 在导入后保持 `0` error / `0` warning
- `NWRPShaderIds`、runtime 上传代码和 `Shadows.hlsl` 的 global 名字保持一致
- `Hard` 输出维持当前效果
- `MediumPCF` 且 `mainLightShadowFilterRadius = 1.0` 时维持当前视觉结果
- `MediumPCF + Enable Dynamic Shadow` 时，asset inspector 能显示对应成本提示

仍需在编辑器中进行视觉场景验证的内容：

- `0.5 / 1.0 / 2.0` 三种半径下的柔化差异
- receiver acne 与 peter-panning 的平衡
- `MediumPCF` 下 dynamic overlay 的一致性

## 后续候选项

不属于本阶段、但合理的后续工作包括：

- 像当前 tent 路径一样，继续合并 hard-shadow 的 static/dynamic compare 样板代码
- 评估是否需要为 `MediumPCF + Dynamic Overlay` 提供更明确的移动端质量 / 成本标签
- 在 crystal 密集场景中做 profiling，确认启用 instancing 的 pass 能带来可测量的 batching 收益
