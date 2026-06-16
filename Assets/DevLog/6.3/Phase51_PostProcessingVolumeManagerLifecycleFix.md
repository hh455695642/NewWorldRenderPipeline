# Phase51 后处理 VolumeManager 生命周期修复

日期：`2026-06-16`

## 概要

本阶段排查并修复 Unity 6.3 分支中 NWRP 后处理 Volume 功能整体失效的问题。

现象是：场景中的 Volume Profile 仍然存在，`NWRPTonemapping`、`NWRPBloom`、`NWRPColorAdjustments`、`NWRPVignette`、`NWRPAntiAliasing`、`NWRPScreenBlur`、`NWRPValleyHeightFog`、`NWRPCloudShadowProjector`、`NWRPFog` 等组件也没有丢脚本，但运行时所有由 Volume 驱动的后处理/环境功能都不生效。

根因不在单个后处理 pass，也不在 Volume Profile 资源本身，而是在自定义 SRP 生命周期里没有初始化 Unity Core RP 的 `VolumeManager`。

Unity 6 的 `VolumeManager.Update(...)` 在 `isInitialized == false` 时会直接返回。NWRP 的 `NWRPRenderer.ConfigureVolumeStack(...)` 虽然每帧调用了：

```csharp
VolumeManager.instance.ResetMainStack();
VolumeManager.instance.Update(volumeTrigger, volumeLayerMask);
frameData.volumeStack = VolumeManager.instance.stack;
```

但由于 `VolumeManager` 从未在 `NewWorldRenderPipeline` 构造阶段初始化，`stack` 不会被正确构建，`ResolvePostProcessingFromVolume(...)` 拿不到任何有效 Volume component，最终所有 active flag 都保持 false。

本阶段修复重点是补齐 NWRP 对 Unity Core Volume 系统的生命周期管理，并为 Player 构建提供 NWRP 自己的默认 VolumeProfile 类型清单，避免依赖 Editor-only 反射。

## 修改文件

- `Assets/NWRP/Runtime/NewWorldRenderPipeline.cs`
- `Assets/NWRP/Runtime/NWRPVolumeDefaults.cs`
- `Assets/NWRP/Runtime/NWRPVolumeDefaults.cs.meta`
- `Assets/NWRP/Tests/EditMode.meta`
- `Assets/NWRP/Tests/EditMode/NWRP.Runtime.Tests.asmdef`
- `Assets/NWRP/Tests/EditMode/NWRP.Runtime.Tests.asmdef.meta`
- `Assets/NWRP/Tests/EditMode/NWRPVolumeManagerLifecycleTests.cs`
- `Assets/NWRP/Tests/EditMode/NWRPVolumeManagerLifecycleTests.cs.meta`

## 解决的问题

### 1. 自定义 SRP 没有初始化 VolumeManager

Unity Core RP 的 `VolumeManager` 是跨 RenderPipeline 生命周期存在的 singleton。Unity 6 中它需要由当前 RenderPipeline 显式管理：

- RenderPipeline 构造时调用 `VolumeManager.Initialize(...)`
- RenderPipeline Dispose 时调用 `VolumeManager.Deinitialize()`

旧 NWRP 没有做这一步，导致：

```text
VolumeManager.isInitialized = false
VolumeManager.Update(...)   = early return
VolumeManager.stack         = null / invalid
NWRPFrameData.volumeStack   = null / invalid
所有 Volume 后处理 active flag = false
```

本阶段在 `NewWorldRenderPipeline` 构造阶段创建默认 VolumeProfile，并初始化 `VolumeManager`：

```csharp
_defaultVolumeProfile = NWRPVolumeDefaults.CreateProfile();
InitializeVolumeManager(_defaultVolumeProfile);
```

在 Dispose 阶段释放：

```csharp
DisposeVolumeManager();
```

这样 NWRP 自己成为 VolumeManager 生命周期的明确 owner，符合 Unity 6 Core RP 的预期。

### 2. Player 构建不能依赖 Editor 反射发现 VolumeComponent

在 Editor 中，Core RP 可以通过反射收集 VolumeComponent 类型；但 Player 构建中不能假设 Editor reflection 路径存在。

如果只调用：

```csharp
VolumeManager.Initialize(null, null);
```

Player 里可能无法构建完整的 NWRP VolumeStack。后处理功能即使在 Editor 里可用，也可能在移动端 Player 中再次失效。

