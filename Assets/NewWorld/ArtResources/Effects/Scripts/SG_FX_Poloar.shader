Shader "NewShaderGraphs/SG_FX_Poloar"
{
    Properties
    {
        [MainTexture] _MainTex ("MainTex", 2D) = "white" {}
        [MainColor][HDR] _Color ("Color", Color) = (1,1,1,1)
        _Tiling_Offset ("Tiling & Offset", Vector) = (1,1,0,0)
        [Toggle] _Loop ("Loop", Float) = 0
        [Toggle] _OnePanner ("OnePanner", Float) = 0
        [Toggle] _PoloarMain ("PoloarMain", Float) = 0

        [Toggle] _Distortion ("Distortion", Float) = 0
        _DistortionTex ("DistortionTex", 2D) = "white" {}
        _DT_T_O ("DT_T&O", Vector) = (1,1,0,-1)
        _Distortion_Int ("Distortion_Int", Float) = 2

        _Mask ("Mask", 2D) = "white" {}
        _MaskTex_Tiling_Offset ("MaskTex_Tiling&Offset", Vector) = (1,1,0,0)
        [Toggle] _MaskLoop ("MaskLoop", Float) = 0
        [Toggle] _MaskOnePanner ("MaskOnePanner", Float) = 0
        Vector1_0a19638930be43d18680f2c83ab994b7 ("MaskTex_Power", Float) = 1
        Vector1_c9ced767039f4a1485757b7071b03403 ("Mask_Int", Float) = 1

        [Toggle(_FLIPBOOKBLENDING_ON)] _FlipbookBlending ("Flipbook Blending", Float) = 0

        [HideInInspector] _Surface ("__surface", Float) = 1
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 0
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _Cull ("__cull", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 5
        [HideInInspector] _DstBlend ("__dst", Float) = 10
        [HideInInspector] _ZTest ("__ztest", Float) = 4
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
            #pragma multi_compile_fog

            #include "../../../../NWRP/ShaderLibrary/Core.hlsl"
            #include "../../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float4 _Tiling_Offset;
                half _Loop;
                half _OnePanner;
                half _PoloarMain;
                half _Distortion;
                float4 _DistortionTex_ST;
                float4 _DT_T_O;
                half _Distortion_Int;
                float4 _Mask_ST;
                float4 _MaskTex_Tiling_Offset;
                half _MaskLoop;
                half _MaskOnePanner;
                half Vector1_0a19638930be43d18680f2c83ab994b7;
                half Vector1_c9ced767039f4a1485757b7071b03403;
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
                half _ReceiveShadows;
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
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 PolarCoordinates(float2 uv)
            {
                float2 delta = uv - float2(0.5, 0.5);
                float radius = length(delta) * 2.0;
                float angle = atan2(delta.x, delta.y) * (1.0 / TWO_PI);
                return float2(radius, angle);
            }

            float2 ApplyPanner(float2 uv, float4 tilingOffset, half loop, half onePanner)
            {
                float2 offsetOrSpeed = tilingOffset.zw;
                float2 offset = lerp(offsetOrSpeed, offsetOrSpeed * _Time.y, step(0.5h, loop));
                offset = lerp(offset, float2(0.0, 0.0), step(0.5h, onePanner));
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

            float2 BuildMainUV(float2 uv)
            {
                float2 baseUV = (_PoloarMain > 0.5h) ? PolarCoordinates(uv) : uv;
                float2 distortionUV = uv * _DT_T_O.xy + _DT_T_O.zw * _Time.y;
                half distortionSample = saturate(SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUV).r);
                half distortionPower = max(_Distortion_Int, 0.0001h);
                half distortion = pow(distortionSample, distortionPower);
                float2 sourceUV = lerp(baseUV, baseUV + distortion.xx, step(0.5h, _Distortion));
                return ApplyPanner(sourceUV, _Tiling_Offset, _Loop, _OnePanner);
            }

            float2 BuildMaskUV(float2 uv)
            {
                return ApplyPanner(uv, _MaskTex_Tiling_Offset, _MaskLoop, _MaskOnePanner);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = GetNWRPParticleVertexColor(input.color);

            #if defined(_FLIPBOOKBLENDING_ON) && !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                GetNWRPParticleUVs(output.uv, output.blendUV, input.texcoord, input.texcoordBlend);
            #else
                GetNWRPParticleUVs(output.uv, output.blendUV, input.texcoord.xyxy, 0.0);
            #endif

                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half blend = half(input.blendUV.z);
                float2 mainUV = BuildMainUV(input.uv);
                float2 mainBlendUV = BuildMainUV(input.blendUV.xy);
                float2 maskUV = BuildMaskUV(input.uv);
                float2 maskBlendUV = BuildMaskUV(input.blendUV.xy);

                half4 mainTex = SampleParticleTexture(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), mainUV, mainBlendUV, blend);
                half mask = SampleParticleTexture(TEXTURE2D_ARGS(_Mask, sampler_Mask), maskUV, maskBlendUV, blend).r;
                half4 baseColor = mainTex * input.color * _Color;
                half maskFactor = pow(saturate(mask), Vector1_0a19638930be43d18680f2c83ab994b7)
                    * Vector1_c9ced767039f4a1485757b7071b03403;
                half alpha = saturate(baseColor.a * maskFactor);

                return half4(MixFog(baseColor.rgb, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
