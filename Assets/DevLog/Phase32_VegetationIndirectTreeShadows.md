# Phase32 Vegetation Indirect Tree Shadows

日期：`2026-05-18`

## 概要

本阶段把 Phase17 中树木阴影依赖源 `MeshRenderer` 的 `ShadowsOnly` 过渡方案，推进为 NWRP 内部的主光 GPU indirect shadow path。

核心目标不是改变植被可见渲染。树木、树叶的可见绘制仍由 `VegetationIndirectRenderer` 通过 `Graphics.RenderMeshIndirect` 提交，并继续使用 GPU culling + procedural instancing。变化点只在阴影提交：当 `NewWorldRP / Feature Settings / Vegetation Indirect Shadows / Enable Vegetation Indirect Tree Shadows` 开启时，树木阴影不再依赖源 `MeshRenderer` 进入 `DrawShadows`，而是在主光 shadow atlas 已绑定后，由专用 `NWRPPass` 追加 `DrawMeshInstancedIndirect` 的 `ShadowCaster` draw。

本阶段范围只覆盖主光阴影：

- realtime main light atlas
- cached static main light shadow
- cached static + dynamic overlay

Additional punctual light shadows v1 不接入 vegetation indirect shadow，避免移动端额外灯 shadow slice 成本失控。

## 修改文件

- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/Settings/NewWorldRP.asset`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowFeature.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowIndirectCasterContext.cs`
- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowDynamicOverlayPass.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/VegetationIndirectShadowFeature.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/Passes/VegetationIndirectShadowPass.cs`
- `Assets/NWRP/Runtime/VegetationIndirectShadows/VegetationIndirectShadowRegistry.cs`
- `Assets/NWRP/Plugins/VegetationGPUInstancer/VegetationIndirectRenderer.cs`

## Pipeline Asset 开关

`NewWorldRenderPipelineAsset.FeatureSettings` 新增：

- `vegetationIndirectShadows`
- `enableVegetationIndirectTreeShadows`

Inspector 路径：

- `Feature Settings`
- `Vegetation Indirect Shadows`
- `Enable Vegetation Indirect Tree Shadows`

该开关默认关闭。默认关闭是有意的，因为旧项目与低能力设备仍需要稳定 fallback，且树木 shadow indirect path 会按 cascade 增加 compute dispatch 和 indirect shadow draw。

`NewWorldRP.asset` 已补齐序列化字段：

```yaml
featureSettings:
  vegetationIndirectShadows:
    enableVegetationIndirectTreeShadows: 0
