# Phase33 Shader Variant Cleanup / Global Fog

日期：`2026-05-18`

## 概要

本阶段围绕 NWRP 的 shader variant 债务做了一轮收口，重点是把雾效从 Unity 内置 `multi_compile_fog` 迁移到 NWRP 自己的全局 uniform 路径，并清理少量 NWRP 内部残留的非 instancing variant。

核心目标不是扩展视觉效果，而是降低移动端 shader 编译组合、包体膨胀和运行时 keyword 管理复杂度。雾效控制保持为全局配置，不做多相机覆盖；当前只让 `Assets/NWRP/Shaders/Environment` 与 `Assets/NWRP/Shaders/Lit` 里的 shader 支持雾效，其它 shader 目录先不纳入生产雾效范围。

本阶段没有新增业务 shader keyword。`multi_compile_instancing` 继续保留，符合 NWRP shader 规范和植被 / 大批量渲染需求。

## 修改范围

运行时与配置：
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/NWRPFogFeature.cs`
- `Assets/NWRP/Runtime/Passes/SetupFogPass.cs`

Shader 公共库：
- `Assets/NWRP/ShaderLibrary/Fog.hlsl`

雾效接入 shader：
- `Assets/NWRP/Shaders/Environment`
- `Assets/NWRP/Shaders/Environment/MobileFallback`
- `Assets/NWRP/Shaders/Lit`

残留 variant 清理 shader：
- `Assets/NWRP/Shaders/Base/NewWorld_Base_Fog.shader`
- `Assets/NWRP/Shaders/Base/NewWorld_Base_Fresnel.shader`
- `Assets/NWRP/Shaders/NPR/NewWorld_NPR_Outline_Shell.shader`

明确未处理范围：
- `Assets/Res`
- `Assets/NewWorld`
- `Assets/Shaders`
- `Assets/URPShaderCodeSample`

这些目录里仍存在较多旧 URP / 特效 shader variant，但不属于本阶段范围，避免把外部资源迁移和 NWRP 内部清理混在一起。

## 雾效控制入口

雾效参数最终放在 `NWRPFog : VolumeComponent` 中，由场景或局部 Volume Profile 控制：
- `enableFog`
- `mode`
- `color`
- `startDistance`
- `endDistance`
- `density`

`NewWorldRenderPipelineAsset.FeatureSettings` 不再承载 Fog 参数；没有 Fog Volume 或 `enableFog` 未启用时，雾效默认关闭。这样可以避免 pipeline asset 成为跨场景的隐式雾效默认源。

`NWRPFogMode` 当前定义为：
- `Off`
- `Linear`
- `Exp`
- `Exp2`

本阶段刻意不做相机私有 Fog 字段。Game Camera 复用 `NWRPCameraData` 的 Volume Layer Mask / Trigger 采样 Volume；Scene / 区域差异交给 Unity Volume 系统处理。

## Feature / Pass 结构

新增 `NWRPFogFeature`：
- 类型：`NWRPFeature`
- 持有一个 `SetupFogPass`
- 由 `NWRPRenderer` 按 pipeline asset 状态接入

新增 `SetupFogPass`：
- `passEvent = NWRPPassEvent.BeforeOpaque`
- 不创建 RenderTexture
- 不提交 draw call
- 不做 fullscreen blit
- 只负责上传全局 shader uniform

上传的全局参数：
- `_NWRPFogMode`
- `_NWRPFogParams`
- `_NWRPFogColor`

`_NWRPFogMode` 使用 float 上传，而不是 int。这样对移动端 shader 后端更保守，避免在所有接入雾效的 shader include 路径里引入不必要的整型 uniform 差异。

当雾效关闭或 asset 无效时，`SetupFogPass` 会显式上传 Off 状态和空参数，避免上一帧或其它渲染路径留下 stale global state。

## ShaderLibrary 雾效路径

`Fog.hlsl` 改为 NWRP uniform fog 实现：
- `ComputeNWRPFogFactorFromEyeDepth`
- `ComputeNWRPFogFactorFromPositionWS`
- `MixNWRPFog`
- `MixNWRPFogColor`

雾效 factor 基于 view-space eye depth 计算；业务 shader 通常从 world position 调用 `ComputeNWRPFogFactorFromPositionWS`，避免继续沿用 clip-space z 作为 fog 输入。

为了不让范围外旧 shader 立刻编译失败，`Fog.hlsl` 仍保留兼容包装：
- `ComputeFogFactor`
- `MixFog`
- `MixFogColor`

但生产接入的 `Environment` 与 `Lit` 已统一改用 NWRP 命名的新 helper。后续如果继续迁移其它目录，应优先使用新 helper，而不是继续扩大旧包装的使用面。

## Environment / Lit 雾效接入

`Assets/NWRP/Shaders/Environment` 与 `Assets/NWRP/Shaders/Lit` 的 forward pass 已移除 `#pragma multi_compile_fog`，改为 uniform 控制。

