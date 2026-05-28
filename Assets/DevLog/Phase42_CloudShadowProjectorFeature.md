# Phase42 CloudShadowProjector 投影云影与扰动

日期: `2026-05-28`

## 概要

本阶段补齐 NWRP 的投影云影能力，采用和 Valley Height Fog 一致的可插拔 Feature 形态：

- `CloudShadowProjectorFeature` 只负责按 Volume 状态入队。
- `CloudShadowProjectorPass` 固定运行在 `NWRPPassEvent.AfterTransparent`。
- `NWRPCloudShadowProjector` 作为主配置源，参数全部走 Volume。
- Shader 使用 `_CameraDepthTexture` 重建 world position，再通过投影盒矩阵生成云影 UV。
- 双层云影独立控制贴图、投影盒、UV 动画、强度、边缘软化和阴影颜色。
- 新增全局 distortion 参数，用低成本 UV 扰动打散贴图重复感。

本阶段仍保持一个 fullscreen hidden shader、一个 pass、一个临时 color RT。深度不由该功能自动创建或拷贝，必须依赖 Renderer Data 已启用的 Depth Texture。

## 问题背景

云影适合做成屏幕空间投影贴花，而不是 CPU 遍历场景对象或绘制 decal mesh：

- 云影覆盖范围大，按对象绘制会带来 CPU culling / draw loop 成本。
- 移动端更需要稳定的 pass 数和可控带宽。
- 云影本质只需要 scene color、camera depth 和投影参数。

因此本阶段采用 fullscreen pass：

1. 从 `_CameraDepthTexture` 重建每个像素的世界坐标。
2. 用 `worldToProjector` 转到投影盒 local 空间。
3. 用 `local.xz + 0.5` 得到云影 UV。
4. 读取贴图 A 通道作为云影 alpha。
5. 对 scene color 做乘法染色。

用户反馈“云太重复”，因此在同一 pass 内增加 distortion。该功能不新增 pass、不新增 keyword，默认 strength 为 0，只有配置 distortion texture 且 strength 大于 0 时才采样扰动贴图。

## 修改文件

- `Assets/NWRP/Runtime/CloudShadows/NWRPCloudShadowProjector.cs`
- `Assets/NWRP/Runtime/CloudShadows/CloudShadowProjectorFeature.cs`
- `Assets/NWRP/Runtime/CloudShadows/Passes/CloudShadowProjectorPass.cs`
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Editor/Pipeline/NWRPRendererDataEditor.cs`
- `Assets/NWRP/Shaders/Environment/CloudShadowProjector.shader`
- `Assets/NWRP/Tests/Editor/ValleyHeightFogOverlayFeatureTests.cs`

## 核心实现

### 1. Volume 作为主配置源

新增:

```text
NWRPCloudShadowProjector
```

Volume 字段分为三组：

- Global: `enable`
- Distortion: `distortionTexture`、`distortionTiling`、`distortionOffset`、`distortionScroll`、`distortionStrength`
- Primary / Secondary Layer: `Enabled`、`Texture`、`Center`、`Rotation`、`Size`、`Tiling`、`Offset`、`Scroll`、`Intensity`、`EdgeSoftness`、`ShadowColor`

`IsActive()` 只要求全局开启，并且任意一层拥有有效 texture 和正 intensity。distortion 不是激活条件，避免只有扰动贴图时触发无意义 fullscreen pass。

### 2. Feature 入队条件

`CloudShadowProjectorFeature` 的职责保持很窄：

- `IsActive(ref frameData)` 检查 Volume 是否 active。
- `CanRun(ref frameData)` 额外检查 `rendererData.EnableDepthTexture == true`。
- 只有满足条件才 enqueue `CloudShadowProjectorPass`。
- target requirements 只请求 `requiresIntermediateColor = true`。

该功能不请求:

- `requiresDepthTexture`
- `requiresDepthTextureCopy`
- `requiresDepthTexturePrepass`

这保证云影不会在 Feature 内偷偷改变深度策略。是否生成 `_CameraDepthTexture` 仍由 Renderer Data 的 Depth Texture 开关决定。

### 3. Pass 数据上传

`CloudShadowProjectorPass` 每帧只上传固定数量的 uniform:

- 两张云影贴图。
- 两个 `worldToProjector` 矩阵。
- 两组 UV tiling / offset。
- 两组 scroll / intensity / edge softness。
- 两组 shadow color。
- 一组 distortion texture / UV / strength。

没有对象遍历，没有 CPU decal draw loop。执行时使用一个临时 color RT，先从 camera color 读入并写到 temp，再 copy 回 camera color，避免同 RT 读写。

### 4. Shader 投影与软边

新增 shader:

```text
Hidden/NWRP/Environment/CloudShadowProjector
```

Shader 只 include NWRP/core blit/depth reconstruction helper，不 include URP。

云影 alpha 计算:

```text
alpha = texture.a * boxSoftMask * intensity
```

投影盒边缘用:

- `smoothstep`
- `fwidth`

不使用 `clip()`，避免硬裁切边缘和移动端上的不稳定过渡。

最终染色为顺序乘法:

```text
sceneColor.rgb *= lerp(1, shadowColor.rgb, alpha)
```

双层按 primary -> secondary 顺序叠加，各自拥有独立 shadow color。

### 5. Distortion

新增 distortion 作为全局 UV 扰动场：

- `distortionTexture` 使用 RG 通道，采样结果 remap 到 `[-1, 1]`。
- `distortionTiling` 使用 world XZ 空间缩放，默认 `0.01`，适合大范围低频扰动。
- `distortionOffset` 支持静态偏移。
- `distortionScroll` 支持时间动画。
- `distortionStrength` 控制最终 UV 偏移幅度，范围 `0 - 0.25`。

Shader 中 distortion 只在 strength 大于 0 时通过 uniform branch 采样：

```text
if (distortionStrength <= 0)
    return 0
