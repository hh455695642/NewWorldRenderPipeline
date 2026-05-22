# Phase36 NWRP Layout and AGENTS Refactor

日期：`2026-05-22`

## 概要

本阶段围绕 `Assets/NWRP` 的文件组织、资源命名和代理规则文档做一次中等规模整理。

目标不是重写渲染路径，也不是拆分 `NWRPRenderer` 或 `NewWorldRenderPipelineAsset` 的行为逻辑，而是在 Phase5 建立的 `NWRPFeature / NWRPPass` 扩展模型之上，把已经稳定下来的功能归属重新摆正：

- `Runtime` 根目录只保留管线核心类型和全局调度类型。
- 具体功能系统进入各自领域目录。
- NWRP 自有运行时系统不再放在 `Plugins` 语义目录下。
- `Editor` 不再把所有面板脚本平铺在根目录，而是按管线、Shader、后处理、光照和相机分组。
- 测试资源修正低风险拼写问题，避免继续传播错误目录名。
- `AGENTS.md` 保持根文档加两个局部文档的结构，补充目录归属和命名规则，避免规则漂移。

本阶段刻意保持低行为风险：

- 不重命名公开 C# 类型。
- 不重命名 enum、serialized field、shader name、LightMode tag、pass name。
- 不改变 `RenderPassEvent`。
- 不新增 shader keyword。
- 不改 shader variant 策略。
- 移动 Unity 资产时保留 `.meta` GUID，避免场景和序列化引用丢失。

## 参考背景

本阶段延续以下 DevLog 中已经建立的边界：

- `Phase5_PassFeatureFramework.md`：NWRP 功能应保持 `NWRPFeature / NWRPPass` 的可插拔结构。
- `Phase17_VegetationIndirectNWRPMigration.md`：植被 indirect renderer 已经是 NWRP 运行时体系的一部分，不应继续表现为外部插件。
- `Phase24_URPCompatibleNamingAndDependencyClosure.md`：`Assets/NWRP` 内部运行时代码和 NWRP-owned shader 不应依赖 URP package source。
- `Phase32_VegetationIndirectTreeShadows.md`：大规模植被与阴影路径需要保持 GPU-driven 和明确 feature 边界。
- `Phase35_ValleyHeightFog_PostTransparentFeature.md`：后处理类功能继续通过显式 feature 和 pass 插入，不回写主渲染流程为特判逻辑。

## 修改范围

### Runtime

相机纹理功能归入 `CameraTextures`：

- `Assets/NWRP/Runtime/DepthTextureFeature.cs`
  -> `Assets/NWRP/Runtime/CameraTextures/DepthTextureFeature.cs`
- `Assets/NWRP/Runtime/OpaqueTextureFeature.cs`
  -> `Assets/NWRP/Runtime/CameraTextures/OpaqueTextureFeature.cs`

雾功能归入 `Fog`：

- `Assets/NWRP/Runtime/NWRPFogFeature.cs`
  -> `Assets/NWRP/Runtime/Fog/NWRPFogFeature.cs`
- `Assets/NWRP/Runtime/Passes/SetupFogPass.cs`
  -> `Assets/NWRP/Runtime/Fog/Passes/SetupFogPass.cs`

描边功能归入 `Outlines`：

- `Assets/NWRP/Runtime/OutlineFeature.cs`
  -> `Assets/NWRP/Runtime/Outlines/OutlineFeature.cs`
- `Assets/NWRP/Runtime/Passes/DrawOutlinePass.cs`
  -> `Assets/NWRP/Runtime/Outlines/Passes/DrawOutlinePass.cs`

植被 indirect renderer 归入 NWRP runtime：

- `Assets/NWRP/Plugins/VegetationGPUInstancer/VegetationIndirectRenderer.cs`
  -> `Assets/NWRP/Runtime/VegetationIndirectRendering/VegetationIndirectRenderer.cs`

本阶段没有改变这些类型的 namespace、类名、serialized field 或运行时行为。

### Shaders

植被 compute shader 从 plugin-style 目录迁入 NWRP shader 体系：

- `Assets/NWRP/Plugins/VegetationGPUInstancer/VegetationCulling.compute`
  -> `Assets/NWRP/Shaders/Compute/Vegetation/VegetationCulling.compute`

该移动只改变文件归属，不改变 compute kernel、buffer 结构或 shader keyword。

### Editor

`Assets/NWRP/Editor` 按职责拆分为：

- `Cameras`
- `Lighting`
- `Pipeline`
- `PostProcessing`
- `Shaders`

移动的 editor 脚本包括：

