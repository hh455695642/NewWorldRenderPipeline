#ifndef NWRP_BLIT_CORE_COMPAT_INCLUDED
#define NWRP_BLIT_CORE_COMPAT_INCLUDED

// Minimal Core Blitter compatibility layer.
// Keep URP-style texture helper names for shader migration, but do not include URP packages.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

#if defined(STEREO_MULTIVIEW_ON) \
    && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN)) \
    && !defined(SHADER_API_SWITCH)
    #define UNITY_STEREO_MULTIVIEW_ENABLED
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#if defined(UNITY_STEREO_MULTIVIEW_ENABLED) && defined(SHADER_STAGE_VERTEX)
    #ifndef UNITY_DECLARE_MULTIVIEW
        #define UNITY_DECLARE_MULTIVIEW(number_of_views) CBUFFER_START(OVR_multiview) uint gl_ViewID; uint numViews_##number_of_views; CBUFFER_END
        #define UNITY_VIEWID gl_ViewID
    #endif
#endif

#if defined(UNITY_STEREO_MULTIVIEW_ENABLED) && defined(SHADER_STAGE_VERTEX)
    #define unity_StereoEyeIndex UNITY_VIEWID
    UNITY_DECLARE_MULTIVIEW(2);
#elif defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    static uint unity_StereoEyeIndex;
#elif defined(UNITY_SINGLE_PASS_STEREO)
    CBUFFER_START(UnityStereoEyeIndex)
        int unity_StereoEyeIndex;
    CBUFFER_END
#endif

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    #define SLICE_ARRAY_INDEX unity_StereoEyeIndex

    #define TEXTURE2D_X(textureName)                                      TEXTURE2D_ARRAY(textureName)
    #define TEXTURE2D_X_PARAM(textureName, samplerName)                   TEXTURE2D_ARRAY_PARAM(textureName, samplerName)
    #define TEXTURE2D_X_ARGS(textureName, samplerName)                    TEXTURE2D_ARRAY_ARGS(textureName, samplerName)
    #define TEXTURE2D_X_HALF(textureName)                                 TEXTURE2D_ARRAY_HALF(textureName)
    #define TEXTURE2D_X_FLOAT(textureName)                                TEXTURE2D_ARRAY_FLOAT(textureName)

    #define LOAD_TEXTURE2D_X(textureName, unCoord2)                       LOAD_TEXTURE2D_ARRAY(textureName, unCoord2, SLICE_ARRAY_INDEX)
    #define LOAD_TEXTURE2D_X_LOD(textureName, unCoord2, lod)              LOAD_TEXTURE2D_ARRAY_LOD(textureName, unCoord2, SLICE_ARRAY_INDEX, lod)
    #define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2)          SAMPLE_TEXTURE2D_ARRAY(textureName, samplerName, coord2, SLICE_ARRAY_INDEX)
    #define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod) SAMPLE_TEXTURE2D_ARRAY_LOD(textureName, samplerName, coord2, SLICE_ARRAY_INDEX, lod)
    #define GATHER_TEXTURE2D_X(textureName, samplerName, coord2)          GATHER_TEXTURE2D_ARRAY(textureName, samplerName, coord2, SLICE_ARRAY_INDEX)
    #define GATHER_RED_TEXTURE2D_X(textureName, samplerName, coord2)      GATHER_RED_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))
    #define GATHER_GREEN_TEXTURE2D_X(textureName, samplerName, coord2)    GATHER_GREEN_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))
    #define GATHER_BLUE_TEXTURE2D_X(textureName, samplerName, coord2)     GATHER_BLUE_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))
#else
    #define SLICE_ARRAY_INDEX 0

    #define TEXTURE2D_X(textureName)                                      TEXTURE2D(textureName)
    #define TEXTURE2D_X_PARAM(textureName, samplerName)                   TEXTURE2D_PARAM(textureName, samplerName)
    #define TEXTURE2D_X_ARGS(textureName, samplerName)                    TEXTURE2D_ARGS(textureName, samplerName)
    #define TEXTURE2D_X_HALF(textureName)                                 TEXTURE2D_HALF(textureName)
    #define TEXTURE2D_X_FLOAT(textureName)                                TEXTURE2D_FLOAT(textureName)

    #define LOAD_TEXTURE2D_X(textureName, unCoord2)                       LOAD_TEXTURE2D(textureName, unCoord2)
    #define LOAD_TEXTURE2D_X_LOD(textureName, unCoord2, lod)              LOAD_TEXTURE2D_LOD(textureName, unCoord2, lod)
    #define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2)          SAMPLE_TEXTURE2D(textureName, samplerName, coord2)
    #define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod) SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, lod)
    #define GATHER_TEXTURE2D_X(textureName, samplerName, coord2)          GATHER_TEXTURE2D(textureName, samplerName, coord2)
    #define GATHER_RED_TEXTURE2D_X(textureName, samplerName, coord2)      GATHER_RED_TEXTURE2D(textureName, samplerName, coord2)
    #define GATHER_GREEN_TEXTURE2D_X(textureName, samplerName, coord2)    GATHER_GREEN_TEXTURE2D(textureName, samplerName, coord2)
    #define GATHER_BLUE_TEXTURE2D_X(textureName, samplerName, coord2)     GATHER_BLUE_TEXTURE2D(textureName, samplerName, coord2)
#endif

#endif // NWRP_BLIT_CORE_COMPAT_INCLUDED
