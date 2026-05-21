Shader "Shader Graphs/AdditiveMask"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Tex", 2D) = "white" {}
        [MainColor][HDR] _Color ("Color", Color) = (0,0,0,0)
        _Tiling_Offset ("Tiling & Offset", Vector) = (1,1,0,0)
        [Toggle] _Loop ("Loop", Float) = 0

        _MaskTex ("Mask Tex", 2D) = "white" {}
        _MaskTiling_Offset ("Mask Tiling & Offset", Vector) = (1,1,0,0)
        [Toggle] _MaskLoop ("Mask Loop", Float) = 0

        _Power1 ("Power 1", Float) = 1
        _Power2 ("Power 2", Float) = 1

        [HideInInspector] _Surface ("__surface", Float) = 1
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 0
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _Cull ("__cull", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 5
        [HideInInspector] _DstBlend ("__dst", Float) = 1
        [HideInInspector] _QueueControl ("__queueControl", Float) = 0
        [HideInInspector] _QueueOffset ("__queueOffset", Float) = 0
        [HideInInspector][ToggleOff] _ReceiveShadows ("Receive Shadows", Float) = 1
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

            Blend SrcAlpha One
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
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float4 _Tiling_Offset;
                half _Loop;
                float4 _MaskTex_ST;
                float4 _MaskTiling_Offset;
                half _MaskLoop;
                half _Power1;
                half _Power2;
                half _Surface;
                half _Blend;
                half _ZWrite;
                half _AlphaClip;
                half _Cull;
                half _SrcBlend;
                half _DstBlend;
                half _QueueControl;
                half _QueueOffset;
                half _ReceiveShadows;
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
                float3 blendUV : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ApplyTilingOffset(float2 uv, float4 tilingOffset, half loop)
            {
                float2 offsetOrSpeed = tilingOffset.zw;
                float2 offset = lerp(offsetOrSpeed, offsetOrSpeed * _Time.y, step(0.5h, loop));
                return uv * tilingOffset.xy + offset;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = GetNWRPParticleVertexColor(input.color);
                GetNWRPParticleUVs(output.uv, output.blendUV, input.texcoord.xyxy, 0.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 mainUV = ApplyTilingOffset(input.uv, _Tiling_Offset, _Loop);
                float2 maskUV = ApplyTilingOffset(input.uv, _MaskTiling_Offset, _MaskLoop);

                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
                half4 baseColor = mainTex * input.color * _Color;
                half maskFactor = pow(saturate(mask), _Power1) * _Power2;
                half3 rgb = baseColor.rgb * baseColor.a * maskFactor;

                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
