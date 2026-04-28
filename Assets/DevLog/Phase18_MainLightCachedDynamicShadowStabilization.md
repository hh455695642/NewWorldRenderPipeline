# Phase18 主光 Cached Dynamic Shadow 稳定化

日期：`2026-04-28`

## 概要

本阶段修复 `MaterialSampleScene` 中 `Character` 使用主光 cached shadow + dynamic overlay 时，Game Camera 移动导致角色投影快速闪烁的问题。

`Character` 位于 layer 18，`NewWorldRP.asset` 中 `dynamicCasterLayerMask = 1 << 18`，问题集中在主光 cached static + dynamic overlay 路径。最终确认闪烁不是角色 ShadowCaster 本身不稳定，而是 cached static atlas 会随 Game Camera 位移/旋转频繁失效，导致 cached cascade 矩阵和动态 overlay 的接收端采样空间不断跳变。相机移动越快，cache rebuild 越频繁，所以闪烁频率也越高。

## 修改文件

- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowCacheState.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDynamicOverlayPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDisabledPass.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/ShaderLibrary/Shadows.hlsl`
- `Assets/Settings/NewWorldRP.asset`

## 关键修复

### 合并动态阴影到主光 atlas

- 移除接收端独立 `_MainLightDynamicShadowmapTexture` 和 `_MainLightDynamicShadowParams`。
- dynamic overlay 不再让材质侧对 static atlas 和 dynamic atlas 做双重采样。
- 每帧先将 cached static atlas 复制到 combined atlas，再把 dynamic caster layer 渲染进同一张 combined atlas。
- receiver 仍只采样 `_MainLightShadowmapTexture`，cached static 和 dynamic overlay 共用同一组 cascade matrix / split sphere。

这样可以避免 static / dynamic 两套 receiver 采样空间在相机运动时产生不同步，也把 Medium PCF 下原本可能出现的两次 9-tap 比较采样降回一次。

### 关闭相机运动触发的 cache 失效

- 新增 `enableCameraMotionInvalidation` 配置，默认关闭。
- `NewWorldRP.asset` 已显式设置 `enableCameraMotionInvalidation: 0`。
- `NeedsStaticCacheRebuild(...)` 只有在该开关启用时才使用 `cameraPositionInvalidationThreshold` / `cameraRotationInvalidationThreshold`。
- 主光方向、阴影距离、分辨率、cascade、bias、caster cull mode、layer mask 和显式 dirty 仍会触发 static cache rebuild。

这保留了手动/设置驱动的 cache 更新能力，同时避免 Game Camera 小幅移动造成 cached shadow 矩阵连续重建。

### Inspector 与兼容迁移

- Inspector 增加 `Camera Motion Invalidates Cache`。
- 只有启用该选项时才显示 camera position / rotation invalidation threshold。
- Inspector 增加 warning，提示启用该选项可能让 cached dynamic shadow 在相机移动时出现跳变。
- 保留旧序列化字段同步，旧 asset 迁移到 structured settings 时不会丢失新开关。

## 性能与移动端策略

- 不新增 RenderPass，仍使用 `NWRPPassEvent.ShadowMap`。
- 不新增 shader keyword，variant 风险为 0。
- receiver 侧只保留一次主光 shadow compare，dynamic overlay 不再额外采样第二张 shadowmap。
- dynamic overlay 开启时额外成本为：
  - 1 次 depth atlas `CopyTexture`
  - 1 次 dynamic caster layer 的 shadow draw
  - 1 张 combined depth RT
- dynamic overlay 关闭时会释放 combined shadowmap，避免长期占用移动端显存。
- combined atlas 绑定时显式使用 `RenderBufferLoadAction.Load / Store`，保证保留复制后的 static depth 内容，减少 tile GPU 行为歧义。
- 如果运行平台不支持基础 `CopyTexture`，dynamic overlay 会退回 static cached shadow，不走高成本 fallback blit。

## 普通实时阴影路径

关闭 cached shadow 时，普通 realtime main-light shadow 仍走 `MainLightShadowCasterPass` 独立路径。

本阶段没有给 cache-off 的 realtime pass 增加新的纹理、pass、keyword 或 public setting；receiver 全局只做了删除 dynamic overlay 遗留参数的签名收敛。因此 cache-off 的普通实时阴影行为不应受此次 bugfix 影响。

## 验证记录

- 当前场景现象：Game Camera 移动时 `Character` 投影闪烁已基本消失。
- C# 编译检查：
  - `dotnet build NWRP.Runtime.csproj /nologo -v:minimal` 通过。
  - `dotnet build NWRP.Editor.csproj /nologo -v:minimal` 通过，仍有 NuGet/MCP 引用版本 warning，非本阶段新增错误。
- 静态检查：
  - `Shadows.hlsl` 不再声明 `_MainLightDynamicShadowmapTexture` / `_MainLightDynamicShadowParams`。
  - `NWRPShaderIds` 不再保留 dynamic shadow receiver ID。
  - receiver 侧没有 `SampleMainLightDynamicShadow...` 遗留调用。

Unity batchmode 曾因当前已有 Unity Editor 打开同一项目而无法启动独立验证：`Multiple Unity instances cannot open the same project`。最终 shader import / Console 状态应以当前 Editor 刷新后的结果为准。

## 当前限制与后续方向

- `enableCameraMotionInvalidation` 关闭后，cached static shadow region 不会跟随 Game Camera 自动漂移。大范围移动、切场景或静态阴影区域需要刷新时，应调用 `MarkMainLightShadowCacheDirty()` 或 `ClearMainLightShadowCache()`。
- 如果未来需要相机长距离连续移动且 static cache 仍必须自动覆盖新区域，应改为显式的 shadow cache anchor / 分块 cache / 低频安全刷新策略，而不是恢复逐相机阈值失效。
- dynamic overlay 当前仍依赖 Unity `DrawShadows` 可见 renderer 集合；大规模 GPU-driven caster 需要后续独立 shadow pass 与 GPU visibility 数据。