```

由于 `NewWorldRenderPipelineAsset` 使用自定义 Inspector，新字段不会自动显示。本阶段在 `NewWorldRenderPipelineAssetEditor` 中显式绑定该字段，并为旧 asset 尚未序列化出字段的情况增加 warning，避免面板上看不到开关时误判为功能未接入。

## Feature / Pass 结构

新增 `VegetationIndirectShadowFeature`：

- 类型：`NWRPFeature`
- 创建：持有一个 `VegetationIndirectShadowPass`
- enqueue 条件：
  - renderer 有效
  - 主光阴影开启
  - `EnableVegetationIndirectTreeShadows = true`

新增 `VegetationIndirectShadowPass`：

- `passEvent = NWRPPassEvent.ShadowMap`
- Frame Debugger 名称：`Render Vegetation Indirect Shadows`
- profiling group：`NWRPProfiling.MainLightShadow`
- 不创建额外 RT
- 不独立管理 shadow atlas 生命周期
- 只在主光 shadow pass 已登记可写入 atlas target 后追加绘制

这保持了 NWRP 的扩展边界：vegetation shadow 是独立 feature/pass，不把树木逻辑塞回 `CameraRenderer` 或主光阴影 pass 的 monolithic 流程里。

## 主光 Shadow Target 交接

新增 `MainLightShadowIndirectCasterContext` 作为同帧上下文，负责让主光阴影 pass 暴露当前可写入的 shadow atlas target。

主光阴影路径在完成 atlas 创建、cascade 数据计算和 render target 绑定后，向 context 添加 target：

- realtime path：每帧登记主光 realtime atlas
- cached static path：只在 static cache dirty / rebuild 帧登记 static atlas
- cached static + dynamic overlay：
  - static cache rebuild 帧登记 static caster target
  - dynamic overlay 登记 dynamic caster target
  - static cache rebuild 同帧允许 static indirect casters 合并进 overlay 的有效结果

`VegetationIndirectShadowPass` 执行时遍历 context target，并对每个 cascade：

1. 根据 cascade view-projection 构建 shadow frustum planes。
2. 按 cascade viewport 写入现有 shadow atlas。
3. 设置 `_ShadowBias`、`_ShadowLightDirection`、shadow caster cull mode。
4. dispatch vegetation culling compute，输出 shadow-visible append buffer。
5. `CopyCounterValue` 到 shadow indirect args。
6. 使用 `CommandBuffer.DrawMeshInstancedIndirect` 绘制 `ShadowCaster` pass。

这条路径不依赖 `ScriptableRenderContext.DrawShadows` 的 CPU renderer list，因此能看到 `Graphics.RenderMeshIndirect` 管理的树木实例数据。

## VegetationIndirectRenderer 接入

`VegetationIndirectRenderer` 实现 `IVegetationIndirectShadowProvider`，并在 enable/disable/destroy 时注册到 `VegetationIndirectShadowRegistry`。

运行时每个 render group 额外维护：

- `shadowVisibleGrassBuffer`
- `shadowIndirectArgsBuffer`
- `shadowMpb`
- `shadowCasterPassIndex`
- `supportsIndirectShadows`
- `isDynamicShadowCaster`

支持 indirect shadow 的材质范围限制为树木相关 shader：

- `NewWorld/Env/Tree`
- `NewWorld/Env/TreeLeaf`
- `NewWorld/Env/MobileFallback/Tree`
- `NewWorld/Env/MobileFallback/TreeLeaf`

草和灌木不默认纳入投影，避免大面积 alpha clip / overdraw 把移动端 shadow atlas 成本推高。

可见渲染仍然固定为：

- `Graphics.RenderMeshIndirect`
- `RenderParams.shadowCastingMode = ShadowCastingMode.Off`
- `RenderParams.receiveShadows` 跟随 `receiveShadows`

这样可以避免 forward indirect draw 与 shadow indirect pass 重复提交阴影。

## 开关关闭时的行为

当 `Enable Vegetation Indirect Tree Shadows` 关闭时：

- `NWRPRenderer` 不创建 runtime `VegetationIndirectShadowFeature`
- `VegetationIndirectShadowPass` 不会 enqueue
- Frame Debugger 中不会出现 `Render Vegetation Indirect Shadows`
- 树木可见渲染仍走 `Graphics.RenderMeshIndirect`
- 树木阴影继续走 Phase17 的源 `MeshRenderer` fallback

fallback 行为：

- `VegetationIndirectRenderer` 在 NWRP culling 前选择可投影的源 renderer
- 源 renderer 被临时启用
- `shadowCastingMode = ShadowCastingMode.ShadowsOnly`
- `receiveShadows = false`
- 可见 indirect draw 继续 `ShadowCastingMode.Off`

因此在 Frame Debugger 中，未开启新开关时看到树木阴影 draw 归到普通 renderer / SRP Batcher 路径是正常现象。这不是可见植被从 indirect 退回了 SRP Batcher，而是阴影 fallback 仍由源 `MeshRenderer` 进入 Unity renderer culling 和 shadow submission。

## 开关开启时的行为

当 `Enable Vegetation Indirect Tree Shadows` 开启时：

- `NWRPRenderer` 会创建或使用 `VegetationIndirectShadowFeature`
- `VegetationIndirectShadowFeature` enqueue `VegetationIndirectShadowPass`
- `VegetationIndirectRenderer.ShouldUseShadowOnlyRendererFallback()` 返回 false
- 源 `MeshRenderer` 不再切到 `ShadowsOnly`
- 树木阴影由 NWRP main-light shadow stage 内部的 `DrawMeshInstancedIndirect` 写入 atlas

Frame Debugger 预期结构：

- `Main Light Shadows`
  - `Render Main Light Shadow` / `Render Main Light Cached Shadow` / `Render Main Light Dynamic Shadow Overlay`
  - `Render Vegetation Indirect Shadows`

如果开关已开启但仍看不到 `Render Vegetation Indirect Shadows`，需要优先检查：

- 当前是否在 Play Mode
- 主光阴影是否开启
- 当前相机是否实际触发主光 shadow atlas
- `VegetationIndirectRenderer.debugUseOriginalRenderer` 是否关闭
- `VegetationIndirectRenderer.castShadows` 是否开启
- compute shader 与 indirect arguments buffer 是否可用
- tree / tree leaf material 是否有 `ShadowCaster` pass
- shader 名称是否在当前 tree allow-list 中
- registry 中是否存在 active provider

如果 pass enqueue 了但没有任何 draw，Frame Debugger 可能只显示很短的 profiling scope，或因为没有命令提交而不明显。

## Cached Shadow 策略

realtime path：

- 每帧每 cascade 计算 vegetation shadow visibility
- 每帧写入 realtime atlas

cached static path：

- static cache dirty / rebuild 帧才写静态树木阴影
- cache 未 dirty 时不重复提交静态树木 shadow draw

cached static + dynamic overlay：

- 树叶、风动画、LOD/距离 fade 等会变化的组视为 dynamic caster
- 真正静态树干可进入 static cache
- dynamic overlay 逐帧提交动态树木阴影

这条策略保持 CPU 和 GPU 成本可控：静态树干不每帧重复写 atlas，动态树叶也不会错误烘进长期缓存。

## Variant 与 Shader 风险

本阶段没有新增 vegetation shadow keyword。

继续复用现有 `Tree.shader` / `TreeLeaf.shader` 的：

- `ShadowCaster` pass
- `#pragma multi_compile_instancing`
- procedural instancing buffer 接口

