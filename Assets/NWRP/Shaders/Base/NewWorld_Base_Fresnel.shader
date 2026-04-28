Shader "NewWorld/Base/Fresnel"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Power ("Power", Float) = 5
        [Toggle] _Reflection ("Reflection", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ _REFLECTION_ON

            #include "../../ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Power;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            // Fresnel 近似: (1 - dot(V, N))^power
            half Fresnel(half3 normal, half3 viewDir, half power)
            {
                return pow((1.0 - saturate(dot(normalize(normal), normalize(viewDir)))), power);
            }

            // 采样反射探针 Cubemap
            half3 Reflection(float3 viewDirWS, float3 normalWS)
            {
                float3 reflectVec = reflect(-viewDirWS, normalWS);
                return DecodeHDREnvironment(
                    SAMPLE_TEXTURECUBE(unity_SpecCube0, samplerunity_SpecCube0, reflectVec),
                    unity_SpecCube0_HDR
                );
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 viewWS = SafeNormalize(IN.viewWS);
                half fresnel = Fresnel(normalWS, viewWS, _Power);
                half4 totalColor = _Color * fresnel;

                #if defined(_REFLECTION_ON)
                    half3 cubemap = Reflection(viewWS, normalWS);
                    totalColor.xyz *= cubemap;
                #endif

                return totalColor;
            }
            ENDHLSL
        }
    }
}
