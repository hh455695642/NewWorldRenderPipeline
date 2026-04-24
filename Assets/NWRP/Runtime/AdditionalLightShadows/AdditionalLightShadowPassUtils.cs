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
            new Vector4[AdditionalLightUtils.MaxAdditionalLightShadowSlices];

        internal static readonly ProfilingSampler RenderRealtimeShadowAtlasSampler =
            new ProfilingSampler("Render Additional Punctual Light Realtime Atlas");

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
            Vector4[] shadowParams,
            Vector4[] atlasRects,
            int atlasWidth,
            int atlasHeight)
        {
            CommandBuffer cmd = frameData.cmd;
            float safeMaxDistance = Mathf.Max(frameData.asset.AdditionalLightShadowDistance, 0.001f);
            float fadeRange = Mathf.Max(safeMaxDistance * 0.1f, 0.001f);
            float invFadeRange = 1f / fadeRange;

            cmd.SetGlobalTexture(NWRPShaderIds.AdditionalLightsShadowmapTexture, shadowmapTexture);
            cmd.SetGlobalMatrixArray(NWRPShaderIds.AdditionalLightsWorldToShadow, worldToShadowMatrices);
            cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsShadowParams, shadowParams);
            cmd.SetGlobalVectorArray(NWRPShaderIds.AdditionalLightsShadowAtlasRects, atlasRects);
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
            Matrix4x4[] matrices = new Matrix4x4[AdditionalLightUtils.MaxAdditionalLightShadowSlices];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.identity;
            }

            return matrices;
        }

        public static float GetPointLightShadowFrustumFovBiasInDegrees(int shadowSliceResolution)
        {
            if (shadowSliceResolution <= 16)
            {
                return 43.0f;
            }

            if (shadowSliceResolution <= 32)
            {
                return 18.55f;
            }

            if (shadowSliceResolution <= 64)
            {
                return 8.63f;
            }

            if (shadowSliceResolution <= 128)
            {
                return 4.13f;
            }

            if (shadowSliceResolution <= 256)
            {
                return 2.03f;
            }

            if (shadowSliceResolution <= 512)
            {
                return 1.00f;
            }

            if (shadowSliceResolution <= 1024)
            {
                return 0.50f;
            }

            return 4.00f;
        }

        public static void FixupPointShadowViewMatrix(ref Matrix4x4 viewMatrix)
        {
            viewMatrix.m10 = -viewMatrix.m10;
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
        }

        public static Vector4 CalculatePointShadowBias(
            float depthBias,
            float lightRange,
            int shadowResolution)
        {
            float fovBias = GetPointLightShadowFrustumFovBiasInDegrees(shadowResolution);
            float cubeFaceAngle = 90f + fovBias;
            float frustumSize = Mathf.Tan(cubeFaceAngle * 0.5f * Mathf.Deg2Rad) * Mathf.Max(lightRange, 0f);
            float texelSize = frustumSize / Mathf.Max(shadowResolution, 1);
            return new Vector4(-depthBias * texelSize, 0f, 0f, 0f);
        }

        public static Vector4 GetPointLightFaceDirection(CubemapFace face)
        {
            return face switch
            {
                CubemapFace.PositiveX => new Vector4(1f, 0f, 0f, 0f),
                CubemapFace.NegativeX => new Vector4(-1f, 0f, 0f, 0f),
                CubemapFace.PositiveY => new Vector4(0f, 1f, 0f, 0f),
                CubemapFace.NegativeY => new Vector4(0f, -1f, 0f, 0f),
                CubemapFace.PositiveZ => new Vector4(0f, 0f, 1f, 0f),
                CubemapFace.NegativeZ => new Vector4(0f, 0f, -1f, 0f),
                _ => Vector4.zero
            };
        }
    }
}
