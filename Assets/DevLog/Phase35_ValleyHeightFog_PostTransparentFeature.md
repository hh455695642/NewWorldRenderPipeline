# Phase35 ValleyHeightFog Post-Transparent Feature

日期：`2026-05-21`

## 概要

本阶段为 NWRP 增加了一个可插拔的 `ValleyHeightFog` 后处理型环境雾功能，用于在不透明与透明渲染完成之后、正式 `PostProcess` 之前，对当前 camera color 做屏幕空间高度雾混合。

核心目标不是把高度雾直接写进主渲染流程，而是沿用 Phase5 建立的 `NWRPFeature / NWRPPass` 扩展模型：功能通过 `ValleyHeightFogFeature` 作为独立 feature 插到 `NewWorldRenderPipelineAsset.featureSettings.features`，运行时由 Volume 控制开关、算法模式和参数。这样可以保持管线主流程稳定，同时允许项目在不同场景中按需启用或移除该雾效。

当前实现支持两套从 URP 项目迁移过来的算法：
- `SingleLayer`：对应 `ValleyHeightFog.shader`，单层高度雾，使用基础高度、距离、密度和一组程序噪声控制。
- `ThreeLayer`：对应 `ValleyHeightFog_3Layer.shader`，底层 / 中层 / 顶层三段高度雾，各自拥有密度、强度和噪声参数。

两套算法位于同一个 hidden shader 内的两个 pass，通过 Volume 的 `mode` 选择 shader pass index；没有使用 shader keyword，也没有新增 variant 组合。

## 修改范围

Runtime：
- `Assets/NWRP/Runtime/NWRPFrameData.cs`
- `Assets/NWRP/Runtime/NWRPRenderer.cs`
- `Assets/NWRP/Runtime/NWRPShaderIds.cs`
- `Assets/NWRP/Runtime/PostProcessing/NWRPValleyHeightFog.cs`
- `Assets/NWRP/Runtime/PostProcessing/ValleyHeightFogFeature.cs`
- `Assets/NWRP/Runtime/PostProcessing/Passes/ValleyHeightFogPass.cs`

ShaderLibrary：
- `Assets/NWRP/ShaderLibrary/DepthWorldReconstructionBlit.hlsl`

Shaders：
- `Assets/NWRP/Shaders/PostProcess/NWRP_ValleyHeightFog.shader`

Editor：
- `Assets/NWRP/Editor/NWRPValleyHeightFogEditor.cs`
- `Assets/NWRP/Editor/NewWorldRenderPipelineAssetEditor.cs`

Tests / Sample：
- `Assets/NWRP/Tests/EditMode/ValleyHeightFogVolumeTests.cs`
- `Assets/NWRP/Tests/Scenes/MaterialSampleScene/NWRP Volime Profile.asset`
- `Assets/Settings/NewWorldRP.asset`

## 接入点与执行顺序

`ValleyHeightFogPass` 的执行点固定为：

```text
NWRPPassEvent.AfterTransparent
```

因此实际顺序为：

```text
Opaque
    -> Skybox
    -> Transparent
    -> Valley Height Fog
    -> NWRP PostProcess
    -> DebugOverlay / FinalBlit
```

这个位置的取舍：
- 能处理透明物体，因为输入颜色是透明渲染后的当前 camera color。
- 不抢占正式后处理链路，Bloom / Tonemapping / Color Adjustments / Vignette / FXAA 仍然在后面执行。
- 不依赖 `_CameraOpaqueTexture`，避免把雾效锁死在 opaque 结果上。

当前 pass 读取：
- `_BlitTexture`：由 NWRP blit source 提供，代表当前透明完成后的 camera color。
- `_CameraDepthTexture`：用于重建 world position。

当前 pass 写入：
- 一个临时 color RT。
- 再 copy 回 `cameraColor`，并恢复 `cameraColor / cameraDepth` render target。

这是一个完整的 fullscreen fog blit 路径，成本比纯全局雾高，但功能定位是屏幕空间后处理高度雾，不侵入 forward shader。

## Depth Texture 依赖

高度雾需要 `_CameraDepthTexture` 重建世界坐标。Feature 激活时会请求：

```text
requiresIntermediateColor = true
requiresDepthTexture = true
```

如果管线全局 depth texture 关闭，或者 depth copy 被配置为 `AfterTransparents`，`ValleyHeightFogFeature` 会主动把 depth copy / depth prepass 提前到本 pass 可用的位置，保证 `AfterTransparent` 执行时 `_CameraDepthTexture` 已经有效。

该策略避免在主 renderer 中硬编码 ValleyHeightFog 特判，保持 feature 自己声明依赖。

## Volume 入口

Volume 菜单：

```text
NWRP/Post-processing/Valley Height Fog
```

运行时开关：
- `enable`

算法模式：
- `mode = SingleLayer`
- `mode = ThreeLayer`

公共参数：
- `fogColor`
- `fogStart`
- `fogLength`

