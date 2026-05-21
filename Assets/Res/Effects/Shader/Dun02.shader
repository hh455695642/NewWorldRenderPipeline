Shader "Effects/Dun02"
{
    Properties
    {
        _Int ("Int", Float) = 1
        _FrePower ("FrePower", Float) = 5
        [MainTexture] _MainTex ("Main Tex", 2D) = "white" {}
        [MainColor][HDR] _Color ("Color", Color) = (1,0.7190735,0,0)
        TexT_O ("TexT&O", Vector) = (1,1,0,0)
        _Beat_Int ("Beat Int", Range(0,10)) = 0
        _Break_Int ("Break Int", Range(0,0.2)) = 0
        _DissovleTex ("Dissovle Tex", 2D) = "white" {}
        _Dissovle ("Dissovle", Range(0,1)) = 0
        [HDR] _EadgeColor ("Eadge Color", Color) = (1,1,1,0)
        _Eadge ("Eadge", Range(0,1)) = 0

        [Toggle(_FLIPBOOKBLENDING_ON)] _FlipbookBlending ("Flipbook Blending", Float) = 0

        [HideInInspector] _Surface ("__surface", Float) = 1
        [HideInInspector] _Blend ("__blend", Float) = 2
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

            Blend SrcAlpha One, One One
            ZWrite Off
            ZTest LEqual
            Cull Back

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
                half _Dissovle;
                half4 _EadgeColor;
                half _Eadge;
                half _FlipbookBlending;
                half _Surface;
                half _Blend;
                half _ZWrite;
                half _AlphaClip;
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

            half4 SampleFxTexture(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 blendUV, half blend)
            {
                half4 result = SAMPLE_TEXTURE2D(tex, samplerTex, uv);
            #if defined(_FLIPBOOKBLENDING_ON)
                result = lerp(result, SAMPLE_TEXTURE2D(tex, samplerTex, blendUV), blend);
            #endif
                return result;
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
                float2 mainUV = input.uv * TexT_O.xy + TexT_O.zw * _Time.y;
                float2 mainBlendUV = input.blendUV.xy * TexT_O.xy + TexT_O.zw * _Time.y;

                half4 mainTex = SampleFxTexture(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), mainUV, mainBlendUV, blend);
                half4 dissolveTex = SAMPLE_TEXTURE2D(_DissovleTex, sampler_DissovleTex, input.uv);

                half dissolveEdge = _Dissovle * 1.1h - 0.1h;
                half4 dissolveMask = step(dissolveEdge, dissolveTex);
                half4 edgeMask = dissolveMask - step(dissolveEdge + _Eadge, dissolveTex);

                half fresnel = pow(1.0h - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), _FrePower);
                half4 fresnelColor = mainTex * (_Color * fresnel * _Int);
                half4 edgeColor = edgeMask * _EadgeColor;
                half4 graphColor = input.color.a * (dissolveMask * input.color) * (fresnelColor * input.color + edgeColor);

                return half4(graphColor.rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
