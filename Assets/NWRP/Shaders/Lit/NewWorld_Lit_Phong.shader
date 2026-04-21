// ============================================================
// NewWorld/Lit/Phong
//
// Phong 光照模型 = Lambert 漫反射 + Phong 镜面反射 + 环境光
// 镜面反射使用反射向量 reflect(-L, N) 与视线方向 V 的点积。
// ============================================================

Shader "NewWorld/Lit/Phong"
{
    Properties
    {
        _BaseColor     ("Base Color", Color)          = (1, 1, 1, 1)
        _SpecularColor ("Specular Color", Color)      = (1, 1, 1, 1)
        _Smoothness    ("Smoothness", Range(0, 1))    = 0.5
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
                OUT.positionHCS   = TransformWorldToHClip(positionWS);
                OUT.normalWS      = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS        = GetWorldSpaceViewDir(positionWS);
                OUT.positionWS    = positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 viewWS   = SafeNormalize(IN.viewWS);

                Light light = GetMainLight(IN.positionWS, normalWS);
                half3 lightColor = light.color * light.distanceAttenuation * light.shadowAttenuation;

                // Smoothness → 指数映射，和 URP 示例保持一致
                half shininess = exp2(10.0 * _Smoothness + 1.0);

                // 漫反射 (Lambert)
                half NdotL = saturate(dot(normalWS, light.direction));
                half3 diffuse = _BaseColor.rgb * lightColor * NdotL;

                // 镜面反射 (Phong)
                half3 specular = SpecularPhong(light.direction, normalWS, viewWS,
                                               _SpecularColor.rgb, shininess) * lightColor;

                // 环境光（简单 SH L0）
                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _BaseColor.rgb;

                return half4(diffuse + specular + ambient, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
