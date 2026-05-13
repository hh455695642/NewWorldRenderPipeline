using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Post-processing/Color Adjustments", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPColorAdjustments : VolumeComponent
    {
        [Tooltip("Beautify-compatible saturation boost. 0 is neutral; 1 matches Beautify's default boost.")]
        public ClampedFloatParameter saturate = new ClampedFloatParameter(1f, -2f, 3f);

        [Tooltip("Final color brightness multiplier.")]
        public ClampedFloatParameter brightness = new ClampedFloatParameter(1f, 0f, 2f);

        [Tooltip("Final color contrast around 0.5.")]
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0.5f, 1.5f);

        [Tooltip("Luma-preserving color separation boost for color vision deficiency compensation.")]
        public ClampedFloatParameter daltonize = new ClampedFloatParameter(0f, 0f, 2f);

        [Tooltip("Sepia blend amount.")]
        public ClampedFloatParameter sepia = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Multiplies the final color by this tint. Alpha controls blend amount.")]
        public ColorParameter tintColor = new ColorParameter(new Color(1f, 1f, 1f, 0f));

        [Tooltip("White balance blend amount.")]
        public ClampedFloatParameter colorTempBlend = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("White balance color temperature in Kelvin.")]
        public ClampedFloatParameter colorTemp = new ClampedFloatParameter(6550f, 1000f, 40000f);

        public bool IsActive()
        {
            return active
                && (IsNonNeutral(saturate, 0f)
                    || IsNonNeutral(brightness, 1f)
                    || IsNonNeutral(contrast, 1f)
                    || IsPositive(daltonize)
                    || IsPositive(sepia)
                    || IsTintActive()
                    || IsPositive(colorTempBlend));
        }

        private static bool IsPositive(FloatParameter parameter)
        {
            return parameter.overrideState && parameter.value > 0.0001f;
        }

        private static bool IsNonNeutral(FloatParameter parameter, float neutralValue)
        {
            return parameter.overrideState
                && Mathf.Abs(parameter.value - neutralValue) > 0.0001f;
        }

        private bool IsTintActive()
        {
            return tintColor.overrideState && tintColor.value.a > 0.0001f;
        }
    }
}
