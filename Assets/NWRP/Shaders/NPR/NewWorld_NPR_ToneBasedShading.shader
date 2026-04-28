// ============================================================
// NewWorld/NPR/ToneBasedShading
//
// 基于色调的着色模型（Gooch Shading 变体）。
// 将光照从传统的「明暗」映射为「冷暖色调」插值：
//   - 向光面 → 暖色调（黄/橙）
//   - 背光面 → 冷色调（蓝/紫）
//
// 通过 WarmColor.a 和 CoolColor.a 控制物体本身颜色
// 对冷暖色调的混合权重。
//
// 参考: Gooch et al. "A Non-Photorealistic Lighting Model"
// https://users.cs.northwestern.edu/~ago820/thesis/node26.html
// ============================================================

Shader "NewWorld/NPR/ToneBasedShading"
{
    Properties
    {
        _BaseColor     ("Base Color", Color)       = (1, 1, 1, 1)
        _WarmColor     ("Warm Color (a=blend)", Color) = (0.6, 0.6, 0.0, 0.5)
        _CoolColor     ("Cool Color (a=blend)", Color) = (0.0, 0.0, 0.6, 0.5)
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
            #pragma multi_compile_instancing

            #include "../../ShaderLibrary/Core.hlsl"
            #include "../../ShaderLibrary/Lighting.hlsl"
            #include "../../ShaderLibrary/BRDF.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewWS      : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half3 _BaseColor;
                half4 _WarmColor;
                half4 _CoolColor;
                half4 _SpecularColor;
                half  _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS      = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 viewWS   = SafeNormalize(IN.viewWS);

                Light light = GetMainLight();
                half3 lightColor = light.color * light.distanceAttenuation;
                half shininess = exp2(10.0 * _Smoothness + 1.0);

                // 反转光照方向以适配 Gooch 公式
                half NdotL = dot(normalWS, -light.direction);

                // kd: 漫反射颜色渐变（背光=黑, 向光=BaseColor）
                half3 kd = _BaseColor * (1.0 - NdotL);

                // 冷色调 = CoolColor + alpha * kd
                half3 kCool = _CoolColor.rgb + _CoolColor.a * kd;
                // 暖色调 = WarmColor + beta * kd
                half3 kWarm = _WarmColor.rgb + _WarmColor.a * kd;

                // 冷暖插值
                half t = (1.0 + NdotL) * 0.5;
                half3 diffuse = t * kCool + (1.0 - t) * kWarm;

                // Blinn-Phong 高光（使用 BRDF.hlsl 积木）
                half3 specular = SpecularBlinnPhong(light.direction, normalWS, viewWS,
                                                    _SpecularColor.rgb, shininess) * lightColor;

                // 环境光
                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _BaseColor;

                return half4(diffuse + specular + ambient, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
