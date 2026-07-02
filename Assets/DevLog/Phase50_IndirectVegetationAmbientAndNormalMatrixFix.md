# Phase50 Indirect 植被环境光与法线矩阵修复

日期：`2026-06-12`

## 概要

本阶段继续收口 `Map_LoopForest` 中 GPU indirect 树木自投影区域“死黑”的问题。

前两期 Phase48 / Phase49 主要解决的是 indirect-only 场景下主光阴影链路被错误关闭的问题：没有普通 `MeshRenderer` caster 时，仍然要让 vegetation indirect shadow caster 能建立 atlas、cascade matrix 和 receiver globals。本阶段处理的是另一条链路：阴影已经存在后，树叶被自投影压暗的区域没有获得稳定的间接光底色。

运行时只读检查确认：这不是没有天空球，也不是 Trilight ambient 完全为空。`Map_LoopForest` 运行时存在 `M_Sky_LoopForest`，环境 SH 非零，探针采样方向亮度大致为：

```text
SH_eval(up)   ~= 0.588
SH_eval(fwd)  ~= 0.191
SH_eval(down) ~= 0.044
```

真正的高风险点在 GPU indirect 可见绘制路径：

- `Graphics.RenderMeshIndirect(...)` 使用的 `RenderParams.lightProbeUsage = BlendProbes` 并不能保证 NWRP vegetation shader 在 indirect draw 中拿到稳定的 `unity_SH*`。
- TreeLeaf / Tree / Shrub / WorldGrass 的 forward pass 之前直接依赖 `SampleSH(...)`，当 indirect draw 未显式携带 SH 时，自投影区域只剩 shadow 后的直接光结果，容易压成死黑。
- 运行态探针确认 TreeLeaf 材质没有 `_NWRPVegetationSH*` 这类自定义属性。
- 树实例存在非均匀缩放样本，而旧的 indirect instancing path 使用 `unity_WorldToObject = transpose(instanceMatrix)` 近似。该近似在非均匀缩放下会污染法线、SH 采样方向和阴影 receiver bias。

本阶段正式修复这两处问题：C# 在 draw group 级别显式采样并上传 vegetation SH，shader 在 indirect path 中使用该 SH；同时 indirect instance 数据携带真实 `worldToObject`，避免用转置矩阵伪造 normal matrix。

## 修改文件

- `Assets/NWRP/Runtime/VegetationIndirectRendering/VegetationIndirectRenderer.cs`
- `Assets/NWRP/Shaders/Compute/Vegetation/VegetationCulling.compute`
- `Assets/NWRP/Shaders/Environment/Includes/VegetationIndirectInstancing.hlsl`
- `Assets/NWRP/Shaders/Environment/Tree.shader`
- `Assets/NWRP/Shaders/Environment/TreeLeaf.shader`
- `Assets/NWRP/Shaders/Environment/Shrub.shader`
- `Assets/NWRP/Shaders/Environment/WorldGrass.shader`

移动端 fallback shader 本阶段不改动。它们仍然服务于原始 `MeshRenderer` fallback 路径，并继续使用普通 `SampleSH(...)`。

## 关键修复

### 1. VegetationIndirectRenderer 显式上传 draw group 级 SH

`VegetationIndirectRenderer` 为每个 indirect draw group 增加自有 SH uniform：

```text
_NWRPVegetationUseCustomSH
_NWRPVegetationSHAr / _NWRPVegetationSHAg / _NWRPVegetationSHAb
_NWRPVegetationSHBr / _NWRPVegetationSHBg / _NWRPVegetationSHBb
_NWRPVegetationSHC
```

提交 `Graphics.RenderMeshIndirect(...)` 前，renderer 会根据当前 group 的 world bounds center 采样一份 SH：

```csharp
LightProbes.GetInterpolatedProbe(group.bounds.center, null, out sh);
```

如果当前场景没有 `LightmapSettings.lightProbes`，或运行时采样失败，则自然回退到：

```csharp
RenderSettings.ambientProbe
```

采样结果通过 Unity 的 `SphericalHarmonicsL2` 系数打包为 7 个向量，写入该 group 的 `MaterialPropertyBlock`。这样 indirect draw 不再依赖 SRP / Unity 内部是否为 `RenderMeshIndirect` 自动补齐 `unity_SH*`，而是有一条 NWRP 自己可控的环境光输入。

当前仍保留：

```csharp
renderParams.lightProbeUsage = LightProbeUsage.BlendProbes;
```

这主要是兼容已有 indirect material 行为。NWRP vegetation shader 的正式路径以 `_NWRPVegetationUseCustomSH` 和 `_NWRPVegetationSH*` 为准。

