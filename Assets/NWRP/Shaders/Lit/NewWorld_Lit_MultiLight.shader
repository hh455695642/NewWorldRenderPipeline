// ============================================================
// NewWorld/Lit/MultiLight
//
// Demonstration shader for main light plus additional point/spot lighting.
// The mobile shadow baseline supports main light realtime shadows and
// additional spot / point light realtime shadows through one shared atlas.
// ============================================================
Shader "NewWorld/Lit/MultiLight"
{
    Properties
    {
        _BaseColor     ("Base Color", Color)       = (1, 1, 1, 1)
        _SpecularColor ("Specular Color", Color)   = (1, 1, 1, 1)
        _Smoothness    ("Smoothness", Range(0, 1)) = 0.5
        [ToggleUI] _ReceiveShadows ("Receive Realtime Shadows", Float) = 1.0
        [ToggleUI] _CastShadows    ("Cast Realtime Shadows", Float) = 1.0
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
            #pragma multi_compile_instancing

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
                half  _CastShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
            #include "../../ShaderLibrary/LightingModels/BlinnPhong.hlsl"
            #undef NWRP_MATERIAL_RECEIVE_SHADOWS

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

                // Main directional light
                Light mainLight = GetMainLight(IN.positionWS, normalWS);
                half3 debugColor;
                if (TryGetMainLightShadowDebugOverride(mainLight, debugColor))
                {
                    return half4(debugColor, 1.0);
                }

                half3 directColor = EvaluateBlinnPhong(
                    mainLight,
                    normalWS,
                    viewWS,
                    _BaseColor.rgb,
                    _SpecularColor.rgb,
                    shininess);

                // Additional lights
                int addLightCount = GetAdditionalLightsCount();
                for (int i = 0; i < addLightCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS, normalWS);
                    directColor += EvaluateBlinnPhong(
                        addLight,
                        normalWS,
                        viewWS,
                        _BaseColor.rgb,
                        _SpecularColor.rgb,
                        shininess);
                }

                // Ambient
                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _BaseColor.rgb;

                return half4(directColor + ambient, 1.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_MainLightShadowCasterCull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowCasterVert
            #pragma fragment ShadowCasterFrag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SpecularColor;
                half  _Smoothness;
                half  _ReceiveShadows;
                half  _CastShadows;
            CBUFFER_END

            #define NWRP_MATERIAL_CAST_SHADOWS _CastShadows
            #include "Includes/ShadowCasterPass.hlsl"
            #undef NWRP_MATERIAL_CAST_SHADOWS
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SpecularColor;
                half  _Smoothness;
                half  _ReceiveShadows;
                half  _CastShadows;
            CBUFFER_END

            #include "Includes/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
