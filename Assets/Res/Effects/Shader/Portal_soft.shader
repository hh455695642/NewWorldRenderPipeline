Shader "Effects/Portal_soft"
{
    Properties
    {
        [HDR] _tint01 ("Tint 01", Color) = (0,0.438499,1,0)
        [MainTexture] _MainTex ("Main Tex", 2D) = "white" {}
        _MaskRange ("Mask Range", Float) = 1
        _Detail ("Detail", Float) = 1
        _Speed ("Speed", Vector) = (1,1,0,0)
        _Mask ("Mask", 2D) = "white" {}
        [HDR] _tint02 ("Tint 02", Color) = (1,1,1,1)
        Vector4_8d1133c65e314797b99728b25497491f ("Noise Tiling & Offset", Vector) = (1,1,0,0)
        _NosietEX ("NosieTex", 2D) = "white" {}
        _Power ("Power", Range(0,10)) = 0
        _Distort_Int ("Distort Int", Range(0,1)) = 0
        _Mask_Power ("Mask Power", Range(0.1,5)) = 1
        _soft ("Soft", Range(0,1)) = 0.02

        [HideInInspector] _Surface ("__surface", Float) = 1
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 0
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
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
            #include "../../../NWRP/ShaderLibrary/DeclareDepthTexture.hlsl"
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
                float4 _Mask_ST;
                half4 _tint02;
                float4 Vector4_8d1133c65e314797b99728b25497491f;
                float4 _NosietEX_ST;
                half _Power;
                half _Distort_Int;
                half _Mask_Power;
                half _soft;
                half _Surface;
                half _Blend;
                half _ZWrite;
                half _AlphaClip;
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
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 PolarCoordinates(float2 uv, float radialScale, float lengthScale)
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

            half SoftParticleFade(float4 screenPos, half strength)
            {
                if (strength <= 0.0001h)
                {
                    return 1.0h;
                }

                float2 screenUV = screenPos.xy / screenPos.w;
                float sceneZ = SampleSceneDepthLinearEye(screenUV);
                float particleZ = LinearEyeDepth(screenPos.z / screenPos.w);
                return half(saturate((sceneZ - particleZ) * strength));
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 uv;
                float3 blendUV;
                GetNWRPParticleUVs(uv, blendUV, input.texcoord.xyxy, 0.0);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = uv;
                output.color = GetNWRPParticleVertexColor(input.color);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 noisePolarUV = PolarCoordinates(input.uv, 1.0, 1.0);
                float2 noiseUV = ApplyScroll(noisePolarUV, Vector4_8d1133c65e314797b99728b25497491f);
                half noise = saturate(SAMPLE_TEXTURE2D(_NosietEX, sampler_NosietEX, noiseUV).r);

                half distortion = pow(noise, _Power) * _Distort_Int;
                float2 distortedUV = input.uv + distortion;

                float2 mainPolarUV = PolarCoordinates(distortedUV, _MaskRange, _Detail);
                float2 mainUV = mainPolarUV + _Speed.xy * _Time.y;
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);

                half4 tint = lerp(_tint01, _tint02, mainTex);
                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, input.uv).r;
                half softFade = SoftParticleFade(input.screenPos, _soft);

                half3 rgb = tint.rgb * input.color.rgb;
                half alpha = mainTex.a * pow(saturate(mask), _Mask_Power) * softFade * input.color.a;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
