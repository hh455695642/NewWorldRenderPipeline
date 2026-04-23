# Phase12 额外聚光灯阴影收口与 `MultiLight / StandardLit` 整理

Date: `2026-04-23`

## 概要

这个阶段主要把当前移动端阴影基线下的两条 Lit shader 链路收平：

- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_StandardLit.shader`
- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_MultiLight.shader`

目标不是扩展新的阴影能力，而是把当前已经确定的能力收口到一致、可维护、可验证的状态：

- 主光方向光实时阴影
- 额外聚光灯实时阴影
- Lit shader 的接收与投射阴影语义一致
- 资产与 Inspector 文案明确强调“仅 additional spot light”，避免误导成“所有 additional lights 都支持实时阴影”

本阶段明确不扩展：

- 点光实时阴影
- NPR / Unlit / 其他 shader 家族的统一整改
- 额外光 receiver bias 的新资产配置

## 本阶段完成的核心修改

### 1. `MultiLight` 补齐完整阴影链路

修改文件：

- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_MultiLight.shader`

完成内容：

- 新增材质开关 `_CastShadows`
- Forward pass 补齐 `#pragma multi_compile_instancing`
- 新增 `ShadowCaster` pass
- 新增 `DepthOnly` pass
- `ShadowCaster` / `DepthOnly` 直接复用：
  - `Assets/NWRP/Shaders/Lit/Includes/ShadowCasterPass.hlsl`
  - `Assets/NWRP/Shaders/Lit/Includes/DepthOnlyPass.hlsl`
- 额外灯从旧接口：
  - `GetAdditionalLight(i, positionWS)`
  改为：
  - `GetAdditionalLight(i, positionWS, normalWS)`
- 额外聚光灯的 `shadowAttenuation` 现在会真正参与 `MultiLight` 的最终直接光照结果

这意味着 `MultiLight` 不再只是“主光有阴影、额外灯只有照明”的演示 shader，而是和当前 `StandardLit` 保持同一条 additional spot shadow 接收路径。

### 2. 共享 Blinn-Phong 光照模型收口

修改文件：

- `Assets/NWRP/ShaderLibrary/LightingModels/BlinnPhong.hlsl`

完成内容：

- 重新整理 shared Blinn-Phong helper 的注释与结构
- `EvaluateBlinnPhongAllLights(...)` 的主光不再走旧的 `GetMainLight()` 无阴影路径
- 主光改为：
  - `GetMainLight(positionWS, normalWS)`
- additional lights 统一走：
  - `GetAdditionalLight(i, positionWS, normalWS)`

这样 `MultiLight` 和 shared helper 的阴影采样语义保持一致，避免后续再把“主光无阴影的旧 helper”扩散回别的 lightweight lit shader。

### 3. 共享阴影代码整理

修改文件：

- `Assets/NWRP/ShaderLibrary/Shadows.hlsl`

本阶段保留并确认了此前已经修好的关键逻辑：

- additional spot light 的 shadow matrix 是透视投影
- receiver 采样前必须做 `shadowCoord.xyz / shadowCoord.w`

同时本阶段补充了两类整理：

- 给 additional spot shadow 的透视坐标采样增加更明确的注释
- 修正 `SampleMainLightStaticShadowAtCoord(...)` 与 `SampleMainLightDynamicShadowAtCoord(...)` 的显式初始化，消除 D3D 下的“potentially uninitialized variable” 编译 warning

这次没有新增 receiver bias 资产参数，仍然保持：

- additional light 只暴露 caster bias
- 不把 caster bias / receiver bias 语义混成一套

### 4. 运行时与资产命名统一到 “Additional Spot Light Shadows”

修改文件：

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Runtime/NWRPProfiling.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowFeature.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/AdditionalLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/AdditionalLightShadows/Passes/AdditionalLightShadowCasterPass.cs`

完成内容：

- `NewWorldRenderPipelineAsset` 里 additional light shadow 相关 tooltip 改成明确的 additional spot light 语义
- Header 改为：
  - `Additional Spot Light Shadows`
- runtime 临时 feature 名称改为：
  - `NWRP Runtime AdditionalSpotLightShadowFeature`
- profiling sample / pass label 统一改成：
  - `Additional Spot Light Shadows`
  - `Render Additional Spot Light Realtime Atlas`
- `CreateAssetMenu` 入口也改成 additional spot light 命名

这部分的重点是把“当前只支持额外聚光灯阴影”的事实直接体现在资产和运行时表述里，减少后续误解。

### 5. `NewWorldRP` Inspector 文案整理

修改文件：

- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`

