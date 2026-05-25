# Phase37 ValleyHeightFog Asset Cleanup

日期：`2026-05-25`

## 概要

本阶段处理 `Assets/Settings/NewWorldRP.asset` 中重复出现的 `Valley Height Fog Feature` 子资产问题，并同步清理不再作为项目必要资源维护的 EditMode 测试程序集。

问题表现是：`NewWorldRP.asset` 顶部存在两个同名、同脚本类型的 `ValleyHeightFogFeature` sub-asset，但 `featureSettings.features` 只引用其中一个。运行时 `NWRPRenderer` 只遍历 `NewWorldRenderPipelineAsset.Features`，因此不会真正执行两次 Valley Height Fog pass；重复对象属于未引用的孤儿子资产，会造成 Inspector / YAML 误读，也可能在后续编辑器辅助按钮再次创建 Feature 时继续堆积。

本阶段目标不是改变高度雾渲染效果，而是清理资产状态并收紧 Editor 创建逻辑：

- `NewWorldRP.asset` 只保留一个被 explicit feature list 引用的 `Valley Height Fog Feature`。
- `NewWorldRenderPipelineAssetEditor.EnsureValleyHeightFogFeature()` 在创建新 Feature 前，会优先复用同一 pipeline asset 下已有但未引用的 `ValleyHeightFogFeature` sub-asset。
- 删除历史遗留的 `Assets/NWRP/Tests/EditMode` 测试程序集文件；这些文件不作为当前 NWRP 必要运行资源保留。

## 根因

重复子资产来自 Phase35 高度雾接入阶段。当时 `Assets/Settings/NewWorldRP.asset` 中同时写入了两个 `Valley Height Fog Feature` sub-asset：

```text
fileID: -6901768154398994806  未被 features 列表引用
fileID: -4768812527477539358  被 features 列表引用
```

后续版本中 `features` 列表里的空引用已经被清掉，但第一个未引用 sub-asset 仍留在 YAML 中。

旧的 Editor 辅助逻辑只检查：

```text
asset.Features
```

也就是显式 Feature 列表。它不会扫描同一个 `.asset` 文件内部已经存在的未引用 sub-asset，所以遇到孤儿 `ValleyHeightFogFeature` 时不会复用，仍可能创建新的同名 Feature。

## 修改范围

### Pipeline Asset

清理：

- `Assets/Settings/NewWorldRP.asset`

删除未引用的 sub-asset：

```text
fileID: -6901768154398994806
```

保留当前有效引用：

```text
featureSettings.features:
  - {fileID: -4768812527477539358}
```

清理后 `NewWorldRP.asset` 中只剩一个 `m_Name: Valley Height Fog Feature`，并且它就是 explicit feature list 中引用的实例。

### Editor

修改：

- `Assets/NWRP/Editor/Pipeline/NewWorldRenderPipelineAssetEditor.cs`

`EnsureValleyHeightFogFeature()` 的逻辑调整为：

```text
1. 先检查 asset.Features 中是否已经引用 ValleyHeightFogFeature。
2. 如果没有引用，则扫描同一 assetPath 下的所有 sub-asset。
3. 如果找到未引用的 ValleyHeightFogFeature，则复用它并加入 asset.Features。
4. 只有完全找不到时，才创建新的 ValleyHeightFogFeature sub-asset。
```

这样可以避免同类孤儿子资产继续堆积，同时保持按钮行为仍然是“确保 explicit feature list 中存在一个 Valley Height Fog Feature”。

### Tests / Temporary Assets

删除：

- `Assets/NWRP/Tests/EditMode/NWRP.EditModeTests.asmdef`
- `Assets/NWRP/Tests/EditMode/NWRP.EditModeTests.asmdef.meta`
- `Assets/NWRP/Tests/EditMode/ValleyHeightFogVolumeTests.cs`
- `Assets/NWRP/Tests/EditMode/ValleyHeightFogVolumeTests.cs.meta`

删除原因：

- 这些文件是历史阶段用于验证高度雾接入的 EditMode 测试资源。
- 当前项目不把这套测试程序集作为 NWRP 必要资源使用。
- 与其加入 `.gitignore` 或额外 `AGENTS.md` 管理临时测试，不如直接删除不再维护的测试文件，避免误认为它们是运行时资源或交付资源。

本阶段没有删除 `Assets/NWRP/Tests/Scenes`、`Materials`、`Meshes` 等样例场景资源。

### URP Global Settings

保留并规范化：

- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`
- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset.meta`
- `ProjectSettings/GraphicsSettings.asset`

项目当前仍安装 `com.unity.render-pipelines.universal`，用于参考、迁移和兼容性验证。Unity 2022 的 URP 包会通过 `GraphicsSettings.m_SRPDefaultSettings` 要求一个有效的 `UniversalRenderPipelineGlobalSettings` 资产；这个引用不代表当前运行管线切换到 URP。

