using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    public enum NWRPFogMode
    {
        Off = 0,
        Linear = 1,
        Exp = 2,
        Exp2 = 3
    }

    [Serializable]
    public sealed class NWRPFogModeParameter
        : VolumeParameter<NWRPFogMode>
    {
        public NWRPFogModeParameter(
            NWRPFogMode value = NWRPFogMode.Linear,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Environment/Fog", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPFog : VolumeComponent
    {
        [Tooltip("Enable NWRP forward fog for cameras sampling this volume.")]
        public BoolParameter enableFog = new BoolParameter(false);

        [Tooltip("Fog equation. Linear is the mobile baseline.")]
        public NWRPFogModeParameter mode =
            new NWRPFogModeParameter(NWRPFogMode.Linear);

        [Tooltip("Fog color blended by Environment and Lit forward shaders.")]
        [ColorUsage(false, true)]
        public ColorParameter color =
            new ColorParameter(new Color(0.5f, 0.55f, 0.6f, 1.0f));

        [Tooltip("Linear fog start distance.")]
        [Min(0f)]
        public FloatParameter startDistance = new FloatParameter(20f);

        [Tooltip("Linear fog end distance.")]
        [Min(0.01f)]
        public FloatParameter endDistance = new FloatParameter(100f);

        [Tooltip("Exponential fog density.")]
        [Min(0f)]
        public FloatParameter density = new FloatParameter(0.01f);

        public bool IsActive()
        {
            return active && enableFog.value;
        }
    }
}
