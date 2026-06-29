# Phase54 Unity 6.3 Mobile Bandwidth / TBDR 对齐开发

日期：`2026-06-29`

## 概要

本阶段把云端 `origin/main` 最新五次提交中的 Mobile Bandwidth / TBDR-friendly 渲染链路迁移到 Unity `6000.3.12f1` 分支，并按当前项目已有的 Unity 6.3 阴影、Volume、RTHandle 改动重新对齐。

需要特别区分两个编号体系：

```text
Unity 6.3 分支 DevLog:
    本文记录为 Phase54

参考来源:
    origin/main Phase51-55
    Camera Attachment
    Fullscreen Helper / FrameGraph
    Camera Texture Policy
    Final Presenter 收口
    Fullscreen Chain
```

本阶段继续保持 NWRP 自研 SRP 架构：

```text
NWRPFeature
    -> NWRPPass
        -> NWRPPassEvent
```

没有迁移到 URP `ScriptableRendererFeature`、URP `ScriptableRenderPass`、URP RenderGraph 或 Built-in pipeline fallback。URP package 仍然只允许作为参考 / 测试依赖存在，NWRP runtime 与 NWRP-owned shader 不依赖 URP 包源码。

本阶段的核心目标是移动端带宽治理，而不是增加新的视觉效果：

- 收敛 camera color / depth attachment 的 load / store 语义。
- 避免隐藏的 `_CameraDepthTexture` / `_CameraOpaqueTexture` copy。
- 用轻量 frame graph 判断 camera color 最后使用者和 final presenter。
- 把 CloudShadow、ValleyFog、ScreenBlur、PostProcess final composite 接入内部 fullscreen chain。
- 减少不必要的 camera target 反复绑定、fullscreen temp、final blit。
- 对 attachment-only depth 启用保守 memoryless。
- 将默认 pipeline asset 调整为移动低带宽 baseline。

本阶段没有引入 Unity 6.5-only API。Unity 6.5 的 URP on-tile post-processing 只作为未来方向预警；当前不把 URP on-tile / RenderGraph-only 架构变成 NWRP 依赖。

## 修改文件

### Runtime core

- `Assets/NWRP/Runtime/NWRPCameraAttachmentPolicy.cs`
- `Assets/NWRP/Runtime/NWRPFrameDebugStats.cs`
- `Assets/NWRP/Runtime/NWRPFrameResources.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPPass.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPRendererData.cs`
- `Assets/NWRP/Runtime/NWRPFeatureScheduler.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`

### Built-in passes

- `Assets/NWRP/Runtime/Passes/CopyColorPass.cs`
- `Assets/NWRP/Runtime/Passes/CopyDepthPass.cs`
- `Assets/NWRP/Runtime/Passes/DepthPrepass.cs`
- `Assets/NWRP/Runtime/Passes/DrawOpaquePass.cs`
- `Assets/NWRP/Runtime/Passes/DrawSkyboxPass.cs`
- `Assets/NWRP/Runtime/Passes/DrawTransparentPass.cs`
- `Assets/NWRP/Runtime/Passes/FinalBlitPass.cs`
- `Assets/NWRP/Runtime/Outlines/Passes/DrawOutlinePass.cs`

### Fullscreen chain

- `Assets/NWRP/Runtime/Passes/INWRPFullscreenEffectNode.cs`
- `Assets/NWRP/Runtime/Passes/NWRPFullscreenPassUtils.cs`
- `Assets/NWRP/Runtime/Passes/NWRPFullscreenChain.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/Passes/ValleyHeightFogPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/Passes/ScreenBlurPass.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFogOverlay/Passes/ValleyHeightFogOverlayPass.cs`

### Camera texture / pluggable feature policy

- `Assets/NWRP/Runtime/CameraTextures/DepthTextureFeature.cs`
- `Assets/NWRP/Runtime/CameraTextures/OpaqueTextureFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/CloudShadowProjector/CloudShadowProjectorFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ValleyHeightFog/ValleyHeightFogFeature.cs`
- `Assets/NWRP/Runtime/PluggableFeatures/ScreenBlur/ScreenBlurFeature.cs`

