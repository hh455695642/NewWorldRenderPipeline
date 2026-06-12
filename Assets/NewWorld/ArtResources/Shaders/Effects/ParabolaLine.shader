Shader "NewWorld/Env/ParabolaLine"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Unfinished Texture", 2D) = "white" {}
        [NoScaleOffset]_MainTex1 ("Finished Texture", 2D) = "white" {}

        [HDR]_Color("Finished Color", Color) = (0,0,0,0)
        [HDR]_Color2("Unfinished Color", Color) = (0,0,0,0)

        _Tiling_Offset("Tiling & Offset", Vector) = (1,1,0,0)

        _LineIntensity("Line Intensity", Float) = 4
        _LineMiddleWidth("Middle Width", Range(0,0.2)) = 0.03
        _LineEndWidth("End Width", Range(0,0.4)) = 0.2

        _Progress("Progress", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "UniversalMaterialType"="Unlit"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "AfterFogParabolaLine"

            Tags
            {
                "LightMode"="AfterFog"
            }

            // Render State
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)

                float4 _Color;
                float4 _Color2;

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


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };


            Varyings vert (Attributes v)
            {
                Varyings o;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;

                return o;
            }


            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                //----------------------------------
                // 1. 线条形状 (两端粗 中间细)
                //----------------------------------

                float dist = abs(uv.y - 0.5);

                float width = lerp(
                    _LineMiddleWidth,
                    _LineEndWidth,
                    abs(uv.x * 2 - 1)
                );

                float lineMask = smoothstep(width, width - 0.01, dist);

                float4 lineColor;
                lineColor.rgb = lineMask * i.color.rgb * _LineIntensity;
                lineColor.a   = lineMask * i.color.a;


                //----------------------------------
                // 2. 箭头扫描动画
                //----------------------------------

                float scan = frac(_Time.y * _Tiling_Offset.z);

                float arrowU = uv.x - scan;

                //float arrowMask = step(0, arrowU) * step(arrowU, 1);

                float2 arrowUV = float2(
                    arrowU * _Tiling_Offset.x,
                    uv.y
                );


                //----------------------------------
                // 3. 贴图采样
                //----------------------------------

                float2 finishedUV = uv * float2(_Tiling_Offset.x, _Tiling_Offset.y);
                finishedUV += _Tiling_Offset.zw * -_Time.y;

                float4 unfinishedTex = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    arrowUV
                ) * i.color;

                float4 finishedTex = SAMPLE_TEXTURE2D(
                    _MainTex1,
                    sampler_MainTex1,
                    finishedUV
                ) * i.color;


                //----------------------------------
                // 4. 未完成区域颜色
                //----------------------------------

                float4 colorDefault =
                    lerp(lineColor,
                         unfinishedTex * _Color2,
                         unfinishedTex.a);


                //----------------------------------
                // 5. 完成进度
                //----------------------------------

                float progressMask =
                    step(1 - _Progress, 1 - uv.x);

                float4 colorProgress =
                    finishedTex * _Color;


                //----------------------------------
                // 6. 最终混合
                //----------------------------------

                float4 col =
                    lerp(colorDefault,
                         colorProgress,
                         progressMask);

                return col;
            }

            ENDHLSL
        }
    }
}