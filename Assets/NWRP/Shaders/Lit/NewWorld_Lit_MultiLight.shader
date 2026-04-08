// ============================================================
// NewWorld/Lit/MultiLight
//
// 多光源演示 Shader（使用 Blinn-Phong 光照模型）。
// 主方向光 + 附加光源（点光源/聚光灯）循环叠加。
//
// 需要场景中放置 Point Light 或 Spot Light 才能看到附加光源效果。
// 附加光源数量上限由 Pipeline Asset 的 maxAdditionalLights 控制。
// ============================================================

Shader "NewWorld/Lit/MultiLight"
{
    Properties
    {
        _BaseColor     ("Base Color", Color)       = (1, 1, 1, 1)
        _SpecularColor ("Specular Color", Color)   = (1, 1, 1, 1)
        _Smoothness    ("Smoothness", Range(0, 1)) = 0.5
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
            #include "../../ShaderLibrary/Lighting.hlsl"
            #include "../../ShaderLibrary/BRDF.hlsl"

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
            CBUFFER_END

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

                // ── 主方向光 ────────────────────────────────────
                Light mainLight = GetMainLight();
                half3 mainLightColor = mainLight.color * mainLight.distanceAttenuation;
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));

                diffuse  += _BaseColor.rgb * mainLightColor * mainNdotL;
                specular += SpecularBlinnPhong(mainLight.direction, normalWS, viewWS,
                                               _SpecularColor.rgb, shininess) * mainLightColor;

                // ── 附加光源循环 ────────────────────────────────
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

                // ── 环境光 ──────────────────────────────────────
                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _BaseColor.rgb;

                return half4(diffuse + specular + ambient, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
