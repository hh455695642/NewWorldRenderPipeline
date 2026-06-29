# Phase55 Bloom Volume Budget / Mobile Fullscreen Budget 拆分

日期：`2026-06-29`

## 概要

本阶段将 Phase54 中新增的 `Enable Mobile Fullscreen Budget` 总开关拆分为更明确的职责边界。

原设计中，Pipeline Asset 的 Mobile Bandwidth 区块同时控制：

```text
Bloom fullscreen RT 预算
Additional Lights 上传数量
Frame Debug Stats 日志
移动端带宽风险提示
```

这会让运行时预算、TA 内容配置、诊断工具混在同一个开关下。实际项目工作流中，TA 会对场景灯光数量做规范与检查；Bloom 的质量 / 带宽预算也更适合放在具体 Volume Profile 中随场景、平台或 lookdev 方案调整。

本阶段调整后：

- 删除 Pipeline Asset 中的 `Enable Mobile Fullscreen Budget` 概念。
- `Additional Lights` 不再被移动端预算裁到 4 个，恢复 NWRP forward lighting 的硬上限 8 个。
- Bloom 的 mip 数与 base size 预算下沉到 `NWRPBloom` Volume。
- `customize` 继续是 Custom Layer Controls 的唯一功能开关。
- `lensDirtIntensity` 继续是 Lens Dirt 的唯一功能开关。
- Pipeline Asset 仅保留诊断用途的 `Log Frame Debug Stats` 和移动端带宽风险提示。

本阶段没有新增 shader keyword，没有新增 shader variant，也没有引入 URP `ScriptableRendererFeature` / `ScriptableRenderPass` 依赖。

## 修改文件

### Runtime

- `Assets/NWRP/Runtime/PostProcessing/NWRPBloom.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/Lighting/AdditionalLightUtils.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`

### Editor

- `Assets/NWRP/Editor/PostProcessing/NWRPBloomEditor.cs`
- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`

### EditMode contract tests

- `Assets/NWRP/Tests/EditMode/TBDRSettingsTests.cs`

## 解决的问题

### 1. Pipeline Asset 的 Mobile Fullscreen Budget 职责过宽

Phase54 中的 Mobile Bandwidth 设置是移动端低带宽 baseline：

```text
enableMobileFullscreenBudget = true
bloomMaxMipCount = 4
bloomMaxBaseSize = 512
maxAdditionalLights = 4
logFrameDebugStats = false
```

其中前三项属于运行时预算策略，第四项属于光照内容限制，第五项属于诊断工具。把它们挂在同一个总开关下会带来两个问题：

- TA 修改 Bloom 质量时必须去 Pipeline Asset，而不是 Volume Profile。
- Additional Lights 数量被管线静默裁剪，容易和场景灯光检查规则重复。

本阶段删除了运行时对以下 Pipeline Asset 属性的依赖：

```text
EnableMobileFullscreenBudget
MobileBloomMaxMipCount
MobileBloomMaxBaseSize
BloomMaxMipCount
BloomMaxBaseSize
MobileMaxAdditionalLights
```

`MobileBandwidthSettings` 现在只保留：

```csharp
public bool logFrameDebugStats = false;
```

旧 asset YAML 中残留的序列化字段不再被运行时代码读取，可在后续保存或专门清理资产时自然移除。

### 2. Bloom 预算应该由 Volume Profile 控制

`NWRPBloom` 新增两个预算参数：

```csharp
public ClampedIntParameter maxMipCount = new ClampedIntParameter(4, 1, 6);
public ClampedIntParameter maxBaseSize = new ClampedIntParameter(512, 64, 4096);
```

默认值保持 Phase54 的移动端 baseline：

```text
maxMipCount = 4
maxBaseSize = 512
```

高质量或桌面 lookdev Profile 可以显式提高：

```text
maxMipCount = 6
maxBaseSize > 512
```

`PostProcessPass.ResolveBloomBudget(...)` 现在只读取当前 `NWRPBloom`：

```csharp
BloomBudget budget = ResolveBloomBudget(bloom, requestedBaseSize);
```

预算规则为：

```text
mipCount = clamp(bloom.maxMipCount, 1, 6)
baseSize = min(requestedBaseSize, bloom.maxBaseSize)
```

这样 Bloom 质量、带宽和具体场景 Profile 绑定，TA 可以直接在 Volume 中审查和调参。

### 3. Custom Layer Controls 不再有双重开关

讨论中确认：Custom Layer Controls 已经由 Volume 中的 `customize` 控制，不应该再额外增加 `allowCustomCompose` 这类重复开关。

本阶段保留单一控制入口：

```text
customize == true
```

运行时规则：

```text
customize == true && maxMipCount == 6:
    执行完整 custom compose