### Lighting / mobile settings

- `Assets/NWRP/Runtime/Lighting/AdditionalLightUtils.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`
- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/Settings/NewWorldRP.asset`

### Shader / shader library

- `Assets/NWRP/ShaderLibrary/DepthWorldReconstructionBlit.hlsl`
- `Assets/NWRP/Shaders/Environment/CloudShadowProjector.shader`
- `Assets/NWRP/Shaders/PostProcess/NWRP_ValleyHeightFog.shader`

### EditMode contract tests

- `Assets/NWRP/Tests/EditMode/TBDRSettingsTests.cs`
- `Assets/NWRP/Tests/EditMode/TBDRFrameGraphTests.cs`
- `Assets/NWRP/Tests/EditMode/TBDRTargetRequirementTests.cs`
- `Assets/NWRP/Tests/EditMode/DepthDrivenBlitShaderContractTests.cs`
- `Assets/NWRP/Tests/EditMode/FullscreenChainContractTests.cs`

## 解决的问题

### 1. Camera target load / store 缺少统一策略

旧路径中，多个 pass 会直接调用：

```csharp
cmd.SetRenderTarget(cameraColor, cameraDepth)
```

这让 camera color / depth 的 load / store 语义分散在 renderer 与 pass 里。对 tile-based GPU 来说，频繁切换 render target 且保守 `Load` / `Store` 会增加 tile load / store 和 external bandwidth。

本阶段新增：

```csharp
NWRPCameraAttachmentPolicy
NWRPCameraAttachmentState
```

统一处理几类场景：

```text
Camera setup 且 clear:
    使用 DontCare load

回到 camera target:
    RestoreCameraRenderTarget(...) 根据状态选择 Load / DontCare

离开 camera target 写 shadow atlas / fullscreen temp / copy target:
    InvalidateCameraRenderTarget(...)

连续绑定同一个 camera target:
    允许 skip bind
```

`NWRPFrameDebugStats` 会统计 camera bind、skip bind、非 camera target bind、discarded depth store 等字段。日志由 pipeline asset 的 `logFrameDebugStats` 控制，默认关闭。

### 2. `_CameraDepthTexture` 可能被 feature 隐式触发

旧逻辑里，Valley Height Fog、Cloud Shadow 等 depth-driven fullscreen effect 容易绕过统一 renderer policy，直接持有或触发 depth copy / depth prepass。

本阶段将 camera texture 开关从 bool 升级为 policy：

```csharp
public enum CameraTexturePolicy
{
    Off,
    AutoFeatureOnly,
    Force
}
```

语义为：

```text
Off:
    绝对禁止自动生成对应 camera texture

AutoFeatureOnly:
    只有 active feature 声明 requirement 时生成

Force:
    兼容旧行为，每帧强制生成
