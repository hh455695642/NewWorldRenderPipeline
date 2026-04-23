using NWRP.Runtime.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal static class AdditionalLightShadowPassUtils
    {
        private static readonly Matrix4x4[] s_DisabledWorldToShadowMatrices =
            CreateDisabledWorldToShadowMatrices();
        private static readonly Vector4[] s_DisabledShadowParams =
            new Vector4[AdditionalLightUtils.MaxAdditionalLights];
        private static readonly Vector4[] s_DisabledAtlasRects =
            new Vector4[AdditionalLightUtils.MaxAdditionalShadowSlices];

        internal static readonly ProfilingSampler RenderRealtimeShadowAtlasSampler =
            new ProfilingSampler("Render Additional Spot / Point Light Realtime Atlas");

        public static void UploadDisabledGlobals(ref NWRPFrameData frameData)
        {
            CommandBuffer cmd = frameData.cmd;
            cmd.SetGlobalTexture(
                NWRPShaderIds.AdditionalLightsShadowmapTexture,
                Texture2D.blackTexture);
            cmd.SetGlobalMatrixArray(
                NWRPShaderIds.AdditionalLightsWorldToShadow,
                s_DisabledWorldToShadowMatrices);
            cmd.SetGlobalVectorArray(
                NWRPShaderIds.AdditionalLightsShadowParams,
                s_DisabledShadowParams);
            cmd.SetGlobalVectorArray(
                NWRPShaderIds.AdditionalLightsShadowAtlasRects,
                s_DisabledAtlasRects);
            cmd.SetGlobalVector(NWRPShaderIds.AdditionalLightsShadowAtlasSize, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.AdditionalLightsShadowGlobalParams, Vector4.zero);
            MainLightShadowPassUtils.ExecuteBuffer(ref frameData);
        }

        public static void UploadReceiverGlobals(
            ref NWRPFrameData frameData,
            Texture shadowmapTexture,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] lightShadowParams,
            Vector4[] sliceAtlasRects,
            int atlasWidth,
            int atlasHeight)
        {
            CommandBuffer cmd = frameData.cmd;
            float safeMaxDistance = Mathf.Max(frameData.asset.AdditionalLightShadowDistance, 0.001f);
            float fadeRange = Mathf.Max(safeMaxDistance * 0.1f, 0.001f);
            float invFadeRange = 1f / fadeRange;

            cmd.SetGlobalTexture(NWRPShaderIds.AdditionalLightsShadowmapTexture, shadowmapTexture);
            cmd.SetGlobalMatrixArray(NWRPShaderIds.AdditionalLightsWorldToShadow, worldToShadowMatrices);
            cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsShadowParams, lightShadowParams);
            cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsShadowAtlasRects, sliceAtlasRects);
            cmd.SetGlobalVector(
                NWRPShaderIds.AdditionalLightsShadowAtlasSize,
                new Vector4(
                    1f / Mathf.Max(atlasWidth, 1),
                    1f / Mathf.Max(atlasHeight, 1),
                    atlasWidth,
                    atlasHeight));
            cmd.SetGlobalVector(
                NWRPShaderIds.AdditionalLightsShadowGlobalParams,
                new Vector4(1f, safeMaxDistance, invFadeRange, 0f));
            MainLightShadowPassUtils.ExecuteBuffer(ref frameData);
        }

        private static Matrix4x4[] CreateDisabledWorldToShadowMatrices()
        {
            Matrix4x4[] matrices = new Matrix4x4[AdditionalLightUtils.MaxAdditionalShadowSlices];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.identity;
            }

            return matrices;
        }
    }
}
