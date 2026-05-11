# Phase24 URP-Compatible Naming / Dependency Closure

日期：`2026-05-11`

## 概要

本阶段围绕 NWRP 当前阶段收口展开，目标不是删除项目中的 URP 包依赖，而是确认并修正 `Assets/NWRP` 自有 runtime / shader 不再依赖 URP 包源码。

项目仍允许在 `Packages/manifest.json` 中保留 `com.unity.render-pipelines.universal`，用于测试、源码参考和迁移对照。但 NWRP 自身必须保持独立：运行时代码不使用 `UnityEngine.Rendering.Universal`，NWRP-owned shader 不 include `Packages/com.unity.render-pipelines.universal/...`。

同时，本阶段明确保留经典 URP 风格命名，方便后续从 URP shader 迁移。例如 `_CameraDepthTexture`、`_CameraOpaqueTexture`、`TransformObjectToWorld`、`GetNormalizedScreenSpaceUV`、`SampleSceneDepth` 这类名称可以继续存在，但实现必须来自 NWRP 自有 ShaderLibrary 或 Unity Core，而不是 URP package。

## 修改文件

- `AGENTS.md`
- `Assets/NWRP/Runtime/AGENTS.md`
- `Assets/NWRP/ShaderLibrary/AGENTS.md`
- `Assets/NWRP/ShaderLibrary/NWRPBlitCoreCompat.hlsl`
- `Assets/NWRP/ShaderLibrary/Shadows.hlsl`
- `Assets/NWRP/Shaders/Utils/CoreBlit.shader`
- `Assets/NWRP/Shaders/Utils/CoreBlitColorAndDepth.shader`

## 解决的问题

### NWRP utility blit shader 仍 include URP ShaderLibrary

`Hidden/NWRP/CoreBlit` 和 `Hidden/NWRP/CoreBlitColorAndDepth` 原本沿用了 URP 14 的 utility shader 结构，其中包含：

