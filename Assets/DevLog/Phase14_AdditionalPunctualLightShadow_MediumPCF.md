# Phase14 Additional Punctual Light Shadow Medium PCF

Date: `2026-04-24`

## 概要

本阶段为 additional punctual light shadows 增加统一的接收端软阴影控制，覆盖 `Spot` 与 `Point`，但不改变 shadow caster atlas 渲染流程。

完成后的能力边界：

- Additional punctual light shadows 支持 `Hard / MediumPCF`。
- 软阴影由 NWRP asset 统一控制，不使用 per-light `LightShadows.Soft`。
- `MediumPCF` 是固定 3x3 tent PCF，不引入 PCSS / EVSM / VSM blur。
- 不新增 `RenderPass`、不新增中间 RT、不新增 shader keyword。
- 点光源仍使用 6 个 cubemap face slice，PCF 只在当前 face slice 内过滤，不跨 face 采样。

## 核心实现

### 1. Asset 设置

修改文件：

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`

完成内容：

- 新增 `AdditionalLightShadowFilterMode`：
  - `Hard`
  - `MediumPCF`
- 新增 `AdditionalLightShadowFilterSettings`：
  - `additionalLightShadowFilterMode`
  - `additionalLightShadowFilterRadius`
- `additionalLightShadowFilterRadius` 默认 `1.0`，Inspector / `OnValidate` 限制在 `0.5 - 1.5`。
- 新增 runtime 只读属性：
  - `AdditionalLightShadowFilterModeSetting`
  - `AdditionalLightShadowFilterRadius`

设计取舍：

- 主光与 additional punctual light 的 filter mode 分离，避免点光 / 聚光软阴影拖高主光配置成本。
- 软阴影开关走 uniform，不新增 shader variant。

### 2. Inspector 接入

修改文件：

- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/NWRP/Editor/NWRPLightEditor.cs`

完成内容：

- 在 NWRP asset 面板的 `Shadow Settings / Additional Punctual Light / Filter` 中显示：
  - `Shadow Filter Mode`
  - `Shadow Filter Radius`，仅在 `MediumPCF` 时显示
- 更新 light inspector 文案，移除 additional punctual shadow hard-only 的旧说明。
- 保持 per-light inspector 只暴露 ON / OFF 语义，避免 per-light soft/hard 组合扩散。

### 3. Runtime global 上传

修改文件：

- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowPassUtils.cs`

完成内容：

- 新增 shader globals：
  - `_AdditionalLightsShadowFilterMode`
  - `_AdditionalLightsShadowFilterRadius`
- disabled path 上传 `Hard / 0`。
- active path 从 NWRP asset 上传 filter mode 和 radius。

RenderPass 影响：

- `AdditionalLightShadowFeature` 不变。
- `AdditionalLightShadowCasterPass` 仍在 `NWRPPassEvent.ShadowMap`。
- 没有新增 shadow pass / fullscreen pass / blit。

### 4. Shader 采样路径

修改文件：

- `Assets/NWRP/ShaderLibrary/Shadows.hlsl`

完成内容：

- 新增 additional punctual shadow filter 常量：
  - `NWRP_ADDITIONAL_LIGHT_SHADOW_FILTER_HARD`
  - `NWRP_ADDITIONAL_LIGHT_SHADOW_FILTER_MEDIUM_PCF`
- 新增 `SampleAdditionalLightShadowTextureMediumTent`。
- `MediumPCF` 使用 3x3 tent 权重：
  - `1 2 1`
  - `2 4 2`
  - `1 2 1`
- 所有 PCF tap 都通过 `ClampAdditionalLightShadowSampleUV` 限制在当前 atlas slice 内，避免 tile bleed。
- `SampleAdditionalLightShadowInternal` 中 hard / soft 互斥采样，soft 模式不额外执行 hard compare。

本次代码检查顺带优化：

- 主光 `SampleMainLightStaticShadowAtCoord` / `SampleMainLightDynamicShadowAtCoord` 原先在 `MediumPCF` 下会先做 1 次 hard compare，再覆盖为 9-tap PCF。
- 已改为 hard / soft 互斥返回，主光 `MediumPCF` 每次接收端采样减少 1 次 shadow compare。

## Shader Variant 与移动端成本

Variant 风险：

- 无新增 `multi_compile`。
- 无新增 `shader_feature_local`。
- Additional punctual shadow filter mode 由 uniform 控制，variant 数量不变。

GPU 成本：

- `Hard`：每个 shadowed punctual light receiver sample 为 1 次 shadow compare。
- `MediumPCF`：每个 shadowed punctual light receiver sample 为 9 次 shadow compare。
- 点光源 caster 成本不变，仍为每盏点光 6 个 shadow face draw。
- 软阴影主要增加 receiver fragment 采样成本，移动端建议控制 `Max Shadowed Punctual Lights` 在 `1 - 2` 后再开启。

带宽 / Tile GPU：

- 不新增 RT。
- 不新增 blit。
- 不新增 MRT。
- PCF 只读已有 additional shadow atlas，避免中途 tile flush 型流程变化。

## 验证记录

静态检查：

- `git diff --check`
  - 无 whitespace error。
- Unity `SerializedObject` 检查：
  - `additionalLightShadows.filter` 可被序列化系统找到。
  - 当前默认值为 `mode = Hard`，`radius = 1`。

Unity 验证：

- `AssetDatabase.Refresh`
  - 无新增 C# / NWRP shader error。
- Console 剩余 error 来自既有 URP sample shader：
  - `Assets/URPShaderCodeSample/Shaders/Effect/FurShell.shader` 的 `ShiftTangent` 重定义。
  - `Lakehani/URP/Lighting/Anisotropic` 的 `ShiftTangent` 重定义。
  - 与本阶段 NWRP 改动无关。
- Console 剩余 warning 来自既有 shader 行尾问题：
  - `Assets/NWRP/Shaders/Lit/NewWorld_Lit_PBR.shader`
  - `Assets/NWRP/Shaders/Lit/NewWorld_Lit_MultiLight.shader`
  - 本阶段已归一化 `Assets/NWRP/ShaderLibrary/Shadows.hlsl` 的行尾，避免新增该类 warning。

## 当前限制与后续方向

当前限制：

- Additional punctual soft shadow 是固定半径 PCF，不做随距离变化的 contact hardening。
- 点光源 PCF 不跨 cubemap face 过滤，face seam 仍需要通过 bias、tile resolution 和内容约束控制。
- 没有新增 atlas debug overlay，仍建议用 Frame Debugger / RenderDoc 查看 slice。

后续可选方向：

- 增加 lightweight debug view：显示 selected shadow light count / slice count，不新增 fullscreen pass。
- 为 additional punctual shadow 增加平台 override，例如低端 Android 强制 `Hard`。
- 若后续 punctual shadow 数量继续增长，再评估 cluster / StructuredBuffer shadow indexing。