customize == true && maxMipCount < 6:
    跳过完整 custom compose
```

原因是 custom compose 依赖完整 0-5 层 bloom pyramid。低 mip 预算下强行执行会让分层 weight / boost / tint 语义不完整，也会重新引入额外全屏 RT 与 blit 成本。

Editor 中新增 warning：

```text
Custom layer compose requires Max Mip Count 6.
The mobile budget will skip the full custom compose path at lower mip counts.
```

该 warning 只提示预算与功能关系，不新增 shader keyword，也不增加 variant 风险。

### 4. Lens Dirt 由 lensDirtIntensity 单独控制

讨论中也确认：Lens Dirt 已经由 `lensDirtIntensity` 控制，不需要再增加 `allowLensDirtExtraCompose`。

本阶段删除了 `BloomBudget.allowLensDirtExtraCompose` 语义。运行时只看：

```text
lensDirtIntensity > 0
```

低 mip 预算下需要额外处理 `lensDirtSpread`。原始 `lensDirtSpread` 范围为 3-5，但如果当前只分配 4 个 mip，最大可用 index 是 3。为避免访问未写入的 pyramid 层，本阶段将采样层级 clamp 到已分配 mip：

```csharp
int index = Mathf.Clamp(lensDirtSpread, 0, lastAvailableMipIndex);
```

当 clamp 到最后可用 mip 时，使用该 mip 的 `down` 纹理作为 dirt source。原因是最后一级 downsample 后不会再参与向上合成写入自己的 `up` 纹理，采 `up` 有读到未写入 RT 的风险。

### 5. Additional Lights 不再被移动预算裁剪

`AdditionalLightUtils.GetUploadLimit(...)` 原先逻辑：

```text
EnableMobileFullscreenBudget == true:
    upload limit = MobileMaxAdditionalLights, 默认 4

EnableMobileFullscreenBudget == false:
    upload limit = MaxAdditionalLights, 默认 8