完成内容：

- 重写并整理 main light / additional spot light 两块阴影设置面板
- 修复此前存在的乱码说明
- 将 additional light 阴影分组明确改成：
  - `Additional Spot Light`
- 将开关文案明确改成：
  - `Enable Additional Spot Light Shadows`
- 说明文字明确写出：
  - 当前是共享 atlas
  - 当前是小预算 realtime spot shadow
  - point light realtime shadows 仍然禁用

这部分没有新增新的 public 字段或 inspector 布局功能，只是把已有能力的说明整理清楚。

## 与已有 Spot Light 精简 Inspector 的关系

本阶段没有重新设计 `NWRPLightEditor`，但最终状态确认保持成立：

- `Inner Spot Angle` 仍然暴露
- 未消费的 Cookie / flare / render mode / per-light shadow resolution 等误导性参数仍然隐藏
- `OnSceneGUI()` 继续转发给 Unity 内建 `LightEditor`

因此：

- Spot Light 在 Scene 中的 icon / cone / range handles 不会因为简化 Inspector 而丢失
- 美术在 Inspector 里看到的项，仍然和当前 additional spot shadow 路径真实消费的参数保持一致

## 验证记录

### 编译与导入

- `dotnet build NWRP.Runtime.csproj -nologo`
  - 结果：`0 warning / 0 error`
- Unity `AssetDatabase.Refresh`
  - 本阶段范围内无新的 shader / C# error

### Shader 结果确认

Unity 内确认：

- `NewWorld/Lit/MultiLight`
  - `HasErrors = false`
  - `PassCount = 3`
  - 包含：
    - `NewWorldForward`
    - `ShadowCaster`
    - `DepthOnly`
- `NewWorld/Lit/StandardLit`
  - `HasErrors = false`
  - `PassCount = 3`

### 场景验证

使用：

- `Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity`

确认结果：

- `Ground` 当前使用 `NewWorld/Lit/StandardLit`
- `SK_Drone` 各个渲染部件当前使用 `NewWorld/Lit/StandardLit`
- `Spot Light` 当前为：
  - `LightType = Spot`
  - `Shadows = Hard`
  - `Spot Angle` 与 `Inner Spot Angle` 均正常存在
- `StandardLit` 状态下，`SK_Drone -> Ground` 的额外聚光灯阴影正常
- 临时把 `Ground + SK_Drone` 切到 `MultiLight` 做运行时预览后，主光阴影与额外聚光灯阴影均正常显示
- 预览完成后，已恢复回 `StandardLit` 材质状态

### Inspector 验证

选中场景中的 `Spot Light` 后确认：

- `Editor.CreateEditor(light)` 返回：
  - `NWRP.Editor.NWRPLightEditor`

说明当前精简 Spot Light Inspector 仍然处于生效状态。

## 本阶段的边界与遗留项

本阶段明确没有处理：

- point light realtime shadow
- `NPR / Unlit / Lambert / PBR` 的全面换行或结构整理
- 其他 lit shader 家族的 shared helper 全量统一

当前仍然保留的一个无关遗留项：

- `Assets/NWRP/Shaders/Lit/NewWorld_Lit_PBR.shader`
  - 仍有换行符一致性 warning

这个 warning 不属于本阶段收口范围，因此本阶段没有顺手修改。

## 最终结论

Phase12 的结果不是增加了一条新的复杂阴影系统，而是把当前已经在项目里推进中的 additional spot light realtime shadow 路径，真正收口成了可交付状态：

- `StandardLit` 与 `MultiLight` 的阴影链路一致
- `MultiLight` 具备完整的接收 / 投射阴影能力
- shared Blinn-Phong helper 不再保留旧的主光无阴影分叉
- runtime / asset / inspector 文案统一强调“additional spot light”
- Unity 场景、shader import、编译结果都已经完成验证

这使得当前移动端基线下的额外聚光灯阴影功能，在实现、命名、材质行为与编辑器表现上都更一致，也更适合作为后续继续演进的稳定节点。
