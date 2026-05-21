Shader "Shader Graphs/Dun02_2"
{
    Properties
    {
        _Int ("Int", Float) = 1
        _FrePower ("FrePower", Float) = 5
        [MainTexture] _MainTex ("MainTex", 2D) = "white" {}
        [MainColor][HDR] _Color ("Color", Color) = (1, 0.7190735, 0, 1)
        TexT_O ("TexT&O", Vector) = (1, 1, 0, 0)
        _Beat_Int ("Beat_Int", Range(0, 10)) = 0
        _Break_Int ("Break_Int", Range(0, 0.2)) = 0
        _DissovleTex ("DissovleTex", 2D) = "white" {}
        TexT_O_1 ("tilling", Vector) = (1, 1, 0, 0)
        _Dissovle ("Dissovle", Range(0, 1)) = 0
        [HDR] _EadgeColor ("EadgeColor", Color) = (1, 1, 1, 1)
        _Eadge ("Eadge", Range(0, 1)) = 0

        [Toggle(_FLIPBOOKBLENDING_ON)] _FlipbookBlending ("Flipbook Blending", Float) = 0

        [HideInInspector] _CastShadows ("_CastShadows", Float) = 0
        [HideInInspector] _Surface ("_Surface", Float) = 1
        [HideInInspector] _Blend ("_Blend", Float) = 2
        [HideInInspector] _AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector] _SrcBlend ("_SrcBlend", Float) = 5
        [HideInInspector] _DstBlend ("_DstBlend", Float) = 1
        [HideInInspector] _ZWrite ("_ZWrite", Float) = 0
        [HideInInspector] _ZWriteControl ("_ZWriteControl", Float) = 0
        [HideInInspector] _ZTest ("_ZTest", Float) = 4
        [HideInInspector] _Cull ("_Cull", Float) = 2
        [HideInInspector] _AlphaToMask ("_AlphaToMask", Float) = 0
        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = 0
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
            #pragma shader_feature_local _FLIPBOOKBLENDING_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include "../../../NWRP/ShaderLibrary/Core.hlsl"
            #include "../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DissovleTex);
            SAMPLER(sampler_DissovleTex);

            CBUFFER_START(UnityPerMaterial)
                half _Int;
                half _FrePower;
                float4 _MainTex_ST;
                half4 _Color;
                float4 TexT_O;
                half _Beat_Int;
                half _Break_Int;
                float4 _DissovleTex_ST;
                float4 TexT_O_1;
                half _Dissovle;
                half4 _EadgeColor;
                half _Eadge;
                half _FlipbookBlending;
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
                float3 normalOS : NORMAL;
                half4 color : COLOR;

            #if defined(_FLIPBOOKBLENDING_ON) && !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                float4 texcoord : TEXCOORD0;
                float texcoordBlend : TEXCOORD1;
            #else
                float2 texcoord : TEXCOORD0;
            #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 blendUV : TEXCOORD1;
                half4 color : COLOR;
                half3 normalWS : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 SampleParticleTexture(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 blendUV, half blend)
            {
                half4 result = SAMPLE_TEXTURE2D(tex, samplerTex, uv);
            #if defined(_FLIPBOOKBLENDING_ON)
                result = lerp(result, SAMPLE_TEXTURE2D(tex, samplerTex, blendUV), blend);
            #endif
                return result;
            }

            float2 ApplyAnimatedUV(float2 uv, float4 tilingOffset)
            {
                return uv * tilingOffset.xy + tilingOffset.zw * _Time.y;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = half3(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = half3(GetWorldSpaceViewDir(positionWS));
                output.color = GetNWRPParticleVertexColor(input.color);

            #if defined(_FLIPBOOKBLENDING_ON) && !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                GetNWRPParticleUVs(output.uv, output.blendUV, input.texcoord, input.texcoordBlend);
            #else
                GetNWRPParticleUVs(output.uv, output.blendUV, input.texcoord.xyxy, 0.0);
            #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half blend = half(input.blendUV.z);
                float2 mainUV = ApplyAnimatedUV(input.uv, TexT_O);
                float2 mainBlendUV = ApplyAnimatedUV(input.blendUV.xy, TexT_O);
                float2 dissolveUV = input.uv * TexT_O_1.xy + TexT_O_1.zw;

                half4 mainTex = SampleParticleTexture(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), mainUV, mainBlendUV, blend);
                half dissolveTex = SAMPLE_TEXTURE2D(_DissovleTex, sampler_DissovleTex, dissolveUV).r;

                half threshold = _Dissovle * 1.1h - 0.1h;
                half dissolveMask = step(threshold, dissolveTex);
                half edgeMask = saturate(dissolveMask - step(threshold + _Eadge, dissolveTex));

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), max(_FrePower, 0.0001h));

                half3 fresnelColor = (mainTex.rgb * _Color.rgb) * (fresnel * _Int);
                half3 edgeColor = edgeMask * _EadgeColor.rgb;
                half3 rgb = (fresnelColor * input.color.rgb + edgeColor) * dissolveMask * input.color.a;

                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