```

本阶段改为：

```csharp
return Mathf.Clamp(MaxAdditionalLights, 0, arrayLimit);
```

也就是说：

```text
Additional Lights forward upload hard limit = 8
```

管线仍然保留原有重要性排序：

```text
sort score = distance^2 / luminance
```

当可见点光 / 聚光超过 8 个时，仍按距离和亮度选择更重要的灯上传。区别是管线不再额外用 Mobile Fullscreen Budget 将数量静默压到 4。

场景灯光数量限制交给 TA 规范和检查工具处理，避免运行时预算与内容规范重复。

## Shader Variant 风险

本阶段没有新增 shader keyword：

```text
新增 global keyword: 0
新增 multi_compile: 0
新增 shader_feature_local: 0
新增 shader 文件: 0
```

Bloom 预算通过 Volume uniform / C# pass allocation 决定，不影响 shader variant 数量。

Additional Lights 上传数量变化只影响 `_AdditionalLightsCount` 和 CPU 上传数组内容，不改变 shader 数组硬上限：

```hlsl
#ifndef MAX_ADDITIONAL_LIGHTS
#define MAX_ADDITIONAL_LIGHTS 8
#endif
```

## 移动端性能影响

### Bloom

默认移动端 Profile 仍保持低带宽 baseline：

```text
maxMipCount = 4
maxBaseSize = 512
```

收益：

- Bloom pyramid RT 数量可控。
- base size 上限由 Volume 明确记录。
- TA 可以按场景区分移动端 Profile 与 lookdev Profile。

代价：

- 如果 `customize = true` 但 `maxMipCount < 6`，完整 Custom Layer compose 不执行。
- 如果需要完整分层 bloom 调色，必须显式把 `maxMipCount` 提到 6，并接受对应 RT / blit 成本。

### Additional Lights

收益：

- 管线行为更透明，不再在移动端预算开关下静默裁掉 4 个以后的灯。
- TA 场景检查与运行时上传逻辑边界清晰。

代价：

- 如果场景中允许过多点光 / 聚光，forward shader per-pixel loop 成本可能上升。
- 需要依赖 TA 规范、检查工具和真机 profile 控制灯光数量。

## 验证

已执行：

```text
dotnet build NWRP.Runtime.Tests.csproj --no-restore -v:minimal
```

结果：

```text
0 warnings
0 errors
```

已执行：

```text
dotnet build NWRP.Editor.csproj --no-restore -v:minimal
```

结果：

```text
0 errors
3 existing MSB reference conflict warnings
```

`NWRP.Editor` 的 warning 来自项目已有 Unity / NuGet assembly reference version conflict，不是本阶段新增 C# 编译错误。

尝试执行 Unity batchmode EditMode：

```text
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter NWRP.Tests.TBDRSettingsTests
```

未能启动，原因是当前项目已被另一个 Unity Editor 实例打开：

```text
It looks like another Unity instance is running with this project open.
Multiple Unity instances cannot open the same project.
```

因此本阶段完成了 C# 编译验证与静态引用检查，实际 Unity Test Runner 仍建议在当前打开的 Editor 内手动执行。

静态搜索确认 NWRP runtime / editor / tests 中不再引用旧移动预算符号：

```text
EnableMobileFullscreenBudget
MobileBloomMaxMipCount
MobileBloomMaxBaseSize
BloomMaxMipCount
BloomMaxBaseSize
MobileMaxAdditionalLights
allowLensDirtExtraCompose
```

## 新增 / 更新测试覆盖

`TBDRSettingsTests` 更新为覆盖：

- 新建 Pipeline Asset 仍保持 texture policy baseline。
- `LogFrameDebugStats` 默认关闭。
- `NWRPBloom` 默认预算为 `maxMipCount = 4`、`maxBaseSize = 512`。
- `ResolveBloomBudget(...)` 从 Bloom Volume 读取预算。
- `customize = true` 且 `maxMipCount < 6` 时不允许完整 custom compose。
- `customize = true` 且 `maxMipCount = 6` 时允许完整 custom compose。
- `lensDirtSpread` 在低 mip 预算下 clamp 到已写入 mip。
- Additional Lights upload limit 使用 renderer hard limit 8。

测试中通过反射访问部分 internal / private contract，目的是保持 runtime API 不为测试而扩大公开面。

## 手动验证清单

建议在当前打开的 Unity Editor 中验证：

1. 打开 Bloom Volume Profile，确认出现 `Max Mip Count` 和 `Max Base Size`。
2. `customize = true` 且 `Max Mip Count < 6` 时，Inspector 显示 warning。
3. `Max Mip Count = 4` 时，Frame Debugger 中 Bloom pyramid 只分配 4 层。
4. `Max Base Size = 512` 时，Bloom base RT 宽度不超过 512。
5. `Max Mip Count = 6` 且 `customize = true` 时，完整 custom compose 路径可执行。
6. `lensDirtIntensity > 0` 且低 mip 预算时，Lens Dirt 不访问未写入 mip。
7. 场景中放置超过 4 个可见点光 / 聚光时，Additional Lights 不再被移动预算裁到 4。
8. 超过 8 个可见点光 / 聚光时，仍按现有重要性排序上传最多 8 个。
9. Pipeline Asset Inspector 中不再显示 `Enable Mobile Fullscreen Budget`、`Bloom Max Mips`、`Bloom Max Base Size`、`Max Additional Lights`。
10. `Log Frame Debug Stats` 仍可输出 fullscreen / RT / copy 统计。

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

- Bloom RT peak memory。
- fullscreen blit count。
- Additional Lights count 对 forward pass GPU time 的影响。
- `customize` 高质量 Profile 对带宽的额外成本。

## 当前注意事项

- 旧 `Assets/Settings/NewWorldRP.asset` 中可能仍残留已废弃的 serialized fields；运行时不再读取，可后续保存资产或专门清理 YAML。
- `MobileBandwidthSettings` 命名目前仍保留，但内容只剩 `logFrameDebugStats`。如果后续要进一步清理 Inspector 命名，可单独迁移为 Diagnostics settings。
- Additional Lights 的移动端成本现在依赖 TA 场景规范控制；如果后续需要更强运行时保护，应做独立 lighting budget 设计，而不是恢复 fullscreen budget 总开关。
- Bloom 预算属于 Volume Profile 内容配置；移动端默认 Profile 应继续保持 `4 mips / 512 base size`。
- 本阶段没有修改 shader variant 策略，不应因 Bloom 预算下沉而新增 keyword。

## 后续方向

- 在 Unity Editor 内运行 `NWRP.Tests.TBDRSettingsTests`。
- 为 TA 场景检查工具补充 Additional Lights 数量规则。
- 视需要清理旧 Pipeline Asset YAML 中废弃的 Mobile Fullscreen Budget 字段。
- 如未来要支持平台分层 Profile，可通过 Volume Profile / Quality 配置管理 Bloom 预算，而不是恢复 Pipeline Asset 总开关。
