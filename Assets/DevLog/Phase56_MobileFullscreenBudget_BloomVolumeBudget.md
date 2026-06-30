# Phase56 Mobile Fullscreen Budget 下沉到 Bloom Volume 预算

日期: `2026-06-30`

## 概要

本阶段把 Phase51 到 Phase55 中遗留在 Pipeline Asset 上的 `Mobile Fullscreen Budget` 总开关收口到更明确的内容质量配置边界。核心变化不是继续扩大移动端预算系统，而是拆掉一个过宽的全局预算入口，让 Bloom 的尺寸和层级预算回到 `NWRPBloom` Volume Profile，由 TA 或具体画面配置决定质量档位。

本阶段的架构目标：

- 删除 Pipeline Asset Inspector 中的 `Enable Mobile Fullscreen Budget` 总开关。
- 删除 Pipeline Asset Inspector 中的 `Bloom Max Mips`、`Bloom Max Base Size`、`Max Additional Lights`。
- `Additional Lights` 不再受移动端 fullscreen 预算裁剪，默认最多上传管线硬上限 8 个。
- Bloom 预算只新增尺寸和层级字段，不新增重复功能开关。
- `customize` 继续作为 Custom Layer Controls 的唯一入口。
- `lensDirtIntensity` 继续作为 Lens Dirt 的唯一入口。
- 不新增 shader keyword，不扩大 shader variant 组合。

本阶段继续保持 NWRP custom SRP 边界，不引入 URP `ScriptableRendererFeature` / `ScriptableRenderPass`，也不把 Bloom、额外光和诊断统计塞进同一个超级 Feature。

## 修改文件

- `Assets/NWRP/Runtime/PostProcessing/NWRPBloom.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/PostProcessPass.cs`
- `Assets/NWRP/Runtime/Lighting/AdditionalLightUtils.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/PostProcessing/NWRPBloomEditor.cs`
- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/Tests/EditMode/TBDRSettingsTests.cs`

## 问题背景

### 1. Pipeline Asset 的移动预算职责过宽

Phase53 之前的移动端低带宽 baseline 把 Bloom mip 数、Bloom base size、additional light upload limit 都挂在 `NewWorldRenderPipelineAsset.mobileBandwidth` 下，并由 `EnableMobileFullscreenBudget` 统一控制。

这在早期收口阶段比较方便，因为可以快速建立一个保守移动端默认：

```text
Bloom max mip = 4
Bloom base size = 512
Mobile max additional lights = 4
```

但后续问题也很明显：

- Bloom 是后处理画质内容，应由 Volume Profile 管理。
- Additional Lights 是场景布光规范问题，不应被 fullscreen budget 间接裁剪。
- 一个总开关同时影响 Bloom、Lens Dirt、Custom Compose、Additional Lights，职责过宽。
- 高质量 lookdev profile 很难只提高 Bloom 质量，而不绕过其它移动端预算语义。
- Pipeline Asset Inspector 中移动带宽区块混合了运行时预算和诊断统计，边界不清晰。

因此本阶段把 Bloom 预算下沉到 `NWRPBloom`，把 Additional Lights 从该预算体系里移出，只保留 `Log Frame Debug Stats` 作为 Pipeline Asset 诊断字段。

### 2. Bloom 预算应该由 Volume Profile 控制

Bloom 的开销主要来自 fullscreen RT 尺寸、pyramid 层级、blur / upsample pass 数量和 HDR RT 格式。它和画面风格、场景亮度、镜头设定、平台档位强相关，属于内容质量配置，而不是管线全局能力开关。

新的 Bloom Volume 字段为：

```csharp
public ClampedIntParameter maxMipCount = new ClampedIntParameter(4, 1, 6);
public ClampedIntParameter maxBaseSize = new ClampedIntParameter(512, 64, 4096);
```

默认值继续保持移动端保守 baseline：

```text
maxMipCount = 4
maxBaseSize = 512
```

高质量 Profile 可显式设置：

```text
maxMipCount = 6
maxBaseSize = 1024 / 2048 / 更高
customize = true
lensDirtIntensity > 0
```

这样质量档位和内容配置绑定，不再依赖 Pipeline Asset 的全局开关。

### 3. Additional Lights 不应被 fullscreen budget 间接裁剪

额外点光和聚光的上传数量现在回到 NWRP 管线硬上限：

```text
AdditionalLightUtils.MaxAdditionalLights = 8
```

本阶段移除了 `AdditionalLightUtils.GetUploadLimit(...)` 中读取 `asset.EnableMobileFullscreenBudget` 和 `asset.MobileMaxAdditionalLights` 的分支。额外光数量由 TA 规范、场景检查工具、光照 authoring 流程控制，而不是由一个 fullscreen 预算开关在运行时静默裁到 4。

保留原有重要性排序逻辑：

- 点光 / 聚光仍按 camera 到 light 的距离和 luminance 估算排序。
- 最多上传 8 个。
- 不新增 per-object / per-instance CPU lighting loop。
- 不改变额外光阴影的独立 shadow budget。

这让运行时行为更可预期：管线负责稳定上传硬上限，内容规范负责控制场景成本。

## 关键实现

### 1. `NWRPBloom` 新增预算字段

`NWRPBloom` 新增两个 Volume 参数：

```csharp
[Tooltip("Maximum bloom pyramid mips allocated by this volume profile.")]
public ClampedIntParameter maxMipCount = new ClampedIntParameter(4, 1, 6);