当前有效状态为：

```text
GraphicsSettings.m_CustomRenderPipeline -> Assets/Settings/NewWorldRP.asset
QualitySettings.customRenderPipeline    -> Assets/Settings/NewWorldRP.asset
GraphicsSettings.m_SRPDefaultSettings:
  UnityEngine.Rendering.Universal.UniversalRenderPipeline
    -> Assets/Settings/UniversalRenderPipelineGlobalSettings.asset
```

因此：

- NWRP 仍是当前 Scriptable Render Pipeline。
- URP Global Settings 只用于满足 URP 包自身的全局设置校验。
- 不应删除该资产；删除后 Unity 会重新提示 `URP Global Settings Select a valid Universal Render Pipeline Global Settings asset`。
- `Current Render Pipeline is New World Render Pipeline` 这类提示只说明当前 active pipeline 不是 URP，本项目这是预期状态。

## 架构影响

运行时渲染路径不变。

`ValleyHeightFogFeature` 仍然是独立 NWRP Feature，不回写 `NWRPRenderer` 主流程，不新增全能型 Feature，也不改变 pass event：

```text
NWRPPassEvent.AfterTransparent
```

`NWRPRenderer` 仍只从 `frameData.asset.Features` 枚举显式 Feature。未引用 sub-asset 不会进入运行时 pass 队列。

本阶段只让 Editor 创建入口更健壮：资产内部存在可复用对象时，优先恢复引用关系，而不是创建重复对象。

## 性能与移动端影响

CPU：

- 无新增 per-frame 逻辑。
- 无新增 renderer loop、culling、Volume 解析或反射调用。
- Editor 扫描 sub-asset 只发生在点击 `Add Valley Height Fog Feature` / 调用 `EnsureValleyHeightFogFeature()` 时，不在运行时路径。

GPU：

- 无新增 RenderPass。
- 无新增 RenderTexture。
- 无新增 fullscreen blit。
- 无新增 shader global。

移动端表现：

- Android / iOS 运行时行为不变。
- Tile-Based GPU 带宽压力不变。
- 高度雾本身仍然只在显式 Feature + Volume 激活时执行。

## Shader Variant

本阶段不修改 shader。

Variant 风险：

- 无新增 `multi_compile`。
- 无新增 `shader_feature`。
- 无新增 URP keyword。
- 无新增 pass 或 shader family。

高度雾算法选择仍由 C# 选择 hidden shader pass index 控制，不通过 keyword 组合控制。

## 兼容性与序列化

`NewWorldRP.asset` 清理的是未引用 sub-asset，不改变有效 Feature 的 fileID：

```text
保留：-4768812527477539358
删除：-6901768154398994806
```

因此当前显式 Feature 引用不丢失，Inspector 中的 Feature 列表保持有效。

Editor 逻辑复用 sub-asset 时通过 `AssetDatabase.LoadAllAssetsAtPath(assetPath)` 查找同文件内对象，不依赖 URP，也不影响 NWRP runtime assembly。

## 验证记录

处理重复 sub-asset 后，曾运行 Unity MCP EditMode 全量测试：

```text
TotalTests: 12
PassedTests: 12
FailedTests: 0
SkippedTests: 0
```

随后根据项目资源维护要求删除 `Assets/NWRP/Tests/EditMode` 下的测试程序集文件。

删除后验证：

```text
AssetDatabase.Refresh: Success
Unity Console 最近错误: 0
```

静态检查：

```text
Assets/Settings/NewWorldRP.asset 中只剩 1 个 Valley Height Fog Feature
featureSettings.features 只引用 fileID -4768812527477539358
Assets/NWRP/Runtime 与 Assets/NWRP/Editor 中无 NWRP.EditModeTests / ValleyHeightFogVolumeTests / NWRP.Tests 引用残留
GraphicsSettings 与 QualitySettings 的当前 Render Pipeline 均指向 Assets/Settings/NewWorldRP.asset
URP Global Settings 引用指向有效资产 Assets/Settings/UniversalRenderPipelineGlobalSettings.asset
```

刷新 AssetDatabase 时 Unity 曾在 `Assets/UniversalRenderPipelineGlobalSettings.asset` 自动生成 URP Global Settings。为避免根目录资源杂乱，本阶段将它规范到 `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`，并同步修正 `GraphicsSettings.m_SRPDefaultSettings` 引用。

## 后续建议

- 如果以后需要保留长期自动化测试，建议重新建立明确命名的测试目录和测试策略，而不是把临时验证文件混入交付资源。
- 如果需要保留测试但不希望进入最终资源包，应通过 asmdef、Editor-only 目录和 CI 规则明确边界，而不是依赖 `.gitignore` 忽略已跟踪 Unity 资源。
- 后续新增 pipeline asset 辅助按钮时，建议都遵循本阶段模式：先检查显式引用，再扫描同 asset 内可复用 sub-asset，最后才创建新对象。