`IsActive()` 只由 `active && enable` 决定。`mode` 只负责选择算法，不隐式开启或关闭 feature。

## SingleLayer 参数

`SingleLayer` 移植自 URP 版本 `ValleyHeightFog.shader`，默认值保持一致：

- `fogColor = (0.8, 0.9, 1, 1)`
- `fogBaseHeight = 50`
- `heightDensity = 0.3`
- `fogStart = 250`
- `fogLength = 100`
- `noiseScale = 0.005`
- `noiseIntensity = 20`
- `noiseSpeed = 0.1`
- `noiseRoughness = 2`
- `noisePersistance = 0.5`

shader 流程：
```text
sample _BlitTexture
sample _CameraDepthTexture
reconstruct positionWS
compute animated layered noise
offset base height
heightFactor = saturate(exp((dynamicBaseHeight - positionWS.y) * heightDensity))
distanceFactor = saturate((distance(camera, positionWS) - fogStart) / fogLength)
lerp(sceneColor, fogColor, heightFactor * distanceFactor * fogColor.a)
```

无效深度 / skybox 在 SingleLayer 中直接返回原 scene color，避免天空盒因为深度无限远产生不稳定雾化。

## ThreeLayer 参数

`ThreeLayer` 移植自 URP 版本 `ValleyHeightFog_3Layer.shader`，默认值保持一致：

Bottom layer：
- `bottomHeight = 10`
- `bottomFade = 6`
- `bottomDensity = 0.012`
- `bottomIntensity = 0.8`
- `bottomNoiseScale = 0.12`
- `bottomNoiseIntensity = 1`

Mid layer：
- `midHeight = 300`
- `midFade = 60`
- `midDensity = 0.003`
- `midIntensity = 0.5`
- `midNoiseScale = 0.003`
- `midNoiseIntensity = 1.1`

Top layer：
- `topIntensity = 0`
- `topDensity = 0.0005`
- `topNoiseScale = 0.005`
- `topNoiseIntensity = 1.5`

Global noise：
- `threeLayerNoiseSpeed = 0.15`
- `threeLayerNoiseRoughness = 2`
- `threeLayerNoisePersistance = 0.35`

距离控制仍复用公共：
- `fogStart`
- `fogLength`

三层组合逻辑：
```text
bottomFog = exp(-height * bottomDensity) * bottomHeightFade * bottomNoise * bottomIntensity
midFog    = exp(-height * midDensity)    * midHeightFade    * midNoise    * midIntensity
topFog    = topIntensity * exp(-max(height - midHeight, 0) * topDensity) * topNoise

heightFactor = saturate(max(bottomFog + midFog, topFog))
distanceFactor = saturate((distance(camera, positionWS) - fogStart) / fogLength)
fogFactor = heightFactor * distanceFactor
```

ThreeLayer 保留了源 shader 的 skybox 修正：当深度无效时，会根据当前 view direction 构造一个安全的远处 world position，并放大 bottom / mid fade，避免天空盒边缘形成硬线。

## Shader 结构

`Hidden/NWRP/PostProcess/ValleyHeightFog` 现在包含两个 pass：

```text
Pass 0: Valley Height Fog
Pass 1: Valley Height Fog 3 Layer
```

C# 侧选择：
```csharp
SingleLayer -> ValleyHeightFogPass.SingleLayerShaderPass
ThreeLayer  -> ValleyHeightFogPass.ThreeLayerShaderPass
```

shader 只 include：
- `NWRPBlitCoreCompat.hlsl`
- `DepthWorldReconstructionBlit.hlsl`
- SRP Core `Blit.hlsl`

明确不 include：
- URP `Core.hlsl`
- URP `DeclareDepthTexture.hlsl`
- 任何 `Packages/com.unity.render-pipelines.universal/...`

颜色源明确为 `_BlitTexture`，深度源明确为 `_CameraDepthTexture`。

## Editor 面板

`NWRPValleyHeightFogEditor` 会根据 `mode` 切换参数面板。

`SingleLayer` 显示：
- `Fog Base`
- `Fog Noise`

`ThreeLayer` 显示：
- `Fog Base`
- `Bottom Fog Layer`
- `Mid Fog Layer`
- `Top Fog Layer`
- `Distance Fog`
- `Noise Settings`

这样避免两套算法参数同时堆在 Inspector 中，减少调参噪音，也降低误改另一套算法参数的概率。

`NewWorldRenderPipelineAssetEditor` 增加了 `Add Valley Height Fog Feature` 辅助按钮，用于把 feature 作为 sub-asset 加到 explicit feature list。该按钮只负责创建 / 挂载 feature，不把 ValleyHeightFog 做成 pipeline asset 内置开关。

## 参数上传与安全 clamp

