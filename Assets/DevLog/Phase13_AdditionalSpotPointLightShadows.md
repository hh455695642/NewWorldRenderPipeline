# Phase13 额外 Spot / Point Light 实时阴影统一与 Light Inspector 收口

Date: `2026-04-23`

## 概要

这个阶段把此前只支持 additional spot light realtime shadow 的路径，扩展成统一的 additional `Spot + Point` realtime shadow 路径，同时继续保持移动端优先的约束：

- 继续复用同一个 `AdditionalLightShadowFeature`
- 继续只保留一个 `ShadowMap` 阶段
- 继续使用一个共享的 2D shadow atlas
- 继续保持 hard shadow only
- 不新增新的 shader keyword

除运行时阴影接入外，本阶段也把 Directional / Spot / Point Light 的简化 Inspector 再收口了一次：

- `NWRPLightEditor` 现在统一处理 `Directional Light`、`Spot Light` 与 `Point Light`
- `Directional Light` 的阴影控件改成纯 `Off / On` 语义，不再显示误导性的 `Hard Shadows`
- `Spot / Point` 的阴影控件继续保持 `No Shadows / Hard Shadows`
- 历史上如果已有灯被设成 `Soft`，在该简化 Inspector 中会被自动夹回 `Hard`
- 主光的 PCF / filter 语义明确回收到 pipeline asset，不再混进单灯面板表述里

这次工作的目标不是做一条更重的多灯阴影系统，而是把当前 additional punctual light shadow 路径收成一条更一致、可验证、可演进的基线。

## 本阶段完成的核心修改

### 1. additional light shadow 从 “spot only” 扩展为 “spot + point”

修改文件：

- `Assets/NWRP/Runtime/AdditionalLightShadows/Passes/AdditionalLightShadowCasterPass.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowFeature.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/Passes/AdditionalLightShadowDisabledPass.cs`
- `Assets/NWRP/Runtime/Lighting/AdditionalLightUtils.cs`

完成内容：

- additional light shadow atlas 现在按 slice 预算统一调度：
  - `Spot Light = 1 slice`
  - `Point Light = 6 slices`
- 共享 atlas 的总 slice 数由：
  - `spotCount + pointCount * 6`
  计算
- 仍然使用方形网格布局打包 atlas，不引入 cubemap shadow texture
- Spot 继续使用：
  - `ComputeSpotShadowMatricesAndCullingPrimitives(...)`
- Point 新增使用：
  - `ComputePointShadowMatricesAndCullingPrimitives(lightIndex, face, 0f, ...)`
- point light 按 `CubemapFace` 六面顺序写入 atlas
- 灯级元数据与 slice 级元数据拆开上传：
  - per-light：`enabled / strength / firstSliceIndex / faceCount`
  - per-slice：`worldToShadow / atlasRect`
- 单个 slice 或单个灯构建失败时仅跳过对应项，不会中断整帧 additional shadow 渲染

这使得 additional punctual light shadow 仍然保持单 feature、单 atlas、低 variant 的结构，但运行时能力从 “仅聚光” 进化成了 “聚光 + 点光共用一套路径”。

### 2. pipeline asset 的预算模型拆成 Spot / Point 双预算

修改文件：

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`

完成内容：

- `AdditionalLightShadowBudgetSettings` 从单预算改成双预算：
  - `maxShadowedAdditionalSpotLights`
  - `maxShadowedAdditionalPointLights`
- 旧字段：
  - `maxShadowedAdditionalLights`
  通过 `FormerlySerializedAs` 兼容迁移到：
  - `maxShadowedAdditionalSpotLights`
- `maxShadowedAdditionalPointLights` 默认值设为 `1`
- `additionalLightShadowResolution` 保留原字段名，但语义改成：
  - 每个 shadow slice 的 tile resolution
- asset inspector 现在统一展示：
  - Spot Budget
  - Point Budget
  - 共享 tile resolution
  - 共享 shadow distance
  - 共享 caster bias
- inspector help box 里明确标注：
  - `Spot = 1 slice`
  - `Point = 6 slices`
  - 两者共用一个 atlas

这部分的重点是让预算成本模型对内容侧和后续维护都更直观，同时避免破坏已有资产序列化。

### 3. ShadowCaster pass 补齐 point light caster bias

修改文件：

- `Assets/NWRP/ShaderLibrary/Passes/ShadowCasterPass.hlsl`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`

完成内容：

- 保留 `_ShadowLightDirection` 给 Directional / Spot 使用
- 新增：
  - `_ShadowLightPosition`
  - `_ShadowLightParams`
