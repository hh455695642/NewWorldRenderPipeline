using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    public enum NWRPAntiAliasingMode
    {
        None = 0,
        FXAA = 1
    }

    [Serializable]
    public sealed class NWRPAntiAliasingModeParameter : VolumeParameter<NWRPAntiAliasingMode>
    {
        public NWRPAntiAliasingModeParameter(
            NWRPAntiAliasingMode value = NWRPAntiAliasingMode.None,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Post-processing/Anti Aliasing", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPAntiAliasing : VolumeComponent
    {
        public const float DefaultFixedThreshold = 0.0833f;
        public const float DefaultRelativeThreshold = 0.166f;
        public const float DefaultSubpixelBlending = 0.75f;

        [Tooltip("Anti-aliasing mode selected by NWRP. FXAA runs in the final post-process composite pass.")]
        public NWRPAntiAliasingModeParameter mode =
            new NWRPAntiAliasingModeParameter(NWRPAntiAliasingMode.None);

        [Tooltip("Minimum local contrast required before FXAA smooths an edge.")]
        public ClampedFloatParameter fixedThreshold =
            new ClampedFloatParameter(DefaultFixedThreshold, 0.0312f, 0.333f);

        [Tooltip("Relative local contrast threshold. Higher values reject more low-contrast edges.")]
        public ClampedFloatParameter relativeThreshold =
            new ClampedFloatParameter(DefaultRelativeThreshold, 0.063f, 0.333f);

        [Tooltip("Subpixel aliasing reduction. Keep below 1 on mobile to avoid over-softening foliage and UI.")]
        public ClampedFloatParameter subpixelBlending =
            new ClampedFloatParameter(DefaultSubpixelBlending, 0f, 1f);

        public bool IsActive()
        {
            return active && mode.value == NWRPAntiAliasingMode.FXAA;
        }
    }
}
