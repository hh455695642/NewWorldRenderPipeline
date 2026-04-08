# Phase 2 - Base/Effect Shader 移植（16 个）

**日期**: 2026-03-31  
**状态**: ✅ 代码完成，待引擎验证

---

## 目标

将 URPShaderCodeSample 中 15 个 Base shader + 1 个 Effect shader 移植到 NWRP，完全脱离 URP 依赖。

## HLSL 库增补

### 修改 4 个文件

| 文件 | 新增内容 |
|------|---------|
| Common.hlsl | `TRANSFORM_TEX(uv, name)` 宏, `DecodeHDREnvironment()` 函数 |
| UnityInput.hlsl | `unity_FogParams` / `unity_FogColor` 全局变量, `TEXTURECUBE(unity_SpecCube0)` + `SAMPLER` 声明 |
| SpaceTransforms.hlsl | `GetOddNegativeScale()`, `ComputeScreenPos()` |
| Core.hlsl | `#include "Fog.hlsl"` |

### 新建 1 个文件

| 文件 | 内容 |
|------|------|
| **Fog.hlsl** | `ComputeFogFactor()` + `MixFog()` + `MixFogColor()`，支持 FOG_LINEAR/EXP/EXP2 三种模式 |

## 移植的 16 个 Shader

### Base（14 个）—— `NWRP/Shaders/Base/`

| Shader | 菜单路径 | 核心功能 |
|--------|---------|---------|
| VertexColor | NewWorld/Base/VertexColor | 顶点色直接渲染 |
| NormalCheck | NewWorld/Base/NormalCheck | 法线可视化调试 |
| TangentCheck | NewWorld/Base/TangentCheck | 切线可视化调试 |
| UVCheck | NewWorld/Base/UVCheck | UV 坐标可视化 |
| Transparent | NewWorld/Base/Transparent | 透明混合（ZWrite Off + SrcAlpha Blend） |
| VertexAnimation | NewWorld/Base/VertexAnimation | Sin 驱动的弹跳顶点动画 |
| Texture | NewWorld/Base/Texture | 基础纹理采样 + TRANSFORM_TEX |
| TextureScrollUV | NewWorld/Base/TextureScrollUV | _Time 驱动 UV 滚动 |
| AlphaTest | NewWorld/Base/AlphaTest | clip() 透明度裁剪 |
| Noise | NewWorld/Base/Noise | 伪随机噪声函数 |
| BitangentCheck | NewWorld/Base/BitangentCheck | 副切线可视化 + GetOddNegativeScale |
| TextureScreenSpace | NewWorld/Base/TextureScreenSpace | 屏幕空间纹理映射 + ComputeScreenPos |
| Fog | NewWorld/Base/Fog | 三种雾效模式（multi_compile_fog） |
| Fresnel | NewWorld/Base/Fresnel | 菲涅尔边缘光 + 反射探针 Cubemap |

### Effect（1 个）—— `NWRP/Shaders/Effect/`

| Shader | 菜单路径 | 核心功能 |
|--------|---------|---------|
| Fractal | NewWorld/Effect/Fractal (Mandelbrot) | Mandelbrot 分形集合实时渲染 |

## 每个 Shader 的统一改动

1. 菜单路径 `Lakehani/URP/...` → `NewWorld/...`
2. 删除 `"RenderPipeline"="UniversalRenderPipeline"` tag
3. Pass 添加 `Name "NewWorldUnlit"` + `Tags { "LightMode" = "NewWorldUnlit" }`
4. include 改为 `../../ShaderLibrary/Core.hlsl`（我们自己的库）
5. 删除所有 `Packages/com.unity.render-pipelines.universal/...` 引用

## C# 管线

**零改动**。所有全局变量由 Unity 引擎核心自动设置。

## URP 隔离验证

`grep -r "com.unity.render-pipelines" Assets/NWRP/` → **0 匹配**，完全独立。

## 验证清单

- [ ] Unity Console 零编译错误
- [ ] VertexColor/NormalCheck/TangentCheck/UVCheck：赋材质后能看到对应的调试颜色
- [ ] Transparent：半透明颜色，可以看穿物体
- [ ] VertexAnimation：物体随时间弹跳
- [ ] Texture：纹理显示正常，Tiling/Offset 生效
- [ ] TextureScrollUV：纹理随时间滚动
- [ ] AlphaTest：白色区域显示，黑色区域被裁剪
- [ ] TextureScreenSpace：纹理固定在屏幕空间
- [ ] Fog：`Lighting > Environment > Fog` 开启后，远处物体渐变为雾色
- [ ] Fresnel：边缘发光；启用 Reflection 后显示环境反射
- [ ] Fractal：Mandelbrot 分形动画缩放

## 下一步（Phase 3）

- 实现 `Lighting.hlsl`（Light 结构体、GetMainLight、LightingLambert、LightingSpecular）
- C# 端 CameraRenderer 添加 `SetupLights()` 传递主光源数据
- 移植 Lambert、Half-Lambert、Phong、Blinn-Phong 等基础光照 Shader
