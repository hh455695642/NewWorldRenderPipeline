#ifndef NEWWORLD_CORE_INCLUDED
#define NEWWORLD_CORE_INCLUDED

// ============================================================
// NewWorld Render Pipeline - Core.hlsl
//
// 主入口头文件。所有 NewWorld 着色器只需：
//   #include "../../ShaderLibrary/Core.hlsl"
// 即可获得全部基础功能（类型、宏、矩阵、空间变换）。
//
// 文件位置: Assets/NWRP/ShaderLibrary/Core.hlsl
//
// Shader include 路径参考（相对 Shader 文件位置）：
//   NWRP/Shaders/Unlit/*.shader         → ../../ShaderLibrary/Core.hlsl
//   NWRP/Shaders/Lit/*.shader           → ../../ShaderLibrary/Core.hlsl
//   NWRP/Shaders/Environment/*.shader   → ../../ShaderLibrary/Core.hlsl
//   NWRP/Shaders/PostProcess/*.shader   → ../../ShaderLibrary/Core.hlsl
// ============================================================

// 1. 基础类型、宏、数学工具
#include "Common.hlsl"

// 2. Unity 引擎内置 Shader 变量（矩阵、时间、屏幕参数等）
#include "UnityInput.hlsl"

// 3. 坐标空间变换函数
#include "SpaceTransforms.hlsl"

// 4. 雾效函数
#include "Fog.hlsl"

#endif // NEWWORLD_CORE_INCLUDED