[Tooltip("Maximum bloom base width before the pyramid halves each mip.")]
public ClampedIntParameter maxBaseSize = new ClampedIntParameter(512, 64, 4096);
```

这两个字段只控制 Bloom pyramid 的资源预算，不作为 Bloom 功能开关。Bloom 是否 active 仍由原有逻辑决定：

```text
NWRPBloom.active
intensity > 0 或 lensDirtIntensity > 0
```

### 2. `PostProcessPass.ResolveBloomBudget(...)` 改为读取 Bloom Volume

旧路径：

```text
ResolveBloomBudget(NewWorldRenderPipelineAsset asset, requestedBaseSize)
asset.EnableMobileFullscreenBudget
asset.MobileBloomMaxMipCount
asset.MobileBloomMaxBaseSize
```

新路径：

```text
ResolveBloomBudget(NWRPBloom bloom, requestedBaseSize)
bloom.maxMipCount
bloom.maxBaseSize
```

预算解析规则：

```text
mipCount = bloom.maxMipCount
baseSize = min(requestedBaseSize, bloom.maxBaseSize)
allowCustomCompose = maxMipCount >= 6
allowLensDirtExtraCompose = true
```

注意：这里没有通过预算禁用 `customize` 或 `lensDirtIntensity`。二者仍然是对应功能的唯一控制入口。预算只决定是否存在足够的 pyramid 层级来执行完整 custom compose，以及 Lens Dirt 应该采样哪一层可用 mip。

### 3. Custom Layer Controls 的执行条件收口

完整 custom compose 仍然依赖 6 层 Bloom pyramid，因为 shader compose pass 会采样完整层级：

```text
_NWRPBloomTexture
_NWRPBloomTexture1
_NWRPBloomTexture2
_NWRPBloomTexture3
_NWRPBloomTexture4
第 5 层输入
```

新条件为：

```text
budget.allowCustomCompose
&& bloom.customize.value
&& bloom.intensity.value > 0
&& budget.mipCount == 6
```

当 `customize == true` 但 `maxMipCount < 6` 时，不执行完整 custom compose。`NWRPBloomEditor` 会显示 warning：

```text
Custom layer compose requires 6 bloom mips.
The current Bloom budget skips the full custom compose path on mobile.
```

这样可以避免在移动端低 mip 预算下访问未分配的 Bloom mip，同时不新增 keyword 或额外 shader 分支。

### 4. Lens Dirt 采样层级按可用 mip clamp

旧路径中 `lensDirtSpread` 会 clamp 到固定 `k_BloomLastMip = 5`，但当移动预算只分配 4 个 mip 时，`lensDirtSpread = 5` 可能指向不存在或本帧未写入的层级。

新路径新增：

```csharp
public static int ResolveLensDirtSourceMip(
    NWRPBloom bloom,
    BloomBudget budget)
{
    int spread = bloom != null ? bloom.lensDirtSpread.value : 0;
    return Mathf.Clamp(spread, 0, budget.lastMipIndex);
}
```

实际绑定 source texture 时再按当前预算选择：

```text
sourceMipIndex >= lastMipIndex -> 使用最后一层 down texture
否则 -> 使用对应 mip 的 up texture
```

这保持了 `lensDirtIntensity` 的控制语义，同时让低 mip 预算下的 Lens Dirt 路径安全可用。

### 5. Bloom RT 格式保持低带宽默认

Phase54 中移动预算开启时优先使用 `B10G11R11_UFloatPack32`。本阶段移除全局预算开关后，Bloom descriptor 直接优先选择低带宽 HDR 格式：

```text
支持 B10G11R11_UFloatPack32 -> 使用 B10G11R11_UFloatPack32
否则支持 R16G16B16A16_SFloat -> 使用 R16G16B16A16_SFloat
否则 -> SystemInfo.GetGraphicsFormat(DefaultFormat.HDR)
```

该策略不依赖 Pipeline Asset 开关。对移动端 Bloom pyramid 来说，默认优先低带宽格式仍然是更合理的 baseline。

### 6. Pipeline Asset 只保留诊断入口

`NewWorldRenderPipelineAsset.MobileBandwidthSettings` 中移除运行时预算字段：

```text
enableMobileFullscreenBudget
bloomMaxMipCount
bloomMaxBaseSize
maxAdditionalLights
```

保留：

```text
logFrameDebugStats
```

`NewWorldRenderPipelineAssetEditor` 中对应移除：

```text
Enable Mobile Fullscreen Budget
Bloom Max Mips
Bloom Max Base Size
Max Additional Lights
```

Inspector 区块调整为：

```text
Diagnostics / Frame Debug
Log Frame Debug Stats
移动端带宽风险提示
```

移动端风险提示仍保留，因为 HDR、post-processing、render scale、forced camera texture、Medium PCF、additional shadows 等仍然需要在 Pipeline Asset 层提醒。

## 行为结果

### 默认 Bloom Profile

默认 `NWRPBloom` 解析结果：

```text
mipCount = 4
baseSize <= 512
allowCustomCompose = false
allowLensDirtExtraCompose = true
```

这保持移动端保守基线，不因为删除 Pipeline Asset 总开关而默认升高 Bloom RT 成本。

### 高质量 Bloom Profile

当 TA 或 LookDev 配置：

```text
maxMipCount = 6
maxBaseSize = 2048
```

解析结果：

```text
mipCount = 6
baseSize <= 2048
allowCustomCompose = true
allowLensDirtExtraCompose = true
```

此时如果 `customize == true` 且 Bloom intensity 有效，会执行完整 custom compose。

### Custom Compose 在低预算下跳过

当：

```text
customize = true
maxMipCount = 4
```

结果：

```text
完整 custom compose 不执行
Bloom Editor 显示预算 warning
基础 Bloom pyramid / final composite 仍执行
```

该行为避免了低 mip 预算访问未分配 RT，同时保持 `customize` 的配置入口不变。

### Lens Dirt 在低预算下安全采样

当：

```text
maxMipCount = 4
lensDirtSpread = 5
```

结果：

```text
lensDirt source mip clamp 到 3
不会访问第 5 层
Lens Dirt 仍由 lensDirtIntensity 控制
```

### Additional Lights

额外点光 / 聚光上传上限现在为：

```text
min(AdditionalLightUtils.MaxAdditionalLights, uploadArrayLength)
```

默认管线硬上限为：

```text
8
```

不再受 Bloom Volume、Pipeline Asset mobile bandwidth 或 fullscreen budget 影响。

## 性能与移动端策略

### CPU

- 没有新增 per-object / per-instance CPU loop。
- Additional Lights 仍沿用已有 visible light 收集和重要性插入排序。
- Bloom budget 解析只读取当前 Volume 参数，成本固定且很低。
- Inspector warning 只在 Editor 绘制时发生，不影响 runtime。
- 没有新增复杂调试系统。

### GPU / 带宽

- 默认 Bloom 仍限制为 4 mip 和 512 base size。
- Bloom RT 格式继续优先低带宽 HDR 格式。
- 高质量 Bloom 必须由 Volume Profile 显式提高预算。
- Lens Dirt 不再因为 spread 超出可用 mip 而访问无效层级。
- Additional Lights 回到 8 个上传硬上限，实际场景成本由内容规范和检查工具控制。
- 不新增 fullscreen pass，不新增 MRT，不新增 depth / opaque copy。

### Tile-Based GPU 取舍

本阶段不是继续压低所有场景默认效果，而是把预算责任移动到更准确的位置：

```text
Bloom RT 尺寸和层级 -> Volume Profile
额外灯光数量 -> TA 规范和检查工具
Frame Debug 统计 -> Pipeline Asset Diagnostics
```

这样可以避免一个 Pipeline Asset 总开关在运行时同时影响多个互不相关系统，后续也更容易按平台档位做 Volume Profile 组合。

## Shader Variant 影响

本阶段没有修改 shader keyword：

```text
新增 multi_compile: 0
新增 shader_feature_local: 0
新增全局 keyword: 0
新增材质 shader: 0
```

Bloom 预算、Custom Compose 条件、Lens Dirt mip clamp、Additional Lights 上限都发生在 C# runtime / editor 层，不增加移动端 shader variant 风险。

## 测试与验证

更新 `TBDRSettingsTests`：

- 不再断言 Pipeline Asset 的 `EnableMobileFullscreenBudget`。
- 不再断言 Pipeline Asset 的 `MobileBloomMaxMipCount`。
- 不再断言 Pipeline Asset 的 `MobileBloomMaxBaseSize`。
- 不再断言 Pipeline Asset 的 `MobileMaxAdditionalLights`。
- 保留 `LogFrameDebugStats` 默认关闭断言。

新增或调整的关键覆盖：

- `BloomBudget_DefaultVolumeUsesMobileBaseline`
- `BloomBudget_HighQualityVolumeAllowsFullPyramid`
- `BloomBudget_CustomComposeRequiresFullMipBudget`
- `BloomBudget_LensDirtSpreadClampsToAvailableMip`
- `BloomDescriptor_UsesMobileLowBandwidthFormatByDefault`
- `AdditionalLights_UploadLimitUsesPipelineHardCap`

已执行 Unity EditMode 验证：

```text
testMode: EditMode
testAssembly: NWRP.Tests.EditMode
Status: Passed
FailedTests: 0
```

Unity MCP 返回的结果列表中，相关 `TBDRSettingsTests` 全部通过；本次运行结果显示 34 个测试项通过，失败为 0。

## 兼容性与迁移说明

### 运行时兼容

旧 Pipeline Asset 中的移动预算字段不再被 runtime 读取。也就是说：

```text
旧 enableMobileFullscreenBudget 不再影响 Bloom
旧 bloomMaxMipCount 不再影响 Bloom
旧 bloomMaxBaseSize 不再影响 Bloom
旧 maxAdditionalLights 不再影响 Additional Lights
```

Bloom 的默认 Volume 字段已经保持移动端基线，因此删除运行时读取不会让默认 Bloom 突然升到完整 6 mip。

### YAML 清理

旧 Pipeline Asset YAML 中可能仍残留历史字段：

```text
enableMobileFullscreenBudget
bloomMaxMipCount
bloomMaxBaseSize
maxAdditionalLights
```

本阶段不做批量 YAML 清理，避免把行为重构和资产迁移混在同一次变更里。后续可单独做一次资产序列化清理，确认所有 Profile 已迁移到 `NWRPBloom.maxMipCount` / `NWRPBloom.maxBaseSize` 后再处理。

### 内容配置建议

移动端默认 Profile：

```text
maxMipCount = 4
maxBaseSize = 512
customize = false
lensDirtIntensity = 0 或低强度
```

高质量或截图 Profile：

```text
maxMipCount = 6
maxBaseSize = 1024 / 2048
customize = 按需开启
lensDirtIntensity = 按需开启
```

额外光建议：

```text
场景实际灯光数量由 TA 规范控制
检查工具标记超预算场景
运行时最多上传 8 个 punctual lights
不要依赖管线静默裁剪到 4
```

## 后续方向

1. 单独做一次 Pipeline Asset YAML 清理，移除历史移动预算字段残留。
2. 给 Volume Profile 增加项目侧质量档位模板，例如 Mobile Low / Mobile High / LookDev。
3. 在 Frame Debugger 中验证不同 Bloom Profile 下的 RT 数量和尺寸。
4. 在真机上记录 Bloom pyramid 的 GPU time、RT peak memory 和 external bandwidth。
5. 如果 Lens Dirt 在低 mip 预算下视觉过糊，可提供 TA 配置建议，而不是新增 shader keyword。
6. 如果额外光数量超标，应优先通过场景检查工具和 authoring 规范处理，而不是重新引入运行时移动预算分支。

Phase56 的价值是把移动端预算从“Pipeline Asset 全局总开关”收口为“功能自己拥有的质量预算”。Bloom 的尺寸和层级跟随 Volume Profile，额外光回到稳定管线硬上限，诊断统计留在 Pipeline Asset。这样既保留移动端默认保守成本，又减少跨系统隐藏耦合。