### 2. Shader 增加统一的 indirect SH 采样入口

`VegetationIndirectInstancing.hlsl` 新增：

```hlsl
half3 SampleVegetationIndirectSH(float3 normalWS)
```

该函数的行为分两类：

- 普通 `MeshRenderer` / 非 indirect path：`_NWRPVegetationUseCustomSH` 默认为 `0`，继续走 `SampleSH(normalWS)`。
- GPU indirect path：C# 通过 MPB 设置 `_NWRPVegetationUseCustomSH = 1`，shader 使用 `_NWRPVegetationSH*` 计算环境 SH。

本阶段同步替换了以下 shader 的 forward ambient 入口：

```text
Tree.shader
TreeLeaf.shader
Shrub.shader
WorldGrass.shader
```

它们不再直接调用 `SampleSH(...)` 作为唯一环境光来源，而是统一调用 `SampleVegetationIndirectSH(...)`。这样树干、树叶、灌木和世界草的 GPU indirect path 都可以获得显式 SH；非 indirect renderer 仍保持原行为。

### 3. 不新增 shader keyword，使用 uniform 控制

本阶段没有新增 `multi_compile`，也没有新增 `shader_feature_local`。

```text
新增 shader keyword: 0
新增 multi_compile: 0
新增 shader_feature_local: 0
```

是否使用自定义 SH 完全由 uniform 控制：

```text
_NWRPVegetationUseCustomSH
```

这符合移动端 variant 控制策略：环境光来源属于运行时 draw state，不应该为了它额外拆 shader variant。现有 variant 风险保持在原范围内：

- GPU instancing 仍依赖已有 `multi_compile_instancing`。
- TreeLeaf 仍保留既有本地 overlay keyword。
- 本阶段不叠加新的跨功能组合。

### 4. Indirect instance 数据携带真实 worldToObject

旧路径在 shader 中通过：

```hlsl
unity_WorldToObject = transpose(instanceMatrix);
```

近似构造 inverse matrix。该做法只在纯旋转 / 均匀缩放场景下相对安全；一旦树实例存在非均匀缩放，法线方向会被污染，进而影响：

- `TransformObjectToWorldNormal(...)`
- SH 采样方向
- shadow receiver bias 方向
- 树叶 alpha clip 区域的明暗边界

本阶段改为在 CPU 构建 instance 数据时写入真实矩阵：

```csharp
Matrix4x4 worldToObject
```

Compute culling 后的 visible buffer 也同步携带：

```hlsl
struct VisibleGrassInstance
{
    float4x4 localToWorld;
    float4x4 worldToObject;
};
```

shader `SetupInstancing()` 中直接赋值：

```hlsl
unity_ObjectToWorld = instanceData.localToWorld;
unity_WorldToObject = instanceData.worldToObject;
```

这样非均匀缩放树不再走错误的转置近似。修复后，树自投影边缘和暗部的法线、SH 与 bias 方向都基于真实 inverse matrix。

### 5. Buffer stride 更新

因为 instance 数据增加了 `worldToObject`，stride 同步调整：

```text
source instance stride  : 144 bytes
visible instance stride : 128 bytes
```

这是一个明确的移动端带宽 / 正确性取舍。相比恢复 CPU per-instance renderer loop，本方案仍保持 GPU-driven：

- 不增加 CPU per-instance draw。
- 不恢复 source `MeshRenderer` fallback。
- 不新增 RenderPass。
- 不新增 RT / Blit / MRT。

后续如果密集植被场景出现 buffer bandwidth 压力，可以考虑把 transform 数据压缩为 `float3x4 + normal matrix` 或基于实例约束做更紧凑编码。但在当前非均匀缩放已进入生产样本的前提下，优先保证法线矩阵正确。

## 与 Phase48 / Phase49 的关系

Phase48 修复的是主光阴影 atlas bootstrap：没有普通 caster 时，indirect caster 仍能让主光阴影路径建立有效 atlas。

Phase49 修复的是相机移动后的 cascade fallback：Unity 原生 culling 没有普通 caster 时，仍能为 indirect-only 阴影生成有效 cascade matrix。

Phase50 修复的是阴影接收后的环境光底色：树叶已经接收到自投影后，暗部必须仍然能从天空 / LightProbe / ambient probe 获得 SH。否则即使主光阴影链路正确，shadowed 区域也会因为 indirect draw 缺少 SH 而变成死黑。

三者不是同一个问题：

```text
Phase48: atlas 是否存在
Phase49: cascade matrix 是否有效
Phase50: shadowed pixel 是否有间接光底色
```

