# Phase39 NWRP Material Workflow Tools

日期: `2026-05-26`

## 概要

本阶段围绕 NWRP 的材质易用性补齐 Editor 工作流:

- 新建 Mesh / Primitive 时, 自动把 Unity 默认材质槽替换为 NWRP 默认 Lit 材质。
- 提供固定默认材质资产 `Assets/NWRP/Materials/M_NWRP_DefaultLit.mat`。
- 在顶部工具栏, SceneView Overlay 和 `NWRP/Tools/Materials` 菜单中提供批量转换入口。
- 批量把 opaque URP Lit 材质迁移到 `NewWorld/Lit/StandardLit`。
- 新增 Editor 测试覆盖默认材质创建, 默认槽替换和 URP Lit 转换核心逻辑。

目标不是改变渲染路径, 也不是把 URP 运行时依赖带回 NWRP, 而是解决资产导入和新建模型时默认 `Standard` / `URP Lit` 材质在 NWRP 中不可见或显示不正确的问题。

本阶段所有逻辑都在 Editor 侧完成:

- 不新增 `NWRPFeature`。
- 不新增 `NWRPPass`。
- 不改 `CameraRenderer` / `NWRPRenderer` 主渲染流程。
- 不新增 shader keyword。
- 不新增 runtime RenderPass / RenderTexture / fullscreen blit。

## 参考背景

本阶段延续已有 DevLog 中建立的边界:

- `Phase4_StandardLit_ShaderGUI.md`: `NewWorld/Lit/StandardLit` 是当前 NWRP 标准 Lit 材质入口。
- `Phase24_URPCompatibleNamingAndDependencyClosure.md`: NWRP-owned runtime/editor 不依赖 URP package source。
- `Phase36_NWRPLayoutAndAgentsRefactor.md`: Editor tooling 按域分组, 本阶段新增内容进入 `Assets/NWRP/Editor/Materials`。
- `Phase38_RendererListAndURPStyleFeatureUI.md`: Editor 体验可以参考 URP 的使用习惯, 但实现必须保持 NWRP 自有类型和边界。

## 修改范围

### Assets

新增默认材质:

- `Assets/NWRP/Materials/M_NWRP_DefaultLit.mat`

该材质使用:

```text
Shader: NewWorld/Lit/StandardLit
Instancing: Enabled
_BaseColor: white
_Metallic: 0
_Smoothness: 0.5
_ReceiveShadows: 1
_CastShadows: 1
```

该默认材质是项目级固定资产, 不是运行时动态材质。这样新建模型获得的是稳定可追踪的 `.mat` 引用, 避免场景里出现不可控的临时材质实例。

### Editor

新增目录:

- `Assets/NWRP/Editor/Materials`

新增脚本:

- `Assets/NWRP/Editor/Materials/NWRPMaterialDefaults.cs`
- `Assets/NWRP/Editor/Materials/NWRPMaterialConverter.cs`
- `Assets/NWRP/Editor/Materials/NWRPMaterialToolbar.cs`

`NWRPMaterialDefaults` 负责:

- 获取或修复 `M_NWRP_DefaultLit.mat`。
- 监听 Editor 中新加入的 `MeshRenderer` / `SkinnedMeshRenderer`。
- 在 NWRP 激活时, 把新 Renderer 的默认槽替换为 NWRP 默认 Lit 材质。
- 提供菜单开关 `NWRP/Tools/Materials/Auto Assign Default Lit Material`。
- 提供菜单项 `NWRP/Tools/Materials/Select Default Lit Material`。

默认替换只处理以下情况:

- `null` 材质槽。
- Unity 内置默认材质。
- Built-in `Standard`。
- `Hidden/InternalErrorShader`。
- `Universal Render Pipeline/Lit`。

如果材质槽已经是 `NewWorld/Lit/StandardLit` 或其他自定义材质, 不会被自动覆盖。

`NWRPMaterialConverter` 负责:

- 扫描 `Assets/` 下所有 Material。
- 识别 shader 名称为 `Universal Render Pipeline/Lit` 的材质。
- 将 opaque URP Lit 材质切换到 `NewWorld/Lit/StandardLit`。
- 保留基础属性和贴图引用。
- 跳过 transparent / alpha clip 材质。