主要策略：
- vertex 阶段基于 world position 计算 fog factor
- fragment 阶段用 `MixNWRPFog` 混合最终颜色
- DepthOnly / ShadowCaster pass 不接入雾效
- debug override 输出不强制混雾，避免破坏调试视图的可读性

移动端取舍：
- CPU 侧只做一次全局参数上传，成本极低
- GPU 侧每个 fragment 保留一次最终颜色 lerp
- 不再产生 fog mode 维度的 shader variant
- 不引入新的贴图采样、RT 或 pass

这比继续使用 `multi_compile_fog` 更适合当前 NWRP：雾效模式属于全局运行时参数，不应该让每个材质 / shader family 都展开额外编译组合。

## NWRP 残留 Variant 清理

`NewWorld_Base_Fog.shader`：
- 移除 `#pragma multi_compile_fog`
- 改用 `ComputeNWRPFogFactorFromPositionWS`
- 改用 `MixNWRPFog`

`NewWorld_Base_Fresnel.shader`：
- 移除 `_REFLECTION_ON` 的 `multi_compile`
- 使用已有 `_Reflection` 材质 float 做 uniform 分支
- 保留材质开关语义，不改变 property 名称

`NewWorld_NPR_Outline_Shell.shader`：
- 移除 `_PIXELWIDTH_ON` 的 `shader_feature_local`
- 使用已有 `_PixelWidth` 材质 float 做 uniform 分支
- 保留常规观察空间扩边与像素等宽扩边两种模式

这些 shader 都不是高频复杂生产大 shader。把少量材质 toggle 改成 uniform 分支，比保留独立 variant 更符合本阶段的 variant 收口目标。

## 明确保留的 Variant

保留 `multi_compile_instancing`：
- NWRP shader 规范要求支持 GPU Instancing
- 环境、植被、Lit、NPR 等 shader 需要继续兼容实例化路径

保留 `Utils/CoreBlit*` 与 `CopyDepth` 的小规模管线变体：
- blit slice / texture array
- HDR decode
- depth MSAA
- output depth

这些属于管线内部工具 shader 的结构性分支，数量可控，且比 runtime uniform 分支更符合实际调用路径。

保留 `Environment/TreeLeaf` 的二色叠加 `shader_feature_local`：
- 当前只有 2 路局部分支
- world noise 路径片元成本相对更高
- 作为生产植被 shader，保留 keyword 可以避免所有材质都承担高成本分支

因此本阶段清理的是无引用、低风险或纯材质开关类 variant，不强行把所有分支都改成 runtime branch。

## Variant 风险结论

本阶段清理后：
- NWRP 内部不再有 `multi_compile_fog`
- `Environment` / `Lit` 的雾效不再增加 variant
- `Base/Fresnel` 不再为反射开关生成 `_REFLECTION_ON` 变体
- `NPR/Outline` 不再为 pixel width 模式生成 `_PIXELWIDTH_ON` 变体

仍然存在但有意保留的非 instancing shader feature：
- `TreeLeaf` 的 `_SECONDCOLOROVERLAYTYPE_WORLD_NOISE_3D / _SECONDCOLOROVERLAYTYPE_UV_GRADIENT`

如果后续要继续压包体，收益更大的方向不在 NWRP 当前残留，而是在外部特效 shader 目录中高引用的 `multi_compile_fog` 和旧 URP 光照 / 阴影 keyword。但那属于单独的特效 shader 迁移任务，需要结合美术材质引用和效果验收，不应混入本阶段。

## 性能与移动端取舍

CPU：
- 雾效只上传少量全局 uniform
- 不增加 per-camera fog 状态
- 不增加材质 keyword 切换
- 不增加 renderer list 或 culling 逻辑

GPU：
- 雾效只增加常规深度 factor 计算和颜色 lerp
- 不增加贴图采样
- 不增加全屏 pass
- 不增加中间 RT

包体 / 编译：
- 去掉 fog 维度的 variant 组合
- 去掉两个低价值材质开关 variant
- 保持 instancing 和必要工具 shader variant

