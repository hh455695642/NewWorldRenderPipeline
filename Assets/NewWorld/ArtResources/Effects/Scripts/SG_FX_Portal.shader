Shader "Shader Graphs/SG_FX_Portal"
{
    Properties
    {
        [MainTexture] _tex ("tex", 2D) = "white" {}
        _Tex_TilingOffset ("Tex_TilingOffset", Vector) = (1,1,0,0)
        [HDR] _Tex_Color ("Tex_Color", Color) = (1,1,1,1)
        _Tex_int ("Tex_int", Float) = 1
        _Tex_Noise ("Tex_Noise", 2D) = "white" {}
        _Tex_noise_TF ("Tex_noise_TF", Vector) = (1,1,0,0)
        _Mask ("Mask", 2D) = "white" {}
        _NoiseTex ("NoiseTex", 2D) = "white" {}
        _Noise_TilingOffset ("Noise_TilingOffset", Vector) = (1,1,0,0)
        _NoisePower ("NoisePower", Float) = 1
        _Noise_Int ("Noise_Int", Float) = 1
        _Fault_Tiling ("Fault_Tiling", Float) = 50
        _Fault_Int ("Fault_Int", Range(0,1)) = 0
        _FAULT_ENUM ("Fault_Enum", Float) = 0
        _Fault_TIme ("Fault_TIme", Float) = 0
        _ScreenPosition_Int ("ScreenPosition_Int", Float) = 0.05
        _dissovle_tex ("dissovle_tex", 2D) = "white" {}
        _dissovle ("dissovle", Float) = 0
        _soft ("soft", Range(0,1)) = 0.02

        [HideInInspector] _CastShadows ("_CastShadows", Float) = 1
        [HideInInspector] _Surface ("_Surface", Float) = 1
        [HideInInspector] _Blend ("_Blend", Float) = 0
        [HideInInspector] _AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector] _SrcBlend ("_SrcBlend", Float) = 5
        [HideInInspector] _DstBlend ("_DstBlend", Float) = 10
        [HideInInspector] _ZWrite ("_ZWrite", Float) = 0
        [HideInInspector] _ZWriteControl ("_ZWriteControl", Float) = 0
        [HideInInspector] _ZTest ("_ZTest", Float) = 4
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
            #include "../../../../NWRP/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_tex);
            SAMPLER(sampler_tex);
            TEXTURE2D(_Tex_Noise);
            SAMPLER(sampler_Tex_Noise);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_dissovle_tex);
            SAMPLER(sampler_dissovle_tex);

            CBUFFER_START(UnityPerMaterial)
                float4 _tex_ST;
                float4 _Tex_TilingOffset;
                half4 _Tex_Color;
                half _Tex_int;
                float4 _Tex_Noise_ST;
                float4 _Tex_noise_TF;
                float4 _Mask_ST;
                float4 _NoiseTex_ST;
                float4 _Noise_TilingOffset;
                half _NoisePower;
                half _Noise_Int;
                half _Fault_Tiling;
                half _Fault_Int;
                half _FAULT_ENUM;
                half _Fault_TIme;
                half _ScreenPosition_Int;
                float4 _dissovle_tex_ST;
                half _dissovle;
                half _soft;
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
                float4 screenPos : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ApplyScroll(float2 uv, float4 tilingOffset)
            {
                return uv * tilingOffset.xy + tilingOffset.zw * _Time.y;
            }

            half SoftParticleFade(float4 screenPos)
            {
                if (_soft <= 0.0001h)
                {
                    return 1.0h;
                }

                float2 screenUV = screenPos.xy / screenPos.w;
                float sceneZ = SampleSceneDepthLinearEye(screenUV);
                float particleZ = LinearEyeDepth(screenPos.z / screenPos.w);
                return half(saturate((sceneZ - particleZ) * _soft));
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 blendUV;
                GetNWRPParticleUVs(output.uv, blendUV, input.texcoord.xyxy, 0.0);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = GetNWRPParticleVertexColor(input.color);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, ApplyScroll(input.uv, _Noise_TilingOffset)).r;
                half texNoise = SAMPLE_TEXTURE2D(_Tex_Noise, sampler_Tex_Noise, ApplyScroll(input.uv, _Tex_noise_TF)).r;
                half noiseShaped = pow(saturate(noise), max(_NoisePower, 0.0001h));

                float stripe = frac(input.uv.y * max(_Fault_Tiling, 0.0001h) + _Time.y * _Fault_TIme);
                half faultGate = step(0.5h, _FAULT_ENUM) * _Fault_Int;
                half fault = (step(0.47, stripe) - step(0.53, stripe)) * faultGate;

                float2 distortion = (noiseShaped - 0.5h).xx * _Noise_Int;
                distortion += (texNoise - 0.5h).xx * (_Noise_Int * 0.5h);
                distortion += (screenUV - 0.5).xy * _ScreenPosition_Int;
                distortion.x += fault;

                float2 texUV = ApplyScroll(input.uv + distortion, _Tex_TilingOffset);
                half4 texColor = SAMPLE_TEXTURE2D(_tex, sampler_tex, texUV);
                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, input.uv).r;
                half dissolve = SAMPLE_TEXTURE2D(_dissovle_tex, sampler_dissovle_tex, input.uv).r;
                half dissolveFade = lerp(1.0h, saturate(dissolve * max(_dissovle, 0.0001h)), step(0.0001h, _dissovle));
                half softFade = SoftParticleFade(input.screenPos);

                half alpha = texColor.a * mask * dissolveFade * softFade * input.color.a;
                half3 rgb = texColor.rgb * _Tex_Color.rgb * input.color.rgb * _Tex_int;
                return half4(MixFog(rgb, input.fogFactor), saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
