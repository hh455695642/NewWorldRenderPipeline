Shader "Shader Graphs/Portalnew"
{
    Properties
    {
        [HDR] _tint01 ("tint01", Color) = (0,0.438499,1,0)
        [MainTexture] _MainTex ("MainTex", 2D) = "white" {}
        _MaskRange ("MaskRange", Float) = 1
        _Detail ("Detail", Float) = 1
        _Speed ("Speed", Vector) = (1,1,0,0)
        [HDR] _tint02 ("maskcolor", Color) = (1,1,1,1)
        _Mask ("Mask", 2D) = "white" {}
        [Toggle] _maskloop ("maskloop", Float) = 0
        _Masktilling ("Masktilling", Vector) = (1,1,0,0)
        _NosietEX ("NosieTex", 2D) = "white" {}
        Vector4_8d1133c65e314797b99728b25497491f ("Vector4", Vector) = (1,1,0,0)
        _Power ("Power", Range(0,10)) = 0
        _Distort_Int ("Distort_Int", Range(0,1)) = 0
        _Mask_Power ("Mask_Power", Range(0.1,5)) = 1

        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "NewWorldRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "PerformanceChecks" = "False"
        }

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include "../../../NWRP/ShaderLibrary/Core.hlsl"
            #include "../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURE2D(_NosietEX);
            SAMPLER(sampler_NosietEX);

            CBUFFER_START(UnityPerMaterial)
                half4 _tint01;
                float4 _MainTex_ST;
                half _MaskRange;
                half _Detail;
                float4 _Speed;
                half4 _tint02;
                float4 _Mask_ST;
                half _maskloop;
                float4 _Masktilling;
                float4 _NosietEX_ST;
                float4 Vector4_8d1133c65e314797b99728b25497491f;
                half _Power;
                half _Distort_Int;
                half _Mask_Power;
                half _QueueOffset;
                half _QueueControl;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 PolarCoordinates(float2 uv, half radialScale, half lengthScale)
            {
                float2 delta = uv - float2(0.5, 0.5);
                float radius = length(delta) * 2.0 * radialScale;
                float angle = atan2(delta.x, delta.y) * (1.0 / TWO_PI) * lengthScale;
                return float2(radius, angle);
            }

            float2 ApplyScroll(float2 uv, float4 tilingOffset)
            {
                return uv * tilingOffset.xy + tilingOffset.zw * _Time.y;
            }

            float2 ApplyMaskUV(float2 uv)
            {
                float2 offset = lerp(_Masktilling.zw, _Masktilling.zw * _Time.y, step(0.5h, _maskloop));
                return uv * _Masktilling.xy + offset;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 blendUV;
                GetNWRPParticleUVs(output.uv, blendUV, input.texcoord.xyxy, 0.0);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = GetNWRPParticleVertexColor(input.color);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 noiseUV = ApplyScroll(PolarCoordinates(input.uv, 1.0h, 1.0h), Vector4_8d1133c65e314797b99728b25497491f);
                half noise = saturate(SAMPLE_TEXTURE2D(_NosietEX, sampler_NosietEX, noiseUV).r);
                half distortion = pow(noise, max(_Power, 0.0001h)) * _Distort_Int;

                float2 mainUV = PolarCoordinates(input.uv + distortion.xx, _MaskRange, _Detail) + _Speed.xy * _Time.y;
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, ApplyMaskUV(input.uv)).r;
                half maskAlpha = pow(saturate(mask), _Mask_Power);

                half3 rgb = lerp(_tint01.rgb, _tint02.rgb, mainTex.r) * input.color.rgb;
                half alpha = mainTex.a * maskAlpha * input.color.a;
                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
