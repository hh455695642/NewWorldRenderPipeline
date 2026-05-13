using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Post-processing/Bloom", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPBloom : VolumeComponent
    {
        [Tooltip("Bloom contribution added before tonemapping.")]
        [Min(0f)]
        public FloatParameter intensity = new FloatParameter(0f);

        [Tooltip("HDR threshold used by the bright-pass extraction.")]
        [Min(0f)]
        public FloatParameter threshold = new FloatParameter(0.75f);

        [Tooltip("Preserves RGB ratios while applying the threshold knee.")]
        public BoolParameter conservativeThreshold = new BoolParameter(false);

        [Tooltip("Controls how strongly each upsample level blends with the previous level.")]
        public ClampedFloatParameter spread = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Clamp HDR input before bloom extraction to keep outlier values stable.")]
        [Min(0f)]
        public FloatParameter maxBrightness = new FloatParameter(1000f);

        [Tooltip("Alpha blends between original bloom color and luminance-tinted bloom color.")]
        [ColorUsage(false, true)]
        public ColorParameter tint = new ColorParameter(new Color(0.5f, 0.5f, 1f, 0f));

        [Tooltip("Uses a four-sample weighted bright-pass to reduce small highlight flicker.")]
        public BoolParameter antiflicker = new BoolParameter(false);

        [Tooltip("Base bloom pyramid resolution. Higher values increase bandwidth.")]
        public ClampedIntParameter resolution = new ClampedIntParameter(1, 1, 10);

        [Tooltip("Combines downsample and blur work for a cheaper, softer mobile path.")]
        public BoolParameter quickerBlur = new BoolParameter(false);

        [Tooltip("Enables per-pyramid-layer weights, boosts, and tints.")]
        public BoolParameter customize = new BoolParameter(false);

        public ClampedFloatParameter weight0 = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter boost0 = new ClampedFloatParameter(0f, 0f, 3f);
        [ColorUsage(false, true)] public ColorParameter tint0 = new ColorParameter(Color.white);

        public ClampedFloatParameter weight1 = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter boost1 = new ClampedFloatParameter(0f, 0f, 3f);
        [ColorUsage(false, true)] public ColorParameter tint1 = new ColorParameter(Color.white);

        public ClampedFloatParameter weight2 = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter boost2 = new ClampedFloatParameter(0f, 0f, 3f);
        [ColorUsage(false, true)] public ColorParameter tint2 = new ColorParameter(Color.white);

        public ClampedFloatParameter weight3 = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter boost3 = new ClampedFloatParameter(0f, 0f, 3f);
        [ColorUsage(false, true)] public ColorParameter tint3 = new ColorParameter(Color.white);

        public ClampedFloatParameter weight4 = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter boost4 = new ClampedFloatParameter(0f, 0f, 3f);
        [ColorUsage(false, true)] public ColorParameter tint4 = new ColorParameter(Color.white);

        public ClampedFloatParameter weight5 = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter boost5 = new ClampedFloatParameter(0f, 0f, 3f);
        [ColorUsage(false, true)] public ColorParameter tint5 = new ColorParameter(Color.white);

        [Tooltip("Lens dirt contribution driven by the bloom pyramid.")]
        [Min(0f)]
        public FloatParameter lensDirtIntensity = new FloatParameter(0f);

        [Tooltip("Brightness threshold for lens dirt contribution.")]
        public ClampedFloatParameter lensDirtThreshold = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Optional lens dirt texture. Falls back to Beautify's default Resources texture when available.")]
        public TextureParameter lensDirtTexture = new TextureParameter(null);

        [Tooltip("Bloom pyramid layer used as the lens dirt luminance source.")]
        public ClampedIntParameter lensDirtSpread = new ClampedIntParameter(3, 3, 5);

        public bool IsActive()
        {
            return active
                && (intensity.value > 0f || lensDirtIntensity.value > 0f);
        }
    }
}
