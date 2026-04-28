# Phase16 管线收口：Instancing / Outline Feature / Camera Targets

Date: `2026-04-24`

## 概要

本阶段围绕资产迁移前的 NWRP 管线收口展开，不实现 URP 资产迁移工具，也不把 `UniversalForward` / `UniversalForwardOnly` 或 URP keyword 债务搬进 NWRP。

完成后的能力边界：

- NWRP-owned shader 的 Forward / ShadowCaster / DepthOnly 路径接入 GPU Instancing instance ID。
- 主光默认阴影过滤回到 Hard Shadow，并同步结构化字段与 legacy bridge 字段。
- Outline 从 Renderer 固定内建 pass 移出，改为独立 `OutlineFeature`，默认关闭。
- camera color/depth target 生命周期预留给后处理，默认路径仍直写 backbuffer，不分配中间 RT，不做 fullscreen blit。

## 核心实现

### 1. Shader Instancing 闭环

修改文件：

- `Assets/NWRP/ShaderLibrary/UnityInput.hlsl`
- `Assets/NWRP/ShaderLibrary/SpaceTransforms.hlsl`
- `Assets/NWRP/ShaderLibrary/Passes/ShadowCasterPass.hlsl`
- `Assets/NWRP/ShaderLibrary/Passes/DepthOnlyPass.hlsl`
- `Assets/NWRP/Shaders/**/*.shader`

完成内容：

- 在 `UnityInput.hlsl` 定义 `UNITY_MATRIX_M` / `UNITY_MATRIX_I_M` 等矩阵别名，并接入 SRP Core `UnityInstancing.hlsl`。
- `SpaceTransforms.hlsl` 新增 `GetObjectToWorldMatrix()` / `GetWorldToObjectMatrix()`，object/world 变换统一走 `UNITY_MATRIX_M` / `UNITY_MATRIX_I_M`。
- `ShadowCasterPass.hlsl` 与 `DepthOnlyPass.hlsl` 的 Attributes 接入 `UNITY_VERTEX_INPUT_INSTANCE_ID`，vertex 开头调用 `UNITY_SETUP_INSTANCE_ID(input)`。
- 所有 NWRP-owned shader 本地 Forward vertex 输入补齐 instance id 与 setup。

Variant 影响：

- 仅使用标准 `#pragma multi_compile_instancing`。
- 每个相关 pass 增加 instancing / non-instancing 两档。
- 不新增 URP keyword，不引入跨功能组合爆炸。

### 2. 主光默认 Hard Shadow

修改文件：

- `Assets/Settings/NewWorldRP.asset`

完成内容：

- `mainLightShadows.atlas.mainLightShadowFilterMode` 改为 `0`。
- legacy bridge `mainLightShadowFilterMode` 同步改为 `0`。
- 通过 Unity 资产实例确认运行时读取结果为 `Hard`，避免保存资产后被桥接字段回写成 `MediumPCF`。

未修改内容：

- 不改附加光阴影开关。
- 不改附加光阴影预算、距离、动态叠加等已有配置。

### 3. Outline Feature 化

修改文件：

- `Assets/NWRP/Runtime/OutlineFeature.cs`
- `Assets/NWRP/Runtime/Passes/DrawOutlinePass.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NewWorldRenderPipelineAsset.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`
- `Assets/Settings/NewWorldRP.asset`

完成内容：

- 新增 `OutlineFeature : NWRPFeature`。
- `DrawOutlinePass` 改为独立 pass，内部绘制 `NewWorldOutline` LightMode。
- `NWRPRenderer` 不再持有固定 `_drawOutlinePass`，也不再默认入队 Outline。
- `FeatureSettings` 增加 `OutlineSettings.enableOutline`，资产默认值为 `false`。
- Asset Inspector 显示 Built-in Outline toggle，并保留显式 feature list。

RenderPassEvent：

- Outline pass 保持 `NWRPPassEvent.Opaque`。
- 默认关闭时移动端 baseline 不额外执行 `DrawRenderers` 调度。

### 4. Camera Color / Depth Target 生命周期

修改文件：

- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPFeature.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`

完成内容：

- 扩展 `NWRPFrameTargets`，记录 backbuffer、camera color、camera depth，以及是否拥有 intermediate RT。
- 新增 `NWRPFrameTargetRequirements`。
- `NWRPFeature` 增加默认返回 false 的 target requirement hook，供后续 PostProcessFeature 请求 intermediate color/depth。
- `NWRPRenderer` 在构建 pass queue 前聚合 feature target requirements。
- 默认无 feature 请求时，camera color/depth 直接指向 `BuiltinRenderTextureType.CameraTarget`。
- 仅在 future feature 显式请求 intermediate color/depth 时才 `GetTemporaryRT`，并在 frame finally 释放。

移动端带宽约束：

- 默认路径不新增中间 RT。
- 默认路径不新增 fullscreen blit。
- 后处理开发时必须显式声明 target requirement，避免 hidden allocation。

## 未实现项

本阶段明确不做：

- 不开发 URP 材质迁移工具。
- 不在 `NWRPRenderer` 中匹配 `UniversalForward` / `UniversalForwardOnly`。
- 不迁移 URP Lit keyword 体系。
- 不实现具体 Transparent / PostProcess 效果。

后续资产迁移策略：

- 旧项目自定义 shader 按植被、角色、特效、环境等类型在 NWRP 中重新实现。
- URP 默认 Lit 资产后续只做最小 remap，不把 URP keyword 组合带入 NWRP。

## 验证记录

静态检查：

- 检查 NWRP shader vertex blocks：
  - 已覆盖 `#pragma multi_compile_instancing`。
  - 本地 `Attributes` 已接入 `UNITY_VERTEX_INPUT_INSTANCE_ID`。
  - vertex 入口已调用 `UNITY_SETUP_INSTANCE_ID`。
- `git diff --check`
  - 无 whitespace error。

Unity 验证：

- `AssetDatabase.Refresh`
  - Unity 编译完成。
  - Console 无 C# / shader import error。
- Game View 截图验证：
  - 样例场景正常渲染。
- 资产字段验证：
  - `MainFilter=Hard`
  - `Structured=Hard`
  - `Legacy=Hard`
  - `EnableOutline=False`

## 当前限制与后续方向

当前限制：

- Outline toggle 只控制内建 runtime fallback `OutlineFeature`；显式添加到 feature list 的 `OutlineFeature` 仍按 feature 自身启用状态执行。
- camera target 生命周期只预留接口，不执行最终 blit 或后处理链。

后续方向：

- 开发 Transparent 顺序时继续保持可选 pass，不进入默认移动端路径。
- 开发 PostProcessFeature 时通过 target requirement 显式请求 intermediate color/depth，并控制全屏 pass 数量。
- 资产迁移阶段优先重写旧项目自定义 shader，再考虑 URP 默认 Lit 的最小 remap。
