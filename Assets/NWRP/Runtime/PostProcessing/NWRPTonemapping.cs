using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    public enum NWRPTonemappingMode
    {
        None = 0,
        Linear = 1,
        ACES = 2,
        ACESFitted = 3,
        AGX = 4
    }

    [Serializable]
    public sealed class NWRPTonemappingModeParameter : VolumeParameter<NWRPTonemappingMode>
    {
        public NWRPTonemappingModeParameter(
            NWRPTonemappingMode value = NWRPTonemappingMode.None,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Post-processing/Tonemapping", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPTonemapping : VolumeComponent
    {
        [Tooltip("Tonemapping operator selected by NWRP. None disables the Tonemapping pass.")]
        public NWRPTonemappingModeParameter mode =
            new NWRPTonemappingModeParameter(NWRPTonemappingMode.None);

        [Tooltip("Multiplier applied before the tonemapping curve.")]
        [Min(0f)]
        public FloatParameter preExposure = new FloatParameter(1f);

        [Tooltip("Multiplier applied after the tonemapping curve.")]
        [Min(0f)]
        public FloatParameter postBrightness = new FloatParameter(1f);

        [Tooltip("Clamp HDR input before tonemapping to keep outlier values stable.")]
        [Min(0f)]
        public FloatParameter maxInputBrightness = new FloatParameter(1000f);

        [Tooltip("Extra gamma exponent used by the AGX operator.")]
        public ClampedFloatParameter agxGamma = new ClampedFloatParameter(2.5f, 0f, 5f);

        public bool IsActive()
        {
            return active && mode.value != NWRPTonemappingMode.None;
        }
    }
}