移动端 baseline 的优先级是控制 variant 与带宽，不追求把每一个材质开关都变成编译期分支。对于低频简单分支，uniform 更利于控制复杂度；对于 TreeLeaf 这类高频植被片元且存在较贵路径的功能，保留小规模局部 keyword 更合理。

## 验证

已完成静态检查：
- `Assets/NWRP/Shaders/Environment` 与 `Assets/NWRP/Shaders/Lit` 中无 `multi_compile_fog`
- `Assets/NWRP/Shaders/Environment` 与 `Assets/NWRP/Shaders/Lit` 中无旧 `ComputeFogFactor` / `MixFog` 调用
- `Assets/NWRP/Shaders` 中无 `multi_compile_fog`
- NWRP 非 instancing `shader_feature` 仅剩计划保留的 `TreeLeaf` 二色叠加
- `git diff --check` 通过，仅有工作区行尾提示

已完成 Unity 验证：
- `AssetDatabase.Refresh` 通过
- Unity Editor 当前不在 compiling / updating 状态
- Console 最近错误与异常为空
- `ShaderUtil.GetShaderMessages` 扫描 `Environment` / `Lit` 目标 shader 无 compiler messages
- `ShaderUtil.GetShaderMessages` 扫描本阶段残留清理的 3 个目标 shader 无 compiler messages

备注：
- 全 NWRP shader 扫描时，`PostProcess/NWRP_Tonemapping.shader` 返回过两个空白 ShaderUtil 条目，severity 与 message 均为空，非 error / warning，且与本阶段修改文件无关。

## 当前边界与后续建议

- 本阶段只完成 NWRP 内部 variant 收口，不处理外部资源 shader。
- 雾效当前只正式覆盖 `Environment` 与 `Lit`；其它目录如果要支持雾效，应按 shader family 单独评估。
- 如果后续要处理特效 shader，应优先从高引用材质的 `Assets/Res/Effects/Shader` 与 `Assets/NewWorld/ArtResources/Effects/Scripts` 入手。
- 特效 shader 清理不应简单全局替换 `multi_compile_fog`，需要先确认透明混合、软粒子、flipbook、distortion、UI 特效等路径是否真的需要场景雾。
- 如果需要更严格的包体控制，可以后续增加 build-time shader variant 审计表，记录 shader、keyword、引用材质数和预估 variant 数，作为质量档位裁剪依据。

## 2026-05-19 补充：Fog Volume 参数接入

在后续确认中，发现仅把 Fog 配置放在 `NewWorldRenderPipelineAsset` 会导致不同场景不能单独调雾效。为保持场景调参能力，同时不把雾效改成高带宽的 screen-space post-process，本阶段追加了 `NWRPFog : VolumeComponent`。

新增行为：
- Volume 菜单：`NWRP/Environment/Fog`
- Volume 参数：`enableFog`、`mode`、`color`、`startDistance`、`endDistance`、`density`
- `NewWorldRenderPipelineAsset.FeatureSettings` 不再承载 Fog 参数，避免管线 asset 变成跨场景雾效默认源
- 无 Fog Volume 或 `enableFog` 未启用时默认关闭雾效
- Volume 中 `enableFog = true` 时，使用 Volume 自身的 `mode`、`color`、`startDistance`、`endDistance`、`density`，未单独覆盖的参数使用组件默认值

实现上没有新增 full-screen pass、RenderTexture、DepthTexture 依赖或 shader keyword。`NWRPRenderer` 现在会把 VolumeStack 同时用于后处理和 Fog 参数解析；`supportsPostProcessing` 仍只控制真正的 PostProcess pass，不再阻止 Fog Volume 被采样。`SetupFogPass` 不再依赖 asset fog getter，而是读取 `NWRPFrameData` 中解析后的 fog 数据再上传全局 uniform。

验证补充：
- `dotnet build NWRP.Runtime.csproj --no-restore`：通过，`0` warning / `0` error
- `dotnet build NWRP.Editor.csproj --no-restore`：通过，`0` error；仍有项目既有 NuGet / Unity 引用版本 warning
- Unity `AssetDatabase.Refresh`：通过
- Unity Console 最近 Error / Exception：为空
- `ShaderUtil.GetShaderMessages` 扫描 `Environment` / `Lit` 目标 shader：无 compiler messages
- 临时 Editor smoke test 验证无 Fog Volume 默认关闭、Volume `enableFog=false`、Volume `enableFog=true + Exp2` 均能正确解析到 frame data
