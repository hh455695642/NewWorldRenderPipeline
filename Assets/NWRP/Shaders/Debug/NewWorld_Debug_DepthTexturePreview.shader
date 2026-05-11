Shader "NewWorld/Debug/DepthTexturePreview"
{
    Properties
    {
        [MainColor]_Tint("Tint", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 1
        _DisplayMode("Display Mode (0 Raw, 1 Linear01, 2 LinearEye)", Range(0, 2)) = 0
        _LinearEyeDepthScale("Linear Eye Depth Scale", Float) = 0.05
        [ToggleUI]_FlipY("Manual Flip Y", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"
            #include "../../ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity;
                half _DisplayMode;
                float _LinearEyeDepthScale;
                half _FlipY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = saturate(input.uv);

                // SampleSceneDepth already applies _CameraDepthTextureScaleBias for SceneView/Preview.
                // Keep this only as an explicit debug override.
                uv.y = _FlipY > 0.5h ? 1.0 - uv.y : uv.y;
                float rawDepth = SampleSceneDepth(uv);
                float linear01 = Linear01Depth(rawDepth);
                float linearEye = saturate(LinearEyeDepth(rawDepth) * _LinearEyeDepthScale);

                half mode = round(_DisplayMode);
                float depthValue = rawDepth;
                depthValue = mode == 1.0h ? linear01 : depthValue;
                depthValue = mode == 2.0h ? linearEye : depthValue;

                half3 preview = (half3)depthValue * _Tint.rgb;
                return half4(preview, saturate(_Opacity * _Tint.a));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
