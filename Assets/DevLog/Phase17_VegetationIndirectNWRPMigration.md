# Phase17 植被间接渲染 NWRP 迁移

日期：`2026-04-28`

## 概要

本阶段将植被间接渲染相关 shader 从原 URP 外壳迁移为 NWRP 自有 pass，同时保留现有 `VegetationIndirectRenderer + VegetationCulling.compute + procedural instancing` 运行时路径。

迁移后的职责划分是：草和灌木默认只接收主光阴影，不投射实时阴影；树干和树叶支持接收并投射主光阴影。由于当前 NWRP 阴影图仍通过 `ScriptableRenderContext.DrawShadows` 渲染，无法直接看到 `Graphics.RenderMeshIndirect` 提交的实例，运行时树木投影暂时通过源 `MeshRenderer` 的 `ShadowsOnly` fallback 接回现有 shadow map。

## 修改文件

- `Assets/NWRP/Shaders/Environment/WorldGrass.shader`
- `Assets/NWRP/Shaders/Environment/Shrub.shader`
- `Assets/NWRP/Shaders/Environment/Tree.shader`
- `Assets/NWRP/Shaders/Environment/TreeLeaf.shader`
- `Assets/NWRP/Shaders/Environment/Includes/VegetationIndirectInstancing.hlsl`
- `Assets/NWRP/Plugins/VegetationGPUInstancer/VegetationIndirectRenderer.cs`
- `AGENTS.md`

## Shader 迁移

### WorldGrass

- 移除 URP pass tag 和 URP include，改用 NWRP shader library。
- Forward pass 改为 `LightMode = NewWorldForward`。
- 保留自定义 `DepthOnly` pass。
- 移除 `DepthNormals`，不新增 `ShadowCaster`。
- 保留 procedural instancing：`#pragma multi_compile_instancing` 和 `#pragma instancing_options procedural:SetupInstancing`。
- 保留风动画、ramp 光照、高度渐变、世界噪声色、雾效和距离 dither。
- 修正 noise color 路径，使材质上的 `_NoiseColor1` / `_NoiseColor2` 能正确混合，并由 `_NoiseColorIntensity` 控制强度。

### Shrub

- 迁移为与 WorldGrass 相同的 NWRP 结构。
- 使用 `NewWorldForward` 和 `DepthOnly`。
- 默认不投射实时阴影。
- 通过材质/运行时 uniform `_ReceiveShadows` 接收 NWRP 主光阴影。

### Tree

- 迁移到 NWRP forward lighting。
- 增加 NWRP `ShadowCaster` 和 `DepthOnly` pass。
- 所有相关 pass 保留 procedural instancing 声明。
- 使用 `_ReceiveShadows` 和 `_CastShadows` 材质控制，不引入 URP shadow keyword。

### TreeLeaf

- 迁移到 NWRP forward lighting。
- 增加带 alpha clip 的 NWRP `ShadowCaster` 和 `DepthOnly` pass。
- 保留树叶染色、第二颜色模式、fake SSS、距离 fade、alpha clip 和 procedural instancing。
- local variant 只保留原有第二颜色行为和 instancing，不引入 URP 阴影变体。

## 运行时渲染器调整

### 阴影策略拆分

- 将原先的全局阴影行为拆成：
  - `castShadows`
  - `receiveShadows`
- 通过 `[FormerlySerializedAs("enableShadow")]` 兼容旧序列化数据。
- 运行时 `MaterialPropertyBlock` 会上传 `_ReceiveShadows`。
- `RenderParams.receiveShadows` 跟随 `receiveShadows`。

### 运行时树木投影

发现的问题：

- 非运行状态下，源 `MeshRenderer` 可见，所以树 shader 可以正常接收和投射阴影。
- 运行状态下，源 renderer 会被关闭，可见渲染改由 `Graphics.RenderMeshIndirect` 提交。
- 当前 NWRP 主光阴影图使用 `DrawShadows(cullingResults)`，只能看到 Unity culling 阶段存在的 renderer，看不到 indirect draw。

实现的过渡方案：

