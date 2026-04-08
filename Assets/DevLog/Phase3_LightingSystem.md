# Phase 3 - 光照系统（从 Lambert 到 PBR）

**日期**: 2026-03-31  
**状态**: ✅ 代码完成，待引擎验证

---

## 目标

为 NWRP 构建完整的光照系统，采用**可组合积木式架构**（区别于 URP 的固定管线），从 Lambert 逐步构建到完整 PBR，同时保证未来可自由扩展任意光照模型。

## 架构设计：四层分离

```
Layer 0 │ Lighting.hlsl              │ 纯光源数据访问（Light 结构体、GetMainLight、GetAdditionalLight）
Layer 1 │ BRDF.hlsl                  │ BRDF 积木函数（Lambert/Phong/GGX/Fresnel...独立函数，不强制结构体）
Layer 2 │ GlobalIllumination.hlsl    │ 间接光（SH 球谐 + 反射探针）
Layer 3 │ LightingModels/*.hlsl      │ 预组装便捷模型（可选，一行调用）
```

**与 URP 的核心区别**: URP 强制 InputData → SurfaceData → BRDFData 管线；NWRP 每层独立可选，Toon/NPR Shader 只需 include Lighting.hlsl 即可完全自由写光照数学。

## C# 管线改动

### CameraRenderer.cs

| 改动 | 内容 |
|------|------|
| 新增 ShaderTagId | `"NewWorldForward"` 用于 Lit Pass，与 `"NewWorldUnlit"` 共存 |
| 新增 SetupLights() | 遍历 `_cullingResults.visibleLights`，上传光源数据到 GPU |
| 主方向光 | 取第一个 Directional Light，设置 `_MainLightPosition` / `_MainLightColor` |
| 附加光源 | 支持 Point / Spot Light，上传位置、颜色、衰减、聚光方向数组 |
| 性能优化 | Vector4[] 数组预分配复用，避免每帧 GC |

### NewWorldRenderPipelineAsset.cs

| 改动 | 内容 |
|------|------|
| 新增 Lighting Header | `maxAdditionalLights` 配置项 [0-8]，默认 4 |

## HLSL ShaderLibrary 新建/修改

### 新建 3 个文件 + 3 个 LightingModel

| 文件 | 内容 |
|------|------|
| **Lighting.hlsl** | Light 结构体、GetMainLight()、GetAdditionalLight(index, positionWS)、GetAdditionalLightsCount()、距离衰减（平滑逆平方）、聚光衰减 |
| **BRDF.hlsl** | DiffuseLambert / DiffuseHalfLambert / DiffuseBurley、SpecularPhong / SpecularBlinnPhong、D_GGX / V_SmithJointApprox / F_Schlick / F_SchlickRoughness / VF_Approximate、ComputeF0 / ComputeDiffuseColor / OneMinusReflectivityMetallic、粗糙度转换函数 |
| **GlobalIllumination.hlsl** | SampleSH(normalWS) L2 完整求值、SampleSH_L0() 快速常数项、SampleGradientAmbient() 三色渐变、SampleReflectionProbe() / SampleEnvironmentReflection() 反射探针采样 |
| **LightingModels/Lambert.hlsl** | EvaluateLambert() / EvaluateLambertAllLights() |
| **LightingModels/BlinnPhong.hlsl** | EvaluateBlinnPhong() / EvaluateBlinnPhongAllLights() |
| **LightingModels/StandardPBR.hlsl** | DirectBRDF_StandardPBR()（单光源 Cook-Torrance）、EvaluateStandardPBR()（全光源 + 间接光一站式） |

### 修改 2 个文件

| 文件 | 改动 |
|------|------|
| **UnityInput.hlsl** | 新增 `unity_AmbientSky` / `unity_AmbientEquator` / `unity_AmbientGround` 全局变量；更新 _MainLightPosition/_MainLightColor 注释 |
| **Common.hlsl** | 新增 `HALF_MIN` 和 `HALF_MIN_SQRT` 常量 |

