Shader "Shader Graphs/Distort"
{
    Properties
    {
        _NosieTex ("NosieTex", 2D) = "white" {}
        _Distort ("Distort", Range(0, 1)) = 0.05
        _NosieTex_T_O ("NosieTex T&O", Vector) = (1, 1, 0, 0)
        [HDR] Color_52a13e18bea44af29b50fcdd858b51eb ("Color", Color) = (1, 1, 1, 1)
        [Toggle] _MaskCircle ("MaskCircle", Float) = 1

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
            #include "../../../NWRP/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_NosieTex);
            SAMPLER(sampler_NosieTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NosieTex_ST;
                half _Distort;
                float4 _NosieTex_T_O;
                half4 Color_52a13e18bea44af29b50fcdd858b51eb;
                half _MaskCircle;
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ApplyNoiseUV(float2 uv)
            {
                return uv * _NosieTex_T_O.xy + _NosieTex_T_O.zw * _Time.y;
            }

            half CircleMask(float2 uv)
            {
                half radial = half(1.0 - saturate(length(uv - 0.5) * 2.0));
                radial = radial * radial * (3.0h - 2.0h * radial);
                return lerp(1.0h, radial, step(0.5h, _MaskCircle));
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.texcoord;
                output.color = GetNWRPParticleVertexColor(input.color);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half mask = CircleMask(input.uv);
                half2 noise = SAMPLE_TEXTURE2D(_NosieTex, sampler_NosieTex, ApplyNoiseUV(input.uv)).rg;
                half2 offset = (noise * 2.0h - 1.0h) * (_Distort * mask);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half3 sceneColor = SampleSceneColor(saturate(screenUV + offset));
                half4 tint = input.color * Color_52a13e18bea44af29b50fcdd858b51eb;
                half alpha = saturate(tint.a * mask);

                return half4(sceneColor * tint.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
