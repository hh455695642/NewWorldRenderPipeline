# Phase 4 - StandardLit PBR + 自定义材质面板

**日期**: 2026-04-01  
**状态**: ✅ 代码完成，待引擎验证

---

## 目标

1. 创建面向生产的完整纹理 PBR Shader（StandardLit）
2. 为所有 NewWorld Shader 提供统一的自定义材质面板

## 4.1 StandardLit Shader

### 贴图通道约定

| 贴图 | 通道 | 用途 |
|------|------|------|
| _BaseMap | RGB | Albedo 基础色 |
| _MaskMap | R | Metallic 金属度 |
| _MaskMap | G | AO 环境遮蔽 |
| _MaskMap | B | 预留（默认白） |
| _MaskMap | A | Smoothness 光滑度 |
| _NormalMap | AG→XY | 切线空间法线（Unity 打包格式） |
| _EmissiveMap | RGB | 自发光颜色 |

### 光照模型

- 直接光: Cook-Torrance (D_GGX + V_SmithJointApprox + F_Schlick)
- 间接漫反射: SH 球谐 × AO
- 间接镜面: 反射探针 × 环境 BRDF × AO
- 自发光: EmissiveMap × EmissiveColor（HDR，叠加到最终输出）
- 支持多光源（主光 + 附加光循环）

### 设计选择

| 决策 | 理由 |
|------|------|
| 未使用 EvaluateStandardPBR() 而是手动组装 | 需要更精细控制：AO 仅影响间接光、法线贴图、自发光叠加 |
| MaskMap 参数为乘法模式 | `mask.r * _Metallic`，贴图白色时参数全局控制；有贴图时逐像素控制 |
| 法线解码用 AG 通道 | Unity 默认法线贴图打包格式（DXT5nm: x→A, y→G） |

## 4.2 自定义材质面板 (NewWorldShaderGUI)

### 文件

`Assets/NWRP/Editor/NewWorldShaderGUI.cs`

### 特性

- **分组折叠**: Surface / PBR·Mask / Normal Map / Emission 四个可折叠分组
- **贴图+参数同行**: BaseMap 和 BaseColor 在同一行，NormalMap 和 NormalStrength 同一行
- **自适应**: 自动检测 Shader 有哪些属性，仅显示存在的分组
- **Other 兜底**: NPR Shader 的专有属性（如 _WarmColor / _CoolColor）自动归入 Other 区
- **隐藏默认项**: 不显示 Render Queue / Double Sided GI / Enable Instancing

### 覆盖范围

已为以下 Shader 添加 `CustomEditor "NWRP.Editor.NewWorldShaderGUI"`:
- 全部 8 个 Lit Shader（Lambert ~ StandardLit）
- 全部 5 个 NPR Shader

## 新建文件

| 文件 | 类型 |
|------|------|
| `Shaders/Lit/NewWorld_Lit_StandardLit.shader` | 完整纹理 PBR |
| `Editor/NewWorldShaderGUI.cs` | 自定义材质面板 |

## 验证清单

- [ ] Unity Console 零编译错误
- [ ] StandardLit: 赋 BaseMap/MaskMap/NormalMap/EmissiveMap 四张贴图
- [ ] StandardLit: Metallic=1 时金属质感，=0 时电介质
- [ ] StandardLit: Smoothness 控制高光锐利/模糊
- [ ] StandardLit: AO 贴图在缝隙处变暗（仅影响间接光）
- [ ] StandardLit: Normal Map 表面凹凸细节正确
- [ ] StandardLit: Emissive 区域发光，HDR 颜色强度可调
- [ ] 材质面板: 所有 Lit/NPR Shader 显示自定义面板而非默认面板
- [ ] 材质面板: 无 Render Queue / Double Sided GI 显示
- [ ] 材质面板: 折叠分组正常工作
