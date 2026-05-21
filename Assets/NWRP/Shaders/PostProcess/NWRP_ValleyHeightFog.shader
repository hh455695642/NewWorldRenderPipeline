Shader "Hidden/NWRP/PostProcess/ValleyHeightFog"
{
    HLSLINCLUDE

        #pragma target 3.0
        #pragma editor_sync_compilation

        #include "../../ShaderLibrary/NWRPBlitCoreCompat.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "../../ShaderLibrary/DepthWorldReconstructionBlit.hlsl"

        #define NWRP_VALLEY_FOG_OCTAVES 4

        float4 _NWRPValleyHeightFogColor;
        float4 _NWRPValleyHeightFogHeightParams;
        float4 _NWRPValleyHeightFogDistanceParams;
        float4 _NWRPValleyHeightFogNoiseParams;
        float4 _NWRPValleyHeightFogNoiseParams2;
        float4 _NWRPValleyHeightFogBottomParams;
        float4 _NWRPValleyHeightFogBottomNoiseParams;
        float4 _NWRPValleyHeightFogMidParams;
        float4 _NWRPValleyHeightFogMidNoiseParams;
        float4 _NWRPValleyHeightFogTopParams;
        float4 _NWRPValleyHeightFogThreeLayerNoiseParams;

        #define NWRP_VALLEY_FOG_BASE_HEIGHT _NWRPValleyHeightFogHeightParams.x
        #define NWRP_VALLEY_FOG_HEIGHT_DENSITY _NWRPValleyHeightFogHeightParams.y
        #define NWRP_VALLEY_FOG_START _NWRPValleyHeightFogDistanceParams.x
        #define NWRP_VALLEY_FOG_LENGTH _NWRPValleyHeightFogDistanceParams.y
        #define NWRP_VALLEY_FOG_INV_LENGTH _NWRPValleyHeightFogDistanceParams.z
        #define NWRP_VALLEY_FOG_NOISE_SCALE _NWRPValleyHeightFogNoiseParams.x
        #define NWRP_VALLEY_FOG_NOISE_INTENSITY _NWRPValleyHeightFogNoiseParams.y
        #define NWRP_VALLEY_FOG_NOISE_SPEED _NWRPValleyHeightFogNoiseParams.z
        #define NWRP_VALLEY_FOG_NOISE_ROUGHNESS _NWRPValleyHeightFogNoiseParams.w
        #define NWRP_VALLEY_FOG_NOISE_PERSISTANCE _NWRPValleyHeightFogNoiseParams2.x
        #define NWRP_VALLEY_FOG_BOTTOM_HEIGHT _NWRPValleyHeightFogBottomParams.x
        #define NWRP_VALLEY_FOG_BOTTOM_FADE _NWRPValleyHeightFogBottomParams.y
        #define NWRP_VALLEY_FOG_BOTTOM_DENSITY _NWRPValleyHeightFogBottomParams.z
        #define NWRP_VALLEY_FOG_BOTTOM_INTENSITY _NWRPValleyHeightFogBottomParams.w
        #define NWRP_VALLEY_FOG_BOTTOM_NOISE_SCALE _NWRPValleyHeightFogBottomNoiseParams.x
        #define NWRP_VALLEY_FOG_BOTTOM_NOISE_INTENSITY _NWRPValleyHeightFogBottomNoiseParams.y
        #define NWRP_VALLEY_FOG_MID_HEIGHT _NWRPValleyHeightFogMidParams.x
        #define NWRP_VALLEY_FOG_MID_FADE _NWRPValleyHeightFogMidParams.y
        #define NWRP_VALLEY_FOG_MID_DENSITY _NWRPValleyHeightFogMidParams.z
        #define NWRP_VALLEY_FOG_MID_INTENSITY _NWRPValleyHeightFogMidParams.w
        #define NWRP_VALLEY_FOG_MID_NOISE_SCALE _NWRPValleyHeightFogMidNoiseParams.x
        #define NWRP_VALLEY_FOG_MID_NOISE_INTENSITY _NWRPValleyHeightFogMidNoiseParams.y
        #define NWRP_VALLEY_FOG_TOP_INTENSITY _NWRPValleyHeightFogTopParams.x
        #define NWRP_VALLEY_FOG_TOP_DENSITY _NWRPValleyHeightFogTopParams.y
        #define NWRP_VALLEY_FOG_TOP_NOISE_SCALE _NWRPValleyHeightFogTopParams.z
        #define NWRP_VALLEY_FOG_TOP_NOISE_INTENSITY _NWRPValleyHeightFogTopParams.w
        #define NWRP_VALLEY_FOG_THREE_NOISE_SPEED _NWRPValleyHeightFogThreeLayerNoiseParams.x
        #define NWRP_VALLEY_FOG_THREE_NOISE_ROUGHNESS _NWRPValleyHeightFogThreeLayerNoiseParams.y
        #define NWRP_VALLEY_FOG_THREE_NOISE_PERSISTANCE _NWRPValleyHeightFogThreeLayerNoiseParams.z

        float NWRPRand3dTo1d(float3 value, float3 dotDir)
        {
            float3 smallValue = cos(value);
            float random = dot(smallValue, dotDir);
            return frac(sin(random) * 143758.5453);
        }

        float3 NWRPRand3dTo3d(float3 value)
        {
            return float3(
                NWRPRand3dTo1d(value, float3(12.989, 78.233, 37.719)),
                NWRPRand3dTo1d(value, float3(39.346, 11.135, 83.155)),
                NWRPRand3dTo1d(value, float3(73.156, 52.235, 9.151)));
        }

        float NWRPEaseInOut(float interpolator)
        {
            float easeInValue = interpolator * interpolator;
            float inv = 1.0 - interpolator;
            float easeOutValue = 1.0 - inv * inv;
            return lerp(easeInValue, easeOutValue, interpolator);
        }

        float NWRPPerlinNoise(float3 value)
        {
            float3 fraction = frac(value);

            float interpolatorX = NWRPEaseInOut(fraction.x);
            float interpolatorY = NWRPEaseInOut(fraction.y);
            float interpolatorZ = NWRPEaseInOut(fraction.z);

            float cellNoiseZ[2];

            [unroll]
            for (int z = 0; z <= 1; z++)
            {
                float cellNoiseY[2];

                [unroll]
                for (int y = 0; y <= 1; y++)
                {
                    float cellNoiseX[2];

                    [unroll]
                    for (int x = 0; x <= 1; x++)
                    {
                        float3 cell = floor(value) + float3(x, y, z);
                        float3 cellDirection = NWRPRand3dTo3d(cell) * 2.0 - 1.0;
                        float3 compareVector = fraction - float3(x, y, z);
                        cellNoiseX[x] = dot(cellDirection, compareVector);
                    }

                    cellNoiseY[y] = lerp(cellNoiseX[0], cellNoiseX[1], interpolatorX);
                }

                cellNoiseZ[z] = lerp(cellNoiseY[0], cellNoiseY[1], interpolatorY);
            }

            return lerp(cellNoiseZ[0], cellNoiseZ[1], interpolatorZ);
        }

        float NWRPSampleLayeredNoise(
            float3 value,
            float noiseRoughness,
            float noisePersistance)
        {
            float noise = 0.0;
            float frequency = 1.0;
            float factor = 1.0;

            [unroll]
            for (int i = 0; i < NWRP_VALLEY_FOG_OCTAVES; i++)
            {
                noise += NWRPPerlinNoise(value * frequency + i * 0.72354) * factor;
                factor *= noisePersistance;
                frequency *= noiseRoughness;
            }

            return noise;
        }

        float NWRPThreeLayerEaseInOut(float interpolator)
        {
            float easeInValue = interpolator * interpolator;
            float inv = 1.0 - interpolator;
            return easeInValue / (easeInValue + inv * inv);
        }

        float NWRPThreeLayerPerlinNoise(float3 value)
        {
            float3 fraction = frac(value);

            float interpolatorX = NWRPThreeLayerEaseInOut(fraction.x);
            float interpolatorY = NWRPThreeLayerEaseInOut(fraction.y);
            float interpolatorZ = NWRPThreeLayerEaseInOut(fraction.z);

            float cellNoiseZ[2];

            [unroll]
            for (int z = 0; z <= 1; z++)
            {
                float cellNoiseY[2];

                [unroll]
                for (int y = 0; y <= 1; y++)
                {
                    float cellNoiseX[2];

                    [unroll]
                    for (int x = 0; x <= 1; x++)
                    {
                        float3 cell = floor(value) + float3(x, y, z);
                        float3 cellDirection = NWRPRand3dTo3d(cell) * 2.0 - 1.0;
                        float3 compareVector = fraction - float3(x, y, z);
                        cellNoiseX[x] = dot(cellDirection, compareVector);
                    }

                    cellNoiseY[y] = lerp(cellNoiseX[0], cellNoiseX[1], interpolatorX);
                }

                cellNoiseZ[z] = lerp(cellNoiseY[0], cellNoiseY[1], interpolatorY);
            }

            return lerp(cellNoiseZ[0], cellNoiseZ[1], interpolatorZ);
        }

        float NWRPSampleThreeLayerLayeredNoise(
            float3 value,
            float noiseRoughness,
            float noisePersistance)
        {
            float noise = 0.0;
            float frequency = 1.0;
            float factor = 1.0;

            [unroll]
            for (int i = 0; i < NWRP_VALLEY_FOG_OCTAVES; i++)
            {
                noise += NWRPThreeLayerPerlinNoise(value * frequency + i * 0.72354) * factor;
                factor *= noisePersistance;
                frequency *= noiseRoughness;
            }

            return noise;
        }

        half4 SampleValleyFogSource(float2 uv)
        {
            return (half4)SAMPLE_TEXTURE2D_X_LOD(
                _BlitTexture,
                sampler_LinearClamp,
                uv,
                _BlitMipLevel);
        }

        half3 BlendValleyFogColor(half3 sceneColor, half fogFactor)
        {
            half fogAlpha = saturate((half)_NWRPValleyHeightFogColor.a);
            return lerp(
                sceneColor,
                (half3)_NWRPValleyHeightFogColor.rgb,
                fogFactor * fogAlpha);
        }

        half4 FragSingleLayer(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord.xy;
            half4 sceneColor = SampleValleyFogSource(uv);

            float rawDepth = SampleSceneDepth(uv);
            if (!IsSceneDepthValid(rawDepth))
            {
                return sceneColor;
            }

            float3 positionWS = ComputeSceneWorldSpacePosition(uv, rawDepth);
            float dynamicBaseHeight = NWRP_VALLEY_FOG_BASE_HEIGHT;

            UNITY_BRANCH
            if (NWRP_VALLEY_FOG_NOISE_SCALE > 0.0
                && NWRP_VALLEY_FOG_NOISE_INTENSITY > 0.0)
            {
                float3 noisePosition = positionWS * NWRP_VALLEY_FOG_NOISE_SCALE;
                noisePosition += _Time.y * NWRP_VALLEY_FOG_NOISE_SPEED;

                float noise = NWRPSampleLayeredNoise(
                    noisePosition,
                    NWRP_VALLEY_FOG_NOISE_ROUGHNESS,
                    NWRP_VALLEY_FOG_NOISE_PERSISTANCE) * 0.5 + 0.5;
                dynamicBaseHeight += (noise - 0.5) * NWRP_VALLEY_FOG_NOISE_INTENSITY;
            }

            float heightDiff = dynamicBaseHeight - positionWS.y;
            half heightFactor = saturate((half)exp(
                heightDiff * NWRP_VALLEY_FOG_HEIGHT_DENSITY));

            float distanceToCamera = distance(_WorldSpaceCameraPos, positionWS);
            half distanceFactor = saturate(
                (half)((distanceToCamera - NWRP_VALLEY_FOG_START)
                * NWRP_VALLEY_FOG_INV_LENGTH));

            half fogFactor = heightFactor * distanceFactor;
            sceneColor.rgb = BlendValleyFogColor(sceneColor.rgb, fogFactor);
            return sceneColor;
        }

        float NWRPSampleThreeLayerNoise(
            float3 positionWS,
            float noiseScale,
            float noiseIntensity,
            bool isSkybox)
        {
            float noise = 0.0;

            UNITY_BRANCH
            if (!isSkybox && noiseScale > 0.0 && noiseIntensity > 0.0)
            {
                float3 noisePosition = positionWS * noiseScale;
                noisePosition += _Time.y * NWRP_VALLEY_FOG_THREE_NOISE_SPEED;
                noise = NWRPSampleThreeLayerLayeredNoise(
                    noisePosition,
                    NWRP_VALLEY_FOG_THREE_NOISE_ROUGHNESS,
                    NWRP_VALLEY_FOG_THREE_NOISE_PERSISTANCE);
            }

            return noise;
        }

        half4 FragThreeLayer(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord.xy;
            half4 sceneColor = SampleValleyFogSource(uv);

            float rawDepth = SampleSceneDepth(uv);
            bool isSkybox = !IsSceneDepthValid(rawDepth);
            float3 positionWS;

            float bottomFade = NWRP_VALLEY_FOG_BOTTOM_FADE;
            float midFade = NWRP_VALLEY_FOG_MID_FADE;

            if (isSkybox)
            {
                float3 safeWorldPos = ComputeWorldSpacePosition(
                    uv,
                    0.5,
                    UNITY_MATRIX_I_VP);
                float3 viewDir = normalize(safeWorldPos - _WorldSpaceCameraPos);
                positionWS = _WorldSpaceCameraPos
                    + viewDir * (NWRP_VALLEY_FOG_START + NWRP_VALLEY_FOG_LENGTH * 2.0);

                bottomFade = max(bottomFade, 150.0);
                midFade = max(midFade, 150.0);
            }
            else
            {
                positionWS = ComputeSceneWorldSpacePosition(uv, rawDepth);
            }

            float height = positionWS.y;

            float bottomFog = exp(-height * NWRP_VALLEY_FOG_BOTTOM_DENSITY);
            bottomFog *= 1.0 - smoothstep(
                NWRP_VALLEY_FOG_BOTTOM_HEIGHT - bottomFade,
                NWRP_VALLEY_FOG_BOTTOM_HEIGHT + bottomFade,
                height);
            float bottomNoise = NWRPSampleThreeLayerNoise(
                positionWS,
                NWRP_VALLEY_FOG_BOTTOM_NOISE_SCALE,
                NWRP_VALLEY_FOG_BOTTOM_NOISE_INTENSITY,
                isSkybox);
            bottomFog *= 1.0 + bottomNoise * NWRP_VALLEY_FOG_BOTTOM_NOISE_INTENSITY;
            bottomFog *= NWRP_VALLEY_FOG_BOTTOM_INTENSITY;

            float midFog = exp(-height * NWRP_VALLEY_FOG_MID_DENSITY);
            midFog *= 1.0 - smoothstep(
                NWRP_VALLEY_FOG_MID_HEIGHT - midFade,
                NWRP_VALLEY_FOG_MID_HEIGHT + midFade,
                height);
            float midNoise = NWRPSampleThreeLayerNoise(
                positionWS,
                NWRP_VALLEY_FOG_MID_NOISE_SCALE,
                NWRP_VALLEY_FOG_MID_NOISE_INTENSITY,
                isSkybox);
            midFog *= 1.0 + midNoise * NWRP_VALLEY_FOG_MID_NOISE_INTENSITY;
            midFog *= NWRP_VALLEY_FOG_MID_INTENSITY;

            float topHeightRelative = max(height - NWRP_VALLEY_FOG_MID_HEIGHT, 0.0);
            float topFog = NWRP_VALLEY_FOG_TOP_INTENSITY;
            topFog *= exp(-topHeightRelative * NWRP_VALLEY_FOG_TOP_DENSITY);
            float topNoise = NWRPSampleThreeLayerNoise(
                positionWS,
                NWRP_VALLEY_FOG_TOP_NOISE_SCALE,
                NWRP_VALLEY_FOG_TOP_NOISE_INTENSITY,
                isSkybox);
            topFog *= 1.0 + topNoise * NWRP_VALLEY_FOG_TOP_NOISE_INTENSITY;

            half heightFactor = saturate((half)max(bottomFog + midFog, topFog));
            float distanceToCamera = distance(_WorldSpaceCameraPos, positionWS);
            half distanceFactor = saturate(
                (half)((distanceToCamera - NWRP_VALLEY_FOG_START)
                * NWRP_VALLEY_FOG_INV_LENGTH));

            half fogFactor = heightFactor * distanceFactor;
            sceneColor.rgb = BlendValleyFogColor(sceneColor.rgb, fogFactor);
            return sceneColor;
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "NewWorldRenderPipeline" }

        Pass
        {
            Name "Valley Height Fog"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragSingleLayer
            ENDHLSL
        }

        Pass
        {
            Name "Valley Height Fog 3 Layer"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragThreeLayer
            ENDHLSL
        }
    }

    Fallback Off
}
