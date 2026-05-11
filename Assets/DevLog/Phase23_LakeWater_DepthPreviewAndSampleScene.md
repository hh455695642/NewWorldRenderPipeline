# Phase23 Lake Water / Depth Preview / Sample Scene

日期：`2026-05-11`

## 概要

本阶段基于当前暂存内容，补齐了 NWRP 在深度采样调试、水面透明渲染验证以及测试场景搭建上的一组配套资源。

Phase22 已经把 `_CameraDepthTexture`、屏幕空间 UV、`_CameraDepthTextureScaleBias` 和 Depth to World 重建路径整理成统一基础契约。本阶段继续沿用这套契约，把水面 shader 和深度预览 shader 迁到 NWRP 原生 include / pass tag / feature toggle 体系下，避免继续依赖 URP shader library 或在各 shader 中手写 Y flip。

本阶段的目标不是扩展渲染管线主流程，而是为后续透明水体、岸线、折射、深度调试和测试场景验证提供可直接使用的 shader、材质和资源样例。

## 修改文件

- `.gitignore`
- `Assets/NWRP/ShaderLibrary/DepthWorldReconstruction.hlsl`
- `Assets/NWRP/Shaders/Debug/NewWorld_Debug_DepthTexturePreview.shader`
- `Assets/NWRP/Shaders/Environment/Lake.shader`
- `Assets/NWRP/Shaders/Environment/Includes/LakeFunctions.hlsl`
- `Assets/NWRP/Shaders/Utils/CopyDepth.shader`
- `Assets/NWRP/Tests/Materials/M_DepthTexturePreview_LinearEye.mat`
- `Assets/NWRP/Tests/Materials/M_Lake.mat`
- `Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity`
- `Assets/NWRP/Tests/Textures/ReflectionProbe-1.exr`
- `Assets/NWRP/Tests/Textures/T_LakeNoise_5.png`
- `Assets/NWRP/Tests/Textures/T_LakeNormal_1.png`
- `Assets/Settings/NewWorldRP.asset`

## 解决的问题

### 水面 shader 迁移到 NWRP 原生路径

旧水面 shader 的功能依赖 URP include、URP LightMode 和大量 URP 关键字组合，不适合直接放进当前自定义 SRP。暂存版本将水面实现迁移为 NWRP shader：

- Pass 使用 `NewWorldUnlit`，由现有透明阶段绘制。
- include 改为 NWRP 的 `Core.hlsl`、`DepthWorldReconstruction.hlsl`、`DeclareOpaqueTexture.hlsl` 和 `Lighting.hlsl`。
- 保留旧材质常用属性名，方便从原项目材质迁移参数。
- 岸线、波浪、渐变等功能使用 uniform toggle，而不是 shader keyword。
- 保留 GPU Instancing 支持。

这样水面可以复用 NWRP 当前的 `_CameraDepthTexture`、`_CameraOpaqueTexture`、主光阴影和 fog 路径，不需要新增 RenderFeature 或额外 fullscreen pass。

### 深度预览 shader 不再重复处理 Y flip

新增 `NewWorld_Debug_DepthTexturePreview.shader`，用于直接预览 `_CameraDepthTexture`：

- `Display Mode = 0`：Raw depth。
- `Display Mode = 1`：Linear01 depth。
- `Display Mode = 2`：LinearEye depth。
- `_LinearEyeDepthScale` 控制线性眼空间深度可视化范围。
- `_FlipY` 仅保留为手动调试兜底，默认关闭。

核心采样路径统一走：

```hlsl
float rawDepth = SampleSceneDepth(uv);
```

`SampleSceneDepth` 内部已经应用 `_CameraDepthTextureScaleBias`，所以正常路径不再在 debug shader 中额外写死 `1 - uv.y`。这避免 SceneView / Preview 或 RT 路径中出现双重翻转。

### Depth to World helper 独立成共享入口

新增 `DepthWorldReconstruction.hlsl`，把深度有效性判断和世界坐标重建收敛到共享 helper：

- `NWRPRawDepthToDeviceDepth`
- `RawDepthToDeviceDepth`
- `IsSceneDepthValid`
- `SampleSceneDepthFromPositionCS`
- `ComputeSceneWorldSpacePosition`
- `ComputeSceneWorldSpacePositionFromPositionCS`
- `ComputeNWRPWorldSpacePosition`

后续水面、软粒子、岸线遮罩、深度 debug shader 都应优先使用这些 helper，不再在各 shader 中重复处理 raw depth、device depth、inverse VP 和平台翻转。

### 测试场景补充水面验证环境

`MaterialSampleScene.unity` 中新增了水面测试所需的场景结构和遮挡参照：

- 湖面材质验证对象。
- 多个岸边 / 墙体 / 遮挡体，用于观察 depth intersection、shoreline 和 refraction。
- 深度预览材质样例，用于对照 `_CameraDepthTexture` 方向和线性化结果。

配套测试资源包括：

- `T_LakeNormal_1.png`：水面法线。
- `T_LakeNoise_5.png`：岸线 dissolve mask。
- `ReflectionProbe-1.exr`：水面 cubemap 反射。

## 关键实现

### Lake.shader

水面 shader 是单透明 Pass：

- `RenderType = Transparent`
- `Queue = Transparent`
- `LightMode = NewWorldUnlit`
- `ZWrite Off`
- `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`

主要功能：

