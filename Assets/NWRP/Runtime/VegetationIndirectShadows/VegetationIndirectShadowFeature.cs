using NWRP.Runtime.Passes;
using UnityEngine;

namespace NWRP
{
    [NWRPFeatureMetadata(
        "Vegetation Indirect Shadows",
        MenuPath = "Vegetation/Indirect Shadows",
        ShowInAddMenu = false,
        SortOrder = 80)]
    public sealed class VegetationIndirectShadowFeature : NWRPFeature, INWRPSerializedFeatureStateProvider
    {
        private VegetationIndirectShadowPass _shadowPass;

        bool INWRPSerializedFeatureStateProvider.DeferSerializedPasses => true;

        protected override void Create()
        {
            _shadowPass = new VegetationIndirectShadowPass();
        }

        public override void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData)
        {
            if (renderer == null
                || _shadowPass == null
                || frameData.asset == null
                || !frameData.asset.EnableMainLightShadows
                || !AllowsIndirectTreeShadows(ref frameData))
            {
                return;
            }

            renderer.EnqueuePass(_shadowPass);
        }

        void INWRPSerializedFeatureStateProvider.RecordSerializedFeatureState(
            ref NWRPSerializedFeatureState state)
        {
            state.hasVegetationIndirectShadow = true;
        }

        private static bool AllowsIndirectTreeShadows(ref NWRPFrameData frameData)
        {
            if (frameData.rendererData != null
                && frameData.rendererData.EnableVegetationIndirectTreeShadows)
            {
                return true;
            }

#if UNITY_EDITOR
            return frameData.camera != null
                && frameData.camera.cameraType == CameraType.SceneView;
#else
            return false;
#endif
        }
    }
}