```

`DepthTextureFeature.AllowsFeatureDepthTextureRequest(...)` 成为 depth consumer 的统一入口。`DepthTexture Off` 时，ValleyFog / CloudShadow 即使 active，也不会触发 hidden `CopyDepth`。

### 3. Fullscreen pass 缺少统一 blit helper

CloudShadow、ValleyFog、ScreenBlur、PostProcess 原本各自管理 fullscreen temp、viewport、source UV、final backbuffer 输出判断。这会让 `_ScaleBiasRt`、render scale、GameView backbuffer Y 翻转、debug stats 和 RT 生命周期分散。

本阶段新增：

```csharp
NWRPFullscreenPassUtils
```

统一处理：

```text
CreateColorDescriptor
AllocateTempColor / ReleaseTempColor
BlitToTarget
BlitToBackBuffer
viewport
render scale
_ScaleBiasRt
fullscreen blit / final blit debug stats
```

所有 fullscreen pass 不再各自临时拼 target 语义，而是进入同一个 helper / chain 合约。

### 4. Final presenter 判断过宽

旧路径中，一个 fullscreen pass 如果认为自己可以输出到 backbuffer，可能在后续仍有 pass 需要读写 camera color 时提前 final-present，导致后续 pass 链路混乱。

本阶段给 `NWRPPass` 增加：

```csharp
GetFrameResourceUsage(ref NWRPFrameData)
CanPresentCameraColorToBackBuffer(ref NWRPFrameData)
```

并新增轻量 frame graph：

```csharp
NWRPFrameResourceAccess
NWRPFramePassResourceUsage
NWRPFrameGraphData
NWRPFrameGraphAnalyzer
```

`NWRPRenderer.BuildPassQueue()` 排序后会分析：

```text
camera color last-use
camera color final-present pass
camera depth last-use
render pass cluster count
是否可以在最后使用者处直写 backbuffer
```

只有最后一个合法 camera color 使用者可以成为 final presenter。若后续 pass 仍读写 camera color，前序 fullscreen pass 必须写回 camera color，而不是直写 backbuffer。

### 5. ScreenBlur 多 iteration 反复写回 cameraColor

ScreenBlur 是典型的 separable blur：

```text
horizontal
vertical
```

旧路径中，多 iteration 容易每轮 vertical 都写回 camera color，造成额外 target store / load。对移动端 tile GPU 来说，这是高带宽路径。

本阶段将 `ScreenBlurPass` 接入 `NWRPFullscreenChain`：

```text
iterations * 2 pass count
temp A/B ping-pong
最后一步才按 frame graph 写回 cameraColor 或 backbuffer
```

这保留了 ScreenBlur 独立 feature 的边界，没有把 ScreenBlur、CloudShadow、ValleyFog 合并成一个超级 shader。

### 6. PostProcess 和 ScreenBlur 存在硬编码耦合

本阶段移除 `PostProcessPass` 对 `ScreenBlurFeature.IsAfterPostProcessActive(...)` 的硬编码依赖。是否可以直写 backbuffer，只由 frame graph 的 last-use / final-present 判断决定。

Bloom pyramid 暂时仍保留在 `PostProcessPass` 内部，不拆成 chain node。这样能保持当前 Bloom 管线稳定，同时让 final composite 进入 fullscreen chain 合约。

### 7. Depth-driven fullscreen shader orientation 风险

depth / world reconstruction 不能直接复用 color source UV。GameView final backbuffer、render scale、平台 Y 翻转都会影响 depth sample 与 world position reconstruction。

本阶段统一约束：

```text
color source:
    使用 Blitter source UV

depth / world reconstruction:
    使用 GetBlitScreenUV(input.positionCS)

DepthWorldReconstructionBlit.hlsl:
    保留 _ScaleBiasRt Y 修正
```

CloudShadow / ValleyFog shader 不新增 keyword，只修正 depth UV contract。

### 8. Phase51-55 对齐复查遗漏点

后续对照 `origin/main` Phase51-55 复查时，补齐了三个小范围遗漏点，均不改变当前 Unity 6.3 分支已有架构：

- `NWRPFullscreenPassUtils` 中两个 width / height 版本的 `BlitToTarget` 已恢复使用 `MakeViewport(width, height)` 构造 viewport，避免外部传入 0 或异常尺寸时直接生成无效 `Rect`。
- `MakeViewport(int width, int height)` 已补回，返回 `new Rect(0f, 0f, Mathf.Max(width, 1), Mathf.Max(height, 1))`，与 `origin/main` 的 fullscreen helper contract 对齐。
- `DrawMobileBandwidthRiskSummary()` 的 TBDR 风险提示补回 main-light `MediumPCF`、additional light shadows、additional-light `MediumPCF` 三类阴影带宽风险，同时保留当前 6.3 分支已有的 `scaled intermediate color/depth` 文案。
- 可选的 C# `.meta` GUID 对齐已执行：确认当前 GUID 没有被其它资产引用后，将 `NWRPCameraAttachmentPolicy`、`NWRPFrameDebugStats`、`NWRPFrameResources`、`INWRPFullscreenEffectNode`、`NWRPFullscreenChain`、`NWRPFullscreenPassUtils` 的 `.meta` GUID 同步为 `origin/main` 对应值，减少后续 cherry-pick / merge 的资产身份差异。

## 关键实现

### NWRPFrameData

新增 frame-level 数据：

```csharp
NWRPCameraAttachmentState cameraAttachmentState;
NWRPFrameGraphData frameGraph;
NWRPTransientResourceAllocator transientResources;
int currentPassIndex;
NWRPFrameDebugStats debugStats;
```

同时保留 Unity 6.3 已有：

```csharp
internal NWRPShadowCullingContext shadowCullingContext;
```

本阶段没有覆盖 Phase52 引入的 shadow culling context，也没有回退到旧 shadow path。

### NWRPRenderer

`NWRPRenderer` 是本阶段的核心集成点：

```text
Setup camera:
    使用 NWRPCameraAttachmentPolicy

