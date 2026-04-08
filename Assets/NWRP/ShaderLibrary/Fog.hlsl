#ifndef NEWWORLD_FOG_INCLUDED
#define NEWWORLD_FOG_INCLUDED

// ============================================================
// NewWorld Render Pipeline - Fog.hlsl
//
// 自主实现的雾效系统。
//
// 使用方式：
//   Shader 中声明: #pragma multi_compile_fog
//   （这是 Unity 引擎级功能，非 URP 专属，会自动定义
//    FOG_LINEAR / FOG_EXP / FOG_EXP2 之一）
//
// 全局变量依赖：
//   unity_FogParams  — 由 SetupCameraProperties() 自动设置
//   unity_FogColor   — 由 SetupCameraProperties() 自动设置
//   来源: RenderSettings（Lighting > Environment > Fog）
// ============================================================

// 计算雾因子（顶点着色器中调用，传入 clip-space Z）
// 返回: 1 = 无雾(保持原色), 0 = 全雾(变为雾色)
float ComputeFogFactor(float z)
{
    #if defined(FOG_LINEAR)
        // Linear: factor = (end - z) / (end - start)
        // unity_FogParams: x = -1/(end-start), y = end/(end-start)
        return saturate(z * unity_FogParams.x + unity_FogParams.y);
    #elif defined(FOG_EXP)
        // Exp: factor = exp(-density * z)
        float f = unity_FogParams.x * z;
        return saturate(exp2(-f));
    #elif defined(FOG_EXP2)
        // Exp2: factor = exp(-(density * z)^2)
        float f = unity_FogParams.x * z;
        return saturate(exp2(-f * f));
    #else
        return 1.0;
    #endif
}

// 将雾混合到最终颜色
half3 MixFog(half3 fragColor, float fogFactor)
{
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        return lerp(unity_FogColor.rgb, fragColor, fogFactor);
    #else
        return fragColor;
    #endif
}

half4 MixFogColor(half4 fragColor, float fogFactor)
{
    fragColor.rgb = MixFog(fragColor.rgb, fogFactor);
    return fragColor;
}

#endif // NEWWORLD_FOG_INCLUDED
