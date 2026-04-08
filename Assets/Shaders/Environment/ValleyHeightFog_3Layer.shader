Shader "NewWorld/Env/ValleyHeightFog_3Layer"
{
    Properties
    {
        // ----------------------------
        // Base
        // ----------------------------
        [Header(Fog Base)]
        _FogColor ("Fog Color", Color) = (0.8,0.9,1,1)
        //[Toggle(_NOISE_DISTANCE_FADE)]
        //_NoiseDistanceFade ("Noise Distance Fade", Float) = 0

        // ----------------------------
        // Bottom Layer
        // ----------------------------
        [Header(Bottom Fog Layer)]
        _BottomHeight ("Height", Float) = 10
        _BottomFade("Fade", Float) = 6
        _BottomDensity ("Density", Range(0.001,0.05)) = 0.012
        _BottomIntensity ("Intensity", Float) = 0.8

        _BottomNoiseScale ("Noise Scale", Range(0,0.5)) = 0.12
        _BottomNoiseIntensity ("Noise Intensity", Range(0,3)) = 1

        // ----------------------------
        // Mid Layer
        // ----------------------------
        [Header(Mid Fog Layer)]
        _MidHeight ("Height", Float) = 300
        _MidFade ("Fade", Float) = 60
        _MidDensity ("Density", Range(0.001,0.05)) = 0.003
        _MidIntensity ("Intensity", Float) = 0.5

        _MidNoiseScale ("Noise Scale", Range(0,0.02)) = 0.003
        _MidNoiseIntensity ("Noise Intensity", Range(0,2)) = 1.1

        // ----------------------------
        // Top Layer
        // ----------------------------
        [Header(Top Fog Layer)]
        _TopIntensity ("Intensity", Range(0,0.5)) = 0
        _TopDensity ("Density", Range(0.0001,0.01)) = 0.0005

        _TopNoiseScale ("Noise Scale", Range(0,0.01)) = 0.005
        _TopNoiseIntensity ("Noise Intensity", Range(0,2)) = 1.5

        // ----------------------------
        // Distance Fog
        // ----------------------------
        [Header(Distance Fog)]
        _FogStart ("Start Distance", Float) = 250
        _FogLength ("Fog Length", Float) = 100

        // ----------------------------
        // Global Noise Settings
        // ----------------------------
        [Header(Noise Settings)]
        _NoiseSpeed ("Noise Speed", Float) = 0.15
        _NoiseRoughness ("Noise Roughness", Float) = 2
        _NoisePersistance ("Noise Persistance", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            //#pragma multi_compile_local _ _NOISE_DISTANCE_FADE

//            TEXTURE2D_X(_CameraOpaqueTexture);
//            SAMPLER(sampler_CameraOpaqueTexture);
            //TEXTURE2D(_BlitTexture);
            // SAMPLER(sampler_BlitTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;

                float _BottomHeight;
                float _BottomFade;
                float _BottomDensity;
                float _BottomIntensity;
                float _BottomNoiseScale;
                float _BottomNoiseIntensity;

                float _MidHeight;
                float _MidFade;
                float _MidDensity;
                float _MidIntensity;
                float _MidNoiseScale;
                float _MidNoiseIntensity;

                float _TopIntensity;
                float _TopDensity;
                float _TopNoiseScale;
                float _TopNoiseIntensity;

                float _FogStart;
                float _FogLength;

                float _NoiseSpeed;
                float _NoiseRoughness;
                float _NoisePersistance;

            CBUFFER_END

            #define OCTAVES 4

            // -------------------------
            // Noise Functions
            // -------------------------

            float rand3dTo1d(float3 value, float3 dotDir = float3(12.9898, 78.233, 37.719))
            {
                float3 smallValue = cos(value);
                float random = dot(smallValue, dotDir);
                random = frac(sin(random) * 143758.5453);
                return random;
            }

            float3 rand3dTo3d(float3 value)
            {
                return float3(
                    rand3dTo1d(value, float3(12.989, 78.233, 37.719)),
                    rand3dTo1d(value, float3(39.346, 11.135, 83.155)),
                    rand3dTo1d(value, float3(73.156, 52.235, 9.151))
                );
            }

            float easeInOut(float t)
            {
                float ti = t * t;
                return ti / (ti + (1 - t) * (1 - t));
            }

            float perlinNoise(float3 value)
            {
                float3 fraction = frac(value);

                float interpX = easeInOut(fraction.x);
                float interpY = easeInOut(fraction.y);
                float interpZ = easeInOut(fraction.z);

                float cellZ[2];

                [unroll]
                for (int z = 0; z <= 1; z++)
                {
                    float cellY[2];

                    [unroll]
                    for (int y = 0; y <= 1; y++)
                    {
                        float cellX[2];

                        [unroll]
                        for (int x = 0; x <= 1; x++)
                        {
                            float3 cell = floor(value) + float3(x, y, z);
                            float3 dir = rand3dTo3d(cell) * 2 - 1;

                            cellX[x] = dot(dir, fraction - float3(x, y, z));
                        }

                        cellY[y] = lerp(cellX[0], cellX[1], interpX);
                    }

                    cellZ[z] = lerp(cellY[0], cellY[1], interpY);
                }

                return lerp(cellZ[0], cellZ[1], interpZ);
            }

            float sampleLayeredNoise(float3 pos, float roughness, float persistance)
            {
                float noise = 0;
                float frequency = 1;
                float amplitude = 1;

                [unroll]
                for (int i = 0; i < OCTAVES; i++)
                {
                    noise += perlinNoise(pos * frequency + i * 0.72354) * amplitude;
                    amplitude *= persistance;
                    frequency *= roughness;
                }

                return noise;
            }

            // -------------------------
            // Fragment
            // -------------------------

            half4 Frag(Varyings input) : SV_Target
            {
                //float4 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.texcoord);
                // ✅ 使用 Blit.hlsl 提供的 _BlitTexture，并补上 LOD 参数
                // 这里应该用 sampler_LinearClamp 而不是 sampler_LinearRepeat
                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                #if UNITY_REVERSED_Z
                float rawDepth = SampleSceneDepth(input.texcoord);
                #else
                float rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(input.texcoord));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, rawDepth,UNITY_MATRIX_I_VP);
                float height = worldPos.y;

                // --------------------
                // Skybox Fix (避免天空盒出现无穷远计算错误或地平线锐利白线)
                // --------------------
                float bottomFadeEffect = _BottomFade;
                float midFadeEffect = _MidFade;
                
                #if UNITY_REVERSED_Z
                bool isSkybox = rawDepth < 0.00001;
                #else
                bool isSkybox = rawDepth > 0.99999;
                #endif

                if (isSkybox)
                {
                    float3 safeWorldPos = ComputeWorldSpacePosition(input.texcoord, 0.5, UNITY_MATRIX_I_VP);
                    float3 viewDir = normalize(safeWorldPos - _WorldSpaceCameraPos);
                    worldPos = _WorldSpaceCameraPos + viewDir * (_FogStart + _FogLength * 2.0);
                    height = worldPos.y;
                    
                    // 柔化天空盒上的高度雾边缘，防止其看起来像一条线
                    bottomFadeEffect = max(_BottomFade, 150.0);
                    midFadeEffect = max(_MidFade, 150.0);
                }
                
                // --------------------
                // Fog Fade
                // --------------------
                //方法一
                //float depthFade = 1 - smoothstep(0.8, 0.99, rawDepth);
                //方法二
                // float depthFade = saturate((0.95 - rawDepth) * 5);
                // depthFade *= depthFade;
                

                // --------------------
                // Bottom Fog
                // --------------------

                float bottomFog = exp(-height * _BottomDensity);

                bottomFog *= 1 - smoothstep(_BottomHeight - bottomFadeEffect, _BottomHeight + bottomFadeEffect, height);


                float3 bottomNoisePos = worldPos * _BottomNoiseScale + _Time.y * _NoiseSpeed;

                float bottomNoise = isSkybox ? 0.0 : sampleLayeredNoise(bottomNoisePos, _NoiseRoughness, _NoisePersistance);

                
                //#ifdef _NOISE_DISTANCE_FADE
                
                //bottomFog *= 1 + bottomNoise * _BottomNoiseIntensity * depthFade;

                //#else

                bottomFog *= 1 + bottomNoise * _BottomNoiseIntensity;

                //#endif


                bottomFog *= _BottomIntensity;

                // --------------------
                // Mid Fog (独立Noise)
                // --------------------

                float midFog = exp(-height * _MidDensity);

                midFog *= 1 - smoothstep(_MidHeight - midFadeEffect, _MidHeight + midFadeEffect, height);

                float3 midNoisePos = worldPos * _MidNoiseScale + _Time.y * _NoiseSpeed;

                float midNoise = isSkybox ? 0.0 : sampleLayeredNoise(midNoisePos, _NoiseRoughness, _NoisePersistance);

                //#ifdef _NOISE_DISTANCE_FADE

                //midFog *= 1 + midNoise * _MidNoiseIntensity * depthFade;

                //#else

                midFog *= 1 + midNoise * _MidNoiseIntensity;

                //#endif

                midFog *= _MidIntensity;

                // --------------------
                // Top Fog
                // --------------------

                float topHeightRelative = max(height - _MidHeight, 0);

                float topFog = _TopIntensity;

                topFog *= exp(-topHeightRelative * _TopDensity);

                float3 topNoisePos = worldPos * _TopNoiseScale + _Time.y * _NoiseSpeed;

                float topNoise = isSkybox ? 0.0 : sampleLayeredNoise(topNoisePos, _NoiseRoughness, _NoisePersistance);

                topFog *= 1 + topNoise * _TopNoiseIntensity;

                // --------------------
                // Combine Layers
                // --------------------

                float heightFactor = saturate(max((bottomFog + midFog), topFog));

                // --------------------
                // Distance Fog
                // --------------------

                float dist = distance(_WorldSpaceCameraPos, worldPos);

                float distFactor = saturate((dist - _FogStart) / _FogLength);

                float fogFactor = heightFactor * distFactor;

                sceneColor.rgb = lerp(sceneColor.rgb, _FogColor.rgb, fogFactor * _FogColor.a);

                return sceneColor;
            }
            ENDHLSL
        }
    }
}