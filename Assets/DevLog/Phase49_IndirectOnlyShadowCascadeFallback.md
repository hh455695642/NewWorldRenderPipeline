# Phase49 Indirect-Only 树阴影相机位移 Cascade Fallback 修复

日期：`2026-06-11`

## 概要

本阶段继续收口 Phase48 的 indirect-only 树阴影问题，修复 `Map_LoopForest` 运行时 Game Camera 沿 Z 方向移动到约 `z = 11` 后，主光阴影被错误关闭的问题。

实际现象分为两层：

- 相机在 `z = 0` 时，树阴影可以正常出现。
- 相机移动到 `z = 10 ~ 11` 后，主光阴影 globals 被写成 disabled，阴影图切到 empty fallback。
- 初版手动 fallback 虽然让阴影不再消失，但在 `z = 11` 附近出现树阴影方向反向的问题，表现为阴影朝向光源方向。

最终确认：这不是材质 `ShadowCaster` pass、receiver shader 或 shadow atlas 采样的问题，而是主光阴影在 indirect-only 场景中缺少可用 cascade matrix 的问题。

Phase48 已经补上了 indirect caster 查询和 `allowEmptyAtlas`，解决了“没有普通 `MeshRenderer` caster 时不创建 atlas”的 bootstrap 漏洞。但在相机移动触发 cached shadow rebuild 或 realtime cascade 重算时，Unity 的：

```csharp
CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(...)
```

仍然会因为 `CullingResults` 中没有普通 shadow caster 而返回 `false`。GPU indirect 树木不属于 Unity 常规 renderer culling 集合，所以即使 `VegetationIndirectShadowPass` 后续可以向 atlas 写入阴影，主光阴影 pass 也会提前拿不到 cascade view / projection / split sphere，最终上传 disabled globals。

本阶段正式补齐这条链路：当场景中没有普通 caster，但存在 vegetation indirect shadow caster 时，主光阴影允许使用 camera frustum 推导 directional shadow cascade 数据，保证 shadow atlas、cascade matrix、split sphere 和 receiver globals 在 indirect-only 场景中仍然有效。

## 修改文件

- `Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs`
- `Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs`

## 关键修复

### 1. 主光 Cascade 计算增加 indirect-only fallback

`MainLightShadowPassUtils.ComputeCascadeData(...)` 新增参数：

```csharp
bool allowCameraFrustumFallback = false
```

默认行为保持不变：优先使用 Unity 的 `ComputeDirectionalShadowMatricesAndCullingPrimitives(...)`。只有调用方明确传入 `allowCameraFrustumFallback = true`，并且 Unity 原生计算失败时，才进入 NWRP 自己的 camera frustum fallback。

新增入口：

```csharp
MainLightShadowPassUtils.TryComputeDirectionalShadowCascade(...)
```

该函数的策略是：

1. 优先走 Unity 原生 directional shadow cascade 计算。
2. 原生路径失败时，如果没有开启 fallback，直接返回失败。
3. 只有 indirect-only shadow caster 存在时，才调用 `TryComputeCameraFrustumDirectionalShadowCascade(...)`。

这样避免把“空普通 caster 也能构建 cascade”的语义扩散到所有 shadow 调用点。

### 2. Camera Frustum Fallback 生成 cascade view / projection / split sphere

新增的 fallback 只在主光 directional shadow 场景下工作，不引入额外 shader keyword，也不创建额外 RT。

计算流程：

- 根据相机 near clip、有效 shadow distance 和当前 cascade split 推导当前 cascade 的 near / far。
- 使用 `Camera.CalculateFrustumCorners(...)` 得到该 cascade frustum 的 8 个 world-space corner。
- 以 8 个 corner 的中心和最大距离构建 cascade bounding sphere。
- 根据主光方向构建 light-space view matrix。
- 在 light-space 下包围 8 个 frustum corner，生成 orthographic projection matrix。
- 写入 `ShadowSplitData.cullingSphere` 和 `shadowCascadeBlendCullingFactor`。

这保证 indirect shadow pass 能拿到有效的：

```text
viewMatrix
projectionMatrix
cascade viewport
cascade split sphere
```

