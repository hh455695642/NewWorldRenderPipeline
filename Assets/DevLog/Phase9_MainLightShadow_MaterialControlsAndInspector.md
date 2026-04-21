# Phase9 主光阴影材质控制与 Inspector 整理

Date: `2026-04-21`

## 概要

这个阶段围绕主光阴影路径完成了两项关联性很强的整理工作：

- 将材质侧实时阴影接收控制统一为 `_ReceiveShadows`
- 重整 `NewWorldRenderPipelineAsset` 的主光阴影设置结构，使 cached shadow 控件更易用、也更适合后续扩展

实现过程中继续遵守当前移动端优先约束：

- 不增加新的 shadow keyword
- 不为 shadow toggle 引入 receiver 侧 feature variants
- 不改变“默认只支持主光阴影”的功能范围
- cached shadow 与 realtime shadow 继续共享同一条主光 receiver 路径

这个检查点还补充了当前共享 `StandardLit` caster 路径上的材质级 realtime shadow caster 开关，并修复了接入过程中暴露出的两个编辑器问题：

- 旧 asset 迁移过程中可能在序列化阶段调用 `AssetDatabase.GetAssetPath()`
- `StandardLit` 的 shader GUI 里，`Base Color` 可能与 `Base Map` 的 tiling 控件重叠

## 对外控制项

### 材质控制项

Receiver 侧控制：

- `_ReceiveShadows`

语义：

- `float(0/1)`
- 默认值 `1`
- 表示“该材质是否接收实时阴影”
- 当前实现先作用于主光 realtime shadow 接收
- cached main light shadow 接收也会同时遵守这个值，因为 cached/static overlay 采样复用了同一个主光 receiver 入口

Caster 侧控制：

- `_CastShadows`

语义：

- `float(0/1)`
- 默认值 `1`
- 表示“该材质是否写入当前主光 realtime shadow caster 路径”
- 不影响 shadow receiving
- 当前先实现于 `NewWorld/Lit/StandardLit`，因为这是仓库里已经具备 `ShadowCaster` 与 `DepthOnly` pass 的共享 lit shader

需要明确的范围说明：

- `_ReceiveShadows` 被定义为未来的全局 realtime shadow receive 开关
- Additional Light Shadows 不在本阶段实现范围内
- 未来如果接入额外实时阴影路径，应继续复用 `_ReceiveShadows`，而不是再引入一个平行材质属性

### Pipeline Asset 控制项

custom inspector 中的 `Main Light` 设置现在分组为：

- `Toggle`
- `Distance / Cascade`
- `Atlas / Resolution`
- `Bias`
- `Cached Shadow`

`Cached Shadow` 分组现在包含：

- `Enable Cached Shadow`
- `Enable Dynamic Shadow`
- `Static Caster Layer Mask`
- `Dynamic Caster Layer Mask`
- `Camera Position Invalidation Threshold`
- `Camera Rotation Invalidation Threshold`
- `Light Direction Invalidation Threshold`

条件显示行为：

- 当 `Enable Main Light Shadow = false` 时，其余内容会在现有 early-out help box 后停止绘制
- 当 `Enable Cached Shadow = false` 时，cached-only 控件保持隐藏
- 当 `Enable Dynamic Shadow = false` 时，`Dynamic Caster Layer Mask` 保持隐藏

这一版实现用明确的 cached-shadow 语义替换了之前含义模糊的 `Debug` 分组。

## 运行时与 Shader 改动

### Receiver 路径

共享 receiver 采样仍然位于：

- `Assets/NWRP/ShaderLibrary/Shadows.hlsl`

Phase9 在主光阴影采样前加入了材质级 early-out：

- 当 `_ReceiveShadows <= 0.5` 时直接返回 `1.0`
- 跳过 realtime shadow atlas compare
- 跳过 cached static atlas 采样
- 跳过 cached dynamic overlay 采样

这是一个带宽优先的移动端取舍：

- 不增加 keyword 分支
- 不增加 variant 组合
- 不增加额外 pass
- 只在现有 receiver 路径里增加一个 uniform 控制的早退

`LightingModels/StandardPBR.hlsl` 也在本阶段完成了对齐，主光路径显式改为：

- `GetMainLight(positionWS, normalWS)`

这样 `StandardPBR` 和 `StandardLit` 都会走同一套共享主光接阴影求值路径。

### Caster 路径

共享 shadow caster 代码现在位于：

- `Assets/NWRP/ShaderLibrary/Passes/ShadowCasterPass.hlsl`

Phase9 为材质级 caster 增加了一个保护：

- `clip(_CastShadows - 0.5)`

当前行为是：

- 物体仍然可能被提交到 shadow caster pass
- 但材质可以阻止它真正写入 shadow map
- 这已经足够实现“按材质控制阴影投射”，且不需要引入 keyword，也不需要重构 feature 布局

当前限制：

