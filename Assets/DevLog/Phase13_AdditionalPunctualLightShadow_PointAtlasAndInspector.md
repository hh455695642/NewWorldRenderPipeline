# Phase13 Additional Punctual Light Shadow 点光源阴影与面板收口

Date: `2026-04-24`

## 概要

本阶段把上一阶段的 additional spot shadow 扩展为 additional punctual shadow，目标是支持点光源实时阴影，同时保持移动端预算可控。

完成后的能力边界：

- `Directional / Spot / Point` 灯光 Inspector 统一为 NWRP 简化面板。
- 每盏灯的 `Shadow Type` 在 Inspector 中只保留 `ON / OFF` 语义。
- Main Light 的 `Hard / MediumPCF` 继续由 NWRP asset 统一控制。
- Additional punctual light 当前仍为 hard shadow-only。
- Additional shadow atlas 改为 slice atlas：`Spot = 1 slice`，`Point = 6 slices`。

## 核心实现

### 1. 灯光 Inspector 收敛

修改文件：

- `Assets/NWRP/Editor/NWRPLightEditor.cs`

完成内容：

- 简化面板从只支持 `Spot` 扩展到 `Directional / Spot / Point`。
- `Directional` 保留 `Type / Color / Intensity / Culling Mask / Shadow ON/OFF / Strength / Near Plane`。
- `Spot` 额外保留 `Range / Spot Angle / Inner Spot Angle`。
- `Point` 额外保留 `Range`。
- Cookie、Flare、Halo、Render Mode、Rendering Layers、per-light shadow resolution、per-light bias、per-light Hard/Soft 等当前 NWRP 不消费的字段继续隐藏。
- 已存量 `LightShadows.Soft` 在简化 Inspector 中会被规范化为 `LightShadows.Hard`，语义上表示“启用实时阴影”。

### 2. Additional punctual shadow slice atlas

修改文件：

- `Assets/NWRP/Runtime/AdditionalLightShadows/Passes/AdditionalLightShadowCasterPass.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/Lighting/AdditionalLightUtils.cs`
- `Assets/NWRP/ShaderLibrary/Shadows.hlsl`

完成内容：

- Additional shadow pass 继续作为独立 `NWRPFeature + NWRPPass`，事件保持在 `NWRPPassEvent.ShadowMap`。
- 候选灯固定为 `Spot / Point`，并要求启用阴影、有有效 shadow strength、存在 shadow caster bounds、在 additional shadow distance 范围内。
- 选择排序为距离相机近优先，同距离下 `Spot` 优先于 `Point`，再按 `visibleLightIndex` 稳定排序。
- `maxShadowedAdditionalLights` 仍表示最多参与阴影的灯数量，不表示 slice 数量。
- `Spot` 继续使用 `ComputeSpotShadowMatricesAndCullingPrimitives`。
- `Point` 使用 6 个 cubemap face slice，并通过 `ComputePointShadowMatricesAndCullingPrimitives` 渲染。
- 点光源 caster bias 使用 depth bias，normal bias 强制为 `0`，避免移动端点光阴影的法线偏移复杂度继续扩大。

### 3. Uniform 数组与移动端预算优化

本阶段检查发现最主要的优化点是 slice 数组上限：

- Forward additional light 上限仍为 `MaxAdditionalLights = 8`。
- Realtime additional shadow caster 预算上限集中为 `MaxShadowedAdditionalLights = 4`。
- `MaxAdditionalLightShadowSlices` 从 `8 * 6 = 48` 收敛为 `4 * 6 = 24`。

这会减少：

- C# 每帧上传的 shadow matrix / atlas rect 数组长度。
- Fragment shader 中声明的 additional shadow slice uniform 数量。
- 移动端 uniform 压力和无效数据带宽。

同时为避免 Unity global array 旧长度缓存，本阶段已经把 slice 数组 shader global 改为新名称：

- `_AdditionalLightsShadowSliceWorldToShadow`
- `_AdditionalLightsShadowSliceAtlasRects`

### 4. Asset 与 Inspector 语义

修改文件：

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/Settings/NewWorldRP.asset`

完成内容：

- 新增 `additionalLightShadowAtlasMaxSize`，作为整张 additional punctual shadow atlas 的最大移动端纹理预算。
- `additionalLightShadowResolution` 明确为 requested per-slice resolution。
- 当前 `Assets/Settings/NewWorldRP.asset` 设置为：
  - `maxShadowedAdditionalLights = 3`
  - `additionalLightShadowResolution = 512`
  - `additionalLightShadowAtlasMaxSize = 2048`
- 这个配置用于覆盖当前测试场景中的 `2 Point + 1 Spot`，共 `13 slices`。
- Inspector 说明中明确：
  - `Requested Tile Resolution` 控制单个 slice 的目标清晰度。
  - `Atlas Max Size` 控制整张 atlas 的移动端显存和尺寸上限。

## Shader Variant 与移动端风险

本阶段没有新增 shader keyword。

Variant 风险：

- 无新增 `multi_compile`。
- Additional punctual shadow 的开关、强度、类型和 first slice index 均通过 uniform 控制。
- 点光源阴影接收端通过 `shadowParams.z` 判断 `Spot / Point`，不会引入 shader variant 组合爆炸。

移动端成本说明：

- 点光源阴影一盏灯固定消耗 6 次 shadow face draw。
- 当前预算上限为 4 盏 additional shadow lights，但实际项目资产设置为 3。
- 多点光源同时开启时，建议优先降低 `Max Shadowed Punctual Lights` 或 `Requested Tile Resolution`，而不是盲目提高 atlas 上限。

## 验证记录

静态检查：

- `git diff --check`
  - 结果：无 whitespace error。
- `dotnet build NewWorldRenderPipeline_Codex.sln`
  - 结果：`0 errors`。
  - 剩余 warning 为项目已有程序集引用冲突，非本阶段改动引入。

Unity 验证：

- `AssetDatabase.Refresh`
  - NWRP 范围内无新增 C# / shader error。
  - Console 中仍有 `Assets/URPShaderCodeSample` 的 `ShiftTangent` 重定义错误，属于无关遗留项。

运行时确认：

- 磁盘上的 `Assets/Settings/NewWorldRP.asset` 保持为：
  - `EnableAdditionalLightShadows = True`
  - `MaxShadowedAdditionalLights = 3`
  - `AdditionalLightShadowResolution = 512`
  - `AdditionalLightShadowAtlasMaxSize = 2048`
- 当前打开的 Unity Editor 中，该 asset 处于 dirty 状态，内存里的 `AdditionalLightShadowResolution` 临时值为 `1024`；本阶段未保存这个编辑器内存覆盖值。
- 当前打开场景中可发现：
  - `2` 个开启阴影的 point light
  - `1` 个开启阴影的 spot light

## 当前限制与后续方向

当前仍保留的限制：

- Additional punctual light shadows 当前 hard-only。
- 未引入 StructuredBuffer，仍使用固定数组以保持移动端兼容和复杂度可控。
- Point light shadow 没有单独的 asset 级 soft / hard 过滤切换。
- 当前没有新增 shadow atlas debug overlay；建议继续用 Frame Debugger / RenderDoc 观察 atlas slice。

后续可选方向：

- 在 asset 中增加 additional punctual shadow 的调试视图，但应避免 fullscreen blit。
- 在 Inspector 中把 `Atlas Max Size` 收入高级区域，减少普通调参误解。
- 若后续 additional shadow 数量继续增加，再评估 StructuredBuffer 或 cluster-based shadow indexing。