Shadow stage 后恢复:
    RestoreCameraRenderTarget(...)

Pass queue 排序后:
    AnalyzeFrameGraph(...)

执行 pass:
    frameData.currentPassIndex = passIndex

配置 camera depth:
    满足 attachment-only 条件时允许 RenderTextureMemoryless.Depth

frame 结束:
    按 asset 开关输出 NWRPFrameDebugStats
```

memoryless depth 使用保守条件：

```text
无 depth texture
无 depth copy
无 depth prepass
无 opaque texture
depth 只作为 attachment 使用
```

### NWRPFeatureScheduler

Scheduler 收敛为：

```text
serialized features:
    使用 NWRPFeatureMetadata.sortOrder 排序

需要 depth texture:
    优先 enqueue serialized DepthTextureFeature
    没有 serialized feature 时再使用 runtime feature

DepthTexture Off:
    清掉 depth texture / copy / prepass requirement

OpaqueTexture Off:
    清掉 opaque texture requirement

CloudShadow / ValleyFog:
    仅声明 requirement，不私自 new / enqueue CopyDepth
```

CloudShadow `SortOrder = 150`，ValleyFog `SortOrder = 220`，确保 CloudShadow 稳定排在 ValleyFog 前。

### NWRPFullscreenChain

新增 internal interface：

```csharp
internal interface INWRPFullscreenEffectNode
{
    NWRPPassEvent PassEvent { get; }
    bool RequiresDepthTexture { get; }
    bool RequiresOpaqueTexture { get; }
    bool IsActive(ref NWRPFrameData frameData);
    bool CanPresentToBackBuffer(ref NWRPFrameData frameData);
    void Prepare(ref NWRPFrameData frameData);
    int GetPassCount(ref NWRPFrameData frameData);
    bool TryGetPass(...);
}
```

`NWRPFullscreenChain` 负责：

```text
校验 node active state 和 required targets
调用 Prepare 上传 shader globals
缓存 material / pass index
分配 fullscreen temp A/B
单步 effect 非 final 时自动 copy back
多步 effect A/B ping-pong
最后一步按 frame graph 写 cameraColor 或 backbuffer
释放 temp RT
非 backbuffer 输出后恢复 camera target
```

该 interface 是 NWRP 内部执行器，不作为第三方 public plugin API。

### MobileBandwidthSettings

`NewWorldRenderPipelineAsset` 新增：

```csharp
MobileBandwidthSettings
```

默认值：

```text
enableMobileFullscreenBudget = true
bloomMaxMipCount = 4
bloomMaxBaseSize = 512
maxAdditionalLights = 4
logFrameDebugStats = false
```

Editor inspector 增加 Mobile Bandwidth 区块：

```text
Enable Mobile Fullscreen Budget
Bloom Max Mips
Bloom Max Base Size
Max Additional Lights
Log Frame Debug Stats
移动端带宽风险提示
```

默认 `Assets/Settings/NewWorldRP.asset` 已进入低带宽 baseline：

```text
HDR off
PostProcessing off
RenderScale off
OpaqueTexture Off
DepthTexture AutoFeatureOnly
MobileFullscreenBudget on
MainLightShadowFilter hard
AdditionalLightShadows off
```

### AdditionalLightUtils

移动预算开启时，additional light upload 使用：

```text
MobileMaxAdditionalLights
```

筛选逻辑按 camera distance / luminance 优先选择近且亮的 punctual lights。未上传 slots 会清零，避免 shader 端读取旧数据。

## 性能与移动端策略

CPU：

- 不引入大规模 CPU per-instance loop。
- 不恢复 CPU `ShadowsOnly` fallback。
- Scheduler 只分析 active feature requirement，不做材质级 opaque texture consumer 扫描。
- Frame graph 是轻量 pass usage 分析，不引入 URP RenderGraph。

GPU / bandwidth：

- 减少 camera target 重复 bind。
- 对可丢弃 depth store 的路径记录并使用更明确的 load / store action。
- Fullscreen temp 统一分配 / 释放，debug stats 可统计 temp RT 数量。
- ScreenBlur 多 iteration 使用 A/B ping-pong，最后一步才写回。
- 只有最后 camera color 使用者可以 final-present。
- attachment-only depth 才允许 memoryless depth。
- Bloom 在 mobile budget 下优先使用 `B10G11R11_UFloatPack32`，不支持时回退 `R16G16B16A16_SFloat`，最后回退 Unity HDR default format。

移动端取舍：

- `Opaque Texture` 默认 Off，不做材质消费者扫描。需要 opaque texture 的场景应显式 Force 或后续补显式 requirement。
- `Depth Texture` 默认 AutoFeatureOnly，只有 ValleyFog / CloudShadow 等 active consumer 请求时生成。
- `DepthTexture Off` 是强约束，不允许 active consumer 偷偷触发 copy depth。
- 不把 ValleyFog / CloudShadow / ScreenBlur 合并成超级 shader，避免维护和 variant 风险失控。
- Unity 6.5 on-tile post-processing 暂不进入当前架构。

## Shader Variant 风险

本阶段没有新增业务 shader keyword。

```text
新增 global keyword: 0
新增业务 multi_compile: 0
新增业务 shader_feature_local: 0
新增 shader 文件: 0
```

本阶段 shader 变更只涉及 depth UV / world reconstruction contract：

```text
DepthWorldReconstructionBlit.hlsl:
    保留 _ScaleBiasRt
    depth reconstruction 使用 GetBlitScreenUV(input.positionCS)

