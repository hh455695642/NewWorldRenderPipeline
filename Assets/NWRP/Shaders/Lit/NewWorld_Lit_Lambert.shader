// ============================================================
// NewWorld/Lit/Lambert
//
// Basic Lambert lighting for verifying the shared main-light path.
// ============================================================

Shader "NewWorld/Lit/Lambert"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [ToggleUI] _ReceiveShadows ("Receive Realtime Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "NewWorldForward"
            Tags { "LightMode" = "NewWorldForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../../ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _ReceiveShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
            #include "../../ShaderLibrary/Lighting.hlsl"
            #undef NWRP_MATERIAL_RECEIVE_SHADOWS

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(input.positionWS, normalWS);
                half3 lightColor = light.color * light.distanceAttenuation * light.shadowAttenuation;
                half NdotL = saturate(dot(normalWS, light.direction));
                half3 diffuse = _BaseColor.rgb * lightColor * NdotL;
                return half4(diffuse, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
