using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    public enum NWRPScreenBlurInjectionPoint
    {
        AfterPostProcess = 0,
        AfterFogOverlay = 1
    }

    [Serializable]
    public sealed class NWRPScreenBlurInjectionPointParameter
        : VolumeParameter<NWRPScreenBlurInjectionPoint>
    {
        public NWRPScreenBlurInjectionPointParameter(
            NWRPScreenBlurInjectionPoint value =
                NWRPScreenBlurInjectionPoint.AfterFogOverlay,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("NWRP/Post-processing/Screen Blur")]
    [SupportedOnRenderPipeline(typeof(NewWorldRenderPipelineAsset))]
    public sealed class NWRPScreenBlur : VolumeComponent
    {
        public const float MaxRadius = 8f;
        public const int MaxIterations = 4;

        [Tooltip("Fullscreen blur sample radius. Costs 2 full-screen blits per iteration.")]
        public ClampedFloatParameter radius =
            new ClampedFloatParameter(0f, 0f, MaxRadius);

        [Tooltip("Separable blur iteration count. Keep at 1-2 on mobile unless profiling allows more.")]
        public ClampedIntParameter iterations =
            new ClampedIntParameter(1, 0, MaxIterations);

        [Tooltip("Pass timing for the blur feature.")]
        public NWRPScreenBlurInjectionPointParameter injectionPoint =
            new NWRPScreenBlurInjectionPointParameter(
                NWRPScreenBlurInjectionPoint.AfterFogOverlay);

        public bool IsActive()
        {
            return active
                && radius.value > 0f
                && iterations.value > 0;
        }

        public NWRPPassEvent GetPassEvent()
        {
            return injectionPoint.value == NWRPScreenBlurInjectionPoint.AfterPostProcess
                ? NWRPPassEvent.AfterPostProcess
                : NWRPPassEvent.BeforePostProcess;
        }
    }
}
