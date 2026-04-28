Shader "Hidden/NWRP/MainLightShadowCasterTint"
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
            "Queue" = "Transparent+100"
        }

        Pass
        {
            Name "CasterTint"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"
            #include "../../ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 mainLightDirection = normalize(_MainLightPosition.xyz);
                MainLightShadowResult shadowResult = GetMainLightShadowResult(
                    input.positionWS,
                    normalWS,
                    mainLightDirection
                );

                const half epsilon = 0.02h;
                half shadowAmount = saturate(1.0h - shadowResult.finalVisibility);

                if (shadowAmount > epsilon)
                {
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                return _BaseColor;
            }

            ENDHLSL
        }
    }
}