- 这并不等价于把整个 shadow caster draw 从 CPU 调度层完全移除
- 如果未来内容对 CPU 侧节省有更强要求，应把 caster 过滤前移到 pass filtering 或 renderer 级 batching 数据阶段

## Asset 结构与迁移

`NewWorldRenderPipelineAsset` 的主光阴影设置已经重组为嵌套块：

- `toggles`
- `distance`
- `atlas`
- `bias`
- `cached`

运行时仍然保留了 `MainLightShadowSettings` 里的兼容桥接字段，因此旧序列化资产不会立即硬崩。

本阶段的迁移行为：

1. 旧版平铺 `mainLightShadows` YAML 会被解析进新的嵌套结构
2. 运行时从新结构读取，作为唯一主数据源
3. 兼容字段仍保持同步，保证旧数据在过渡期内可继续存活
4. 一旦保存，asset 会按新的分组布局写回磁盘

本阶段还完成了一项关键安全修复：

- 旧 YAML 加载不再从 serialization callback 路径触发
- `OnAfterDeserialize()` 现在只做 memory-only initialization
- asset-file migration 只会从安全的 editor/runtime 访问路径触发

这个修复避免了编辑器异常：

- `GetAssetPath is not allowed to be called during serialization`

## Editor / Inspector 修复

### Pipeline Asset Inspector

主光阴影 Inspector 的绘制逻辑被抽成了可复用的 foldout section helper，后续如果扩展到其他光源类型，可以继续复用这一模式。

当前 section 行为：

- `Main Light` 以 foldout 形式展示
- cached 相关控件以 cached-shadow 语义分组，而不再归类到 `Debug`
- 信息提示框现在也跟 cached-shadow 状态绑定，而不是始终表现为泛化的 shadow debug 信息

### Shader GUI

`NewWorldShaderGUI` 现在会暴露：

- `Receive Realtime Shadows`
- `Cast Realtime Shadows`

对 `StandardLit` 来说，`Surface` 区还修复了此前的布局问题：

- `Base Color` 改为独立一行
- `Base Map` 单独一行绘制
- `Tiling / Offset` 不再与颜色控件重叠

同时，在材质 GUI 中修改 `_CastShadows` 时，会主动把 cached main light shadow atlas 标记为 dirty，保证 cached 内容能在材质 caster 状态变化后更新。

## Variant 与移动端成本说明

Phase9 有意不增加任何新的 shadow keyword。

Keyword 影响：

- `_ReceiveShadows` 只用 uniform 控制
- `_CastShadows` 只用 uniform 控制
- 不增加 `shader_feature`
- 不增加 `multi_compile`

本阶段的 variant 风险：

- receiver 侧 variant 增长：`none`
- caster 侧 variant 增长：`none`

移动端成本取舍：

- `_ReceiveShadows = 0` 时，可以同时省掉 realtime 和 cached main-light receiver 路径中的 shadow compare 开销
- `_CastShadows = 0` 时，可以阻止该材质写入 shadow map，但当前还不会移除 caster draw submission 本身

这个取舍在当前检查点是可接受的，因为：

- 实现简单
- 不会引入 variant 爆炸
- 不会破坏现有 feature/pass 合约

## 验证记录

本阶段完成的验证：

- Unity shader 编译在 Phase9 改动下保持 `0` error
- 旧版 `Assets/Settings/NewWorldRP.asset` 的值已能通过新分组运行时路径正确读取
- 保存后，`NewWorldRP.asset` 会以新的分组布局序列化
- `_ReceiveShadows` 与 `_CastShadows` 已在 custom material GUI 中暴露
- `StandardLit` 的 `Base Color` 不再与 `Base Map` 的 tiling 控件占同一行

项目中仍存在但不属于本阶段引入的问题：

- 现有主光阴影 runtime 代码里 `ShadowDrawingSettings(CullingResults, int)` 的过时 API warning
- 与 NWRP 阴影逻辑无关的 Unity MCP / NuGet analyzer warning

这些 warning 不是本阶段带入的。

## 当前限制

这个阶段的范围仍然是刻意收窄的：

- `_ReceiveShadows` 当前只接入了主光阴影 receiver 路径
- Additional Light Shadows 仍是后续工作
- `_CastShadows` 目前只接在共享 `StandardLit` shadow caster 路径上
- caster suppression 现在是材质级写入抑制，不是 renderer 级 draw rejection

## 后续候选项

本阶段之后合理的后续方向包括：

- 将 `_CastShadows` 扩展到其它具备共享 `ShadowCaster` 支持的 NWRP lit shaders
- 在未来 Additional Light Shadows 中直接复用 `_ReceiveShadows`
- 如果确实需要更强的 CPU 侧节省，把 caster suppression 前移到 fragment `clip()` 之前
- 如果后续增加 additional-light shadow 面板，可以继续抽取 cached-shadow inspector subsection helper
