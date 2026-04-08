Shader "NewWorld/Base/UVCheck"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../../ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // UV 坐标显示为红绿色，超出 0-1 范围的加蓝色标记
            half4 frag(Varyings IN) : SV_Target
            {
                float4 uv = float4(IN.uv.xy, 0, 0);
                half4 c = frac(uv);
                if (any(saturate(uv) - uv))
                    c.b = 0.5;
                return c;
            }
            ENDHLSL
        }
    }
}