CloudShadowProjector.shader:
    depth / world reconstruction 不使用 raw source UV

NWRP_ValleyHeightFog.shader:
    depth / world reconstruction 不使用 raw source UV
```

既有 `multi_compile_instancing` 保持原状。CopyDepth / CoreBlit 仍使用既有 local variant，不新增全局业务组合。

## 与 Phase48-53 的关系

Phase48 / Phase49 / Phase50 是本阶段必须保护的底层基线：

```text
Phase48:
    indirect-only vegetation shadow atlas bootstrap

Phase49:
    indirect-only cascade fallback

Phase50:
    vegetation indirect custom SH 与真实 worldToObject
```

本阶段没有回滚这些能力：

- `VegetationIndirectShadowRegistry` caster query 保留。
- `allowEmptyAtlas` 与 indirect-only atlas lifecycle 保留。
- camera frustum cascade fallback 只在无 regular caster 且存在 indirect caster 时生效。
- `_NWRPVegetationUseCustomSH` 继续是 uniform，不新增 keyword。
- `SampleVegetationIndirectSH(...)` 保留。
- instance / visible buffer 保持真实 `worldToObject`。

Phase51 / Phase52 / Phase53 是 Unity 6.3 分支已有基础：

```text
Phase51:
    VolumeManager lifecycle 修复

Phase52:
    shadow culling context / renderer list / RTHandle 收敛

Phase53:
    SceneView cached main light shadow policy
