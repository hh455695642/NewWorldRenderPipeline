Shader "NewWorld/Env/ParabolaLine"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Unfinished Texture", 2D) = "white" {}
        [NoScaleOffset]_MainTex1 ("Finished Texture", 2D) = "white" {}

        [HDR]_Color("Finished Color", Color) = (0, 0, 0, 0)
        [HDR]_Color2("Unfinished Color", Color) = (0, 0, 0, 0)

        _Tiling_Offset("Tiling & Offset", Vector) = (1, 1, 0, 0)

        _LineIntensity("Line Intensity", Float) = 4
        _LineMiddleWidth("Middle Width", Range(0, 0.2)) = 0.03
        _LineEndWidth("End Width", Range(0, 0.4)) = 0.2

        _Progress("Progress", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "AfterFogParabolaLine"
            Tags { "LightMode"="AfterFog" }

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "../../../../NWRP/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Color2;
                float4 _Tiling_Offset;
                half _LineIntensity;
                half _LineMiddleWidth;
                half _LineEndWidth;
                half _Progress;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_MainTex1);
            SAMPLER(sampler_MainTex1);

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                half dist = (half)abs(uv.y - 0.5);
                half width = lerp(
                    _LineMiddleWidth,
                    _LineEndWidth,
                    (half)abs(uv.x * 2.0 - 1.0));
                half lineMask = smoothstep(width, width - 0.01h, dist);

                half4 lineColor;
                lineColor.rgb = lineMask * input.color.rgb * _LineIntensity;
                lineColor.a = lineMask * input.color.a;

                float scan = frac(_Time.y * _Tiling_Offset.z);
                float arrowU = uv.x - scan;
                float2 arrowUV = float2(arrowU * _Tiling_Offset.x, uv.y);

                float2 finishedUV = uv * _Tiling_Offset.xy;
                finishedUV += _Tiling_Offset.zw * -_Time.y;

                half4 unfinishedTex =
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, arrowUV)
                    * input.color;
                half4 finishedTex =
                    SAMPLE_TEXTURE2D(_MainTex1, sampler_MainTex1, finishedUV)
                    * input.color;

                half4 colorDefault =
                    lerp(lineColor, unfinishedTex * _Color2, unfinishedTex.a);

                half progressMask = step(1.0h - _Progress, 1.0h - (half)uv.x);
                half4 colorProgress = finishedTex * _Color;

                return lerp(colorDefault, colorProgress, progressMask);
            }
            ENDHLSL
        }
    }
}