- Point Light 的 shadow caster bias 不再错误复用固定光方向，而是在 vertex 中按：
  - `lightPos - positionWS`
  计算逐顶点 light direction 后再施加 bias
- Main light shadow 上传逻辑同步补齐这些 global，避免运行时 global 状态残留

这一步是点光阴影能够稳定工作的关键，否则 caster bias 语义仍然会停留在 directional / spot 的假设上。

### 4. receiver 端改成 per-light metadata + per-slice atlas 采样

修改文件：

- `Assets/NWRP/ShaderLibrary/Shadows.hlsl`
- `Assets/NWRP/ShaderLibrary/Lighting.hlsl`
- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_MultiLight.shader`

完成内容：

- additional shadow 的 world-to-shadow matrix 与 atlas rect 不再按“每灯一份”处理，而是按 slice 上传
- receiver 对 `Spot Light` 直接读取：
  - `firstSliceIndex`
- receiver 对 `Point Light` 根据：
  - `positionWS - lightPositionWS`
  的主轴方向选择 cubemap face，再映射到：
  - `firstSliceIndex + faceOffset`
- Spot / Point 两种 additional punctual light 都继续走：
  - `shadowCoord.xyz / shadowCoord.w`
  后的硬阴影深度比较
- 不新增新的 shader keyword，光型分流完全依赖 uniform metadata
- additional light 的位置 / 颜色 / 衰减等共享数组声明统一整理，确保 `Shadows.hlsl` 直连路径下也能访问 point shadow receiver 所需的 light position

这部分保证了 shader 侧不会因为 point shadow 接入而把 variant 规模拉高，也没有拆出第二套 receiver 分支系统。

### 5. 运行时命名与 profiling 语义更新为 Spot / Point

修改文件：

- `Assets/NWRP/Runtime/NWRPProfiling.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowFeature.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`

完成内容：

- 用户可见命名从 “Additional Spot Light Shadows” 扩展成更准确的：
  - `Additional Spot / Point Light Shadows`
- 移除旧文案里 “point light intentionally disabled” 一类描述
- runtime temporary feature name 也同步更新为 spot / point 统一语义
- profiling sample / atlas pass label 与当前真实能力保持一致

这样 profiling、资产、运行时特征名三者的口径重新统一，避免继续让 editor 文案滞后于实际实现。

### 6. `NWRPLightEditor` 扩展成 Directional / Spot / Point 共用的简化 light inspector

修改文件：

- `Assets/NWRP/Editor/NWRPLightEditor.cs`

完成内容：

- 自定义 light inspector 现在会统一接管：
  - `Directional Light`
- 自定义 light inspector 不再只接管 `Spot Light`，现在会同时接管：
  - `Spot Light`
  - `Point Light`
- 仅保留当前 NWRP main-light / local-light 路径真实消费的参数
- 其他灯型仍然回退到 Unity 默认 `LightEditor`
- Directional Light 只保留当前主光链路实际消费的项：
  - `Color`
  - `Intensity`
  - `Shadows`
  - `Shadow Strength`
  - `Shadow Near Plane`
- Spot Light 继续保留：
  - `Inner Spot Angle`
  - cone-only 的 shape 提示
- Point Light 不再暴露当前运行时未消费的误导性项：
  - cookie
  - flare
  - render mode
  - per-light shadow resolution
  - custom culling overrides
- `Culling Mask` 也从简化 inspector 中移除，因为当前 NWRP 的主光 / 额外光运行时路径都没有消费它

这部分让 directional light 与 point light 在 editor authoring 侧都和当前 runtime 能力重新对齐，不会再落回 Unity 默认 Inspector 那套更宽泛、但对当前管线并不准确的暴露方式。

### 7. Directional / Spot / Point 的阴影面板去掉无效的 Unity 默认语义

修改文件：

- `Assets/NWRP/Editor/NWRPLightEditor.cs`

完成内容：

- Directional / Spot / Point Light 的阴影控件都不再直接绘制 Unity 原生 enum
- `Directional Light` 改成仅显示：
  - `Shadows = Off / On`
- `Spot Light` 与 `Point Light` 继续仅显示：
  - `No Shadows`
  - `Hard Shadows`
- 如果已有 Directional / Spot / Point Light 的历史数据里仍然保留：
  - `LightShadows.Soft`
  在简化 Inspector 打开时会自动夹回：
  - `LightShadows.Hard`
- `Shadow Strength` 与 `Shadow Near Plane` 仍只在非 `None` 状态下显示
- Directional 的说明文案额外明确：
  - 主光 receiver filter 由 pipeline asset 的 main light shadow filter mode 控制
  - 灯本身只控制主光是否投射阴影

这一步的重点不是“隐藏一个按钮”，而是保证简化 Inspector 暴露出来的选项完全和当前 mobile-first shadow baseline 一致，避免 artist 继续误配到当前实现根本不会兑现的 soft shadow 路径，也避免在 main light 已开启 PCF 时仍然看到误导性的 `Hard Shadows` 文案。

## 验证记录

### 编译与导入

- `dotnet build NWRP.Runtime.csproj -nologo`
  - 默认输出路径因 `obj\\Debug` 下的 `NWRP.Runtime.dll` 被占用，改用临时中间目录后重新编译
  - 结果：`0 warning / 0 error`
- `dotnet build NWRP.Editor.csproj -nologo`
  - 结果：通过
  - 仍然只有原有的 3 个外部依赖冲突 warning，无新增本次改动相关 warning / error
- Unity `AssetDatabase.Refresh`
  - 本阶段范围内无新的 C# / shader import error

### Shader 与 runtime 结果确认

Unity 内确认：

- `NewWorld/Lit/StandardLit`
  - `HasErrors = false`
  - `PassCount = 3`
- `NewWorld/Lit/MultiLight`
  - `HasErrors = false`
  - `PassCount = 3`

额外光阴影全局参数中可确认混合 additional shadow metadata 已正确上传：

- 额外 `Spot Light` 条目：
  - `FaceCount = 1`
- 额外 `Point Light` 条目：
  - `FaceCount = 6`

这说明当前 atlas 上传路径已经能同时表达 spot shadow 与 point shadow，而不是停留在旧的 “每灯一矩阵” spot-only 模型上。

### 场景与 Inspector 验证

使用：

- `Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity`

确认结果：

- 临时把场景中的 `Point Light` 切到 `Hard Shadows` 后，`StandardLit` 链路下可看到 point shadow 参与 additional shadow 全局数据上传
- 同场景内已有的 additional `Spot Light` 阴影行为未回退
- Spot + Point 同时存在时，两者都能进入同一个 additional shadow atlas
- `Editor.CreateEditor(spotLight)` 返回：
  - `NWRP.Editor.NWRPLightEditor`
- `Editor.CreateEditor(pointLight)` 返回：
  - `NWRP.Editor.NWRPLightEditor`
- `Editor.CreateEditor(directionalLight)` 返回：
  - `NWRP.Editor.NWRPLightEditor`
- Directional / Spot / Point 简化 Inspector 中都不再暴露 `Soft Shadows`
- Directional Light 的阴影控件显示为：
  - `Shadows = Off / On`
  而不是 Unity 默认的：
  - `Hard Shadows`

场景验证期间对 `Point Light` 的 `Shadow Type` 只做了临时切换，验证后已恢复原值，未覆盖当前 dirty scene 的现状。

## 本阶段的边界与遗留项

本阶段明确没有处理：

- main light shadow 路径重构
- point light soft shadow
- lit shader keyword 结构调整
- additional light 的 receiver bias 公开资产配置
- `NPR / Unlit / 其他 shader 家族` 的同步接入

当前 additional punctual light shadow 仍然保持：

- 一个 feature
- 一个 shadow atlas 阶段
- hard shadow only
- 共享 tile resolution
- 移动端小预算优先

也就是说，这次并没有把系统推向更复杂的“通用多光源阴影框架”，而是用尽量少的结构增量，把现有 mobile-first 方案扩展到了 point light。

## 最终结论

Phase13 的结果不是单纯“把点光阴影打开了”，而是把 additional punctual light realtime shadow 路径完整升级成了一套统一的 `Spot + Point` 实现：

- runtime 端统一 atlas、统一预算、统一 metadata
- shader 端不增加新 keyword，仍保持 hard shadow only
- asset / profiling / inspector 的命名与真实能力重新对齐
- Directional / Spot / Point 的 authoring 面板都收口到当前管线实际支持的能力范围
- main light 的阴影过滤语义重新回到 pipeline asset，而不是继续停留在单灯 `Hard Shadows` 话术上

这样当前 NWRP 在移动端基线下的 additional realtime shadow 系统，就从 “额外聚光特判” 演进成了更稳的 “额外 punctual light shadow” 节点，后续再继续做预算、过滤或平台分层时，基座会更干净。
