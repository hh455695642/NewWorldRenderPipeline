# Phase 1 - 自定义渲染管线骨架搭建

**日期**: 2026-03-31  
**状态**: ✅ 已完成，编译通过

---

## 目标

从零搭建一套完全独立于 URP 的自定义渲染管线（NWRP），实现最小可运行闭环：C# 管线 + HLSL 库 + 验证用 Shader。

## 完成的工作

### 1. NWRP 文件夹结构（仿照 Universal RP 官方布局）

```
Assets/NWRP/
├── Editor/
│   └── NWRP.Editor.asmdef
├── Runtime/
│   ├── NWRP.Runtime.asmdef
│   ├── NewWorldRenderPipelineAsset.cs
│   ├── NewWorldRenderPipeline.cs
│   └── CameraRenderer.cs
├── ShaderLibrary/
│   ├── Core.hlsl              ← 主入口，一行 include 即可
│   ├── Common.hlsl            ← 精度别名、CBUFFER/纹理宏、数学工具
│   ├── UnityInput.hlsl        ← Unity 引擎内置变量（UnityPerDraw cbuffer 等）
│   └── SpaceTransforms.hlsl   ← OS/WS/VS/CS 空间变换全集
├── Shaders/
│   └── Unlit/
│       └── NewWorld_Unlit_Color.shader
├── Tests/
└── Textures/
```

### 2. C# 渲染管线（3 个文件）

| 文件 | 职责 |
|------|------|
| `NewWorldRenderPipelineAsset.cs` | ScriptableObject 配置资源，暴露 SRP Batcher / Dynamic Batching / GPU Instancing 开关 |
| `NewWorldRenderPipeline.cs` | 管线主循环，遍历所有相机调用 CameraRenderer |
| `CameraRenderer.cs` | 单相机渲染器：Cull → Setup → DrawOpaque → Skybox → DrawTransparent → Submit |

- 命名空间统一为 `NWRP`
- 通过 `.asmdef` 隔离编译，`NWRP.Runtime` / `NWRP.Editor` 各自独立
- 支持的 LightMode 标签：`NewWorldUnlit`、`SRPDefaultUnlit`

### 3. HLSL ShaderLibrary（4 个文件）

| 文件 | 内容摘要 |
|------|---------|
| `Common.hlsl` | real 精度别名、数学常量（PI 等）、CBUFFER_START/END 宏、TEXTURE2D/SAMPLER 全套纹理宏、工具函数（SafeNormalize / Remap / Luminance 等） |
| `UnityInput.hlsl` | unity_MatrixVP / V / InvV 等相机矩阵、_Time / _ScreenParams / _ZBufferParams 等全局变量、UnityPerDraw cbuffer 完整布局（SRP Batcher 兼容）、_MainLightPosition/Color 预留 |
| `SpaceTransforms.hlsl` | 位置变换（ObjectToWorld / WorldToHClip / ObjectToHClip 等）、方向变换、法线变换（逆转置矩阵）、切线空间构建、深度线性化、屏幕 UV、相机方向工具 |
| `Core.hlsl` | 主入口，按顺序 include 以上三个文件 |

### 4. 验证 Shader

`NewWorld/Unlit/Color` — 最简无光照纯色 Shader，`#include "../../ShaderLibrary/Core.hlsl"` 引用自有库，使用自写的 `TransformObjectToHClip()` 完成顶点变换。SRP Batcher 兼容（UnityPerMaterial cbuffer）。

## 设计决策

| 决策 | 理由 |
|------|------|
| 保留 URP Package 不删除 | 作为参考源码，URPShaderCodeSample 示例依赖它编译 |
| HLSL 用相对路径 include | `../../ShaderLibrary/Core.hlsl`，Shaders/ 下所有子目录深度一致，路径统一 |
| asmdef 隔离 | 确保整个 NWRP 文件夹可无冲突拷贝到任意 Unity 2022.3.x / URP 14.x 工程 |
| UnityPerDraw 严格匹配 Unity 内部布局 | 字段顺序/类型必须一致，否则 SRP Batcher 回退慢速路径 |

## 使用方式

1. 拷贝 `Assets/NWRP/` 到目标工程
2. `Create → Rendering → New World Render Pipeline Asset`
3. `Project Settings → Graphics → Scriptable Render Pipeline Settings` 指向该 Asset
4. **`Project Settings → Quality` → 每个质量级别的 `Render Pipeline Asset` 也必须替换**（Quality 级别的设置优先级高于 Graphics 全局设置，不替换会导致 NewWorld Shader 物体不可见）
5. 材质选择 `NewWorld/Unlit/Color`，赋予场景物体即可看到纯色渲染

## 下一步（Phase 2）

- 纹理采样支持（Unlit/Texture Shader）
- Alpha Test / Alpha Blend 透明模式
- 更多 Unlit 变体（UV 动画、顶点色等）