本阶段新增 `NWRPVolumeDefaults`，集中维护 NWRP runtime 需要进入 VolumeStack 的组件类型：

```text
NWRPTonemapping
NWRPBloom
NWRPColorAdjustments
NWRPVignette
NWRPAntiAliasing
NWRPScreenBlur
NWRPValleyHeightFog
NWRPCloudShadowProjector
NWRPFog
```

`NWRPVolumeDefaults.CreateProfile()` 会创建一个隐藏的 runtime default profile，并把这些组件加入其中。Core RP 使用该 profile 构建默认 stack 和 base component type array。

后续新增 NWRP VolumeComponent 时，应把类型追加到这里。该文件就是后续 Volume 系统的显式扩展点。

### 3. 避免 stale VolumeManager 污染 NWRP

`VolumeManager` 是 singleton，如果此前由其他 RenderPipeline 或旧 NWRP 实例初始化过，继续复用旧状态存在风险：

- base component type list 可能不是 NWRP 的完整类型集合
- default stack 可能缺少新加的 NWRP VolumeComponent
- Camera / SceneView 交替渲染时更难判断问题来源

本阶段初始化前先检查：

```csharp
if (volumeManager.isInitialized)
{
    volumeManager.Deinitialize();
}
```

然后用 NWRP 自己的默认 profile 重新初始化。这样 VolumeStack 的组件集合始终由 NWRP 显式定义，不依赖外部管线残留状态。

### 4. 后处理 active 映射链路补回

修复后，`NWRPRenderer` 现有逻辑可以恢复正常：

```text
Camera / NWRPCameraData
    -> ConfigureVolumeStack
        -> VolumeManager.ResetMainStack
        -> VolumeManager.Update(trigger, layerMask)
        -> frameData.volumeStack
            -> ResolvePostProcessingFromVolume
                -> tonemappingActive / bloomActive / colorAdjustmentsActive / ...
            -> ResolveFogSettings
                -> fogActive / fogMode / fogColor / fog distances
```

本阶段没有修改 `PostProcessPass` 内部效果实现，也没有改动各个 feature 的 pass event。修复的是 VolumeStack 构建与组件解析的根链路。

## 关键实现

### NewWorldRenderPipeline

新增字段：

```csharp
private readonly VolumeProfile _defaultVolumeProfile;
```

构造阶段：

```csharp
_defaultVolumeProfile = NWRPVolumeDefaults.CreateProfile();
InitializeVolumeManager(_defaultVolumeProfile);
```

释放阶段：

```csharp
DisposeVolumeManager();
```

`DisposeVolumeManager()` 负责：

1. 如果 `VolumeManager` 当前已初始化，则 `Deinitialize()`
2. 销毁 NWRP 创建的隐藏 default profile 和其中的组件

该逻辑跟随 RenderPipeline 生命周期，不进入每帧渲染路径。

### NWRPVolumeDefaults

新增 runtime helper：

```csharp
internal static class NWRPVolumeDefaults
```

职责：

- 维护 NWRP VolumeComponent 类型清单
- 创建 `HideAndDontSave` 的 runtime default `VolumeProfile`
- 在 Dispose 时销毁 profile 和内部组件

这里没有使用反射，也没有扫描程序集。类型表是显式的，便于移动端 Player 可控、可审计。

## 性能与移动端策略

本阶段属于生命周期修复，不增加渲染成本：

- 不新增 `NWRPPass`
- 不新增 RenderTexture
- 不新增 full-screen blit
- 不新增 MRT
- 不新增 Compute
- 不新增每帧 CPU 大循环
- 不改变任何 shader pass

新增的默认 VolumeProfile 只在 pipeline 创建时构造一次，在 Dispose 时销毁。每帧仍然沿用原本的 VolumeManager stack update 路径。

对移动端的影响：

- 修复会让原本应该 active 的后处理重新生效，因此项目需要继续按 Volume / Camera / Renderer Feature 开关控制成本。
- 默认 inactive 的 VolumeComponent 仍不会触发对应 pass 或内部成本。
- `supportsPostProcessing`、`NWRPCameraData.renderPostProcessing`、Volume active 状态仍是主要控制入口。

## Shader Variant 风险

本阶段没有任何 shader keyword 变化。

