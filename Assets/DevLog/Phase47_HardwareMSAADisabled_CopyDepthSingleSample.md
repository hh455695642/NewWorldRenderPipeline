# Phase47 Hardware MSAA Disabled and CopyDepth Single Sample Path

日期：`2026-06-08`

## 概要

本阶段明确收敛 NWRP 的抗锯齿策略：管线不支持硬件 MSAA，运行时渲染目标固定为 `1x render target`，屏幕空间抗锯齿继续由 Phase34 引入的 FXAA 后处理承担。

此前项目中存在几类容易混淆的 MSAA 痕迹：

- `QualitySettings.asset` 当前质量档仍为 `antiAliasing: 4`。
- `CopyDepthPass` 和 `Hidden/NWRP/CopyDepth` 保留了 `_DEPTH_MSAA_2/_DEPTH_MSAA_4/_DEPTH_MSAA_8` depth resolve 分支。
- `NWRPRenderer` 中大量 `RenderTextureDescriptor.msaaSamples = 1` / `bindMS = false` 容易被误认为“MSAA 功能未完成的残留代码”。

本阶段的结论是：

- `NWRPAntiAliasing` 不删除，它表示 FXAA Volume 后处理，不是 MSAA。
- `NWRPRenderer` 中显式写死的 `msaaSamples = 1` / `bindMS = false` 不删除，它们是禁用硬件 MSAA 的防线。
- `CopyDepth` 中未被当前 NWRP 主路径使用的 MSAA depth resolve 变体删除，避免 shader variant 和兼容性语义继续误导后续开发。

## 修改文件

- `ProjectSettings/QualitySettings.asset`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/Passes/CopyDepthPass.cs`
- `Assets/NWRP/ShaderLibrary/Passes/CopyDepthPass.hlsl`
- `Assets/NWRP/Shaders/Utils/CopyDepth.shader`

## 问题背景

NWRP 当前 camera color、camera depth、`_CameraDepthTexture`、`_CameraOpaqueTexture` 以及后处理临时 RT 都在 descriptor 层固定为单采样：

```text
msaaSamples = 1
bindMS = false
```

这意味着 NWRP 内部没有完整硬件 MSAA 路径，也没有建立以下必要链路：

- 多采样 color/depth attachment 生命周期。
- color resolve 到后处理输入。
- depth resolve 到 `_CameraDepthTexture`。
- render scale 与 MSAA resolve 的组合规则。
- camera targetTexture MSAA 与中间 RT 的一致性策略。
- GLES / Metal / Vulkan / D3D 的平台差异处理。

在这种状态下继续保留 `CopyDepth` 的 MSAA depth resolve shader variant 会产生两个问题：

1. 让调用者误以为 NWRP 已支持硬件 MSAA depth texture。
2. 为移动端引入额外 shader variant 和 `Texture2DMS` 兼容性风险。

移动端 tile-based GPU 上，硬件 MSAA 往往会放大 color/depth attachment 带宽和 resolve 成本。NWRP 当前更适合保持 `1x + FXAA` 的轻量 baseline，后续如果确实需要硬件 MSAA，应作为独立阶段重新设计完整 RT / resolve / depth texture 方案，而不是保留半套隐藏兼容分支。

## 关键实现

### 1. 关闭 Unity QualitySettings 硬件 MSAA

当前质量档为 `High Fidelity`，此前序列化为：

```yaml
antiAliasing: 4
```

本阶段改为：

```yaml
antiAliasing: 0
```

该字段对应 Unity 编辑器：

```text
Edit > Project Settings > Quality > High Fidelity > Rendering > Anti Aliasing
```

它是 Unity 内置 `QualitySettings.antiAliasing`，不同于 Camera Inspector 中的 `Allow MSAA`，也不同于 URP Asset 的 MSAA Samples。NWRP 是自定义 SRP，不使用 URP Asset 的 MSAA 配置。

### 2. 删除 CopyDepth MSAA shader variants

`Hidden/NWRP/CopyDepth` 删除：

```hlsl
#pragma multi_compile_local_fragment _ _DEPTH_MSAA_2 _DEPTH_MSAA_4 _DEPTH_MSAA_8
```

仅保留：

```hlsl
#pragma multi_compile_local_fragment _ _OUTPUT_DEPTH
```

`_OUTPUT_DEPTH` 仍用于区分输出到 depth target 或 R32 float color target 的路径；这与硬件 MSAA 无关，继续保留。

### 3. 固定 CopyDepth 为单采样读取

`CopyDepthPass.hlsl` 删除：

- `NWRP_DEPTH_MSAA_SAMPLES`
- `_DEPTH_MSAA_2/_4/_8` 分支
- `Texture2DMS<float, N>`
- sample loop resolve
- `_CameraDepthAttachment_TexelSize`
- reversed-Z min/max resolve 宏

当前采样固定为：

```hlsl
TEXTURE2D(_CameraDepthAttachment);

float SampleCopyDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraDepthAttachment, sampler_PointClamp, uv).r;
}
```

这与 NWRP 当前所有自建 camera depth attachment 的 `msaaSamples = 1` 保持一致。

### 4. 简化 CopyDepthPass C# keyword 管理

`CopyDepthPass.cs` 删除 MSAA keyword 常量与按 source RT samples 启用 keyword 的逻辑。

`ConfigureKeywords` 现在只处理：

```text
_OUTPUT_DEPTH
```

同时删除 `_CameraDepthAttachment_TexelSize` 的全局上传，以及 `NWRPShaderIds.CameraDepthAttachmentTexelSize`。单采样路径不再需要把 source depth 尺寸传给 shader resolve loop。

### 5. 保留单采样 RT 约束

`NWRPRenderer` 和各后处理 / pluggable feature 临时 RT 中的以下设置继续保留：

```text
msaaSamples = 1
bindMS = false
```

这不是残留代码，而是 NWRP 禁用硬件 MSAA 的显式约束。后续新增 RT descriptor 时也应延续该策略，除非未来单独进入“完整硬件 MSAA 支持”阶段。

## 性能与移动端策略

CPU：

- 删除 `CopyDepthPass` 中按 RT sample count 切换 keyword 的逻辑。
- 不改变 pass 调度，不新增 renderer feature，不新增 frame data 字段。
- 不影响 FXAA Volume 读取和后处理 active 判断。

GPU：

- `CopyDepth` shader variant 数量减少。
- 删除 `Texture2DMS` 与多 sample loop resolve 路径。
- 不新增 full-screen blit。
- 不新增 RenderTexture。
- 不改变 `_CameraDepthTexture` 的生成时机与 `AfterOpaques` / `AfterTransparents` / `ForcePrepass` 语义。

移动端取舍：

- 当前 baseline 明确为 `1x RT + FXAA`。
- 避免半成品 MSAA 路径在 Mali / Adreno / Apple GPU 上产生不可预期 depth resolve 差异。
- 如需更高质量 AA，应优先评估 FXAA 参数、SMAA 成本或未来独立 MSAA Phase，而不是在 CopyDepth 内恢复局部 MSAA 兼容分支。

## Variant 风险

本阶段是 variant 减法：

```text
删除 keyword:
_DEPTH_MSAA_2
_DEPTH_MSAA_4
_DEPTH_MSAA_8

保留 keyword:
_OUTPUT_DEPTH
```

FXAA 仍然不通过 shader keyword 控制，而是由 C# 根据 Volume active 状态选择后处理 shader pass，并通过 uniform 上传参数。硬件 MSAA 与 FXAA 的职责边界因此更清晰。

## 与 Phase34 / Phase46 的关系

Phase34 引入 FXAA，并已经说明当时不实现 MSAA / SMAA / TAA。本阶段把这个设计约束落到项目设置和 CopyDepth 代码层，避免 QualitySettings 与 CopyDepth 残留分支继续暗示 NWRP 支持硬件 MSAA。

Phase46 修正 `_CameraDepthTexture` 在 `ForcePrepass` 模式下的 UV 采样约定。本阶段不改变该约定，只清理 `CopyDepth` 的 MSAA resolve 预留路径。`CopyDepthPass` 仍负责 copy 路径下的 source/destination Y flip 和 `_OUTPUT_DEPTH` 输出模式。

## 验证记录

静态清理验证：

```text
DEPTH_MSAA
Texture2DMS
kDepthMsaa
CameraDepthAttachmentTexelSize
_CameraDepthAttachment_TexelSize
antiAliasing: 4
```

以上关键字在 `Assets/NWRP` 与 `ProjectSettings` 中已无残留。

单采样约束验证：

```text
Assets/NWRP/Runtime/NWRPRenderer.cs
Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs
Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs
Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Passes/ScreenBlurPass.cs
Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs
```

仍保留 `msaaSamples = 1` / `bindMS = false`。

C# 编译验证：

```text
dotnet build NWRP.Runtime.csproj --no-restore
0 warnings / 0 errors
```

```text
dotnet build NWRP.Editor.csproj --no-restore
0 errors
3 warnings
```

Editor warnings 为项目既有 Unity / NuGet reference conflict warning。

未完成项：

- Unity batchmode 打开项目被当前已打开的 Unity Editor 实例阻止，日志为 `HandleProjectAlreadyOpenInAnotherInstance`，因此本阶段未完成独占 batchmode shader import 验证。
- `NWRP.EditModeTests.csproj` 与 `NWRP.Tests.EditMode.csproj` 当前被既有缺失测试源文件阻塞，不属于本阶段改动引入。
- 尚未进行 Frame Debugger 人工检查；预期 camera color/depth/depth texture/opaque texture 仍为 1x，无 MSAA resolve pass。

## 当前边界与后续建议

- NWRP 当前不支持硬件 MSAA target / depth resolve。
- 不删除 `NWRPAntiAliasing`，它继续表示 FXAA 后处理。
- 不批量修改测试场景中的 `m_AllowMSAA`，该字段来自 Camera 组件序列化；NWRP 层和 QualitySettings 已明确禁用硬件 MSAA。
- 后续新增 RT descriptor 时应默认显式设置 `msaaSamples = 1` / `bindMS = false`。
- 若未来要恢复硬件 MSAA，应独立设计 Phase，至少同时覆盖 color resolve、depth texture resolve、post-process 输入、render scale、targetTexture、平台差异和 Frame Debugger / RenderDoc 验证。
