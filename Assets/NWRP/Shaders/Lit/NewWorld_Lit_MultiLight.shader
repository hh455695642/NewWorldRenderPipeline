// ============================================================
// NewWorld/Lit/MultiLight
//
// Demonstration shader for main light + additional point/spot lights.
// Additional light count is clamped by the renderer-side runtime limit.
// ============================================================
Shader "NewWorld/Lit/MultiLight"
{
    Properties
    {
        _BaseColor     ("Base Color", Color)       = (1, 1, 1, 1)
        _SpecularColor ("Specular Color", Color)   = (1, 1, 1, 1)
        _Smoothness    ("Smoothness", Range(0, 1)) = 0.5
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
                float3 viewWS      : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SpecularColor;
                half  _Smoothness;
                half  _ReceiveShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
            #include "../../ShaderLibrary/Lighting.hlsl"
            #undef NWRP_MATERIAL_RECEIVE_SHADOWS
            #include "../../ShaderLibrary/BRDF.hlsl"

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS  = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS      = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 viewWS   = SafeNormalize(IN.viewWS);
                half shininess = exp2(10.0 * _Smoothness + 1.0);

                half3 diffuse  = half3(0, 0, 0);
                half3 specular = half3(0, 0, 0);

                // Main directional light
                Light mainLight = GetMainLight(IN.positionWS, normalWS);
                half3 mainLightColor = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));

                diffuse  += _BaseColor.rgb * mainLightColor * mainNdotL;
                specular += SpecularBlinnPhong(mainLight.direction, normalWS, viewWS,
                                               _SpecularColor.rgb, shininess) * mainLightColor;

                // Additional lights
                int addLightCount = GetAdditionalLightsCount();
                for (int i = 0; i < addLightCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS);
                    half3 attenuatedColor = addLight.color * addLight.distanceAttenuation;
                    half addNdotL = saturate(dot(normalWS, addLight.direction));

                    diffuse  += _BaseColor.rgb * attenuatedColor * addNdotL;
                    specular += SpecularBlinnPhong(addLight.direction, normalWS, viewWS,
                                                   _SpecularColor.rgb, shininess) * attenuatedColor;
                }

                // Ambient
                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _BaseColor.rgb;

                return half4(diffuse + specular + ambient, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