- 深浅水颜色基于 scene depth / water plane 的深度差混合。
- 岸线使用 depth fade、世界空间投影 UV、dissolve mask 和线条动画生成。
- 两层 Gerstner wave 只在顶点阶段提供低成本位移和水面顶部颜色混合。
- 法线使用世界空间投影的双向流动法线，远距离降低强度。
- 折射采样 `_CameraOpaqueTexture`，并用 depth 检查避免明显穿帮。
- 反射使用显式 cubemap 采样，不依赖 URP reflection probe keyword。
- 主光阴影通过 NWRP `Lighting.hlsl` 接入，材质侧保留 `_ReceiveShadows` 控制。

### LakeFunctions.hlsl

本地 include 承担水面专用函数：

- 法线解包与双向法线混合。
- Gerstner wave 位移。
- depth fade / shoreline mask。
- screen-space refraction。
- 简化的 alpha layer 和 stylized specular。

这些函数没有放进 `ShaderLibrary`，是为了避免把水体专用逻辑扩散成全局公共库，符合当前项目“不要全能型超级 shader / 超级 include”的约束。

### DepthTexturePreview.shader

调试 shader 保持轻量：

- 单 Pass。
- 不新增 texture。
- 不新增 RenderFeature。
- 不新增 shader keyword。
- 使用 `_DisplayMode` uniform 切换 raw / linear01 / linear eye。

它的价值是快速确认 `_CameraDepthTexture` 的方向、线性化和 SceneView / GameView 是否一致。

### CopyDepth.shader

新增 `Hidden/NWRP/CopyDepth` shader 资源，为运行时 CopyDepth pass 提供可定位的 shader asset：

- 支持 `_DEPTH_MSAA_2 / _DEPTH_MSAA_4 / _DEPTH_MSAA_8`。
- 支持 `_OUTPUT_DEPTH`。
- pass include 复用 `ShaderLibrary/Passes/CopyDepthPass.hlsl`。

该 shader 本身只承载 copy depth 的绘制入口，不把 copy 逻辑复制到材质 shader 中。

## 性能与移动端策略

- 水面仍为单透明 pass，不新增 MRT。
- 水面不新增 fullscreen blit；折射复用已有 `_CameraOpaqueTexture`。
- 深度读取复用已有 `_CameraDepthTexture`。
- 岸线 mask 采样和两次法线采样是主要片元成本，适合作为湖面类局部透明效果，不应默认铺满大屏幕高 overdraw 区域。
- Gerstner wave 在顶点阶段执行，成本低于片元阶段波形叠加。
- 水面计算中颜色、mask、开关优先使用 `half`，Depth to World 和世界坐标相关路径保留 `float`。
- 测试贴图 meta 未启用额外高成本导入选项，normal/noise/cubemap 均按资源类型导入。

## Shader Variant 风险

本阶段水面 shader 的功能开关不使用 `shader_feature`：

- `_ENABLESHORELINE` 是 uniform。
- `_ENABLEWAVE` 是 uniform。
- `_ENABLEGRADATION` 是 uniform。
- `_ReceiveShadows` 是 uniform。

保留的 variant 来源：

- `#pragma multi_compile_instancing`
- `#pragma multi_compile_fog`
- CopyDepth shader 的本地 MSAA / output depth keyword。

没有迁入 URP 的 `_MAIN_LIGHT_SHADOWS`、`_FORWARD_PLUS`、reflection probe blending / box projection 等 keyword，避免透明水面 shader variant 爆炸。

## 验证记录

- 水面 shader 使用 NWRP include 和 `NewWorldUnlit` pass tag。
- 深度预览 shader 默认不再手动翻转 Y。
- `M_Lake.mat` 绑定了湖水法线、岸线噪声和 cubemap。
- `M_DepthTexturePreview_LinearEye.mat` 使用 `DisplayMode = 2` 观察线性眼空间深度。
- `MaterialSampleScene.unity` 添加了用于岸线、遮挡、折射和深度预览的测试对象。
- 暂存内容中没有为水面新增 RenderFeature / RenderPass，符合透明阶段复用策略。

## 当前需要复核的点

- `Assets/Settings/NewWorldRP.asset` 中 `mainLightShadowFilterMode` 从 `Hard` 改为 `MediumPCF`，这会改变移动端主光阴影采样成本；如果不是本阶段目标，建议提交前单独确认。
- `Assets/Settings/NewWorldRP.asset` 的 `featureSettings.features` 暂存为一个 `{fileID: 0}` 空引用，需要确认不是 Unity 序列化误写。
- `.gitignore` 中重复出现了 `UserSettings` 规则，同时新增了 `Assets/Plugins/` 与 `Assets/Beautify/` 忽略项；如果这些目录里已有迁移资源，需要确认不会误屏蔽后续必要提交。
- `MaterialSampleScene.unity` 改动较大，提交前建议用 Unity 打开场景确认对象层级、材质引用和 prefab override 都符合预期。

## 后续方向

- 如果水面要进入正式示例场景，应把测试材质和贴图从 `Tests` 目录迁到稳定资源目录，避免测试资源和运行资源混用。
- 如果后续添加更复杂的水体功能，如 caustics、foam trail、局部扰动，应优先拆成少量 uniform 控制或独立 shader，而不是继续叠加 keyword。
- 如果水面需要大面积使用，应优先做 overdraw 和透明排序验证，再决定是否拆分低成本移动端版本。
- Depth to World、depth preview、shoreline、soft particle 后续都应复用 Phase22 / Phase23 的 depth sampling helper，不再各自维护 Y flip 和 inverse VP 逻辑。