- `NWRPCameraDataAutoAdd.cs`
- `NWRPLightEditor.cs`
- `NewWorldRenderPipelineAssetEditor.cs`
- `NWRPAntiAliasingEditor.cs`
- `NWRPBloomEditor.cs`
- `NWRPColorAdjustmentsEditor.cs`
- `NWRPFogEditor.cs`
- `NWRPTonemappingEditor.cs`
- `NWRPValleyHeightFogEditor.cs`
- `NWRPVignetteEditor.cs`
- `NewWorldShaderGUI.cs`
- `ShaderGraphCodeShaderMigrator.cs`

保持 `NWRP.Editor` namespace 和 shader `CustomEditor` 字符串不变，因此材质 Inspector 和自定义面板的绑定关系不应变化。

### Tests / Sample Assets

测试 mesh 资源中的拼写错误目录统一为 `Materials`：

- `Assets/NWRP/Tests/Meshes/Flower/Materical`
  -> `Assets/NWRP/Tests/Meshes/Flower/Materials`
- `Assets/NWRP/Tests/Meshes/Grass/Materical`
  -> `Assets/NWRP/Tests/Meshes/Grass/Materials`
- `Assets/NWRP/Tests/Meshes/Shrub/Materical`
  -> `Assets/NWRP/Tests/Meshes/Shrub/Materials`
- `Assets/NWRP/Tests/Meshes/Tree/Materical`
  -> `Assets/NWRP/Tests/Meshes/Tree/Materials`

MaterialSampleScene 的 Volume Profile 文件名修正为：

- `Assets/NWRP/Tests/Scenes/MaterialSampleScene/NWRP Volime Profile.asset`
  -> `Assets/NWRP/Tests/Scenes/MaterialSampleScene/NWRP Volume Profile.asset`

同时将该 asset 内部 `m_Name` 修正为 `NWRP Volume Profile`。

本阶段没有批量改 `p_Common_*` prefab 或 scene instance 名称，避免无意义触碰大量 scene YAML。

## AGENTS 规则更新

本阶段继续保持 3 个 `AGENTS.md`：

- `AGENTS.md`
- `Assets/NWRP/Runtime/AGENTS.md`
- `Assets/NWRP/ShaderLibrary/AGENTS.md`

根文档新增目录和命名规则：

- `Runtime` 根目录只放 pipeline core 类型。
- 具体 runtime feature 放入 `Runtime/<FeatureArea>`。
- NWRP-owned runtime system 不放入 `Assets/NWRP/Plugins`。
- compute shader 放入 `Assets/NWRP/Shaders/Compute/<Domain>`。
- editor tooling 按 domain 分组。
- 新测试资源避免 typo、空格和 parenthesized variant 等命名噪声。

`Runtime/AGENTS.md` 聚焦本地约束：

- feature implementation 放入领域目录。
- feature-owned pass 优先放在 feature 本地 `Passes` 子目录。
- GPU-driven renderer integration 应通过明确 provider / registry 接口接入，不把 renderer-specific loop 写回 shadow 或 camera pass。

`ShaderLibrary/AGENTS.md` 聚焦 include 边界：

- material-facing `.shader` 继续放在 `Assets/NWRP/Shaders`。
- `.compute` 放在 `Assets/NWRP/Shaders/Compute`。
- shared include 使用 NWRP-owned name，只有迁移收益明确时才保留 URP-compatible alias。

## 架构影响

本阶段是文件组织层面的重构，不改变渲染顺序。

Pass 顺序仍然遵守：

```text
BeforeShadowMap
ShadowMap
BeforeDepthPrepass
DepthPrepass
BeforeOpaque
Opaque
Skybox
BeforeTransparent
Transparent
AfterTransparent
PostProcess
DebugOverlay
```

本阶段也没有把 feature 逻辑合并回 `NWRPRenderer` 或 `CameraRenderer`。已有功能仍由各自 `NWRPFeature` enqueue 对应 pass。

## 性能与移动端影响

CPU：

- 无新增 per-frame 逻辑。
- 无新增 culling、renderer list 或反射路径。
- 文件移动不改变运行时调度。

GPU：

- 无新增 RenderPass。
- 无新增 RenderTexture。
- 无新增 fullscreen blit。
- 无新增 compute dispatch。
- 无新增 shader keyword 或 variant 组合。

因此本阶段对 Android / iOS 运行时性能应为零行为影响。主要收益是降低后续维护成本，让 feature 边界、shader 资源归属和文档规则更一致。

## Unity 序列化与 GUID

本阶段所有移动和重命名均保留 `.meta` GUID。

关键 GUID 已验证：