从而继续向主光 shadow atlas 写入 GPU indirect 树阴影。

### 3. 修正 fallback 光向符号，避免阴影反向

初版 fallback 复用了 receiver 侧的 light direction 语义：

```csharp
Vector3 lightDirection = -mainLight.transform.forward;
```

这个方向表示“表面指向光源”的方向，和 `_MainLightPosition` / `_ShadowLightDirection` 的接收端语义一致。但 shadow map view 需要沿“光线传播方向”看向场景，两者方向相反。

本阶段修正为：

```csharp
Vector3 shadowViewDirection = -lightDirection;
Quaternion lightRotation = Quaternion.LookRotation(shadowViewDirection, lightUp);
Vector3 lightPosition = center - shadowViewDirection * radius;
```

这样：

- receiver 侧仍保持原有 light direction 语义。
- caster 侧 shadow view 使用光线传播方向。
- `z = 11` 附近不再出现树阴影朝向光源方向的反向现象。

### 4. Cached Static + Dynamic Overlay 路径识别 indirect-only caster

`MainLightShadowStaticCachePass` 现在会区分：

```text
hasRegularShadowCasters
hasIndirectStaticCasters
hasIndirectDynamicCasters
```

当没有普通 caster，但存在 static 或 dynamic indirect caster 时：

- 允许 `ComputeCascadeData(...)` 使用 camera frustum fallback。
- 允许 `RenderMainLightShadowAtlas(...)` 使用 `allowEmptyAtlas`。
- 只在存在 static indirect caster 时注册 static cache target。
- dynamic overlay 后续仍然可以基于有效 cascade 数据写入 combined atlas。

这修复了相机移动导致 static cache rebuild 时，indirect-only 场景被错误判定为“无阴影”的问题。

### 5. Realtime 主光阴影路径同步补齐

`MainLightShadowCasterPass` 同步区分：

```text
hasRegularShadowCasters
hasIndirectShadowCasters
```

只有两者都不存在时，才上传 disabled globals。

当只有 indirect caster 存在时：

- 不创建 `ShadowDrawingSettings` 的普通 draw 提交。
- 不调用 `ScriptableRenderContext.DrawShadows(...)`。
- 仍然清空并绑定主光 realtime shadow atlas。
- 仍然计算 cascade matrix、split sphere 和 viewport。
- 仍然向 `MainLightShadowIndirectCasterContext` 注册 atlas target。

这保证 SceneView / 非 cached realtime 路径不会再次把主光阴影 globals 覆盖成 disabled。

### 6. Empty Atlas 有效性收口

`MainLightShadowPassUtils.RenderMainLightShadowAtlas(...)` 保留 `allowEmptyAtlas` 语义，并增加 `HasValidCascadeData(...)` 判断。

当没有普通 caster 但允许 empty atlas 时，函数不会盲目返回成功，而是要求 cache state 中已有有效 cascade data。也就是说，empty atlas 只是允许 indirect pass 后续写入，不代表可以跳过 cascade 数据构建。

## 与 Phase48 的关系

如果会话 `019eb572-6ecb-7312-8a29-3ad0074846dc` 对应 Phase48 的 indirect-only tree shadow bootstrap 修复，那么本阶段可以视为 Phase48 的遗漏补洞。

Phase48 解决的是：

- 主光阴影 pass 能识别 vegetation indirect caster。
- 没有普通 `MeshRenderer` caster 时允许创建 empty atlas。
- `VegetationIndirectShadowPass` 可以向主光 atlas 写入 indirect tree shadow。

Phase49 解决的是：

- 相机移动或 realtime 路径重算 cascade 时，即使 Unity 原生 culling 没有普通 caster，也能生成有效 cascade matrix。
- 避免 cached shadow rebuild 后 `_MainLightShadowParams` 被写成 `0`。
- 修正手动 fallback 中 shadow view direction 的符号，避免阴影方向反转。

因此，`z = 11` 阴影消失的问题本质上不是 Phase48 方向错了，而是 Phase48 只补了 atlas bootstrap，没有补完整 cascade data fallback。

## 性能与移动端策略

CPU：