## 新建 7 个 Lit Shader

| Shader | 菜单路径 | 核心功能 |
|--------|---------|---------|
| Lambert | NewWorld/Lit/Lambert | Lambert 漫反射（NdotL） |
| HalfLambert | NewWorld/Lit/HalfLambert | Half-Lambert（背光面更柔和） |
| Ambient | NewWorld/Lit/Ambient | SH 球谐环境光 + 三色渐变 |
| Phong | NewWorld/Lit/Phong | Lambert 漫反射 + Phong 镜面反射 + 环境光 |
| BlinnPhong | NewWorld/Lit/BlinnPhong | Lambert 漫反射 + Blinn-Phong 镜面反射 + 环境光 |
| MultiLight | NewWorld/Lit/MultiLight | 主光 + 附加光源循环（Blinn-Phong 模型） |
| PBR | NewWorld/Lit/PBR | 金属工作流 PBR（Cook-Torrance + SH + 反射探针） |

所有 Lit Shader 统一使用 `LightMode = "NewWorldForward"` Pass Tag。

## 验证清单

- [ ] Unity Console 零编译错误
- [ ] Lambert：方向光下球体有明暗面，旋转光源方向变化正确
- [ ] HalfLambert：背光面不完全黑，比 Lambert 更柔和
- [ ] Ambient：仅环境光无方向光时，物体根据法线方向有颜色变化
- [ ] Phong：调高 Smoothness 可见镜面高光亮点
- [ ] BlinnPhong：类似 Phong 但高光形状略有差异
- [ ] MultiLight：场景放置 Point/Spot Light，附加光源照亮范围正确，衰减自然
- [ ] PBR：Metallic=0（电介质）漫反射为主；Metallic=1（金属）仅有反射；Roughness 控制高光大小
- [ ] PBR 间接光：Roughness=0 时反射探针清晰；Roughness=1 时模糊

## 设计决策

| 决策 | 理由 |
|------|------|
| Lighting.hlsl 不加入 Core.hlsl | Unlit Shader 不应承担光照代码开销 |
| BRDF 函数只接收标量/向量参数 | 不强制结构体，最大化组合自由度 |
| LightingModels/ 为可选便捷层 | 一行调用方便快速原型，自定义 Shader 完全可以不用 |
| C# 数组预分配 | 避免每帧 GC 分配 Vector4 数组 |
| 附加光源聚光内角取外角 80% | Unity 默认行为近似，衰减曲线平滑 |

## 文件结构（Phase 3 新增部分）

```
Assets/NWRP/
├── ShaderLibrary/
│   ├── Lighting.hlsl              ← NEW  Layer 0: 光源数据
│   ├── BRDF.hlsl                  ← NEW  Layer 1: BRDF 积木
│   ├── GlobalIllumination.hlsl    ← NEW  Layer 2: SH + 反射探针
│   ├── LightingModels/            ← NEW  Layer 3: 便捷模型
│   │   ├── Lambert.hlsl
│   │   ├── BlinnPhong.hlsl
│   │   └── StandardPBR.hlsl
│   ├── Common.hlsl                ← 修改  +HALF_MIN/HALF_MIN_SQRT
│   └── UnityInput.hlsl            ← 修改  +ambient 变量
└── Shaders/
    └── Lit/                       ← NEW  光照 Shader 目录
        ├── NewWorld_Lit_Lambert.shader
        ├── NewWorld_Lit_HalfLambert.shader
        ├── NewWorld_Lit_Ambient.shader
        ├── NewWorld_Lit_Phong.shader
        ├── NewWorld_Lit_BlinnPhong.shader
        ├── NewWorld_Lit_MultiLight.shader
        └── NewWorld_Lit_PBR.shader
```

## 下一步（Phase 4）

- 阴影系统（ShadowMap 生成 + 接收）
- 法线贴图支持（PBR Shader 扩展）
- Albedo/Metallic/Roughness 贴图采样
- 更多自定义光照模型示例（Toon Shader 等）
