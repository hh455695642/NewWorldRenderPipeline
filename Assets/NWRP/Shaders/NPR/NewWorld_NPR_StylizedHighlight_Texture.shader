// ============================================================
// NewWorld/NPR/StylizedHighlight (Texture)
//
// 使用纹理定义任意形状的风格化高光。
//
// 原理：
// 1. 计算 Blinn-Phong 半角向量 H = normalize(V + L)
// 2. 将 H 变换到切线空间（TBN）
// 3. 将切线空间 H 的 xy 分量映射为 UV 坐标
// 4. 用映射后的 UV 采样形状纹理
//
// 这样任何形状（星形、心形、十字等）的纹理都能变成高光形状。
// 高光会自动跟随光照和视角旋转。
//
// 切线空间操作展示了 NWRP SpaceTransforms.hlsl 的 TBN 工具。
//
// 参考: https://zhuanlan.zhihu.com/p/640258070
// ============================================================

Shader "NewWorld/NPR/StylizedHighlight (Texture)"
{
    Properties
    {
        _SpecularShapeMap ("Highlight Shape Map", 2D) = "white" {}
        _SpecularColor   ("Specular Color", Color)    = (1, 1, 1, 1)
        _SpecularScale   ("Specular Scale", Float)    = 1.0
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 viewTS      : TEXCOORD0;
                float3 lightDirTS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _SpecularColor;
                half  _SpecularScale;
            CBUFFER_END

            TEXTURE2D(_SpecularShapeMap);
            SAMPLER(sampler_SpecularShapeMap);

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);

                // 构建 TBN 矩阵
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                float3x3 tbnWorld = float3x3(
                    normalInputs.tangentWS,
                    normalInputs.bitangentWS,
                    normalInputs.normalWS
                );

                // 将视线和光照方向变换到切线空间
                OUT.viewTS     = TransformWorldToTangent(GetWorldSpaceViewDir(positionWS), tbnWorld);
                OUT.lightDirTS = TransformWorldToTangent(GetMainLight().direction, tbnWorld);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 viewTS     = normalize(IN.viewTS);
                half3 lightDirTS = normalize(IN.lightDirTS);

                // 切线空间半角向量
                half3 halfDirTS = normalize(viewTS + lightDirTS);

                // 将半角向量 xy 映射为 UV
                // 反转轴向以适应 UV 方向
                half2 flip  = halfDirTS.xy * -1.0;
                // 缩放控制高光大小
                half2 scale = flip / max(0.01, _SpecularScale);
                // [-1,1] → [0,1]
                half2 uv = scale + 0.5;

                // 采样形状纹理
                half4 shape = SAMPLE_TEXTURE2D(_SpecularShapeMap, sampler_SpecularShapeMap, uv);

                // 与光源颜色混合
                Light light = GetMainLight();
                half3 lightColor = light.color * light.distanceAttenuation;

                return shape * _SpecularColor * half4(lightColor, 1.0);
            }

            ENDHLSL
        }
    }

    CustomEditor "NWRP.Editor.NewWorldShaderGUI"
}
