// ============================================================
// NewWorld/Unlit/Color
//
// 最简单的无光照着色器 —— 用于验证自定义渲染管线是否正常工作。
// 功能：将物体渲染为纯色（_BaseColor），无纹理、无光照。
//
// 此 Shader 包含的是 NWRP 自己的 ShaderLibrary/Core.hlsl，
// 完全独立于 URP，零外部依赖。
// ============================================================
Shader "NewWorld/Unlit/Color"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue"      = "Geometry"
        }

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // ── 引入 NewWorld 自己的核心头文件 ───────────────
            // 从 Assets/Shaders/Unlit/ 到 Assets/ShaderLibrary/
            // 需要向上两级：../../ShaderLibrary/Core.hlsl
            #include "../../ShaderLibrary/Core.hlsl"

            // ── 材质属性 cbuffer（SRP Batcher 兼容） ─────────
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            // ── 顶点输入 ─────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            // ── 顶点输出 / 片元输入 ─────────────────────────
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            // ── 顶点着色器 ───────────────────────────────────
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // 使用我们自己写的 TransformObjectToHClip()
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            // ── 片元着色器 ───────────────────────────────────
            half4 Frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }

            ENDHLSL
        }
    }
}
