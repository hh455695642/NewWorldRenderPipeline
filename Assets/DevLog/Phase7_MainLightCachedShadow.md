# Phase7 主光 Cached Shadow

Date: `2026-04-15`

## 概要

这个阶段在 Phase6 的硬阴影基线之上，为 NWRP 增加了主方向光 cached shadow 路径。

目标不是复刻插件架构，而是在保持默认 realtime 路径不变的前提下，为 NWRP 增加一条移动端优先的 cached shadow 实现：

- static main light shadow atlas cache
- 可选的逐帧 dynamic shadow overlay
- 不引入 fullscreen combine pass
- 不增加新的 shader keyword

`Phase6_MainLightShadow_HardOnly_Stabilization.md` 仍然是硬阴影正确性基线。本阶段记录 cached shadow 的接入，以及为了让它在编辑器环境中稳定工作所做的后续修复。

## 对外控制项

pipeline asset 现在为主光阴影路径提供三层控制：

- `Enable Main Light Shadow`
- `Enable Cached Shadow`
- `Enable Dynamic Shadow`

cached shadow 相关控制还包括：

- `Static Caster Layer Mask`
- `Dynamic Caster Layer Mask`
- `Camera Position Invalidation Threshold`
- `Camera Rotation Invalidation Threshold`
- `Light Direction Invalidation Threshold`

运行时 API：

- `MarkMainLightShadowCacheDirty()`
- `ClearMainLightShadowCache()`

当前更新策略是 `OnDirty`，并不是插件里的 `Manual` 或 `EverySecond`。

## 运行时结构

主光 cached shadow 的运行时代码现在位于：

| 路径 | 职责 |
|------|------|
| `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs` | 选择 disabled / realtime / static cache / dynamic overlay 路径 |
| `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowCacheState.cs` | 维护长生命周期 atlas 状态、cascade 数据与 invalidation 签名 |
| `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs` | 共享 atlas 尺寸、剔除、cascade 构建与 shadow global 上传辅助逻辑 |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs` | 传统 realtime 全图集更新路径 |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs` | dirty 帧上的 static atlas 重建 |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDynamicOverlayPass.cs` | 逐帧 dynamic caster overlay atlas |
| `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDisabledPass.cs` | 显式上传 shadow-disabled globals |

receiver 侧采样仍然位于 `Assets/NWRP/ShaderLibrary/Shadows.hlsl`：

- `_MainLightShadowmapTexture` 用于 static 或 realtime atlas
- `_MainLightDynamicShadowmapTexture` 用于 dynamic overlay atlas
- 双采样结果通过 `min(static, dynamic)` 合并

当前实现中的行为规则：

- cached shadow 只对 `Game Camera` 生效
- `SceneView` 和 `Preview` camera 会回退到 realtime main light shadow
- 关闭 cached shadow 时，NWRP 继续走原有的 realtime 主光阴影 pass
- 开启 cached shadow 且关闭 dynamic shadow 时，只采样 static atlas

当前 pipeline asset 的 `FeatureCount = 0`，因此该系统通过 `NewWorldRenderPipelineAsset` 的 runtime fallback `MainLightShadowFeature` 运行，而不是依赖序列化的 feature asset。

## 本阶段修复的问题

这个阶段修掉了 cached shadow 初始接入中的三个具体问题：

1. Static cache 首帧可能构建出空 atlas

- 根因：atlas 渲染阶段读取了 cacheState 里的 `CascadeCount`，但此时 `CommitStaticCache()` 还没有把本帧值写进去
- 修复：渲染辅助函数改为显式接收本帧 `cascadeCount`，并且只有在至少一个 cascade 真正渲染成功后才提交 static cache

2. 编辑器多相机渲染会让 Game View cache 长时间保持 dirty

- 根因：`Game Camera`、`SceneView` 和 `Preview` 共享同一个 cached state，而 invalidation 又依赖 camera pose
- 修复：cached shadow 现在只对 `Game Camera` 启用；编辑器辅助相机会走 realtime fallback，不再覆盖共享 cache state

3. Dynamic overlay 可能因为依附无效 static cache 而消失

- 根因：dynamic overlay 依赖 `HasValidCache`，但此前初始化路径可能留下一个无效 static atlas
- 修复：`HasValidCache` 只会在 static atlas 真正渲染成功后置位；当 static cache 无效时，dynamic overlay 会干净地回退到空 shadowmap

## 当前限制

这个检查点的范围是刻意收窄的：

- 只支持一个主方向光
- 只支持 `1-2` 个 cascades
- 只支持 hard shadow
- cached path 只对 `Game Camera` 开启
- static caster 的 transform 改变不会自动刷新 cache
- static cache 重建仍然依赖 Game Camera pose，因为当前 cascade matrix 仍然是 camera-relative

预期结果：

- 移动 static layer 上的 caster 时，阴影不会立即更新
- cache 会在调用 `MarkMainLightShadowCacheDirty()` 后，或者主光 / Game Camera 超过 invalidation threshold 时重建

## 验证记录

当前检查点的验证结果：

- Unity Console：`0` error / `0` warning
- `Enable Cached Shadow = false`：realtime 主光阴影路径仍然正常工作
- `Enable Cached Shadow = true` + `Enable Dynamic Shadow = true`：dynamic layer caster 可以恢复逐帧阴影投射
- `SceneView` 不再污染共享的 `Game Camera` cached atlas
- shader globals 在 `NWRPShaderIds` 与 `Shadows.hlsl` 之间保持一致

## 后续候选项

不属于这次提交，但合理的后续方向包括：

- `Manual` cached update mode
- `ThrottledOnDirty` cached update mode

`EverySecond` 被有意排除在本阶段之外。
