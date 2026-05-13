using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    public enum NWRPVignetteFitMode
    {
        FitToWidth = 0,
        FitToHeight = 1
    }

    [Serializable]
    public sealed class NWRPVignetteFitModeParameter : VolumeParameter<NWRPVignetteFitMode>
    {
        public NWRPVignetteFitModeParameter(
            NWRPVignetteFitMode value = NWRPVignetteFitMode.FitToWidth,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Post-processing/Vignette", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPVignette : VolumeComponent
    {
        [Tooltip("Outer vignette radius control. 0 is neutral; higher values darken closer to screen center.")]
        public ClampedFloatParameter outerRing = new ClampedFloatParameter(0f, -2f, 1f);

        [Tooltip("Inner clear radius control. 0 is neutral; higher values preserve a smaller clear center.")]
        public ClampedFloatParameter innerRing = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Uniformly expands the vignette fade into the clear center.")]
        public ClampedFloatParameter fade = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Vignette center in normalized screen UV.")]
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

        [Tooltip("Vignette color. Alpha controls overall opacity.")]
        public ColorParameter color = new ColorParameter(new Color(0f, 0f, 0f, 1f));

        [Tooltip("Keep the vignette circular across non-square aspect ratios.")]
        public BoolParameter circularShape = new BoolParameter(false);

        [Tooltip("Circular vignette aspect fit mode.")]
        public NWRPVignetteFitModeParameter fitMode =
            new NWRPVignetteFitModeParameter(NWRPVignetteFitMode.FitToWidth);

        [Tooltip("Non-circular vignette vertical aspect scale.")]
        public ClampedFloatParameter aspectRatio = new ClampedFloatParameter(1f, 0f, 1f);

        public bool IsActive()
        {
            return active
                && (IsPositive(outerRing)
                    || IsPositive(innerRing)
                    || IsPositive(fade));
        }

        private static bool IsPositive(FloatParameter parameter)
        {
            return parameter.overrideState && parameter.value > 0.0001f;
        }
    }
}
