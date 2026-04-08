Shader "NewWorld/Env/MineralCrystal"
{
    Properties
    {
        [Header(Base Maps)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _MaskMap ("Mask Map", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        _EmissiveMap ("Emissive Map", 2D) = "black" {}
        [HDR] _EmissiveColor ("Emissive Color", Color) = (0, 0, 0, 1)
        
        [Header(Crystal Effect)]
        _CrystalColor ("Crystal Tint Color", Color) = (0.5, 0.9, 1.0, 1.0)
        
        [Header(Parallax Fake Volume)]
        _NoiseMap ("Noise Map", 2D) = "white" {}
        _ParallaxDepth ("Parallax Depth", Range(0, 0.5)) = 0.1
        _ParallaxLayers ("Parallax Layers", Range(1, 8)) = 4
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1.0
        _NoiseIntensity ("Noise Intensity", Range(0, 2)) = 1.0
        [HDR] _InnerGlowColor ("Inner Glow Color", Color) = (0.3, 0.8, 1.0, 1.0)
        
        [Header(MatCap)]
        _MatCapMap ("MatCap Map", 2D) = "black" {}
        _MatCapIntensity ("MatCap Intensity", Range(0, 2)) = 1.0
        [HDR] _MatCapColor ("MatCap Color", Color) = (1, 1, 1, 1)
        _MatCapBlendMode ("MatCap Blend (0:Add, 1:Multiply, 2:Screen)", Range(0, 2)) = 0
        
        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.5
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
            float4 _BaseMap_ST;
            float4 _NoiseMap_ST;
            float  _NormalStrength;
            half4  _EmissiveColor;
            
            half4  _CrystalColor;
            
            float  _ParallaxDepth;
            float  _ParallaxLayers;
            float  _NoiseScale;
            float  _NoiseIntensity;
            half4  _InnerGlowColor;
            
            float  _MatCapIntensity;
            half4  _MatCapColor;
            float  _MatCapBlendMode;
            
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
            
            // 仅保留 fog，不声明任何光照/阴影变体
            #pragma multi_compile_fog
            
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
                float3 viewDirTS   : TEXCOORD4;
                half   fogFactor   : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);      SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissiveMap);  SAMPLER(sampler_EmissiveMap);
            TEXTURE2D(_NoiseMap);     SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_MatCapMap);    SAMPLER(sampler_MatCapMap);
            
            // ---- Parallax Helpers ----
            float2 ParallaxOffset(float3 viewDirTS, float depth)
            {
                float2 parallaxDir = viewDirTS.xy / (viewDirTS.z + 0.42);
                return parallaxDir * depth;
            }
            
            half4 SampleParallaxLayers(float2 uv, float3 viewDirTS, float crystalMask)
            {
                half4 accumColor = half4(0, 0, 0, 0);
                float layerCount = _ParallaxLayers;
                float layerDepth = _ParallaxDepth / layerCount;
                
                UNITY_LOOP
                for (int i = 0; i < (int)layerCount; i++)
                {
                    float currentDepth = layerDepth * (i + 1);
                    float2 offsetUV = uv + ParallaxOffset(viewDirTS, currentDepth);
                    
                    float2 noiseUV = offsetUV * _NoiseScale;
                    half noiseValue = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, noiseUV, 0).r;
                    
                    float depthFade = 1.0 - (float(i) / layerCount);
                    depthFade = depthFade * depthFade;
                    
                    accumColor.rgb += noiseValue * _InnerGlowColor.rgb * depthFade * _NoiseIntensity;
                    accumColor.a += noiseValue * depthFade;
                }
                
                accumColor /= layerCount;
                return accumColor * crystalMask;
            }
            
            // ---- MatCap Blend ----
            half3 BlendMatCap(half3 baseColor, half3 matCapColor, float blendMode)
            {
                half3 result;
                if (blendMode < 0.5)
                    result = baseColor + matCapColor;           // Add
                else if (blendMode < 1.5)
                    result = baseColor * matCapColor;           // Multiply
                else
                    result = 1.0 - (1.0 - baseColor) * (1.0 - matCapColor); // Screen
                return result;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput   = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS   = normalInput.normalWS;
                
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS  = half4(normalInput.tangentWS.xyz, sign);
                
                output.viewDirWS  = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                
                // Tangent-space view dir for parallax
                float3 viewDirWS = output.viewDirWS;
                output.viewDirTS = float3(
                    dot(viewDirWS, normalInput.tangentWS),
                    dot(viewDirWS, normalInput.bitangentWS),
                    dot(viewDirWS, normalInput.normalWS)
                );
                
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 uv = input.uv;
                
                // ---- Sample Base Textures ----
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half3 albedo      = baseMap.rgb;
                half  crystalMask = baseMap.a;
                
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
                
                // ============ Crystal Logic ============
                half3 crystalEffect = half3(0, 0, 0);
                
                if (crystalMask > 0.01)
                {
                    // 1. Fake Volumetric (Parallax Layers)
                    half4 parallaxColor = SampleParallaxLayers(uv, input.viewDirTS, crystalMask);
                    crystalEffect += parallaxColor.rgb;
                    
                    // 2. MatCap
                    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
                    float2 matCapUV   = viewNormal.xy * 0.5 + 0.5;
                    half3  matCapSample = SAMPLE_TEXTURE2D(_MatCapMap, sampler_MatCapMap, matCapUV).rgb;
                    half3  matCapColor  = matCapSample * _MatCapColor.rgb * _MatCapIntensity * crystalMask;
                    crystalEffect = BlendMatCap(crystalEffect, matCapColor, _MatCapBlendMode);
                    
                    // 3. Fresnel
                    float NdotV   = saturate(dot(normalWS, input.viewDirWS));
                    half  fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                    crystalEffect += fresnel * _FresnelColor.rgb * crystalMask;
                    
                    // 4. Crystal Tint
                    albedo = lerp(albedo, albedo * _CrystalColor.rgb, crystalMask * 0.5);
                }
                
                // ============ Half-Lambert Lighting ============
                Light mainLight   = GetMainLight();
                half  NdotL       = dot(normalWS, mainLight.direction);
                half  halfLambert = NdotL * 0.5 + 0.5;
                
                half3 ambient = half3(0.15, 0.15, 0.18);
                half3 diffuse = albedo * (mainLight.color.rgb * halfLambert + ambient) * ao;
                
                // ============ Final ============
                half3 finalColor = diffuse + emission + crystalEffect;
                
                finalColor = MixFog(finalColor, input.fogFactor);
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // ==================== ShadowCaster ====================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
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