Runtime 上传的 shader globals：
- `_NWRPValleyHeightFogColor`
- `_NWRPValleyHeightFogHeightParams`
- `_NWRPValleyHeightFogDistanceParams`
- `_NWRPValleyHeightFogNoiseParams`
- `_NWRPValleyHeightFogNoiseParams2`
- `_NWRPValleyHeightFogBottomParams`
- `_NWRPValleyHeightFogBottomNoiseParams`
- `_NWRPValleyHeightFogMidParams`
- `_NWRPValleyHeightFogMidNoiseParams`
- `_NWRPValleyHeightFogTopParams`
- `_NWRPValleyHeightFogThreeLayerNoiseParams`

上传前做了必要 clamp：
- `fogLength >= 0.001`
- `heightDensity >= 0.01`
- `noiseRoughness >= 0.001`
- `noisePersistance` 限制在 `[0, 1]`
- 三层 density / fade / noise scale / noise intensity 按 Volume 字段范围或安全下限裁剪

这些 clamp 避免除零、负 fade、负 density 或噪声参数越界造成的 shader 不稳定。

## 性能与移动端取舍

CPU：
- 增加一次 Volume 组件解析。
- 增加少量 global uniform 上传。
- 不增加 per-object / renderer list / culling 逻辑。

GPU：
- 激活 ValleyHeightFog 时增加 fullscreen fog blit。
- 当前实现需要一个临时 color RT，并 copy 回 camera color。
- SingleLayer 和 ThreeLayer 只执行其中一个 shader pass，不会同时跑两套算法。
- ThreeLayer 比 SingleLayer 更贵，因为最多会进行 bottom / mid / top 三组噪声采样。

带宽：
- 这是一次屏幕空间后处理雾，带宽成本高于 forward shader 内的普通雾混合。
- 但它可以统一处理透明后的最终颜色，并且不需要改所有材质 shader。
- 对移动端应作为显式 Volume 效果启用，不建议默认全项目常开。

后续如果需要进一步压低移动端成本，可以评估：
- 半分辨率 fog buffer + 双边上采样。
- ThreeLayer 噪声降采样或只对关键 layer 开噪声。
- 用蓝噪声 / 低频噪声纹理替代 3D procedural noise。
- 将无噪声或单层低成本雾固化进 forward shader，屏幕空间高度雾只用于特殊场景。

## Variant 风险

本阶段没有新增 shader keyword：
- 无 `multi_compile`
- 无 `shader_feature`
- 无 URP keyword
- 无 `_NOISE_DISTANCE_FADE` 之类迁移自 URP 的 keyword

算法切换使用 C# 选择 shader pass index，而不是 shader keyword。这样会增加 hidden shader 的 pass 数，但不会形成 keyword 组合爆炸，更符合 NWRP 当前的移动端 variant 控制策略。

## 验证记录

已完成 Unity / 静态验证：
- `AssetDatabase.Refresh` 通过。
- `NWRP.EditModeTests`：`11 / 11` 通过。
- `NWRP_ValleyHeightFog.shader` 导入状态：`IsSupported = true`。
- `NWRP_ValleyHeightFog.shader` 编译状态：`HasErrors = false`。
- `NWRP_ValleyHeightFog.shader` pass 数：`2`。
- Unity Console 最近错误为空。
- `git diff --check` 通过，仅有工作区 CRLF 提示。

静态扫描结果：
- ValleyHeightFog shader 中无 `_CameraOpaqueTexture`。
- ValleyHeightFog shader 中无 `multi_compile`。
- ValleyHeightFog shader 中无 `shader_feature`。
- ValleyHeightFog shader 中无 `render-pipelines.universal` include。
- ValleyHeightFog shader 中无旧 debug `worldDebugColor` 路径。

EditMode 覆盖点：
- `NWRPFrameData` 暴露 `valleyHeightFog` 与 `valleyHeightFogActive`。
- Volume 默认参数包含 SingleLayer 和 ThreeLayer 两套字段。
- `IsActive()` 只跟随 `active && enable`。
- Feature active 时请求 intermediate color 和 depth texture。
- Pass event 固定为 `AfterTransparent`。
- Pass 会根据 Volume `mode` 选择 shader pass index。
- Shader 静态约束无 URP include / keyword / `_CameraOpaqueTexture`。

## 当前边界与后续建议

- 当前 Feature 仍依赖 camera / asset 后处理开关：`supportsPostProcessing` 与 camera `renderPostProcessing` 必须允许后处理，Volume 效果才会执行。
- 当前实现不是 NWRP 内置强制功能，必须在 NWRP asset explicit feature list 中插入 `ValleyHeightFogFeature`。
- 当前没有加入 Frame Debugger 自动验证；人工检查时应确认顺序为 `Transparent -> Valley Height Fog -> NWRP PostProcess`。
- 当前没有做半分辨率优化，ThreeLayer 在低端移动设备上需要实机评估。
- 后续如果要继续扩展，不建议继续往同一个 feature 中堆更多无关屏幕空间效果；应保持 ValleyHeightFog 只负责山谷高度雾。
