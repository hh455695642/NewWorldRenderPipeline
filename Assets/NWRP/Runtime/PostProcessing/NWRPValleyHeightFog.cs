using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    public enum NWRPValleyHeightFogMode
    {
        SingleLayer,
        ThreeLayer
    }

    [Serializable]
    public sealed class NWRPValleyHeightFogModeParameter
        : VolumeParameter<NWRPValleyHeightFogMode>
    {
        public NWRPValleyHeightFogModeParameter(
            NWRPValleyHeightFogMode value,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Post-processing/Valley Height Fog", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPValleyHeightFog : VolumeComponent
    {
        [Tooltip("Enable valley height fog for cameras sampling this volume.")]
        public BoolParameter enable = new BoolParameter(false);

        [Tooltip("Select the height fog algorithm used by the fullscreen pass.")]
        public NWRPValleyHeightFogModeParameter mode =
            new NWRPValleyHeightFogModeParameter(NWRPValleyHeightFogMode.SingleLayer);

        [Tooltip("Height fog color blended over the transparent-resolved camera color.")]
        [ColorUsage(false, true)]
        public ColorParameter fogColor =
            new ColorParameter(new Color(0.8f, 0.9f, 1f, 1f));

        // Single-layer fog. Matches NewWorld/Env/ValleyHeightFog.
        [Tooltip("Base world height where valley fog starts thinning out.")]
        public FloatParameter fogBaseHeight = new FloatParameter(50f);

        [Tooltip("Vertical density transition. Higher values make low areas foggier faster.")]
        public ClampedFloatParameter heightDensity =
            new ClampedFloatParameter(0.3f, 0.01f, 2f);

        [Tooltip("Camera distance where height fog starts.")]
        [Min(0f)]
        public FloatParameter fogStart = new FloatParameter(250f);

        [Tooltip("Distance range before fog reaches full strength.")]
        [Min(0.001f)]
        public FloatParameter fogLength = new FloatParameter(100f);

        [Tooltip("World-space procedural noise scale.")]
        [Min(0f)]
        public FloatParameter noiseScale = new FloatParameter(0.005f);

        [Tooltip("Height offset amount applied by procedural noise.")]
        [Min(0f)]
        public FloatParameter noiseIntensity = new FloatParameter(20f);

        [Tooltip("World-space procedural noise animation speed.")]
        public FloatParameter noiseSpeed = new FloatParameter(0.1f);

        [Tooltip("Noise frequency multiplier per octave.")]
        public ClampedFloatParameter noiseRoughness =
            new ClampedFloatParameter(2f, 0.001f, 8f);

        [Tooltip("Noise amplitude multiplier per octave.")]
        public ClampedFloatParameter noisePersistance =
            new ClampedFloatParameter(0.5f, 0f, 1f);

        // Three-layer fog. Matches NewWorld/Env/ValleyHeightFog_3Layer.
        [Tooltip("Bottom fog layer height.")]
        public FloatParameter bottomHeight = new FloatParameter(10f);

        [Tooltip("Bottom fog layer height fade range.")]
        [Min(0f)]
        public FloatParameter bottomFade = new FloatParameter(6f);

        [Tooltip("Bottom fog layer density.")]
        public ClampedFloatParameter bottomDensity =
            new ClampedFloatParameter(0.012f, 0.001f, 0.05f);

        [Tooltip("Bottom fog layer intensity.")]
        [Min(0f)]
        public FloatParameter bottomIntensity = new FloatParameter(0.8f);

        [Tooltip("Bottom fog layer noise scale.")]
        public ClampedFloatParameter bottomNoiseScale =
            new ClampedFloatParameter(0.12f, 0f, 0.5f);

        [Tooltip("Bottom fog layer noise intensity.")]
        public ClampedFloatParameter bottomNoiseIntensity =
            new ClampedFloatParameter(1f, 0f, 3f);

        [Tooltip("Mid fog layer height.")]
        public FloatParameter midHeight = new FloatParameter(300f);

        [Tooltip("Mid fog layer height fade range.")]
        [Min(0f)]
        public FloatParameter midFade = new FloatParameter(60f);

        [Tooltip("Mid fog layer density.")]
        public ClampedFloatParameter midDensity =
            new ClampedFloatParameter(0.003f, 0.001f, 0.05f);

        [Tooltip("Mid fog layer intensity.")]
        [Min(0f)]
        public FloatParameter midIntensity = new FloatParameter(0.5f);

        [Tooltip("Mid fog layer noise scale.")]
        public ClampedFloatParameter midNoiseScale =
            new ClampedFloatParameter(0.003f, 0f, 0.02f);

        [Tooltip("Mid fog layer noise intensity.")]
        public ClampedFloatParameter midNoiseIntensity =
            new ClampedFloatParameter(1.1f, 0f, 2f);

        [Tooltip("Top fog layer intensity.")]
        public ClampedFloatParameter topIntensity =
            new ClampedFloatParameter(0f, 0f, 0.5f);

        [Tooltip("Top fog layer density.")]
        public ClampedFloatParameter topDensity =
            new ClampedFloatParameter(0.0005f, 0.0001f, 0.01f);

        [Tooltip("Top fog layer noise scale.")]
        public ClampedFloatParameter topNoiseScale =
            new ClampedFloatParameter(0.005f, 0f, 0.01f);

        [Tooltip("Top fog layer noise intensity.")]
        public ClampedFloatParameter topNoiseIntensity =
            new ClampedFloatParameter(1.5f, 0f, 2f);

        [Tooltip("Three-layer procedural noise animation speed.")]
        public FloatParameter threeLayerNoiseSpeed = new FloatParameter(0.15f);

        [Tooltip("Three-layer noise frequency multiplier per octave.")]
        public ClampedFloatParameter threeLayerNoiseRoughness =
            new ClampedFloatParameter(2f, 0.001f, 8f);

        [Tooltip("Three-layer noise amplitude multiplier per octave.")]
        public ClampedFloatParameter threeLayerNoisePersistance =
            new ClampedFloatParameter(0.35f, 0f, 1f);

        public bool IsActive()
        {
            return active && enable.value;
        }
    }
}