当前迁移属性:

```text
_BaseColor      -> _BaseColor
_BaseMap        -> _BaseMap
_Metallic       -> _Metallic
_Smoothness     -> _Smoothness
_OcclusionStrength -> _OcclusionStrength
_BumpScale      -> _NormalStrength
_BumpMap        -> _NormalMap
_EmissionColor  -> _EmissiveColor
_EmissionMap    -> _EmissiveMap
_MetallicGlossMap -> _MaskMap
```

`NWRPMaterialToolbar` 提供入口:

- Unity 顶部 Toolbar 按钮: `URP Lit -> NWRP`
- SceneView Overlay: `NWRP Materials`
- 菜单: `NWRP/Tools/Materials/Convert All URP Lit Materials To NWRP StandardLit`
- 菜单: `NWRP/Tools/Materials/Convert Selected URP Lit Materials To NWRP StandardLit`

Toolbar 使用 Editor 反射挂到 Unity 顶部右侧区域。SceneView Overlay 使用 `EditorToolbarButton / ToolbarOverlay`, 作为备用和显式入口, 避免 Unity 顶部 Toolbar 内部结构变化时完全失去按钮入口。

### Tests

新增 Editor 测试程序集:

- `Assets/NWRP/Tests/Editor/NWRP.Editor.Tests.asmdef`
- `Assets/NWRP/Tests/Editor/NWRPMaterialToolTests.cs`

覆盖项:

- `DefaultMaterialFactoryCreatesNwrpLitMaterialWithInstancing`
- `AssignDefaultMaterialReplacesBuiltInDefaultButPreservesCustomSlots`
- `ConverterMigratesOpaqueUrpLitMaterialToNwrpStandardLit`

测试使用临时目录:

```text
Assets/NWRP/Tests/EditorGenerated
```

每个测试前后会清理该目录, 避免临时材质资产残留到项目资源中。

## 自动默认材质策略

新建 mesh 的问题本质是 Editor 资产工作流问题, 不应该通过 runtime 渲染兜底解决。

本阶段选择 Editor 侧自动赋材质, 原因:

- 不增加 runtime per-frame 判断。
- 不影响打包后的渲染路径。
- 不改变 SRP Batcher / GPU Instancing 兼容性。
- 可以保留材质资产引用, 方便美术后续替换。
- 避免在 shader 或 pass 中为错误材质做兼容分支。

触发路径:

```text
ObjectFactory.componentWasAdded
EditorApplication.hierarchyChanged
EditorApplication.delayCall
```

处理对象:

```text
MeshRenderer
SkinnedMeshRenderer
```

生效前提:

```text
GraphicsSettings.currentRenderPipeline is NewWorldRenderPipelineAsset
或
QualitySettings.renderPipeline is NewWorldRenderPipelineAsset
```

自动赋材质默认启用。需要临时关闭时可通过:

```text
NWRP/Tools/Materials/Auto Assign Default Lit Material
```

该开关存储在 `EditorPrefs`, 属于本机编辑器偏好, 不进入项目序列化资产。

## URP Lit 批量转换策略

转换器只处理材质资产, 不改 Prefab / Scene 中 Renderer 的材质数组结构。Renderer 只要引用的是同一个材质资产, 材质 shader 切换后会自然生效。

透明和 AlphaClip 材质会跳过:

```text
_Surface > 0.5
_AlphaClip > 0.5
```

原因是当前 `NewWorld/Lit/StandardLit` 是 opaque-only shader。强行迁移透明或裁剪材质会改变渲染队列, 深度写入, overdraw 行为和视觉语义, 风险高于收益。

后续如果需要迁移透明材质, 应单独建立 NWRP Transparent Lit 或 Cutout shader family, 不应继续给当前 StandardLit 堆分支。

## 架构影响

本阶段不改变 pass event 合约:

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

没有新增 renderer feature, 也没有新增 renderer data 配置项。

新增的 `Materials` Editor 域只服务资产和编辑器操作:

```text
Assets/NWRP/Editor/Materials
Assets/NWRP/Materials
Assets/NWRP/Tests/Editor
```

这保持了 `Assets/NWRP/Runtime` 的纯运行时边界, 也避免把易用性工具混入管线调度。

## 性能与移动端影响

CPU:

- 运行时无新增 per-frame 成本。
- 自动赋材质只发生在 Editor 中 Renderer 新建或层级变化后。
- 批量转换只在用户点击工具按钮时执行。

GPU:

- 无新增 RenderPass。
- 无新增 RenderTexture。
- 无新增 fullscreen blit。
- 无新增 MRT。
- 无新增 compute dispatch。

移动端:

- Android / iOS runtime 行为不变。
- Tile-Based GPU 带宽压力不变。
- 默认材质启用 GPU Instancing, 与项目移动端优先策略一致。

## Shader Variant

本阶段不修改 shader 文件。

Variant 风险:

- 无新增 `multi_compile`。
- 无新增 `shader_feature_local`。
- 无新增 URP keyword。
- 无新增 pass。
- 无新增 shader family。

`NewWorld/Lit/StandardLit` 原有 instancing 支持保持不变:

```text
#pragma multi_compile_instancing
```

URP Lit 批量转换会清理迁移后材质上的 shader keyword 列表, 避免 URP 材质残留 keyword 误导后续检查。

## URP 依赖边界

本阶段没有新增 `UnityEngine.Rendering.Universal` 引用。

转换器只用 shader 名称字符串识别 URP Lit:

```text
Universal Render Pipeline/Lit
```

这属于资产迁移兼容逻辑, 不代表 NWRP 依赖 URP runtime/editor API。

静态检查范围:

```text
Assets/NWRP/Editor/Materials
Assets/NWRP/Tests/Editor
```

未发现:

```text
UnityEngine.Rendering.Universal
ScriptableRendererFeature
ScriptableRenderPass
Packages/com.unity.render-pipelines.universal
```

## 验证记录

先做了红灯验证:

```text
NWRP.Editor.NWRPMaterialDefaults 不存在时, 反射断言按预期失败。
```

实现后执行:

```text
AssetDatabase.Refresh
```

结果:

```text
Success
Unity Console 编译错误: 0
```

通过 Unity MCP `script_execute` 执行了核心行为验证:

- 默认材质能创建为 `NewWorld/Lit/StandardLit`。
- 默认材质启用 instancing。
- `ReplaceDefaultSlots` 会替换 Unity 默认槽和 `null` 槽。
- 自定义 NWRP 材质槽不会被覆盖。
- opaque URP Lit 材质能转换为 `NewWorld/Lit/StandardLit`。
- `_BaseColor` / `_Metallic` / `_Smoothness` 能保留。
- 通过 `ObjectFactory.CreatePrimitive` 模拟新建 primitive 后, 自动赋材质链路生效。

静态检查:

```text
git diff --check -- Assets/NWRP/Editor/Materials Assets/NWRP/Materials Assets/NWRP/Tests/Editor
```

结果:

```text
通过
```

正式 Unity Test Runner 当前未能执行, 原因是编辑器中已有未保存场景:

```text
MaterialSampleScene
Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity
```

MCP Test Runner 要求所有打开场景都已保存。为了不覆盖用户场景状态, 本阶段没有自动保存该场景。

当前工作树中还存在与本阶段无关的:

```text
Assets/Settings/NewWorldRP.asset
```

该文件已在本阶段开始前处于 modified 状态, 本阶段没有回滚或整理它。

## 后续建议

- 如果后续需要迁移 URP transparent / cutout 材质, 应新增独立 NWRP shader family, 不要把透明和裁剪路径塞进当前 opaque StandardLit。
- 可以在后续增加更精细的材质迁移报告, 例如输出被跳过材质列表和原因。
- 如果 Unity 顶部 Toolbar 内部 API 在未来版本变化, 仍保留 `SceneView Overlay` 和菜单入口作为稳定 fallback。
- 如果要做大批量项目迁移, 建议先只跑 `Convert Selected`, 通过视觉抽样确认属性映射后再跑全项目转换。