- `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
- `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl`

这会导致 NWRP 自有 shader 在编译层面依赖 URP 包源码，不符合当前“自定义 SRP 只依赖 Core”的收口目标。

本阶段改为新增 `NWRPBlitCoreCompat.hlsl`：

- 只 include `com.unity.render-pipelines.core`。
- 提供 Unity Core `Blit.hlsl` / `BlitColorAndDepth.hlsl` 所需的 `TEXTURE2D_X`、`SAMPLE_TEXTURE2D_X_LOD`、`SLICE_ARRAY_INDEX` 等宏。
- 保留 URP 风格 helper 名称，保证迁移 shader 和阅读 Unity Core blit 逻辑时仍然直观。
- 不引入 URP package include。

### URP debug-only blit variant 不适合 NWRP baseline

原 `CoreBlit.shader` 中保留了 URP Debug Display 相关 pass：

- `BilinearDebugDraw`
- `NearestDebugDraw`
- `_LINEAR_TO_SRGB_CONVERSION`
- `DEBUG_DISPLAY`

NWRP 当前没有接入 URP debug display 系统，保留这些 pass 会额外拉入 URP debug include，并增加无意义 variant。

本阶段移除这两个 debug-only pass。保留当前 NWRP 实际使用的 Core Blitter pass，包括 nearest / bilinear / quad / padding / octahedral 相关路径，确保 `CopyColor`、`FinalBlit` 和 Unity Core `Blitter` 初始化仍可工作。

### Main light Medium PCF shader warning

`Lake.shader` 复验时暴露了 `Shadows.hlsl` 中 `SampleMainLightStaticShadowAtCoord` 的 D3D warning：

```text
use of potentially uninitialized variable
```

问题来自 main light 9-tap PCF 使用宏展开，部分后端对宏内局部变量初始化分析不稳定。

本阶段将 main light Medium PCF 采样改为显式函数体：

- 移除 `NWRP_SAMPLE_MAIN_LIGHT_TENT9` 宏。
- 显式初始化 `visibility` 和 `uv`。
- `SampleMainLightStaticShadowAtCoord` 先走 Hard 默认值，再在 `MediumPCF` 模式下覆盖。

这不改变采样权重和对外阴影行为，只消除后端 warning 风险。

### AGENTS 规则与当前实现不同步

旧规则中仍写着 main light receiver filtering 只允许 `Hard`，但当前 runtime / shader / asset 已经存在显式 `MediumPCF` 模式。

本阶段同步规则：

- baseline 默认仍是 `Hard`。
- `MediumPCF` 是已存在的 NWRP 显式资产开关。
- 这不代表恢复 soft shadow，也不代表允许新增 PCSS / EVSM / 更宽 PCF 阶梯。
- 明确 `Packages/manifest.json` 可以保留 URP 作为测试依赖。
- 明确 NWRP-owned runtime / shader 禁止依赖 URP package source。
- 明确 URP-compatible 命名允许保留，但实现必须归属 NWRP 或 Unity Core。

## 关键实现

### NWRPBlitCoreCompat.hlsl

该 include 是一个极薄的 Core Blitter 兼容层，不承担通用 shader library 职责。

主要内容：

- include `Core/ShaderLibrary/Common.hlsl`
- include `Core/ShaderLibrary/Packing.hlsl`
- include `Core/ShaderLibrary/UnityInstancing.hlsl`
- 定义 stereo / multiview 下的 `unity_StereoEyeIndex`
- 定义 `TEXTURE2D_X` / `LOAD_TEXTURE2D_X` / `SAMPLE_TEXTURE2D_X` / gather 相关宏

这些名字沿用 URP 风格，是为了兼容 Unity Core `Blit.hlsl` 和后续迁移经验；但文件本身不依赖 URP。

### CoreBlit.shader

`Hidden/NWRP/CoreBlit` 的 include 改为：

```hlsl
#include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
```

保留的 variant：

- `DISABLE_TEXTURE2D_X_ARRAY`
- `BLIT_SINGLE_SLICE`
- `BLIT_DECODE_HDR`，仅用于 octahedral HDR decode pass

移除的 variant：

- `_LINEAR_TO_SRGB_CONVERSION`
- `DEBUG_DISPLAY`

这些移除项属于 URP debug display 路径，不属于当前 NWRP baseline。

### CoreBlitColorAndDepth.shader

`Hidden/NWRP/CoreBlitColorAndDepth` 同样改为 NWRP 兼容 include + Unity Core utility include：

```hlsl
#include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/BlitColorAndDepth.hlsl"
```

该 shader 保留两个 pass：

- Color Only
- Color And Depth

当前 NWRP 仍通过 Unity Core `Blitter.Initialize(coreBlitShader, coreBlitColorAndDepthShader)` 初始化，不改 C# 路径，避免扩大风险。

### Shadows.hlsl

main light Medium PCF 仍是 3x3 tent kernel：

- 权重总和为 16。
- sample UV 继续经过 cascade atlas rect clamp。
- `_MainLightShadowFilterRadius` 继续作为 receiver-side texel radius。

本阶段只把宏展开改为显式函数体，修复编译器 warning，不新增 keyword，也不改变 `Hard` 默认路径。

## 性能与移动端策略

- 不新增 RenderPass。
- 不新增 RenderTexture。
- 不新增 fullscreen blit。
- 不改 runtime `Blitter` C# 调度路径。
- 移除 URP debug display pass，减少无效 shader pass 和 debug variant。
- `MediumPCF` 仍是显式资产开关，移动端 baseline 继续建议使用 `Hard`。
- `CoreBlit` 保留 GLES fallback 所需结构，继续兼容 OpenGLES 路径。

## Shader Variant 风险

本阶段没有给业务材质新增 keyword。

保留的 blit utility keyword 只存在于 hidden utility shader：

- `DISABLE_TEXTURE2D_X_ARRAY`
- `BLIT_SINGLE_SLICE`
- `BLIT_DECODE_HDR`

移除的 URP debug display keyword：

- `DEBUG_DISPLAY`
- `_LINEAR_TO_SRGB_CONVERSION`

由于这些 shader 是 `Hidden/NWRP/...` utility shader，不会叠加到 lit / environment / water 等业务 shader variant 矩阵中。

## 验证记录

### 依赖扫描

对 `Assets/NWRP` 执行扫描，排除 AGENTS 文档本身后，没有发现以下依赖：

- `Packages/com.unity.render-pipelines.universal`
- `UnityEngine.Rendering.Universal`
- `ScriptableRendererFeature`
- `ScriptableRenderPass`

这说明 NWRP 自有 runtime / shader 已经不依赖 URP package source。

### Shader 复验

Unity Editor 中复验以下 shader，结果均为 `HasErrors = false`：

- `Hidden/NWRP/CoreBlit`
- `Hidden/NWRP/CoreBlitColorAndDepth`
- `Hidden/NWRP/CopyDepth`
- `NewWorld/Env/Lake`
- `NewWorld/Lit/StandardLit`

之前 `Lake.shader` 的 include error 已经不复现，`SampleMainLightStaticShadowAtCoord` 的 D3D warning 已消失。

### Console

清空 Console 后执行 `AssetDatabase.Refresh(ForceUpdate)`，Console 结果为空。

### 画面冒烟检查

当前打开场景：

- `Assets/NWRP/Tests/Scenes/MaterialSampleScene.unity`

使用 `Main Camera` 截图，材质样例场景可以正常渲染。Game View 直接截图曾出现纯黑，但相机直出截图正常，因此不作为 CoreBlit 失败证据。

## 当前需要复核的点

- `Main Camera` 上仍存在 `UniversalAdditionalCameraData` 组件，这是场景/测试资产层面的 URP 遗留组件，不在 `Assets/NWRP` 代码依赖范围内。本阶段不删除。
- 项目中 `Packages/manifest.json` 仍保留 URP，用于测试和参考，这是预期行为。
- 第三方 `Assets/Beautify/URP`、历史 `Assets/URPShaderCodeSample` 删除/迁移状态属于当前工作区已有改动，本阶段未接管。

## 后续方向

- 后续迁移 URP shader 时，优先在 NWRP ShaderLibrary 中提供同名 helper 或薄 alias，而不是 include URP ShaderLibrary。
- 如果需要更多 URP-compatible helper，应分层放置：公共迁移 helper 放 ShaderLibrary，功能专用 helper 保持在 shader family 本地 include。
- 若后续要清理场景中的 `UniversalAdditionalCameraData`、URP asset 或第三方 URP 插件，应作为单独资产整理阶段处理，避免和 NWRP runtime/shader 独立性混在一起。
- `CoreBlit` 后续如果只需要 final blit / copy color，可以再考虑裁剪 octahedral 相关 pass；本阶段保留它们是为了维持 Unity Core `Blitter` 兼容和低风险收口。