- `DepthTextureFeature.cs`：`43f46d40fd9d43388024a8cb7eece1ab`
- `OpaqueTextureFeature.cs`：`e2d98b06ccc3bc347a1211a5baa9f2fa`
- `NWRPFogFeature.cs`：`1528bed8222acc0479ed7079e62164c1`
- `SetupFogPass.cs`：`c7cef8fd359379543a20e1a98b200d3b`
- `OutlineFeature.cs`：`b06b23b8fb814d54a87a95e9cc3f8f92`
- `DrawOutlinePass.cs`：`8779cc2be0fd4f9c8b5af4cfc4f0d7ab`
- `VegetationIndirectRenderer.cs`：`50687cb27b0c84d4d9caf3175a8efe5c`
- `VegetationCulling.compute`：`a98125b7291de38408ddc24df2ababdd`
- `NWRP Volume Profile.asset`：`616b2d9995a303642a4636fa6fc9be1b`

额外检查结果：

- `Assets/NWRP` 下没有重复 `.meta` GUID。
- 移动后的 C#、editor 和 compute 文件内容与移动前 HEAD blob 一致。
- 移动后的类型均可从 Unity domain 中解析。

## 验证记录

Unity MCP EditMode 测试：

```text
NWRP.EditModeTests
TotalTests: 11
PassedTests: 11
FailedTests: 0
SkippedTests: 0
```

覆盖到的 `ValleyHeightFogVolumeTests`：

- `AssetEditorCreatesAndAddsValleyHeightFogFeatureSubAsset`
- `FrameDataExposesValleyHeightFogState`
- `PipelineAssetDoesNotExposeValleyHeightFogToggle`
- `ValleyHeightFogFeatureDoesNotRequestTargetsWhenInactive`
- `ValleyHeightFogFeatureRequestsColorAndDepthWhenActive`
- `ValleyHeightFogPassRunsAfterTransparent`
- `ValleyHeightFogPassSelectsShaderPassFromVolumeMode`
- `ValleyHeightFogShaderUsesNwrpAlgorithmPathOnly`
- `VolumeEnableIsRuntimeActivationSwitch`
- `VolumeOwnsThreeLayerFogParametersAndDefaults`
- `VolumeOwnsUrpHeightFogParametersAndDefaults`

Unity 侧动态校验：

- `NWRP.DepthTextureFeature` 可解析。
- `NWRP.OpaqueTextureFeature` 可解析。
- `NWRP.OutlineFeature` 可解析。
- `NWRP.NWRPFogFeature` 可解析。
- `NWRP.Runtime.Passes.DrawOutlinePass` 可解析。
- `NWRP.Runtime.Passes.SetupFogPass` 可解析。
- `VegetationIndirectRenderer` 可解析。
- 新路径下的 script / compute / profile asset 均可通过 `AssetDatabase.LoadAssetAtPath` 读取。
- 当前加载的 `MaterialSampleScene` 无 missing script、missing material、missing shader。
- Unity Console 最近错误列表为空。

静态搜索：

- 未发现 `Materical` / `Volime` 残留。
- 未发现旧 `Assets/NWRP/Plugins/VegetationGPUInstancer` 路径残留。
- 未发现移动前 runtime feature / pass 路径残留。
- 排除 `AGENTS.md` 后，`Assets/NWRP` 内未发现：
  - `UnityEngine.Rendering.Universal`
  - `ScriptableRendererFeature`
  - `ScriptableRenderPass`
  - `Packages/com.unity.render-pipelines.universal`

## 当前现场注意

`Assets/Settings/NewWorldRP.asset` 在本阶段验证过程中由 Unity Inspector / 场景保存产生了资产序列化变化：

- `enableRenderScale` 当前为 `1`。
- feature list 中原有 null entry 被清理。
- `m_EditorClassIdentifier` 空值出现 Unity YAML 格式化差异。

该变化不属于本阶段目录重构的核心目标，但当前 asset 可正常加载为 `NewWorldRenderPipelineAsset`，feature list 无 null entry，`ValleyHeightFogFeature` sub-asset 可见且启用。

提交前需要按项目当前需求决定是否保留这部分 asset 序列化变化。

## 后续建议

- 后续新增 runtime feature 时，优先按领域目录落位，不再把具体 feature 文件放在 `Runtime` 根目录。
- 如果某个 feature 拥有专用 pass，优先使用本地 `Passes` 子目录，只有跨系统共享的 built-in renderer pass 才继续放在 `Runtime/Passes`。
- 新增 compute shader 时放入 `Shaders/Compute/<Domain>`，不要放入 plugin-style 目录。
- 如果未来真的引入第三方插件，应让 `Plugins` 表示清晰的外部边界，并避免和 NWRP-owned runtime 混放。
- 测试资源后续命名应继续保持低噪声；scene / prefab instance name 的批量清理应单独立项，避免和渲染功能变更混在一起。