开关由 pipeline asset runtime toggle 控制，不通过 shader keyword 控制，因此不会引入新的 A x B x C variant 组合。

主要成本来自运行时：

- 每个 cascade 的 compute culling dispatch
- 每个 tree / tree leaf render group 的 indirect shadow draw
- tree leaf alpha clip 在 shadow atlas 中的 overdraw
- cached + dynamic overlay 模式下 static/dynamic target 的分流

移动端建议：

- 默认关闭该开关，在目标机型上验证后按质量档开启
- 控制 cascade 数量和 shadow atlas resolution
- 优先让树干进入 static cache，树叶进入 dynamic overlay
- 不把草默认纳入投影
- additional light shadow 继续保持不接入 vegetation indirect shadow

## 调试与验证

Frame Debugger：

- 开关关闭：不应看到 `Render Vegetation Indirect Shadows`，树木阴影可能显示为 SRP Batcher / ordinary renderer shadow draw。
- 开关开启：应在 `Main Light Shadows` stage 中看到 `Render Vegetation Indirect Shadows`。
- 可见植被 draw 仍应来自 `Graphics.RenderMeshIndirect`，而不是源 renderer。

RenderDoc：

- 检查 shadow pass 绑定的 atlas target。
- 检查 `_ShadowBias`、`_ShadowLightDirection`、cascade viewport。
- 检查 `_VisibleVegetationBuffer` 是否绑定为 shadow-visible buffer。
- 检查 indirect args 的 instance count 是否由 append buffer counter copy 得到。

本阶段已完成的静态检查：

- `git diff --check` 通过。
- `Assets/Settings/NewWorldRP.asset` 已补齐 `vegetationIndirectShadows` 序列化字段。
- 当前源码不再包含旧的 `Vector4.forward` 编译错误点，shadow culling forward 参数已改为显式 `new Vector4(0f, 0f, 1f, 0f)`。

本阶段未完成的验证：

- Unity MCP `AssetDatabase.Refresh` 在本轮补字段后等待超时，后续 MCP 状态查询返回 null。
- EditMode tests 未运行成功；当前打开场景曾处于 dirty 状态，Unity Test Runner 要求先保存场景。
- 尚未在 Android Vulkan / GLES3 / iOS Metal 真机上验证 GPU cost。

## 当前边界与后续建议

- v1 只支持树干和树叶的主光 indirect shadow。
- v1 不把草默认纳入投影。
- v1 不接入 additional punctual light shadow。
- v1 不新增 shader variant。
- v1 仍复用 vegetation 现有 compute culling kernel，后续可评估更贴合 shadow cascade 的 chunk/cluster 级预剔除。
- 后续如果要支持额外灯阴影，应单独做 `VegetationIndirectAdditionalShadowPass`，并按 selected spot/point light slices 控制预算，默认关闭。
- 后续可以增加轻量 debug counter，统计 cascade dispatch count、indirect draw count 和 visible instance count，但不应引入常驻复杂调试系统。
