Shader "NewWorld/Env/ShardCrystal"
{
    Properties
    {
        [Header(Base Maps)]
        [NoScaleOffset] _BaseMap ("Base Map", 2D) = "white" {}
        [NoScaleOffset] _MaskMap ("Mask Map (R:Met G:AO A:Smooth)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        [NoScaleOffset] _EmissiveMap ("Emissive Map", 2D) = "black" {}
        [HDR] _EmissiveColor ("Emissive Color", Color) = (0, 0, 0, 1)
        
        [Header(MatCap)]
        [NoScaleOffset] _MatCapMap ("MatCap Map", 2D) = "black" {}
        _MatCapIntensity ("MatCap Intensity", Range(0, 5)) = 1.0
        [HDR] _MatCapColor ("MatCap Color", Color) = (1, 1, 1, 1)
        
        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 5)) = 0.5
        [HDR] _FresnelColor ("Fresnel Color", Color) = (0.5, 0.9, 1.0, 1.0)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Blend One Zero
        ZWrite On
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float  _NormalStrength;
            half4  _EmissiveColor;
            float  _MatCapIntensity;
            half4  _MatCapColor;
            float  _FresnelPower;
            float  _FresnelIntensity;
            half4  _FresnelColor;
        CBUFFER_END
        ENDHLSL        
        
        // ==================== ForwardLit ====================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 tangentWS   : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);      SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissiveMap);  SAMPLER(sampler_EmissiveMap);
            TEXTURE2D(_MatCapMap);    SAMPLER(sampler_MatCapMap);
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput   = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.uv         = input.uv;
                output.normalWS   = normalInput.normalWS;
                
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS  = half4(normalInput.tangentWS.xyz, sign);
                
                output.viewDirWS  = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                float2 uv = input.uv;
                
                // ---- Sample Textures ----
                half3 albedo  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half4 maskMap = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
                half  ao      = maskMap.g;
                
                // ---- Normal Map ----
                half4 normalMapSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
                half3 normalTS = UnpackNormalScale(normalMapSample, _NormalStrength);
                
                float  sgn       = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 TBN      = half3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                float3 normalWS  = normalize(mul(normalTS, TBN));
                
                // ---- Emission ----
                half3 emission = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, uv).rgb * _EmissiveColor.rgb;
                
                // ============ Crystal Effects ============
                
                // 1. MatCap
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float2 matCapUV   = viewNormal.xy * 0.5 + 0.5;
                half3  matCapColor = SAMPLE_TEXTURE2D(_MatCapMap, sampler_MatCapMap, matCapUV).rgb 
                                     * _MatCapColor.rgb * _MatCapIntensity;
                
                // 2. Fresnel
                float NdotV   = saturate(dot(normalWS, input.viewDirWS));
                half  fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                
                half3 crystalEffect = matCapColor + fresnel * _FresnelColor.rgb;
                
                // 3. Mix into emission
                emission = lerp(emission, crystalEffect, 0.35);
                
                // ============ Half-Lambert Lighting ============
                Light mainLight  = GetMainLight();
                half  NdotL      = dot(normalWS, mainLight.direction);
                half  halfLambert = NdotL * 0.5 + 0.5;
                
                half3 ambient = half3(0.15, 0.15, 0.18);
                half3 diffuse = albedo * (mainLight.color.rgb * halfLambert + ambient) * ao;
                
                // ============ Final Output ============
                half3 finalColor = diffuse + emission;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // ==================== ShadowCaster ====================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            Cull [_MainLightShadowCasterCull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            
            // Lighting.hlsl 包含了 Shadows.hlsl 所需的全部依赖
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float3 _LightDirection;
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                
                output.positionCS = positionCS;
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // ==================== DepthOnly ====================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            
            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // ==================== DepthNormals ====================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            
            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }
    
    FallBack Off
}
