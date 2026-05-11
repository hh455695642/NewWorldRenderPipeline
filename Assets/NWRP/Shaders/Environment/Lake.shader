Shader "NewWorld/Env/Lake"
{
    Properties
    {
        [Header(Color Settings)]
        [HDR]_Color_Shallow("Color Shallow", Color) = (0.2269659, 0.822786, 0.4507858, 1)
        [HDR]_Color_Deep("Color Deep", Color) = (0, 0.2541522, 0.4507858, 1)
        _Water_Depth("Water Depth", Float) = 0.3
        _DistanceMask_Start("Distance Mask Start", Float) = 5
        _DistanceMask_Fade("Distance Mask Fade", Float) = 10
        [ToggleUI]_ShoreFade("Shore Fade", Float) = 1
        _ShoreFade_Smoothness("Shore Fade Smoothness", Range(0, 1)) = 0.2

        [Header(Gradation)]
        [ToggleUI]_ENABLEGRADATION("Enable Gradation", Float) = 0
        _GradationRange("Gradation Range", Float) = 0.8
        _Color0("Shore Color", Color) = (1, 0.4, 0.1, 1)
        _Color1("Yellow", Color) = (1, 0.9, 0.2, 1)
        _Color2("Green", Color) = (0.2, 1, 0.6, 1)
        _Color3("Blue", Color) = (0.1, 0.6, 1, 1)
        _Color4("Deep Blue", Color) = (0, 0.2, 0.6, 1)

        [Header(Shoreline)]
        [ToggleUI]_ENABLESHORELINE("Enable Shoreline", Float) = 1
        _SL_Color("SL Color", Color) = (1, 1, 1, 1)
        _SL_WaterDepth("SL Water Depth", Float) = 0.3
        _SL_Speed("SL Speed", Float) = 0.05
        _SL_Ammount("SL Amount", Float) = 5
        _SL_Thickness("SL Thickness", Range(0, 1)) = 0.3
        _SL_CenterMask("SL Center Mask", Range(0, 1)) = 0.5
        _SL_CenterMaskFade("SL Center Mask Fade", Range(0, 1)) = 0
        [NoScaleOffset]_SL_Dissolve_Mask("SL Dissolve Mask", 2D) = "white" {}
        _SL_Dissolve("SL Dissolve", Float) = 0.7
        _SL_GradientDissolve("SL Shore Gradient Dissolve", Range(-1, 1)) = 0
        _SL_MaskPan("SL Mask Pan", Vector) = (0.01, 0, 0, 0)
        _SL_MaskScale("SL Mask Scale", Float) = 4
        _SL_MaskTile("SL Mask Tile", Vector) = (1, 1, 0, 0)
        [ToggleUI]_SL_EnableTrail("SL Directional Lines", Float) = 0
        _SL_Trail_Fade("SL Trail Fade", Range(0, 2)) = 1

        [Header(Normal Map)]
        [Normal][NoScaleOffset]_Normal_Map("Normal Map", 2D) = "bump" {}
        _Normal_Strength("Normal Strength", Float) = 0.1
        _Normal_Pan("Normal Pan", Float) = 0.1
        _Normal_Scale("Normal Scale", Float) = 5
        _Normal_DistanceStrength("Normal Distance Strength", Float) = 0.01

        [Header(Lighting)]
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0.6)
        [HDR]_Specular_Color("Specular Color", Color) = (1, 1, 1, 1)
        _Specular_Spread("Specular Spread", Range(0, 1)) = 0
        _Specular_Hardness("Specular Hardness", Range(0, 1)) = 0
        _Specular_Size("Specular Size", Range(0, 1)) = 1
        [ToggleUI]_ReceiveShadows("Receive Realtime Shadows", Float) = 1

        [Header(Reflection)]
        _Cubemap("Reflection Cubemap", Cube) = "" {}
        _Reflection_Strength("Reflection Strength", Range(0, 1)) = 1
        _Reflection_Fresnel("Reflection Fresnel", Float) = 1
        _Reflection_Distortion("Reflection Distortion", Float) = 0

        [Header(Refraction)]
        _Refraction_Strength("Refraction Strength", Float) = 0.5
        _Refraction_Distance_Strength("Refraction Distance Strength", Float) = 0.1
        _Refraction_Distance_Fade("Refraction Distance Fade", Float) = 0.5

        [Header(Wave)]
        [ToggleUI]_ENABLEWAVE("Enable Wave", Float) = 1
        _Wave_Top_Color("Wave Top Color", Color) = (0.2982912, 0.9207434, 0.9245283, 1)
        _1st_Wave_Length("1st Wave Length", Float) = 3
        _1st_Wave_Height("1st Wave Height", Float) = 0.01
        _1st_Wave_Speed("1st Wave Speed", Float) = 1
        _1st_Wave_Direction("1st Wave Direction", Vector) = (1, 0, 0, 0)
        _1st_Wave_Sharpness("1st Wave Sharpness", Float) = 0.3
        _2nd_Wave_Length("2nd Wave Length", Float) = 5
        _2nd_Wave_Height("2nd Wave Height", Float) = 0.015
        _2nd_Wave_Speed("2nd Wave Speed", Float) = 1.3
        _2nd_Wave_Sharpness("2nd Wave Sharpness", Float) = 0.3
        _2nd_Wave_Direction("2nd Wave Direction", Vector) = (-1, 0, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "../../ShaderLibrary/Core.hlsl"
            #include "../../ShaderLibrary/DepthWorldReconstruction.hlsl"
            #include "../../ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color_Shallow;
                half4 _Color_Deep;
                half _Water_Depth;
                half _DistanceMask_Start;
                half _DistanceMask_Fade;
                half _GradationRange;
                half4 _Color0;
                half4 _Color1;
                half4 _Color2;
                half4 _Color3;
                half4 _Color4;
                half _ShoreFade;
                half _ShoreFade_Smoothness;
                half _ENABLEGRADATION;
                half _ENABLESHORELINE;
                half4 _SL_Color;
                half _SL_WaterDepth;
                half _SL_Speed;
                half _SL_Ammount;
                half _SL_Thickness;
                half _SL_CenterMask;
                half _SL_CenterMaskFade;
                half _SL_Dissolve;
                half _SL_GradientDissolve;
                half _SL_MaskScale;
                half _SL_EnableTrail;
                half _SL_Trail_Fade;
                half4 _SL_MaskPan;
                half4 _SL_MaskTile;
                half _Normal_Strength;
                half _Normal_Pan;
                half _Normal_Scale;
                half _Normal_DistanceStrength;
                half4 _Specular_Color;
                half _Specular_Size;
                half _Specular_Hardness;
                half _Specular_Spread;
                half4 _ShadowColor;
                half _ReceiveShadows;
                half _Reflection_Strength;
                half _Reflection_Fresnel;
                half _Reflection_Distortion;
                half _Refraction_Distance_Strength;
                half _Refraction_Distance_Fade;
                half _Refraction_Strength;
                half _ENABLEWAVE;
                half4 _Wave_Top_Color;
                half _1st_Wave_Length;
                half _1st_Wave_Height;
                half _1st_Wave_Speed;
                half _1st_Wave_Sharpness;
                half _2nd_Wave_Length;
                half _2nd_Wave_Height;
                half _2nd_Wave_Speed;
                half _2nd_Wave_Sharpness;
                half4 _1st_Wave_Direction;
                half4 _2nd_Wave_Direction;
            CBUFFER_END

            #define NWRP_MATERIAL_RECEIVE_SHADOWS _ReceiveShadows
            #include "../../ShaderLibrary/Lighting.hlsl"
            #undef NWRP_MATERIAL_RECEIVE_SHADOWS

            TEXTURE2D(_SL_Dissolve_Mask);
            SAMPLER(sampler_SL_Dissolve_Mask);
            TEXTURE2D(_Normal_Map);
            SAMPLER(sampler_Normal_Map);
            TEXTURECUBE(_Cubemap);
            SAMPLER(sampler_Cubemap);

            #include "Includes/LakeFunctions.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half waveHeight : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half waveEnable = step(0.5h, _ENABLEWAVE);

                float3 firstDisplacement;
                float3 secondDisplacement;
                half3 firstWaveNormal;
                half3 secondWaveNormal;
                GerstnerWave(
                    _1st_Wave_Length,
                    _1st_Wave_Sharpness,
                    _1st_Wave_Height,
                    _1st_Wave_Speed,
                    _1st_Wave_Direction.xyz,
                    positionWS,
                    firstDisplacement,
                    firstWaveNormal);
                GerstnerWave(
                    _2nd_Wave_Length,
                    _2nd_Wave_Sharpness,
                    _2nd_Wave_Height,
                    _2nd_Wave_Speed,
                    _2nd_Wave_Direction.xyz,
                    positionWS,
                    secondDisplacement,
                    secondWaveNormal);

                float3 waveDisplacement = (firstDisplacement + secondDisplacement) * waveEnable;
                positionWS += waveDisplacement;

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half3 waveNormalWS = normalize(firstWaveNormal + secondWaveNormal);
                output.normalWS = normalize(lerp((half3)normalInput.normalWS, waveNormalWS, waveEnable));
                output.tangentWS = (half3)normalInput.tangentWS;
                output.bitangentWS = (half3)normalInput.bitangentWS;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                output.waveHeight = (half)waveDisplacement.y;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 screenUV = saturate(GetNormalizedScreenSpaceUV(input.positionCS));
                float rawDepth = SampleSceneDepth(screenUV);
                if (!IsSceneDepthValid(rawDepth))
                {
                    return 0.0h;
                }

                half distanceMask = DistanceFromCamera(
                    input.positionWS,
                    _DistanceMask_Start,
                    _DistanceMask_Fade);

                half3 unscaledNormalTS;
                half3 normalTS;
                TwoWayNormalBlend(
                    input.positionWS,
                    distanceMask,
                    _Normal_Strength,
                    _Normal_DistanceStrength,
                    _Normal_Pan,
                    _Normal_Scale,
                    unscaledNormalTS,
                    normalTS);

                half3x3 tangentToWorld = half3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS));
                half3 normalWS = normalize((half3)TransformTangentToWorldDir(normalTS, tangentToWorld));
                half3 viewDirWS = normalize((half3)GetWorldSpaceViewDir(input.positionWS));

                half waterDepth = DepthFadeWorldPosition(
                    _Water_Depth,
                    screenUV,
                    rawDepth,
                    input.positionCS,
                    input.positionWS);

                half4 color = lerp(_Color_Deep, _Color_Shallow, waterDepth);

                half gradationEnable = step(0.5h, _ENABLEGRADATION);
                half gradation = 1.0h - DepthFadeWorldPosition(
                    _GradationRange,
                    screenUV,
                    rawDepth,
                    input.positionCS,
                    input.positionWS);
                half3 gradColor = _Color0.rgb;
                gradColor = lerp(gradColor, _Color1.rgb, smoothstep(0.0h, 0.25h, gradation));
                gradColor = lerp(gradColor, _Color2.rgb, smoothstep(0.25h, 0.5h, gradation));
                gradColor = lerp(gradColor, _Color3.rgb, smoothstep(0.5h, 0.75h, gradation));
                gradColor = lerp(gradColor, _Color4.rgb, smoothstep(0.75h, 1.0h, gradation));
                color.rgb = lerp(color.rgb, gradColor, gradationEnable);

                half shorelineMask = ShoreLineGenerator(
                    _SL_WaterDepth,
                    _SL_Speed,
                    _SL_Ammount,
                    _SL_Thickness,
                    _SL_CenterMask,
                    _SL_CenterMaskFade,
                    _SL_Trail_Fade,
                    _SL_GradientDissolve,
                    _SL_Dissolve,
                    _SL_MaskPan.xy,
                    _SL_MaskTile.xy,
                    _SL_MaskScale,
                    _SL_EnableTrail,
                    screenUV,
                    rawDepth,
                    input.positionCS,
                    input.positionWS) * step(0.5h, _ENABLESHORELINE);
                color.rgb = ColorLayerAlpha(color, _SL_Color, shorelineMask).rgb;
                color.a = LayerAlpha(_SL_Color, color.a, shorelineMask);

                half waveMask = saturate(input.waveHeight * 10.0h) * step(0.5h, _ENABLEWAVE);
                color.rgb = ColorLayerAlpha(color, _Wave_Top_Color, waveMask).rgb;

                half shoreFade = lerp(
                    1.0h,
                    SmoothMask(_ShoreFade - 1.0h, _ShoreFade_Smoothness, saturate(1.0h - waterDepth)),
                    saturate(_ShoreFade));

                half3 refractedColor;
                float2 refractedUV;
                WaterRefractionScreenSpace(
                    unscaledNormalTS.xy,
                    _Refraction_Distance_Strength,
                    _Refraction_Distance_Fade,
                    shoreFade,
                    _Refraction_Strength,
                    input.positionCS,
                    screenUV,
                    input.positionWS,
                    refractedColor,
                    refractedUV);

                color.rgb = lerp(refractedColor, color.rgb, color.a);
                color.a = shoreFade;

                half fresnelPower = max(_Reflection_Fresnel, 1.0e-3h);
                half reflectionFresnel = pow(
                    1.0h - saturate(dot(normalize(input.normalWS), viewDirWS)),
                    fresnelPower) * _Reflection_Strength;
                half3 reflectionNormal = normalize(input.normalWS + normalWS * _Reflection_Distortion);
                half3 reflectionDir = reflect(-viewDirWS, reflectionNormal);
                half3 reflectionColor = SAMPLE_TEXTURECUBE(_Cubemap, sampler_Cubemap, reflectionDir).rgb;
                color.rgb = lerp(color.rgb, reflectionColor, saturate(reflectionFresnel));

                Light mainLight = GetMainLight(input.positionWS, normalWS);
                color.rgb += StylizedSpecular(
                    _Specular_Size,
                    _Specular_Hardness,
                    _Specular_Spread,
                    _Specular_Color.rgb,
                    normalWS,
                    viewDirWS,
                    mainLight);

                half shadowMask = saturate((1.0h - mainLight.shadowAttenuation) * _ShadowColor.a);
                color.rgb = lerp(color.rgb, _ShadowColor.rgb, shadowMask);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