```

本阶段在这些基础上继续推进移动带宽治理，没有覆盖 `NWRPShadowCullingContext`，也没有把 fullscreen chain 临时 RT 与已有持久 RTHandle helper 混成同一个职责。

## 验证记录

### 静态检查

已完成：

```text
git diff --check
```

结果：

```text
0 errors
仅有 CRLF 提示
```

已确认 NWRP runtime 没有新增 URP runtime 依赖：

```text
rg -n -g '!AGENTS.md' "UnityEngine.Rendering.Universal|ScriptableRendererFeature|ScriptableRenderPass" Assets/NWRP/Runtime
无命中
```

已确认 NWRP-owned shader / shader library 没有 include URP package source：

```text
rg -n -g '!AGENTS.md' "Packages/com.unity.render-pipelines.universal" Assets/NWRP/ShaderLibrary Assets/NWRP/Shaders
无命中
```

已检查 shader pragma diff：

```text
git diff -U0 -- Assets/NWRP/Shaders Assets/NWRP/ShaderLibrary
没有新增 pragma
```

### dotnet 编译

已完成：

```text
dotnet build NWRP.Runtime.csproj --no-restore
0 warnings / 0 errors
```

已完成：

```text
dotnet build NWRP.Runtime.Tests.csproj --no-restore
0 warnings / 0 errors
```

已完成：

```text
dotnet build NWRP.Editor.csproj --no-restore
0 errors
3 existing MSB reference conflict warnings
```

`NWRP.Editor` 的 warning 来自项目已有 Unity / NuGet assembly reference version conflict，不是本阶段新增编译错误。

### Phase51-55 对齐复查验证

已完成：

```text
rg -n "new Rect\(0f, 0f, width, height\)|MakeViewport" Assets/NWRP/Runtime/Passes/NWRPFullscreenPassUtils.cs
```

结果确认：`NWRPFullscreenPassUtils.cs` 中不再存在 `new Rect(0f, 0f, width, height)`，只保留两个 `MakeViewport(width, height)` 调用和一个 `MakeViewport` 定义。

已确认 `DrawMobileBandwidthRiskSummary()` 同时包含：

```text
HDR color
post-processing
scaled intermediate color/depth
forced opaque texture
forced depth texture
main-light Medium PCF
additional light shadows
additional-light Medium PCF
```

已确认同步后的六个 `.meta` GUID 只在各自 `.meta` 文件中出现，没有额外资产引用需要迁移。

本轮追加修复后重新执行：

```text
dotnet build NWRP.Runtime.csproj --no-restore
0 warnings / 0 errors

dotnet build NWRP.Editor.csproj --no-restore
0 errors
3 existing MSB reference conflict warnings
```

`NWRP.Editor` 首次并行编译时遇到 `obj/Debug/NWRP.Runtime.dll` 文件锁，改为串行重跑后通过；该文件锁来自并行 build 竞争，不是代码编译错误。

### Unity Editor / Test Runner

尝试运行 Unity batchmode EditMode：

```text
Unity.exe -batchmode -projectPath ... -runTests -testPlatform editmode
```

未能启动，原因是当前项目已被另一个 Unity Editor 实例打开：

```text
It looks like another Unity instance is running with this project open.
Multiple Unity instances cannot open the same project.
```

因此本阶段完成了 C# 编译与静态边界验证，但 Unity Test Runner 的实际 EditMode 执行仍需要在当前打开的 Editor 内手动运行。

读取当前 Editor log 尾部，确认项目以 Unity `6000.3.12f1` 打开并完成脚本编译，未看到新的 NWRP 脚本编译错误。

## 新增合约测试覆盖

### TBDRSettingsTests

覆盖：

- 新建 pipeline asset 默认 `OpaqueTexture = Off`。
- 新建 pipeline asset 默认 `DepthTexture = AutoFeatureOnly`。
- Mobile fullscreen budget 默认开启。
- Bloom max mip = 4。
- Bloom max base size = 512。
- Mobile additional light cap = 4。
- Frame debug stats log 默认关闭。

### TBDRFrameGraphTests

覆盖：

- 最后一个 camera color 使用者可以成为 final presenter。
- final presenter 后仍有 camera color user 时拒绝 final-present。
- camera depth last-use 能被识别。
- render pass cluster 能统计。
- transient allocator 能在 lifetime 不重叠时复用 physical resource。

### TBDRTargetRequirementTests

覆盖：

- ValleyFog / CloudShadow active + AutoFeatureOnly 请求 depth texture。
- DepthTexture Off 阻止 depth consumer request。
- ScreenBlur 在 `supportsPostProcessing = false` 时仍可请求 intermediate color。
- CloudShadow metadata sort order 稳定早于 ValleyFog。

### DepthDrivenBlitShaderContractTests

覆盖：

- `DepthWorldReconstructionBlit.hlsl` 保留 `_ScaleBiasRt`。
- depth / world reconstruction 使用 `GetBlitScreenUV(input.positionCS)`。
- CloudShadow / ValleyFog shader 不回退到 raw source UV 采样 depth。

### FullscreenChainContractTests

覆盖：

- CloudShadow / ValleyFog / ScreenBlur / PostProcess 实现 internal fullscreen node contract。
- node contract 包含 prepare / pass count / pass query / final-present。
- ScreenBlur 多 iteration pass count = `iterations * 2`。
- PostProcess 不硬编码 ScreenBlur after-postprocess 分支。

## 手动验证清单

建议在当前 Unity Editor 中验证：

1. Baseline：无 post / 无 depth consumer，确认不生成 hidden CopyDepth。
2. DepthTexture Off：ValleyFog / CloudShadow active 时也不应触发 depth copy。
3. DepthTexture AutoFeatureOnly：ValleyFog only、CloudShadow only、ValleyFog + CloudShadow 均可请求 `_CameraDepthTexture`。
4. copyDepth AfterOpaques / AfterTransparents：`CopyDepthPass` 必须排在 depth consumer 前。
5. CloudShadow + ValleyFog：CloudShadow 稳定先于 ValleyFog。
6. ScreenBlur BeforePostProcess / AfterPostProcess：多 iteration 应由 chain ping-pong，最后一步才写回。
7. PostProcess only：final composite 只能在 camera color last-use 时直写 backbuffer。
8. PostProcess + ScreenBlur：PostProcess 不再硬编码 ScreenBlur after-postprocess 分支。
9. GameView final backbuffer：ValleyFog / CloudShadow depth reconstruction 不应上下颠倒。
10. SceneView：Effects / Post Processing toolbar 仍控制 SceneView 后处理表现。
11. Frame Debugger：检查 fullscreen blit 数、temporary RT 数、camera target bind / skip。
12. `Map_LoopForest`：确认 Phase48-50 的 indirect vegetation shadow、SH 底色、真实 normal matrix 没有回归。

真机建议：

```text
Android Mali / Adreno:
    AGI
    RenderDoc
    Snapdragon Profiler

