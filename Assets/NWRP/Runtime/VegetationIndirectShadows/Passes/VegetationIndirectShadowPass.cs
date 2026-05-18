using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    public sealed class VegetationIndirectShadowPass : NWRPPass
    {
        private static readonly int s_IdVisibleBuffer =
            Shader.PropertyToID("_VisibleVegetationBuffer");
        private static readonly int s_IdAllGrass = Shader.PropertyToID("_AllGrass");
        private static readonly int s_IdVisibleGrass = Shader.PropertyToID("_VisibleGrass");
        private static readonly int s_IdGrassCount = Shader.PropertyToID("_GrassCount");
        private static readonly int s_IdCullMode = Shader.PropertyToID("_CullMode");
        private static readonly int s_IdFrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int s_IdCamPos = Shader.PropertyToID("_CamPos");
        private static readonly int s_IdCamForward = Shader.PropertyToID("_CamForward");
        private static readonly int s_IdCullDist = Shader.PropertyToID("_CullDist");
        private static readonly int s_IdCullDistSqr = Shader.PropertyToID("_CullDistSqr");
        private static readonly int s_IdCylinderMaxDistance =
            Shader.PropertyToID("_CylinderMaxDistance");
        private static readonly int s_IdCylinderNearRadius =
            Shader.PropertyToID("_CylinderNearRadius");
        private static readonly int s_IdCylinderFarRadius =
            Shader.PropertyToID("_CylinderFarRadius");

        private readonly List<VegetationIndirectShadowDraw> _draws =
            new List<VegetationIndirectShadowDraw>(64);
        private readonly Plane[] _shadowPlanes = new Plane[6];
        private readonly Vector4[] _shadowPlaneVectors = new Vector4[6];

        public VegetationIndirectShadowPass()
            : base(
                NWRPPassEvent.ShadowMap,
                "Render Vegetation Indirect Shadows",
                NWRPProfiling.MainLightShadow)
        {
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!CanRender(ref frameData))
                return;

            for (int targetIndex = 0;
                targetIndex < MainLightShadowIndirectCasterContext.TargetCount;
                targetIndex++)
            {
                MainLightShadowIndirectCasterContext.Target target =
                    MainLightShadowIndirectCasterContext.GetTarget(targetIndex);
                if (target == null || target.shadowmapTexture == null || target.cascadeCount <= 0)
                    continue;

                _draws.Clear();
                CollectDraws(target);
                if (_draws.Count == 0)
                    continue;

                DrawTarget(ref frameData, target);
            }

            frameData.context.SetupCameraProperties(frameData.camera);
        }

        private static bool CanRender(ref NWRPFrameData frameData)
        {
            return frameData.asset != null
                && frameData.asset.EnableMainLightShadows
                && frameData.asset.EnableVegetationIndirectTreeShadows
                && MainLightShadowIndirectCasterContext.IsValid
                && MainLightShadowIndirectCasterContext.TargetCount > 0;
        }

        private void CollectDraws(MainLightShadowIndirectCasterContext.Target target)
        {
            // Extension point: add new caster policies at provider level; keep this pass focused on
            // writing main-light shadow atlas targets, not vegetation-type branching.
            VegetationIndirectShadowRegistry.Compact();
            for (int i = 0; i < VegetationIndirectShadowRegistry.ProviderCount; i++)
            {
                IVegetationIndirectShadowProvider provider =
                    VegetationIndirectShadowRegistry.GetProvider(i);
                provider?.TryCollectIndirectShadowDraws(
                    target.includeStaticCasters,
                    target.includeDynamicCasters,
                    _draws);
            }
        }

        private void DrawTarget(
            ref NWRPFrameData frameData,
            MainLightShadowIndirectCasterContext.Target target)
        {
            CommandBuffer cmd = frameData.cmd;
            cmd.SetRenderTarget(
                target.shadowmapTexture,
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store);
            cmd.SetGlobalFloat(
                NWRPShaderIds.MainLightShadowCasterCull,
                (float)frameData.asset.MainLightShadowCasterCullModeSetting);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, target.shadowLightDirection);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
            cmd.SetGlobalDepthBias(1.0f, 2.5f);

            for (int cascadeIndex = 0; cascadeIndex < target.cascadeCount; cascadeIndex++)
            {
                MainLightShadowCascadeData cascadeData = target.cascadeData[cascadeIndex];
                if (cascadeData.resolution <= 0)
                    continue;

                PrepareCascadeCulling(cascadeData);
                cmd.SetViewport(new Rect(
                    cascadeData.offsetX,
                    cascadeData.offsetY,
                    cascadeData.resolution,
                    cascadeData.resolution));
                cmd.SetViewProjectionMatrices(
                    cascadeData.viewMatrix,
                    cascadeData.projectionMatrix);
                cmd.SetGlobalVector(
                    NWRPShaderIds.ShadowBias,
                    MainLightShadowPassUtils.CalculateShadowBias(
                        frameData.asset.MainLightShadowBias,
                        frameData.asset.MainLightShadowNormalBias,
                        cascadeData.projectionMatrix,
                        cascadeData.resolution));

                DrawCascade(ref frameData, cascadeData);
            }

            cmd.SetGlobalDepthBias(0f, 0f);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowBias, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightDirection, Vector4.zero);
            cmd.SetGlobalVector(NWRPShaderIds.ShadowLightPosition, Vector4.zero);
            cmd.SetGlobalFloat(NWRPShaderIds.MainLightShadowCasterCull, (float)CullMode.Back);
            ExecuteBuffer(ref frameData);
        }

        private void PrepareCascadeCulling(MainLightShadowCascadeData cascadeData)
        {
            Matrix4x4 worldToClip = cascadeData.projectionMatrix * cascadeData.viewMatrix;
            GeometryUtility.CalculateFrustumPlanes(worldToClip, _shadowPlanes);
            for (int i = 0; i < 6; i++)
            {
                Plane plane = _shadowPlanes[i];
                _shadowPlaneVectors[i] = new Vector4(
                    plane.normal.x,
                    plane.normal.y,
                    plane.normal.z,
                    plane.distance);
            }
        }

        private void DrawCascade(ref NWRPFrameData frameData, MainLightShadowCascadeData cascadeData)
        {
            Vector4 cullingSphere = cascadeData.cullingSphere;
            Vector3 sphereCenter = new Vector3(cullingSphere.x, cullingSphere.y, cullingSphere.z);
            float sphereRadius = Mathf.Max(cullingSphere.w, 0.001f);
            float sphereRadiusSqr = sphereRadius * sphereRadius;

            for (int i = 0; i < _draws.Count; i++)
            {
                VegetationIndirectShadowDraw draw = _draws[i];
                if (!CanDraw(draw, sphereCenter, sphereRadiusSqr))
                    continue;

                DispatchAndDraw(ref frameData, draw, sphereCenter, sphereRadius, sphereRadiusSqr);
            }
        }

        private bool CanDraw(
            VegetationIndirectShadowDraw draw,
            Vector3 sphereCenter,
            float sphereRadiusSqr)
        {
            if (draw.mesh == null
                || draw.material == null
                || draw.cullingShader == null
                || draw.allInstancesBuffer == null
                || draw.shadowVisibleBuffer == null
                || draw.shadowArgsBuffer == null
                || draw.materialProperties == null
                || draw.instanceCount <= 0
                || draw.shadowCasterPassIndex < 0)
            {
                return false;
            }

            Vector3 closestPoint = draw.bounds.ClosestPoint(sphereCenter);
            if ((closestPoint - sphereCenter).sqrMagnitude > sphereRadiusSqr)
                return false;

            return GeometryUtility.TestPlanesAABB(_shadowPlanes, draw.bounds);
        }

        private void DispatchAndDraw(
            ref NWRPFrameData frameData,
            VegetationIndirectShadowDraw draw,
            Vector3 sphereCenter,
            float sphereRadius,
            float sphereRadiusSqr)
        {
            CommandBuffer cmd = frameData.cmd;

            cmd.SetBufferCounterValue(draw.shadowVisibleBuffer, 0);
            cmd.SetComputeIntParam(draw.cullingShader, s_IdCullMode, 0);
            cmd.SetComputeVectorArrayParam(draw.cullingShader, s_IdFrustumPlanes, _shadowPlaneVectors);
            cmd.SetComputeVectorParam(
                draw.cullingShader,
                s_IdCamPos,
                new Vector4(sphereCenter.x, sphereCenter.y, sphereCenter.z, 0f));
            cmd.SetComputeVectorParam(draw.cullingShader, s_IdCamForward, new Vector4(0f, 0f, 1f, 0f));
            cmd.SetComputeFloatParam(draw.cullingShader, s_IdCullDist, sphereRadius);
            cmd.SetComputeFloatParam(draw.cullingShader, s_IdCullDistSqr, sphereRadiusSqr);
            cmd.SetComputeFloatParam(draw.cullingShader, s_IdCylinderMaxDistance, 0f);
            cmd.SetComputeFloatParam(draw.cullingShader, s_IdCylinderNearRadius, 0f);
            cmd.SetComputeFloatParam(draw.cullingShader, s_IdCylinderFarRadius, 0f);
            cmd.SetComputeBufferParam(
                draw.cullingShader,
                draw.cullingKernelIndex,
                s_IdAllGrass,
                draw.allInstancesBuffer);
            cmd.SetComputeBufferParam(
                draw.cullingShader,
                draw.cullingKernelIndex,
                s_IdVisibleGrass,
                draw.shadowVisibleBuffer);
            cmd.SetComputeIntParam(draw.cullingShader, s_IdGrassCount, draw.instanceCount);

            int threadGroups = Mathf.Max(1, Mathf.CeilToInt(draw.instanceCount / 64.0f));
            cmd.DispatchCompute(draw.cullingShader, draw.cullingKernelIndex, threadGroups, 1, 1);
            cmd.CopyCounterValue(draw.shadowVisibleBuffer, draw.shadowArgsBuffer, sizeof(uint));
            draw.materialProperties.SetBuffer(s_IdVisibleBuffer, draw.shadowVisibleBuffer);
            cmd.DrawMeshInstancedIndirect(
                draw.mesh,
                0,
                draw.material,
                draw.shadowCasterPassIndex,
                draw.shadowArgsBuffer,
                0,
                draw.materialProperties);
        }

        private static void ExecuteBuffer(ref NWRPFrameData frameData)
        {
            frameData.context.ExecuteCommandBuffer(frameData.cmd);
            frameData.cmd.Clear();
        }
    }
}