- 新增逻辑只在“无普通 caster + 存在 vegetation indirect caster + Unity 原生 cascade 计算失败”时启用。
- fallback 每个 cascade 只计算 8 个 frustum corner、一个 bounding sphere 和一个 light-space ortho projection。
- 不引入 CPU per-instance loop。
- 不恢复 source `MeshRenderer` 的 `ShadowsOnly` fallback。
- 不把大规模植被阴影退回 Unity 普通 renderer culling 路径。

GPU：

- 不新增 shadow RT。
- 不新增 RenderPass。
- 不新增 full-screen blit。
- 不新增 shader keyword。
- indirect tree shadow 仍然复用已有 main-light atlas / combined atlas。
- GPU 成本仍然主要来自 vegetation shadow culling dispatch、indirect draw 和 TreeLeaf alpha clip overdraw。

移动端取舍：

- fallback 的 cascade 包围盒来自 camera frustum，可能比 Unity 原生 caster bounds 更保守，阴影有效范围会更稳定，但局部 shadow texel 利用率可能略低。
- 该成本只发生在 indirect-only shadow 场景，不影响普通 caster 路径。
- 仍建议控制主光 cascade 数量、atlas resolution 和树叶 alpha clip 投影面积。
- 草、灌木和 additional punctual light shadow 仍不默认纳入本阶段范围。

## Variant 风险

本阶段不修改 shader，不新增 keyword。

```text
新增 shader keyword: 0
新增 multi_compile: 0
新增 shader_feature_local: 0
```

主光阴影开关、cached shadow、dynamic overlay、vegetation indirect shadow 仍由 C# runtime 配置和 renderer data 控制，不通过 shader variant 叠加组合。

## 验证记录

已完成静态检查：

```text
git diff --check -- Assets/NWRP/Runtime/MainLightShadows/MainLightShadowPassUtils.cs Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowStaticCachePass.cs Assets/NWRP/Runtime/MainLightShadows/Passes/MainLightShadowCasterPass.cs
通过
```

已完成 Unity Editor 验证：

```text
AssetDatabase.Refresh
通过，无新增 Error / Exception
```

Play Mode 运行时探针确认：

```text
z = 0
_MainLightShadowParams.x = 1
_MainLightShadowCascadeCount = 2
shadowmap = NWRP_MainLightShadows_CombinedShadowmap

z = 11
_MainLightShadowParams.x = 1
_MainLightShadowCascadeCount = 2
shadowmap = NWRP_MainLightShadows_CombinedShadowmap
```

此前出问题时，`z = 10 ~ 11` 会变为：

```text
_MainLightShadowParams = (0, 0, 0, 0)
_MainLightShadowCascadeCount = 0
shadowmap = NWRP_MainLightShadows_EmptyShadowmap
```

修复后该状态不再复现。

已额外验证：

- `Map_LoopForest` 运行时相机移动到 `z = 11` 后，主光阴影 globals 仍保持启用。
- fallback 光向符号修正后，shadow view direction 与 receiver light direction 语义分离。
- Unity Console 清理后重新检查，无新增 Error / Exception / Assert。

截图记录：

```text
C:\Users\ruze\AppData\Local\Temp\DefaultCompany\NewWorldRenderPipeline\NWRPShadowDirectionProbeAfterSignFix\play_shadow_direction_z_11.png
```

## 当前边界与后续建议

- fallback cascade 使用 camera frustum 估算，不等同于 Unity 原生 caster bounds 裁剪；它优先保证 indirect-only 阴影链路不断，而不是追求最紧 shadow projection。
- 如果后续发现远距离相机移动时 cascade texel 利用率不足，可以考虑增加 shadow cache anchor 或 chunk / cluster 级 shadow bounds，但不建议恢复 CPU renderer fallback。
- 可以增加轻量 debug counter，统计当前帧是否进入 camera frustum fallback、indirect caster provider 数量、shadow target 数量和 indirect shadow draw 数量；默认应关闭。
- Android / iOS 真机仍建议用 RenderDoc / Xcode GPU Frame Capture 验证 atlas 写入、cascade viewport 和 TreeLeaf alpha clip overdraw。
- 本阶段不扩大 vegetation shadow 类型范围；树干、树叶以外的植被投影策略应继续单独评估。