iOS Metal:
    Xcode GPU Frame Capture
```

重点观察：

- external bandwidth
- tile store / load
- RT peak memory
- fullscreen blit count
- depth texture copy 是否按 policy 生成
- bloom base size / mip count 是否受 mobile budget 限制

## 当前注意事项

- `INWRPFullscreenEffectNode` 是 internal contract，不是公开插件 API。
- Feature 只声明 target requirement，不应私自 new / enqueue 全局 camera texture copy pass。
- `CameraTexturePolicy.Off` 是硬约束，后续 feature 不应绕过。
- `NWRPFrameDebugStats` 是诊断数据，不应被 gameplay 逻辑依赖。
- `NWRPTransientRTHandles` 仍可服务已有持久 RTHandle 资源；fullscreen chain temp 以本阶段 chain 合约为准。
- 当前没有引入 Unity 6.5-only API；如果后续升级到 6.5，需要单独评估 NWRP 是否自研 tile-local fullscreen contract。
- 当前工作区还存在其它 Unity 自动序列化 / 既有脏改动，例如测试场景、材质、URP asset 删除、Screenshots / Build Profiles / Recovery 等；这些不属于本阶段 DevLog 描述的核心实现范围。

## 后续方向

- 在当前打开的 Unity Editor 中运行新增 TBDR EditMode tests 与已有 `NWRPAdditionalShadowLayoutTests`、`NWRPVolumeManagerLifecycleTests`。
- 补一轮 SceneView / GameView / PlayMode 的 visual smoke，重点看 depth-driven fullscreen orientation。
- 在移动真机上用 GPU 工具确认 tile store / load 与 fullscreen blit 数是否符合预期。
- 如果后续有材质明确依赖 `_CameraOpaqueTexture`，优先通过显式 requirement 或 renderer data Force，而不是恢复默认 opaque copy。
- 如果未来升级 Unity 6.5，单独评估 URP on-tile post-processing 的思想是否值得转化为 NWRP 自研 tile-local fullscreen contract，不能直接依赖 URP 实现。
