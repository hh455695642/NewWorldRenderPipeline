Shader "Shader Graphs/Fx_Base"
{
    Properties
    {
        [HideInInspector] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        [Enum(blend,10,add,1)] _Float1 ("Blend Mode", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Float2 ("Cull Mode", Float) = 0
        [Toggle] _Float4 ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _Ztestmode ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _Float60 ("Color Mask", Float) = 15

        _Float16 ("Soft Particle Distance", Float) = 0
        [Toggle] _Float5 ("Invert Soft Particle Edge", Float) = 0
        _Float28 ("Soft Edge Intensity", Float) = 1
        _Float30 ("Soft Edge Power", Float) = 1

        [HDR] _Color0 ("Color", Color) = (1,1,1,1)
        _Float14 ("Color Intensity", Float) = 1
        _Float15 ("Alpha Intensity", Range(0,10)) = 1

        [NoScaleOffset] _maintex ("Main Texture", 2D) = "white" {}
        [Enum(A,0,R,1)] _Keyword0 ("Main Channel", Float) = 0
        [Enum(Repeat,0,Clamp,1)] _Keyword8 ("Main UV Wrap", Float) = 0
        _Float39 ("Main Rotation", Range(-1,1)) = 0
        _Vector8 ("Main Tiling Offset", Vector) = (1,1,0,0)
        _Vector0 ("Main Scroll Speed", Vector) = (0,0,0,0)
        [Toggle] _Float10 ("Custom1.xy Controls Main Offset", Float) = 0

        [NoScaleOffset] _Mask ("Mask 01", 2D) = "white" {}
        [Enum(A,0,R,1)] _Keyword4 ("Mask 01 Channel", Float) = 1
        [Enum(Repeat,0,Clamp,1)] _Keyword10 ("Mask 01 UV Wrap", Float) = 0
        _Float43 ("Mask 01 Rotation", Range(-1,1)) = 0
        _Vector10 ("Mask 01 Tiling Offset", Vector) = (1,1,0,0)
        _Vector11 ("Mask 01 Scroll Speed", Vector) = (0,0,0,0)

        [Toggle] _Use_mask2 ("Use Mask 02", Float) = 0
        [NoScaleOffset] _Mask1 ("Mask 02", 2D) = "white" {}
        [Enum(A,0,R,1)] _Keyword6 ("Mask 02 Channel", Float) = 1
        [Enum(Repeat,0,Clamp,1)] _Keyword11 ("Mask 02 UV Wrap", Float) = 0
        _Float42 ("Mask 02 Rotation", Range(-1,1)) = 0
        _Vector12 ("Mask 02 Tiling Offset", Vector) = (1,1,0,0)
        _Vector13 ("Mask 02 Scroll Speed", Vector) = (0,0,0,0)
        [Toggle] _Float12 ("Custom1.zw Controls Mask 01 Offset", Float) = 0

        [NoScaleOffset] _dissolvetex ("Dissolve Texture", 2D) = "white" {}
        [Enum(Repeat,0,Clamp,1)] _Keyword12 ("Dissolve UV Wrap", Float) = 0
        _Float41 ("Dissolve Rotation", Range(-1,1)) = 0
        [Toggle] _Float53 ("Dissolve Polar UV", Float) = 0
        _Vector14 ("Dissolve Tiling Offset", Vector) = (1,1,0,0)
        _Vector15 ("Dissolve Scroll Speed", Vector) = (0,0,0,0)
        _Float6 ("Dissolve", Range(0,1)) = 0
        _Float8 ("Dissolve Softness", Range(0.5,1)) = 0.5
        [Toggle] _Float25 ("Use Dissolve Edge", Float) = 0
        _Float17 ("Dissolve Edge Width", Range(0,0.1)) = 0
        [HDR] _Color1 ("Dissolve Edge Color", Color) = (1,1,1,1)
        [Toggle] _Float11 ("Custom2.x Controls Dissolve", Float) = 0

        [NoScaleOffset] _noise ("Noise Texture", 2D) = "white" {}
        [Enum(Repeat,0,Clamp,1)] _Keyword14 ("Noise UV Wrap", Float) = 0
        _Float44 ("Noise Rotation", Range(-1,1)) = 0
        [NoScaleOffset] _TextureSample1 ("Noise Mask", 2D) = "white" {}
        _Float57 ("Noise Mask Rotation", Range(-1,1)) = 0
        [Toggle] _Float54 ("Noise Polar UV", Float) = 0
        _Vector16 ("Noise Tiling Offset", Vector) = (1,1,0,0)
        _Vector17 ("Noise Scroll Speed", Vector) = (0,0,0,0)
        [Enum(off,0,on,1)] doublenoise ("Double Noise", Float) = 0
        _Float9 ("Main Distortion Intensity", Float) = 0
        _Float18 ("Mask Distortion Intensity", Float) = 0
        _Float33 ("Dissolve Distortion Intensity", Float) = 0

        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
        [HideInInspector][ToggleOff] _ReceiveShadows ("Receive Shadows", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "NewWorldRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull [_Float2]
        AlphaToMask Off

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            Blend SrcAlpha [_Float1], One OneMinusSrcAlpha
            ZWrite [_Float4]
            ZTest [_Ztestmode]
            ColorMask [_Float60]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include "../../../NWRP/ShaderLibrary/Core.hlsl"
            #include "../../../NWRP/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../../../NWRP/ShaderLibrary/ParticlesInstancing.hlsl"

            TEXTURE2D(_maintex);
            SAMPLER(sampler_maintex);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURE2D(_Mask1);
            SAMPLER(sampler_Mask1);
            TEXTURE2D(_dissolvetex);
            SAMPLER(sampler_dissolvetex);
            TEXTURE2D(_noise);
            SAMPLER(sampler_noise);
            TEXTURE2D(_TextureSample1);
            SAMPLER(sampler_TextureSample1);

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float _AlphaCutoff;
                float _Float1;
                float _Float2;
                float _Float4;
                float _Ztestmode;
                float _Float60;
                float _Float16;
                float _Float5;
                float _Float28;
                float _Float30;
                half4 _Color0;
                float _Float14;
                float _Float15;
                float4 _maintex_ST;
                float _Keyword0;
                float _Keyword8;
                float _Float39;
                float4 _Vector8;
                float4 _Vector0;
                float _Float10;
                float4 _Mask_ST;
                float _Keyword4;
                float _Keyword10;
                float _Float43;
                float4 _Vector10;
                float4 _Vector11;
                float _Use_mask2;
                float4 _Mask1_ST;
                float _Keyword6;
                float _Keyword11;
                float _Float42;
                float4 _Vector12;
                float4 _Vector13;
                float _Float12;
                float4 _dissolvetex_ST;
                float _Keyword12;
                float _Float41;
                float _Float53;
                float4 _Vector14;
                float4 _Vector15;
                float _Float6;
                float _Float8;
                float _Float25;
                float _Float17;
                half4 _Color1;
                float _Float11;
                float4 _noise_ST;
                float _Keyword14;
                float _Float44;
                float4 _TextureSample1_ST;
                float _Float57;
                float _Float54;
                float4 _Vector16;
                float4 _Vector17;
                float doublenoise;
                float _Float9;
                float _Float18;
                float _Float33;
                float _QueueOffset;
                float _QueueControl;
                float _ReceiveShadows;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 custom1 : TEXCOORD1;
                float4 custom2 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 custom1 : TEXCOORD1;
                float4 custom2 : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 RotateUV(float2 uv, float rotation)
            {
                float angle = rotation * 6.28318530718;
                float s = sin(angle);
                float c = cos(angle);
                float2 centered = uv - 0.5;
                return float2(centered.x * c - centered.y * s, centered.x * s + centered.y * c) + 0.5;
            }

            float2 PolarUV(float2 uv)
            {
                float2 delta = uv - 0.5;
                float radius = length(delta) * 2.0;
                float angle = atan2(delta.x, delta.y) * (1.0 / 6.28318530718);
                return float2(radius, angle);
            }

            float2 ApplyWrap(float2 uv, float wrapMode)
            {
                return (wrapMode > 0.5) ? saturate(uv) : frac(uv);
            }

            half SelectChannel(half4 value, float channel)
            {
                return (channel > 0.5) ? value.r : value.a;
            }

            float2 BuildUV(
                float2 baseUV,
                float4 tilingOffset,
                float4 scrollSpeed,
                float wrapMode,
                float rotation,
                float usePolar,
                float2 customOffset,
                float useCustomOffset,
                float noiseOffset)
            {
                float2 uv = (usePolar > 0.5) ? PolarUV(baseUV) : baseUV;
                uv = uv * tilingOffset.xy + tilingOffset.zw + scrollSpeed.xy * _Time.y;
                uv += customOffset * step(0.5, useCustomOffset);
                uv += noiseOffset;
                uv = RotateUV(uv, rotation);
                return ApplyWrap(uv, wrapMode);
            }

            half SampleNoise(float2 baseUV)
            {
                float needsNoise = abs(_Float9) + abs(_Float18) + abs(_Float33);
                if (needsNoise <= 0.0001)
                {
                    return 0.0h;
                }

                float2 noiseUV = BuildUV(baseUV, _Vector16, _Vector17, _Keyword14, _Float44, _Float54, float2(0.0, 0.0), 0.0, 0.0);
                float2 noiseMaskUV = RotateUV(baseUV, _Float57);
                half noiseA = SAMPLE_TEXTURE2D(_noise, sampler_noise, noiseUV).r;
                half noiseB = SAMPLE_TEXTURE2D(_TextureSample1, sampler_TextureSample1, noiseMaskUV).r;
                return (doublenoise > 0.5) ? saturate(noiseA + noiseB) : noiseA * noiseB;
            }

            half ComputeSoftParticle(float4 screenPos, out half edge)
            {
                edge = 0.0h;

                if (_Float16 <= 0.0001)
                {
                    return 1.0h;
                }

                float2 screenUV = screenPos.xy / screenPos.w;
                float sceneZ = SampleSceneDepthLinearEye(screenUV);
                float particleZ = LinearEyeDepth(screenPos.z / screenPos.w);
                half distanceFade = half(saturate(abs(sceneZ - particleZ) / max(_Float16, 0.0001)));
                half invert = half(step(0.5, _Float5));
                edge = invert * (1.0h - distanceFade);
                return lerp(distanceFade, 1.0h, invert);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = GetNWRPParticleVertexColor(input.color);

                float3 unusedBlend;
                GetNWRPParticleUVs(output.uv, unusedBlend, input.texcoord.xyxy, 0.0);

                output.custom1 = input.custom1;
                output.custom2 = input.custom2;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half noise = SampleNoise(input.uv);
                half centeredNoise = noise - 0.5h;

                float2 mainUV = BuildUV(
                    input.uv,
                    _Vector8,
                    _Vector0,
                    _Keyword8,
                    _Float39,
                    0.0,
                    input.custom1.xy,
                    _Float10,
                    centeredNoise * _Float9);

                half4 mainTex = SAMPLE_TEXTURE2D(_maintex, sampler_maintex, mainUV);
                half mainChannel = SelectChannel(mainTex, _Keyword0);

                float2 maskUV = BuildUV(
                    input.uv,
                    _Vector10,
                    _Vector11,
                    _Keyword10,
                    _Float43,
                    0.0,
                    input.custom1.zw,
                    _Float12,
                    centeredNoise * _Float18);
                half4 maskTex = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, maskUV);
                half maskA = SelectChannel(maskTex, _Keyword4);

                half maskB = 1.0h;
                if (_Use_mask2 > 0.5)
                {
                    float2 mask2UV = BuildUV(input.uv, _Vector12, _Vector13, _Keyword11, _Float42, 0.0, float2(0.0, 0.0), 0.0, centeredNoise * _Float18);
                    half4 mask2Tex = SAMPLE_TEXTURE2D(_Mask1, sampler_Mask1, mask2UV);
                    maskB = SelectChannel(mask2Tex, _Keyword6);
                }

                float2 dissolveUV = BuildUV(
                    input.uv,
                    _Vector14,
                    _Vector15,
                    _Keyword12,
                    _Float41,
                    _Float53,
                    float2(0.0, 0.0),
                    0.0,
                    centeredNoise * _Float33);
                half dissolveTex = SAMPLE_TEXTURE2D(_dissolvetex, sampler_dissolvetex, dissolveUV).r;
                half dissolveControl = lerp(half(_Float6), half(input.custom2.x), half(step(0.5, _Float11)));
                half dissolveWidth = max(1.0h - half(_Float8), 0.001h);
                half dissolveMask = saturate((dissolveTex - dissolveControl) / dissolveWidth + 1.0h);
                half edgeMask = half(step(0.5, _Float25)) * saturate(1.0h - abs(dissolveTex - dissolveControl) / max(half(_Float17), 0.0001h));

                half softEdge;
                half softAlpha = ComputeSoftParticle(input.screenPos, softEdge);

                half3 rgb = mainTex.rgb * input.color.rgb * _Color0.rgb * half(_Float14);
                rgb *= mainChannel;
                half softPower = half(max(_Float30, 0.0001));
                rgb += half(_Float28) * pow(saturate(softEdge), softPower);
                rgb = lerp(rgb, _Color1.rgb, edgeMask);

                half alpha = mainChannel
                    * input.color.a
                    * _Color0.a
                    * half(_Float15)
                    * maskA
                    * maskB
                    * dissolveMask
                    * softAlpha;

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
