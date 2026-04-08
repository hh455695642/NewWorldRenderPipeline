Shader "NewWorld/Env/TreeLeaf"
{
    Properties
    {
        [Header(Maps)]
        [Space(5)]
        [NoScaleOffset] _Albedo("Base", 2D) = "white" {}
        _AlphaCutoff("Opacity Cutoff", Range(0, 1)) = 0.35

        [Header(Settings)]
        [Space(5)]
        _MainColor("Main Color", Color) = (1,1,1,1)

        [Header(Second Color)]
        [Space(5)]
        _SecondColor("Second Color", Color) = (0,0,0,0)
        [KeywordEnum(World_Noise_3D, UV_Gradient)] _SecondColorOverlayType("Overlay Method", Float) = 0
        _SecondColorOffset("Offset", Float) = 1
        _SecondColorFade("Balance", Float) = 1
        _WorldNoiseScale("World Noise Scale", Float) = 1

        [Header(Distance Fade)]
        [Space(5)]
        _FadeDistance("Distance", Float) = 30
        _FadeFalloff("Falloff", Range(0, 1)) = 0.7

        [Header(FakeSSS)]
        [Space(5)]
        _TranslucencyInt("Translucency", Range(0, 10)) = 1
        _TranslucencyColor("Translucency Color", Color) = (1,1,1,0)

        [HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline" 
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest"
        }

        Cull Off
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TranslucencyColor;
            float4 _SecondColor;
            float4 _MainColor;
            float _TranslucencyInt;
            float _SecondColorFade;
            float _SecondColorOffset;
            float _WorldNoiseScale;
            float _FadeFalloff;
            float _FadeDistance;
            float _AlphaCutoff;
        CBUFFER_END

        TEXTURE2D(_Albedo);
        SAMPLER(sampler_Albedo);

        // Simplex Noise 3D
        float3 mod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
        float4 mod289(float4 x) { return x - floor(x / 289.0) * 289.0; }
        float4 permute(float4 x) { return mod289((x * 34.0 + 1.0) * x); }
        float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - r * 0.85373472095314; }

        float snoise(float3 v)
        {
            const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
            float3 i = floor(v + dot(v, C.yyy));
            float3 x0 = v - i + dot(i, C.xxx);
            float3 g = step(x0.yzx, x0.xyz);
            float3 l = 1.0 - g;
            float3 i1 = min(g.xyz, l.zxy);
            float3 i2 = max(g.xyz, l.zxy);
            float3 x1 = x0 - i1 + C.xxx;
            float3 x2 = x0 - i2 + C.yyy;
            float3 x3 = x0 - 0.5;
            i = mod289(i);
            float4 p = permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0)) + i.y + float4(0.0, i1.y, i2.y, 1.0)) + i.x + float4(0.0, i1.x, i2.x, 1.0));
            float4 j = p - 49.0 * floor(p / 49.0);
            float4 x_ = floor(j / 7.0);
            float4 y_ = floor(j - 7.0 * x_);
            float4 x = (x_ * 2.0 + 0.5) / 7.0 - 1.0;
            float4 y = (y_ * 2.0 + 0.5) / 7.0 - 1.0;
            float4 h = 1.0 - abs(x) - abs(y);
            float4 b0 = float4(x.xy, y.xy);
            float4 b1 = float4(x.zw, y.zw);
            float4 s0 = floor(b0) * 2.0 + 1.0;
            float4 s1 = floor(b1) * 2.0 + 1.0;
            float4 sh = -step(h, 0.0);
            float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
            float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
            float3 g0 = float3(a0.xy, h.x);
            float3 g1 = float3(a0.zw, h.y);
            float3 g2 = float3(a1.xy, h.z);
            float3 g3 = float3(a1.zw, h.w);
            float4 norm = taylorInvSqrt(float4(dot(g0, g0), dot(g1, g1), dot(g2, g2), dot(g3, g3)));
            g0 *= norm.x;
            g1 *= norm.y;
            g2 *= norm.z;
            g3 *= norm.w;
            float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
            m = m * m;
            m = m * m;
            float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));
            return 42.0 * dot(m, px);
        }

        float CalculateSecondColorMask(float3 worldPos, float2 uv)
        {
            float noiseValue = 0;
            
            #if defined(_SECONDCOLOROVERLAYTYPE_WORLD_NOISE_3D)
                noiseValue = snoise(worldPos * _WorldNoiseScale) * 0.5 + 0.5;
            #elif defined(_SECONDCOLOROVERLAYTYPE_UV_GRADIENT)
                noiseValue = uv.y;
            #else
                noiseValue = snoise(worldPos * _WorldNoiseScale) * 0.5 + 0.5;
            #endif

            float offset = noiseValue + (1.0 - _SecondColorOffset);
            float invOffset = 1.0 - offset;
            float fadeAdjust = _SecondColorFade - (-0.5);
            float mask = lerp(offset, invOffset, fadeAdjust);
            return saturate(mask);
        }

        float CalculateDistanceFade(float3 positionWS)
        {
            float dist = distance(_WorldSpaceCameraPos, positionWS);
            float fade = saturate((dist - _FadeDistance) / (_FadeDistance * (1.0 - _FadeFalloff + 0.001)));
            return lerp(1.0, 1.0 - fade, 1.0);
        }

        float3 CalculateTranslucency(float3 viewDir, float3 lightDir, float shadowAtten, float3 lightColor)
        {
            float VdotL = saturate(dot(-viewDir, lightDir));
            float3 translucency = VdotL * shadowAtten * lightColor * _TranslucencyColor.rgb * _TranslucencyInt;
            return translucency;
        }

        ENDHLSL

        // Forward Pass
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma shader_feature_local _SECONDCOLOROVERLAYTYPE_WORLD_NOISE_3D _SECONDCOLOROVERLAYTYPE_UV_GRADIENT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.texcoord;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample albedo
                float4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                
                // Distance fade alpha
                float distanceFade = CalculateDistanceFade(input.positionWS);
                float alpha = albedoSample.a * distanceFade;
                
                // Alpha test
                clip(alpha - _AlphaCutoff);

                // Base color
                float3 baseColor = _MainColor.rgb * albedoSample.rgb;

                // Second color blending
                float secondColorMask = CalculateSecondColorMask(input.positionWS, input.uv);
                float3 secondColor = _SecondColor.rgb * albedoSample.rgb;
                baseColor = lerp(baseColor, secondColor, secondColorMask);


                // Normalize
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Main light
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float shadowAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // Half Lambert diffuse
                float NdotL = dot(normalWS, lightDir);
                float halfLambert = NdotL * 0.5 + 0.5;
                float3 diffuse = baseColor * mainLight.color * halfLambert * shadowAtten;

                // Translucency
                float3 translucency = CalculateTranslucency(viewDirWS, lightDir, shadowAtten, mainLight.color) * baseColor;

                // Ambient (SH or Lightmap)
                float3 ambient = baseColor * SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);

                // Additional lights
                float3 additionalLights = float3(0, 0, 0);
                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0; lightIndex < pixelLightCount; ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        float addNdotL = dot(normalWS, normalize(light.direction));
                        float addHalfLambert = addNdotL * 0.5 + 0.5;
                        float addAtten = light.distanceAttenuation * light.shadowAttenuation;
                        additionalLights += baseColor * light.color * addHalfLambert * addAtten;
                    }
                #endif

                // Final color
                float3 finalColor = diffuse + ambient + translucency + additionalLights;

                // Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ShadowCaster Pass
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

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.uv = input.texcoord;
                output.positionWS = positionWS;

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                float distanceFade = CalculateDistanceFade(input.positionWS);
                clip(albedoSample.a * distanceFade - _AlphaCutoff);

                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                output.uv = input.texcoord;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                float distanceFade = CalculateDistanceFade(input.positionWS);
                clip(albedoSample.a * distanceFade - _AlphaCutoff);

                return input.positionCS.z;
            }
            ENDHLSL
        }

        // DepthNormals Pass
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
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
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
                output.uv = input.texcoord;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                float distanceFade = CalculateDistanceFade(input.positionWS);
                clip(albedoSample.a * distanceFade - _AlphaCutoff);

                return half4(normalize(input.normalWS) * 0.5 + 0.5, 0.0);
            }
            ENDHLSL
        }

        // Meta Pass
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment

            #pragma shader_feature_local _SECONDCOLOROVERLAYTYPE_WORLD_NOISE_3D _SECONDCOLOROVERLAYTYPE_UV_GRADIENT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings MetaPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.staticLightmapUV, 0, unity_LightmapST, unity_DynamicLightmapST);
                output.uv = input.texcoord;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                return output;
            }

            half4 MetaPassFragment(Varyings input) : SV_TARGET
            {
                float4 albedoSample = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                clip(albedoSample.a - _AlphaCutoff);

                float3 baseColor = _MainColor.rgb * albedoSample.rgb;

                float secondColorMask = CalculateSecondColorMask(input.positionWS, input.uv);
                float3 secondColor = _SecondColor.rgb * albedoSample.rgb;
                baseColor = lerp(baseColor, secondColor, secondColorMask);

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = baseColor;
                metaInput.Emission = 0;

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