- 缓存源 renderer 的原始 `enabled`、`shadowCastingMode`、`receiveShadows` 和 layer。
- 运行时当 `castShadows = true`，在 NWRP culling 前将源 renderer 切到 `ShadowCastingMode.ShadowsOnly`。
- indirect forward draw 固定使用 `ShadowCastingMode.Off`，避免重复提交重型阴影绘制。
- 当 `castShadows = false`，源 renderer 保持关闭，只保留 indirect forward 渲染。
- 在 disable、destroy、编辑器非运行 update 和 reinitialize 时恢复源 renderer 原始状态。
- 运行时重新初始化会读取缓存的源 renderer 状态，而不是读取临时 disabled / shadows-only 状态，避免实例 buffer 被误重建为空。

性能说明：

- 这是树木投影的过渡桥接方案，能在当前 NWRP 阴影架构下恢复正确性，但不是最终 GPU-driven shadow 方案。
- 草和灌木应继续放在独立 `VegetationIndirectRenderer` 上，并保持 `castShadows = false`。
- 后续大规模树木投影应实现独立 `NWRPFeature/NWRPPass`，用共享或光源视角 GPU visibility 数据提交 vegetation shadow draw。

## Instancing Include

- `VegetationIndirectInstancing.hlsl` 保持 `_VisibleVegetationBuffer` 矩阵接口和 compute buffer stride 不变。
- 移除 shader 侧矩阵求逆依赖，避免 Unity D3D shader 编译失败。
- 恢复原始路径中更稳定的 `transpose` 近似来写入 `unity_WorldToObject`。
- 注释中明确该近似适合当前植被路径，不应直接复用于未来非均匀缩放树木或更严格的 ShadowCaster 路径。

## Variant 与移动端策略

- 迁移后的环境植被 shader 不再包含 URP include 和 URP 光照/阴影 keyword。
- shader 中不再保留 `_MAIN_LIGHT_SHADOWS`、`_MAIN_LIGHT_SHADOWS_SCREEN`、`_SHADOWS_SOFT` 或 URP additional light variants。
- 接收阴影通过 runtime uniform 控制，不新增 keyword。
- 不新增 fullscreen pass、RenderTexture，也不改变 compute culling 流程。
- 保留的主要宽变体只有 instancing，以及已有需求中的 fog。

## AGENTS 更新

- 根 `AGENTS.md` 已补充环境 shader 归属路径：`Assets/NWRP/Shaders/Environment`。
- 增加植被相关规则：
  - Environment / vegetation shader 禁止包含 URP shader library。
  - 草和树保持 shader 拆分。
  - 草/灌木默认只接收阴影。
  - 树木阴影复杂度应放在树专用 shader 或未来 NWRP pass 中，不塞回 `WorldGrass`。

## 验证记录

Unity MCP 验证：

- `AssetDatabase.Refresh(ForceUpdate)` 完成。
- refresh 后编辑器状态：
  - `isCompiling=False`
  - `isUpdating=False`
  - `playmode=False`
- `VegetationIndirectRenderer` 可解析为有效 `MonoBehaviour`。
- shader 查找结果：
  - `NewWorld/Env/WorldGrass found=True supported=True`
  - `NewWorld/Env/Shrub found=True supported=True`
  - `NewWorld/Env/Tree found=True supported=True`
  - `NewWorld/Env/TreeLeaf found=True supported=True`
- Console 按 `Assets/NWRP` 过滤后，错误和警告均为 0。

静态检查：

- 环境植被 shader 不再包含 URP package/include 引用或 URP shadow keyword。
- `VegetationIndirectInstancing.hlsl` 中不再包含 shader 侧矩阵求逆调用。

已知非项目噪声：

- Unity MCP 自身临时日志存储曾报告 `Temp/mcp-server/ai-editor-logs*.txt` sharing violation。该日志不是 NWRP C# 编译错误，也不是 shader import 错误。

## 当前限制

- 运行时树木投影已通过当前 shadow-only 源 renderer bridge 恢复正确性，但还不是完全 GPU-driven。
- shadow-only 源 renderer 的 culling 仍跟随当前相机侧植被 culling 策略，用于控制编辑器和运行时成本。
- 树木投影 renderer 的 cull distance 应大于草，避免阴影提前消失或跳变。
- 后续应将树木阴影提交迁入独立 NWRP vegetation shadow pass，而不是长期依赖源 renderer fallback。
