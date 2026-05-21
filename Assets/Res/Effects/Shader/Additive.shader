Shader "Effects/additive"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Tex", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Tiling_Offset ("Tiling & Offset (xy=Tiling zw=Offset/Speed)", Vector) = (1,1,0,0)
        [Toggle] _Loop ("Loop (UV Scroll)", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
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
        }

        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull [_Cull]

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float4 _Tiling_Offset;
                half _Loop;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 uv;
                float3 blendUV;
                GetNWRPParticleUVs(uv, blendUV, input.uv.xyxy, 0.0);

                float2 tiling = _Tiling_Offset.xy;
                float2 offset = lerp(_Tiling_Offset.zw, _Tiling_Offset.zw * _Time.y, step(0.5h, _Loop));

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = uv * tiling + offset;
                output.color = GetNWRPParticleVertexColor(input.color);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 combined = texColor * input.color * _Color;
                half3 rgb = combined.rgb * combined.a;
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
