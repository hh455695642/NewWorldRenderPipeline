Shader "NewWorld/Base/TextureScreenSpace"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPosition = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 透视除法得到屏幕空间 UV
                float2 textureCoordinate = IN.screenPosition.xy / IN.screenPosition.w;
                // 修正宽高比，避免纹理拉伸
                float aspect = _ScreenParams.x / _ScreenParams.y;
                textureCoordinate.x = textureCoordinate.x * aspect;
                textureCoordinate = TRANSFORM_TEX(textureCoordinate, _BaseMap);
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, textureCoordinate);
            }
            ENDHLSL
        }
    }
}