```

这样默认关闭时不增加纹理采样成本；开启时每个像素只增加一次 distortion texture 采样，并同时影响双层云影 UV。该策略比每层独立扰动更省带宽，适合作为移动端默认方案。

## 设计取舍

### 不自动补深度

云影依赖世界坐标重建，但本功能不负责创建 `_CameraDepthTexture`。原因：

- 深度贴图策略是 renderer-level 成本决策。
- 自动补深度会让一个视觉 Feature 隐式增加 depth copy / prepass。
- 移动端项目需要清晰知道哪些 Renderer Data 会产生深度带宽。

因此 depth disabled 时 CloudShadow 不入队，也不请求任何 depth requirement。

### 不新增 RenderPassEvent

云影需要影响透明之后的最终 scene color，同时又应早于后处理。因此直接复用现有:

```text
NWRPPassEvent.AfterTransparent
```

不新增主渲染器阶段，避免 pass order 继续膨胀。

### Distortion 不做 keyword

distortion 是强度和贴图驱动的运行时功能，不需要 variant:

- 关闭时 strength 为 0。
- 开启时通过 uniform branch 控制采样。
- 不使用 `shader_feature` / `multi_compile`。

这符合 NWRP 当前的 variant 控制原则。

## 性能与 Variant

CPU:

- 每帧固定上传少量矩阵、颜色、UV 参数。
- 无对象遍历。
- 无 per-decal draw loop。
- Feature duplicate 由 renderer/editor 侧复用逻辑限制。

GPU:

- 1 个 fullscreen cloud pass。
- 1 个临时 color RT，用于读写分离。
- 默认每层各 1 次云影 alpha 采样。
- distortion 关闭时不采样 distortion texture。
- distortion 开启时增加 1 次 RG 采样，同时服务双层。
- 无 MRT。

Shader Variant:

- 不新增 keyword。
- 不使用 `multi_compile`。
- 不使用 `shader_feature`。
- 不使用 `multi_compile_instancing`，因为该 shader 是 fullscreen hidden pass，不是材质实例化 pass。
- variant 数量保持 flat。

## 验证记录

Editor tests:

```text
NWRP.Editor.Tests
25 passed / 0 failed
```

覆盖内容:

- pass event 固定为 `AfterTransparent`。
- Feature 仅在 active Volume + Renderer Depth Texture 开启时入队。
- depth disabled 时不入队，也不请求 depth。
- target requirements active 时只请求 intermediate color。
- Volume 暴露双层独立字段和 distortion 字段。
- Editor AddCloudShadowProjectorFeature 能复用已有 feature。
- Shader 路径与名称正确。
- Shader 包含 `smoothstep`、`fwidth`、A 通道 alpha、乘法染色和 distortion uniform。
- Shader 不包含 `clip(`、URP include、`multi_compile`、`shader_feature`、`multi_compile_instancing`。

静态校验:

```text
CloudShadows runtime:
- no CopyDepthPass
- no DepthPrepass
- no requiresDepthTexture = true

CloudShadowProjector.shader:
- no render-pipelines.universal
- no clip(
- no #pragma multi_compile
- no #pragma shader_feature
- no #pragma multi_compile_instancing
```

## 当前限制与后续方向

- distortion 当前为全局一组参数，同时作用于双层云影；如果后续确实需要更强的美术独立性，可以扩展为每层 strength，但会增加参数和潜在采样成本。
- 当前没有 debug overlay。若需要排查投影盒和 alpha，可后续新增 editor/debug view，但不应进入默认移动端路径。
- distortion 默认关闭。推荐移动端只在云影贴图重复明显的镜头中开启，并控制 distortion texture 分辨率和采样频率。
- 如果未来引入云影高度衰减、法线遮蔽或体积云联动，应继续保持独立 uniform 控制，避免通过 keyword 堆叠 variant。
