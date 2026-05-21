Shader "Shader Graphs/Dissovle2"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Tex", 2D) = "white" {}
        [MainColor][HDR] _Color ("Color", Color) = (0,0,0,0)
        _MainTexT_O ("MainTex T&O", Vector) = (1,1,0,0)

        _MaskTex ("Mask Tex", 2D) = "white" {}
        _MaskT_O ("Mask T&O", Vector) = (1,1,0,0)

        Texture2D_aa22deb2aa4244b99e7c2bf5b8f9ae7e ("NosieTex", 2D) = "white" {}
        _NosieT_O ("Nosie T&O", Vector) = (1,1,0,0)
        [Toggle] _Swtich ("Swtich", Float) = 0

        _InvFade ("Soft Particles Factor", Float) = 1
        _Eage ("Eage", Float) = 0
        [HDR] _EalgeColor ("EalgeColor", Color) = (0.259434,0.901956,1,0)
        _Cutoff ("Alpha Clip Threshold", Range(0,1)) = 0.5

        [Toggle(_FLIPBOOKBLENDING_ON)] _FlipbookBlending ("Flipbook Blending", Float) = 0

        [HideInInspector] _Surface ("__surface", Float) = 1
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 0
        [HideInInspector] _AlphaClip ("__clip", Float) = 1
        [HideInInspector] _Cull ("__cull", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 5
        [HideInInspector] _DstBlend ("__dst", Float) = 10
        [HideInInspector] _ZTest ("__ztest", Float) = 4
        [HideInInspector] _QueueControl ("__queueControl", Float) = 0
        [HideInInspector] _QueueOffset ("__queueOffset", Float) = 0
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
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _FLIPBOOKBLENDING_ON
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include "../../../NWRP/ShaderLibrary/Core.hlsl"
            #include "../../../NWRP/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(Texture2D_aa22deb2aa4244b99e7c2bf5b8f9ae7e);
            SAMPLER(samplerTexture2D_aa22deb2aa4244b99e7c2bf5b8f9ae7e);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float4 _MainTexT_O;
                float4 _MaskTex_ST;
                float4 _MaskT_O;
                float4 Texture2D_aa22deb2aa4244b99e7c2bf5b8f9ae7e_ST;
                float4 _NosieT_O;
                half _Swtich;
                half _InvFade;
                half _Eage;
                half4 _EalgeColor;
                half _Cutoff;
                half _FlipbookBlending;
                half _Surface;
                half _Blend;
                half _ZWrite;
                half _AlphaClip;
                half _Cull;
                half _SrcBlend;
                half _DstBlend;
                half _ZTest;
                half _QueueControl;
                half _QueueOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
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
                float4 screenPos : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ApplyTilingOffset(float2 uv, float4 tilingOffset, bool loopOffset)
            {
                float2 offset = loopOffset ? tilingOffset.zw * _Time.y : tilingOffset.zw;
                return uv * tilingOffset.xy + offset;
            }

            half4 SampleParticleTexture(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 blendUV, half blend)
            {
                half4 result = SAMPLE_TEXTURE2D(tex, samplerTex, uv);
            #if defined(_FLIPBOOKBLENDING_ON)
                result = lerp(result, SAMPLE_TEXTURE2D(tex, samplerTex, blendUV), blend);
            #endif
                return result;
            }

            half SoftParticleFade(float4 screenPos)
            {
                if (_InvFade <= 0.0001h)
                {
                    return 1.0h;
                }

                float2 screenUV = screenPos.xy / screenPos.w;
                float sceneZ = SampleSceneDepthLinearEye(screenUV);
                float particleZ = LinearEyeDepth(screenPos.z / screenPos.w);
                return half(saturate((sceneZ - particleZ) * _InvFade));
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
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
                float2 mainUV = ApplyTilingOffset(input.uv, _MainTexT_O, true);
                float2 mainBlendUV = ApplyTilingOffset(input.blendUV.xy, _MainTexT_O, true);
                float2 noiseUV = ApplyTilingOffset(input.uv, _NosieT_O, _Swtich > 0.5h);
                float2 maskUV = input.uv * _MaskT_O.xy + _MaskT_O.zw;

                half4 mainTex = SampleParticleTexture(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), mainUV, mainBlendUV, blend);
                half4 baseColor = mainTex * input.color * _Color;
                half noise = saturate(SAMPLE_TEXTURE2D(Texture2D_aa22deb2aa4244b99e7c2bf5b8f9ae7e, samplerTexture2D_aa22deb2aa4244b99e7c2bf5b8f9ae7e, noiseUV).r);
                half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
                half threshold = saturate(baseColor.a);
                half dissolve = step(noise, threshold);
                half edge = saturate(dissolve - step(noise + max(_Eage, 0.0h), threshold));
                half alpha = dissolve * mask * SoftParticleFade(input.screenPos);

                clip(alpha - _Cutoff);

                half3 rgb = baseColor.rgb + edge * _EalgeColor.rgb;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
