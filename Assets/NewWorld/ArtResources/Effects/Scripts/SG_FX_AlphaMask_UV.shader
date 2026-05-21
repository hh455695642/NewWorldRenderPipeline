Shader "Shader Graphs/SG_FX_AlphaMask_UV"
{
    Properties
    {
        [MainTexture] _MainTex ("MainTex", 2D) = "white" {}
        [MainColor][HDR] _Color ("Color", Color) = (1,1,1,1)
        [Toggle] _Loop ("Loop", Float) = 0
        _Tiling_Offset ("Tiling&Offset", Vector) = (1,1,0,0)
        _MaskTex ("MaskTex", 2D) = "white" {}
        [Toggle] _MaskLoop ("MaskLoop", Float) = 0
        _MaskTiling_Offset ("MaskTiling&Offset", Vector) = (1,1,0,0)
        _Power1 ("Power1", Float) = 1
        _Power2 ("Power2", Float) = 1
        [Toggle] _maskonepanner ("maskonepanner", Float) = 0

        [HideInInspector] _CastShadows ("_CastShadows", Float) = 1
        [HideInInspector] _Surface ("_Surface", Float) = 1
        [HideInInspector] _Blend ("_Blend", Float) = 0
        [HideInInspector] _AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector] _SrcBlend ("_SrcBlend", Float) = 5
        [HideInInspector] _DstBlend ("_DstBlend", Float) = 10
        [HideInInspector] _ZWrite ("_ZWrite", Float) = 0
        [HideInInspector] _ZWriteControl ("_ZWriteControl", Float) = 0
        [HideInInspector] _ZTest ("_ZTest", Float) = 8
        [HideInInspector] _Cull ("_Cull", Float) = 0
        [HideInInspector] _AlphaToMask ("_AlphaToMask", Float) = 0
        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = 0
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

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #pragma multi_compile_fog

            #include "../../../../NWRP/ShaderLibrary/Core.hlsl"
            #include "../../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Loop;
                float4 _Tiling_Offset;
                float4 _MaskTex_ST;
                half _MaskLoop;
                float4 _MaskTiling_Offset;
                half _Power1;
                half _Power2;
                half _maskonepanner;
                half _CastShadows;
                half _Surface;
                half _Blend;
                half _AlphaClip;
                half _SrcBlend;
                half _DstBlend;
                half _ZWrite;
                half _ZWriteControl;
                half _ZTest;
                half _Cull;
                half _AlphaToMask;
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
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ApplyPanner(float2 uv, float4 tilingOffset, half loop, half disableOffset)
            {
                float2 offset = lerp(tilingOffset.zw, tilingOffset.zw * _Time.y, step(0.5h, loop));
                offset = lerp(offset, float2(0.0, 0.0), step(0.5h, disableOffset));
                return uv * tilingOffset.xy + offset;
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
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 mainUV = ApplyPanner(input.uv, _Tiling_Offset, _Loop, 0.0h);
                float2 maskUV = ApplyPanner(input.uv, _MaskTiling_Offset, _MaskLoop, _maskonepanner);
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
                half4 baseColor = mainTex * input.color * _Color;
                half alpha = baseColor.a * pow(saturate(mask), _Power1) * _Power2;
                return half4(MixFog(baseColor.rgb, input.fogFactor), saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
