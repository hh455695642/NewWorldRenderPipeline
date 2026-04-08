# Phase 3.5 - NPR 风格化 Shader 移植

**日期**: 2026-03-31  
**状态**: ✅ 代码完成，待引擎验证

---

## 目标

移植 URPShaderCodeSample/NPR 文件夹中 5 个最具代表性的风格化 Shader 到 NWRP，**验证四层架构对自定义光照模型的支持能力**。

## 选择标准

从 19 个 NPR Shader 中选出 5 个，覆盖核心技术类别：

| Shader | 技术类别 | 为什么选 |
|--------|---------|---------|
| Cel Ramp | Ramp 贴图查找 | NPR 基石技术 |
| Cel Procedural | 程序化色阶 + fwidth 抗锯齿 | 无需美术资源的硬边光照 |
| Tone Based | 冷暖色调理论 | 完全不同的光照哲学 |
| Outline Shell | 双 Pass 背面扩张 | 管线级别改动（新增描边 Pass） |
| Stylized Highlight | 切线空间高光形状 | TBN 矩阵高级用法 |

### 跳过的（及原因）

- Ramp Fresnel/Specular: 与 Ramp 大同小异
- SDF 面部阴影: 需要专用 SDF 贴图资源
- Edge Detection 系列: 需要后处理框架（Phase 4+）
- Depth Offset Rimlight: 需要深度纹理 Pass
- 其他 Stylized Highlight 变体: Texture 版最具代表性

## ShaderLibrary 补充

### SpaceTransforms.hlsl 新增

| 函数/宏 | 用途 |
|---------|------|
| `TransformWViewToHClip()` | View → Clip 别名（兼容 URP 命名） |
| `UNITY_MATRIX_VP/V/I_V/P` | 矩阵宏别名 |
| `VertexNormalInputs` 结构体 | TBN 数据打包 |
| `GetVertexNormalInputs()` | 从 OS 法线/切线构建 WS TBN |
| `TransformWorldToTangent()` | 世界 → 切线空间 |
| `TransformTangentToWorldDir()` | 切线 → 世界空间 |
| `GetWorldSpaceViewDirRaw()` | 非归一化视线方向（顶点输出用） |

## C# 管线改动

### CameraRenderer.cs

| 改动 | 内容 |
|------|------|
| 新增 ShaderTagId | `"NewWorldOutline"` 用于描边 Pass |
| 新增描边绘制 | 在不透明物体之后、天空盒之前，独立 DrawRenderers 调用描边 Pass |

## 新建 5 个 NPR Shader

### `Shaders/NPR/` 目录

| Shader | 菜单路径 | 核心技术 |
|--------|---------|---------|
| CelShading Ramp | NewWorld/NPR/CelShading (Ramp) | NdotL → 1D Ramp 纹理采样 |
| CelShading Procedural | NewWorld/NPR/CelShading (Procedural) | Half-Lambert + smoothstep 色阶 + fwidth 抗锯齿 |
| ToneBasedShading | NewWorld/NPR/ToneBasedShading | Gooch 冷暖色调插值 |
| Outline Shell | NewWorld/NPR/Outline (Shell Method) | 双 Pass: Forward + Outline(Cull Front, 法线扩张) |
| StylizedHighlight Texture | NewWorld/NPR/StylizedHighlight (Texture) | TBN 空间半角向量 → UV → 形状纹理采样 |

## 架构验证结果

- **Cel Ramp/Procedural**: 仅 `#include Lighting.hlsl`，完全不碰 BRDF.hlsl ✅
- **Tone Based**: 混合使用 Lighting.hlsl（光源数据）+ BRDF.hlsl（SpecularBlinnPhong 积木）✅
- **Outline Shell**: 仅 `#include Core.hlsl`，描边 Pass 不需要任何光照 ✅
- **Stylized Highlight**: 使用 Lighting.hlsl + SpaceTransforms.hlsl 的 TBN 工具 ✅

**结论：四层架构完全支持任意自定义光照模型，NPR Shader 可以自由选择需要的层级。**

## 验证清单

- [ ] Unity Console 零编译错误
- [ ] Cel Ramp: 准备一张 Ramp 纹理（硬边渐变），赋给材质后球体呈现色阶效果
- [ ] Cel Procedural: 调节 BackRange 和 DiffuseRampSmoothness，可见明暗二分色；调节 SpecularRange 可见硬边高光
- [ ] Tone Based: 设置冷色（蓝）暖色（黄），球体呈现冷暖色调过渡
- [ ] Outline Shell: 物体周围出现黑色描边；开启 PixelWidth 后描边在不同距离等宽
- [ ] Stylized Highlight: 准备一张高光形状纹理（如星形），高光区域呈对应形状

## 下一步（Phase 4）

- 阴影系统（ShadowMap）
- 后处理框架（支持 Edge Detection 等全屏效果）
- 法线贴图 / PBR 贴图扩展