```text
新增 shader keyword: 0
新增 multi_compile: 0
新增 shader_feature_local: 0
新增 shader 文件: 0
修改 shader 文件: 0
```

后处理功能仍通过 C# active state、Volume 参数、shader pass index 或 uniform 控制，不引入新的业务材质 variant 组合。

## 与旧阶段的关系

Phase25 建立了 NWRP 后处理框架和 Tonemapping 入口。

Phase26 补齐了 Camera 开关语义、SceneView fallback 和每相机 VolumeStack 刷新逻辑。

Phase27 / Phase28 / Phase34 继续把 Bloom、Color Adjustments、Vignette、FXAA 接入统一 `NWRP PostProcess`。

Phase35 / Phase42 / Phase43 等阶段加入了独立 feature + Volume 驱动的屏幕空间效果。

本阶段不是新增效果，而是修复这些已有 Volume 驱动功能共同依赖的底层入口：

```text
VolumeManager 生命周期
    -> VolumeStack 构建
        -> NWRPFrameData active flag
            -> PostProcess / Fog / Pluggable Feature 是否执行
```

如果 VolumeManager 没初始化，前面所有阶段的 VolumeComponent 即使资源完整也无法生效。

## 验证记录

### Unity MCP / Editor 验证

已通过 Unity MCP 连接当前 Unity Editor，并确认：

```text
Unity version: 6000.3.12f1
Project: NewWorldRenderPipeline6_Codex
Active scene: Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity
```

### EditMode 测试

新增并运行：

```text
NWRP.Tests.NWRPVolumeManagerLifecycleTests.PipelineConstructorInitializesVolumeManagerWithNWRPVolumeComponents
NWRP.Tests.NWRPVolumeManagerLifecycleTests.VolumeResolveMapsAllNWRPPostProcessingComponentsToFrameData
```

最终结果：

```text
Mode: EditMode
Total: 2
Passed: 2
Failed: 0
Skipped: 0
ResultState: Passed
```

覆盖内容：

- pipeline 构造后 `VolumeManager.isInitialized == true`
- `VolumeManager.stack` 非空
- stack 中包含全部当前 NWRP VolumeComponent
- 激活所有后处理 / 环境 VolumeComponent 后，`NWRPFrameData` 中对应 active flag 均能解析为 true
- fog mode / fog component 引用能正确写入 frame data

### Console 状态

最终测试前读取 Unity Console，没有新的 NWRP 编译错误。

测试过程中 Unity Test Framework 曾输出过一次 Editor undo 相关的原生断言：

```text
Assertion failed on expression: 'targetScene != nullptr'
UnityEditor.TestTools.TestRunner.TestRun.Tasks.PerformUndoTask
```

该断言来自 Unity Test Framework 的 `PerformUndoTask`，不影响本阶段测试结果。测试结果 XML 显示两条测试均为 `Passed`。

## 当前注意事项

- `Assets/NWRP/Tests/EditMode` 是可跟踪目录；不要把本阶段测试放回 `Assets/NWRP/Tests/Editor`，该目录当前被 `.gitignore` 忽略。
- 后续新增 NWRP VolumeComponent 时必须同步更新 `NWRPVolumeDefaults`，否则 Player 构建可能缺少对应组件类型。
- 本阶段没有做 GameView / SceneView 的人工视觉截图对比；当前验证覆盖的是 VolumeManager 生命周期、VolumeStack 类型完整性和 frame data active 映射链路。
- 工作区中存在其他 Unity 自动序列化或既有未提交改动，例如测试场景、ProjectSettings、URP asset 删除、Screenshots / Build Profiles 等；这些不属于本阶段修复内容。

## 后续方向

- 可以为 `NWRPVolumeDefaults` 增加更明确的维护约束，例如在新增 VolumeComponent 的 PR checklist 中要求同步类型表。
- 如果后续 VolumeComponent 数量继续增长，可以考虑增加 Editor-only 检查，扫描 `Assets/NWRP/Runtime` 中继承 `VolumeComponent` 且 `SupportedOnRenderPipeline(typeof(NewWorldRenderPipelineAsset))` 的类型，并与 `NWRPVolumeDefaults` 类型表对比，作为测试失败条件。
- 可在真实 Android / iOS Player 中补一次 smoke test，确认 Player 构建不依赖 Editor reflection 也能正确创建 VolumeStack。
