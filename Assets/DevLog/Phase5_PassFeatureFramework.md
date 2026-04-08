# Phase 5 - Pass/Feature 调度框架落地（无阴影实现）

**日期**: 2026-04-08  
**状态**: ✅ 代码完成，待引擎验证

---

## 目标

将 NWRP 从“单体 CameraRenderer 直写流程”升级为：

- 固定内置 Pass（保证最小可跑闭环、默认输出不变）
- 可插拔 Feature Pass（便于后续按模块扩展：阴影/后处理/大规模渲染/调试）
- Pass 顺序契约稳定（避免 ad-hoc 排序导致系统失控）
- 运行时低开销（Pass/Feature 实例复用，避免帧分配和隐藏耦合）

## 完成的工作

### 5.1 Runtime 调度骨架（自研，不依赖 URP Feature）

| 文件 | 职责 |
|------|------|
| `Assets/NWRP/Runtime/NWRPPassEvent.cs` | Pass 顺序契约（包含阴影扩展点 `BeforeShadowMap/ShadowMap`，本 Phase 不实现阴影） |
| `Assets/NWRP/Runtime/NWRPFrameData.cs` | per-camera 帧数据（context/camera/culling/cmd/asset + 目标预留） |
| `Assets/NWRP/Runtime/NWRPPass.cs` | Pass 抽象基类：`passEvent` + `Execute(ref frameData)` |
| `Assets/NWRP/Runtime/NWRPFeature.cs` | Feature 抽象基类（ScriptableObject）：开关 + `Create()` + `AddPasses()` |
| `Assets/NWRP/Runtime/NWRPShaderIds.cs` | 集中管理 Shader 全局 PropertyToID（本 Phase 仅保留光照相关） |
| `Assets/NWRP/Runtime/NWRPRenderer.cs` | Pass 队列、稳定排序、执行内置 Pass + Feature Pass |

关键行为：

- `NWRPRenderer.EnqueuePass()` 仅入队，不执行；每帧统一排序执行
- 稳定排序：优先 `passEvent`，同事件按 `enqueueIndex` 保序（避免 Feature 叠加时顺序抖动）
- Feature 生命周期：`EnsureCreated()` 仅首次调用 `Create()`，OnValidate 时重置（编辑器改动后可重建）

### 5.2 内置 Pass 拆分（保持当前输出路径）

将原先 `CameraRenderer` 的流程拆成 7 个内置 Pass（只做结构切分，不引入额外 RT）：

| Pass | 事件 | 说明 |
|------|------|------|
| `SetupCameraPass` | `BeforeShadowMap` | `SetupCameraProperties` + Clear + BeginSample |
| `SetupLightsPass` | `BeforeShadowMap` | 主光 + 附加光数据上传（沿用旧逻辑，数组复用无 GC） |
| `DrawOpaquePass` | `Opaque` | Opaque 绘制（NewWorldUnlit/SRPDefaultUnlit/NewWorldForward） |
| `DrawOutlinePass` | `Opaque` | Outline 绘制（NewWorldOutline） |
| `DrawSkyboxPass` | `Skybox` | Skybox |
| `DrawTransparentPass` | `Transparent` | Transparent 绘制（同 Opaque tag 集合） |
| `SubmitPass` | `DebugOverlay` | EndSample + Submit（强制最后执行） |

Pass 文件位于：`Assets/NWRP/Runtime/Passes/`

### 5.3 Pipeline 入口调整

- `NewWorldRenderPipeline` 不再直接驱动单体渲染逻辑，改为持有 `NWRPRenderer` 并调用 `Render()`。
- `CameraRenderer` 保留为兼容 facade（避免历史调用点断裂），内部转发给 `NWRPRenderer`。

### 5.4 Pipeline Asset 扩展点（Feature Toggle）

`NewWorldRenderPipelineAsset`：

- 新增 `FeatureSettings/features`（`List<NWRPFeature>`），允许为空；为空时仅跑内置 Pass
- 保留原字段名：`useSRPBatcher/useGPUInstancing/maxAdditionalLights`（避免序列化破坏）

## 重要变更（按移动端约束收敛）

- 动态合批已移除：
  - 移除 Asset 字段 `useDynamicBatching`
  - 渲染侧强制 `enableDynamicBatching = false`
- 阴影设置已移除：本 Phase 不再在 Asset 里预留 `ShadowSettings`

## 验证清单（Editor）

- [ ] Unity Console 零编译错误
- [ ] `Assets/Scenes/SampleScene.unity`：渲染结果与改造前一致
- [ ] `Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity`：Lit/NPR 材质一致
- [ ] Frame Debugger：Pass 顺序符合 `SetupCamera -> SetupLights -> Opaque -> Outline -> Skybox -> Transparent -> Submit`
- [ ] Profiler：渲染主路径 GC Alloc = 0（关注 Feature Pass 入队与列表复用）

## 下一步（Phase 6 方向）

- 引入阴影 Feature（主方向光 ShadowMap Pass + 采样库），按 `NWRPPassEvent.BeforeShadowMap/ShadowMap` 接入
- 补齐 Shader 侧 Instancing 规范与统一的 ShadowCaster/DepthOnly include 方案（严格控制变体）

