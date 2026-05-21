Shader "Shader Graphs/ShuJuRen"
{
    Properties
    {
        _CutOffHeight ("CutOffHeight", Range(-2, 2)) = 0
        _DissovleMap ("DissovleMap", 2D) = "white" {}
        [Toggle] _DissovleOnePanner ("DissovleOnePanner", Float) = 0
        [Toggle] _Dissovleloop ("Dissovleloop", Float) = 0
        _DissovleTilling ("DissovleTilling", Vector) = (1, 1, 0, 0)

        _PatternMap ("PatternMap", 2D) = "white" {}
        _PatternMap2 ("PatternMap2", 2D) = "white" {}
        _PatternTilling ("PatternTilling", Range(0, 20)) = 1
        [HDR] _PatternColor ("PatternColor", Color) = (1, 1, 1, 1)
        _PatternFlowSpeed ("PatternFlowSpeed", Range(-4, 4)) = 0

        [HDR] _GlowColor ("GlowColor", Color) = (1, 1, 1, 1)
        _GlowWidth ("GlowWidth", Range(0.001, 1)) = 0.05
        [HDR] _FresnelColor ("FresnelColor", Color) = (1, 1, 1, 1)
        _FresnelPower ("FresnelPower", Range(0.1, 10)) = 2

        [HideInInspector] _WorkflowMode ("_WorkflowMode", Float) = 1
        [HideInInspector] _CastShadows ("_CastShadows", Float) = 0
        [HideInInspector] _ReceiveShadows ("_ReceiveShadows", Float) = 0
        [HideInInspector] _Surface ("_Surface", Float) = 1
        [HideInInspector] _Blend ("_Blend", Float) = 0
        [HideInInspector] _AlphaClip ("_AlphaClip", Float) = 1
        [HideInInspector] _BlendModePreserveSpecular ("_BlendModePreserveSpecular", Float) = 0
        [HideInInspector] _SrcBlend ("_SrcBlend", Float) = 5
        [HideInInspector] _DstBlend ("_DstBlend", Float) = 10
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
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include "../../../NWRP/ShaderLibrary/Core.hlsl"
            #include "../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_DissovleMap);
            SAMPLER(sampler_DissovleMap);
            TEXTURE2D(_PatternMap);
            SAMPLER(sampler_PatternMap);
            TEXTURE2D(_PatternMap2);
            SAMPLER(sampler_PatternMap2);

            CBUFFER_START(UnityPerMaterial)
                half _CutOffHeight;
                float4 _DissovleMap_ST;
                half _DissovleOnePanner;
                half _Dissovleloop;
                float4 _DissovleTilling;
                float4 _PatternMap_ST;
                float4 _PatternMap2_ST;
                half _PatternTilling;
                half4 _PatternColor;
                half _PatternFlowSpeed;
                half4 _GlowColor;
                half _GlowWidth;
                half4 _FresnelColor;
                half _FresnelPower;
                half _WorkflowMode;
                half _CastShadows;
                half _ReceiveShadows;
                half _Surface;
                half _Blend;
                half _AlphaClip;
                half _BlendModePreserveSpecular;
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
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half3 normalWS : TEXCOORD2;
                half3 viewDirWS : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ApplyDissolveUV(float2 uv)
            {
                float2 offset = lerp(_DissovleTilling.zw, _DissovleTilling.zw * _Time.y, step(0.5h, _Dissovleloop));
                offset = lerp(offset, float2(0.0, 0.0), step(0.5h, _DissovleOnePanner));
                return uv * _DissovleTilling.xy + offset;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.texcoord;
                output.color = GetNWRPParticleVertexColor(input.color);
                output.normalWS = half3(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = half3(GetWorldSpaceViewDir(positionWS));
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half dissolve = SAMPLE_TEXTURE2D(_DissovleMap, sampler_DissovleMap, ApplyDissolveUV(input.uv)).r;
                half width = max(_GlowWidth, 0.0001h);
                half heightSignal = half(input.positionOS.y) - _CutOffHeight + (dissolve - 0.5h) * width;
                half reveal = smoothstep(-width, width, heightSignal);
                half edge = 1.0h - smoothstep(0.0h, width, abs(heightSignal));

                float2 flow = float2(_PatternFlowSpeed * _Time.y, 0.0);
                float2 patternUV = input.uv * max(_PatternTilling, 0.0001h);
                half patternA = SAMPLE_TEXTURE2D(_PatternMap, sampler_PatternMap, patternUV + flow).r;
                half patternB = SAMPLE_TEXTURE2D(_PatternMap2, sampler_PatternMap2, patternUV - flow).r;
                half pattern = saturate(patternA + patternB);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), max(_FresnelPower, 0.0001h));

                half alpha = saturate((reveal + edge) * input.color.a);
                clip(lerp(1.0h, alpha - 0.001h, step(0.5h, _AlphaClip)));

                half3 rgb =
                    _PatternColor.rgb * pattern * reveal +
                    _GlowColor.rgb * edge +
                    _FresnelColor.rgb * fresnel;
                rgb *= input.color.rgb;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