因此本阶段不通过“假 ambient 常量”硬抬亮，也不修改主光阴影开关，而是补齐 indirect vegetation forward shading 的真实 SH 输入。

## 性能与移动端策略

CPU：

- SH 只按 draw group / chunk 级采样，不按实例采样。
- 不引入大规模 CPU per-instance loop。
- 不恢复 `debugUseOriginalRenderer` 作为正式方案。
- 不把植被渲染退回 Unity 常规 renderer culling 路径。

GPU：

- 不新增 RenderPass。
- 不新增 RenderTexture。
- 不新增 full-screen Blit。
- 不新增 MRT。
- forward shader 只增加一次 uniform 分支和 SH 计算路径选择。
- visible instance buffer 增加 `worldToObject` 带宽，换取非均匀缩放下的法线正确性。

移动端取舍：

- 当前改动对 tile memory 没有新增 RT 压力。
- shader variant 数量不增长，包体和 warmup 压力不增加。
- buffer stride 增加需要在 Android / iOS 真机上继续观察密集植被场景的带宽压力。
- 如果后续确认所有植被实例都可约束为均匀缩放，可以再评估压缩 normal matrix；当前不能假设这一点。

## Variant 风险

本阶段 shader keyword 风险为零增长。

```text
新增 shader keyword: 0
新增 multi_compile: 0
新增 shader_feature_local: 0
```

`_NWRPVegetationUseCustomSH` 是普通 uniform，不产生 variant 组合。Tree / TreeLeaf / Shrub / WorldGrass 的 shader pass 数量和 LightMode tag 不变。

需要继续关注的既有风险：

- TreeLeaf alpha clip 在阴影与 forward pass 中仍是 overdraw 风险点。
- vegetation indirect path 仍依赖 GPU instancing / indirect buffer 正确绑定。
- TreeLeaf 本地 overlay variant 仍应保持局部，不应扩散到 Tree / Shrub / Grass。

## 验证记录

已完成静态检查：

```text
git diff --check
通过
```

已完成 Unity Editor 编译 / 刷新检查：

```text
AssetDatabase.Refresh
通过，无新增 Error / Exception
```

已完成 Play Mode 运行时初始化检查：

```text
VegetationIndirectRenderer initialized. Chunks=43, Instances=185
VegetationIndirectRenderer initialized. Chunks=14, Instances=984
```

已完成测试运行：

```text
NWRP.Editor.Tests
45 / 46 passed
```

剩余的 1 个失败为既有无关项：

```text
NWRP.Editor.Tests.ValleyHeightFogOverlayFeatureTests.ParabolaLineShaderUsesNwrpAfterFogContract
```

失败原因是该测试期望 shader 中存在 `#pragma multi_compile_instancing`，与本阶段 vegetation SH / normal matrix 修复无关。

本地临时编辑器测试曾覆盖以下约束：

- `GrassInstance` 包含 `worldToObject`。
- visible instance stride 与 HLSL struct 对齐。
- `VegetationIndirectInstancing.hlsl` 存在 `SampleVegetationIndirectSH(...)`。
- Tree / TreeLeaf / Shrub / WorldGrass forward ambient 已切到 `SampleVegetationIndirectSH(...)`。
- NWRP vegetation shader 没有新增 keyword。

由于 `Assets/NWRP/Tests/Editor/` 当前被 `.gitignore` 忽略，这组临时测试未作为正式测试资产提交。

## 当前边界与待复查

Unity MCP 在一次 domain reload 后断开，日志显示：

```text
Connection not available and auto-reconnect disabled for endpoint: /hub/mcp-server
```

因此最后一步“通过 MCP / Frame Debugger 反查 TreeLeaf indirect draw 的 MPB 已携带 `_NWRPVegetationUseCustomSH = 1` 和非零 `_NWRPVegetationSH*`”没有完成。代码路径已经静态对齐，但仍建议在 MCP 恢复后补做运行时确认。

后续建议复查：

1. 进入 `Map_LoopForest` Play Mode。
2. 选中 TreeLeaf indirect draw，确认 MPB 中存在：

```text
_NWRPVegetationUseCustomSH = 1
_NWRPVegetationSHAr / Ag / Ab
_NWRPVegetationSHBr / Bg / Bb
_NWRPVegetationSHC
```

3. 对比三种状态：

```text
Indirect 原路径
显式 SH 后
debugUseOriginalRenderer 原 MeshRenderer
```

4. 如果 SH 恢复后仍有局部 acne 或过暗边缘，再回到 receiver bias 调参；不建议用固定 ambient 常量硬抬亮。
5. Android / iOS 真机上继续观察 visible instance buffer stride 增加后的带宽成本。

