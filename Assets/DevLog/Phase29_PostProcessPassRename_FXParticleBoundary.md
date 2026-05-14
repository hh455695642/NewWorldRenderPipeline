# Phase29 PostProcessPass 重命名 / FX 粒子边界

日期：`2026-05-14`

## 概要

本阶段收口 NWRP 后处理框架中的一个小型命名债。运行时 pass 类型已经是 `PostProcessPass`，Frame Debugger 中也已经只暴露一个外部 `NWRP PostProcess` pass，但源码文件仍沿用最初只做 tonemapping 时的 `TonemappingPass.cs` 命名。

本次将文件正式重命名为 `PostProcessPass.cs`，并保留原有 `.meta` GUID。运行时行为不变：Bloom、Tonemapping、Color Adjustments 和 Vignette 仍然在同一个外部 post-process pass 内部执行；没有修改 shader、render target、keyword 或 pass event。

## 修改内容

- 将 `Assets/NWRP/Runtime/PostProcessing/Passes/TonemappingPass.cs` 重命名为 `PostProcessPass.cs`。
- 将配套 `.meta` 文件重命名为 `PostProcessPass.cs.meta`，保留现有 Unity asset GUID。
- 更新 `NWRP.Runtime.csproj`，确保本地 `dotnet build` 使用新的源码路径。
- 历史 DevLog 中提到 `TonemappingPass.cs` 的内容保留为阶段记录，不回改历史文本。

## FX 粒子边界

后续内置 Unity 粒子支持应从 `ParticleSystemRenderer` 和 NWRP 自有 FX shader 开始。普通粒子材质只要 shader 暴露 `NewWorldUnlit` 或 `NewWorldForward` pass tag，就可以通过现有透明渲染路径绘制。

不要为普通内置粒子新增 `NWRPFeature`。自定义 feature 应预留给未来 GPU-driven particle 或 indirect-draw 系统，这类系统才需要显式 buffer、剔除、排序或共享 visibility data。

建议的 FX shader 拆分：

- Alpha blend 粒子 shader。
- Additive 粒子 shader。
- Soft particle shader，复用 `_CameraDepthTexture` 和 `DepthWorldReconstruction.hlsl`。
- Dissolve 粒子 shader，mask 使用 uniform 控制，不叠加 keyword 组合。
- Distortion / refraction shader，仅在 pipeline asset 启用 opaque texture 支持时使用 `_CameraOpaqueTexture`。

## Variant 与移动端成本

- 保留 `#pragma multi_compile_instancing`，用于粒子 instancing 兼容。
- 不要把 blend mode、soft particle、dissolve、distortion 组合成 keyword 矩阵。
- 成本分层优先使用独立 shader 或 uniform-controlled branch。
- Soft particle 继承现有 depth texture 路径的带宽成本。
- Distortion / refraction 继承现有 opaque texture 路径的带宽成本。
- 本阶段不引入 geometry shader、MRT 或额外 fullscreen pass。

## 验证

本阶段验证项：

- 确认 post-processing pass 源码路径下只保留 `PostProcessPass.cs`。
- 确认 runtime 引用仍然实例化 `PostProcessPass`。
- 构建 `NWRP.Runtime.csproj`。
- 执行 `git diff --check`。
