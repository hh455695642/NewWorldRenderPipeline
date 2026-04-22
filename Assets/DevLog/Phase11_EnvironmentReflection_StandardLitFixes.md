# Phase11 环境反射与 StandardLit 修复汇总

Date: `2026-04-22`

## 概要

这个阶段最终确认：`SK_Drone` 的异常不是 `indirectSpecular` 公式本身写错，也不是运行时环境反射刷新链路缺失，真正的回归点在 `NewWorld/Lit/StandardLit` 自身的 shader 布局。

现象表现为：

- `SK_Drone` 与 `SK_Drone_old` 使用相同贴图和几乎相同的材质参数
- `SK_Drone_old` 使用从“主光源软阴影”版本拷贝出的 `NewWorld/Lit/StandardLit_old`，环境反射正常
- `SK_Drone` 使用当前 `NewWorld/Lit/StandardLit`，看起来像 `indirectSpecular` 没有正确工作

## 问题定位

### 1. 新旧 `StandardLit` 的间接镜面公式一致

对比：

- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_StandardLit.shader`
- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_StandardLit_old.shader`

确认以下逻辑没有分叉：

- `MaskMap` 读取
- `metallic / smoothness / ao` 计算
- `ComputeF0`
- `SampleSH`
- `SampleEnvironmentReflection`
- `indirectSpecular = SampleEnvironmentReflection(...) * envBRDF * ao`

因此根因不是 `indirectSpecular` 这条公式被改坏。

### 2. 真正问题在 `UnityPerMaterial` 的 pass 间布局不一致

当前 `StandardLit` 相比 `StandardLit_old` 新增了：

- `_ReceiveShadows`
- `_CastShadows`

但修复前这两个字段没有在所有 pass 中保持完全一致的 `UnityPerMaterial` 布局：

- `Forward` pass 使用了一套材质 CBUFFER
- `ShadowCaster` pass 只声明了 `_CastShadows`
- `DepthOnly` pass 没有复用同一份完整布局

这会破坏 SRP Batcher 对材质常量布局一致性的假设，导致部分材质参数在当前 shader 中存在读偏风险。  
`SK_Drone` 恰好又强依赖：

- `_Metallic`
- `_Smoothness`
- `_OcclusionStrength`

这些值一旦读错，最终就会表现为：

- `f0` 错
- `envBRDF` 错
- `indirectSpecular` 看起来像失效

而 `StandardLit_old` 没有引入这组新的 pass 间布局分叉，所以表现正常。

## 最终修复

修改文件：

- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_StandardLit.shader`

修复方式：

- 保留当前 `StandardLit` 的材质功能字段
- 让 `Forward`、`ShadowCaster`、`DepthOnly` 三个 pass 使用完全一致的 `UnityPerMaterial` 字段顺序与布局
- 不额外引入新的运行时同步逻辑
- 不保留额外的辅助 `.hlsl` 输入文件，直接在当前 shader 内内联同一份 CBUFFER

统一后的 `UnityPerMaterial` 包含：

- `_BaseColor`
- `_BaseMap_ST`
- `_Metallic`
- `_Smoothness`
- `_OcclusionStrength`
- `_NormalStrength`
- `_EmissiveColor`
- `_ReceiveShadows`
- `_CastShadows`

## 未保留的中间方案

在定位过程中，曾短暂验证过两类非最终方案：

1. 运行时环境反射同步

- `EnvironmentReflectionSync.cs`
- `DynamicGI.UpdateEnvironment()` 驱动的自动刷新链路

2. 独立的 `StandardLitInput.hlsl`

- 用于把 `UnityPerMaterial` 抽到单独 include

在最终根因确认后，这两类方案都没有保留进最终版本：

- 运行时环境反射同步不是这次 `SK_Drone` / `SK_Drone_old` 分叉的根因
- `StandardLitInput.hlsl` 只是过渡性整理手段，最终已内联回 shader

## 最终结论

本阶段最终确认并修复的是一个 shader 自身问题：

- `NewWorld/Lit/StandardLit` 在新增 `_ReceiveShadows` / `_CastShadows` 后，没有维持所有 pass 的 `UnityPerMaterial` 完全一致

这才是导致 `SK_Drone` 没有正确表现环境反射、而 `SK_Drone_old` 正常的关键原因。

因此，本次最终结论不是：

- 环境球没有绑定
- `unity_SpecCube0` 名字不对
- `indirectSpecular` 公式被删掉

而是：

- 当前 `StandardLit` 的 pass 间材质常量布局不一致，导致 SRP Batcher 下材质参数读取不可靠

## 验证记录

本阶段完成后的确认项：

- 新旧 `StandardLit` 的 `indirectSpecular` 公式对比确认一致
- 当前 `StandardLit` 已统一 `Forward` / `ShadowCaster` / `DepthOnly` 的 `UnityPerMaterial` 布局
- 不再保留运行时环境反射同步方案
- 不再保留 `StandardLitInput.hlsl` 过渡文件
- Unity 刷新编译通过，Console 无新增 shader error

## 后续可选项

如果后续还要继续做回归验证，可继续保留以下对照方式：

- `SK_Drone` 使用当前 `StandardLit`
- `SK_Drone_old` 使用 `StandardLit_old`

这样可以继续作为环境反射与材质常量读取问题的对照基线。
