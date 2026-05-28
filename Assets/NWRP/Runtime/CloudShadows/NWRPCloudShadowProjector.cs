using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("NWRP/Environment/Cloud Shadow Projector", typeof(NewWorldRenderPipeline))]
    public sealed class NWRPCloudShadowProjector : VolumeComponent
    {
        [Tooltip("Enable fullscreen projected cloud shadows for cameras sampling this volume.")]
        public BoolParameter enable = new BoolParameter(false);

        [Header("Distortion")]
        public TextureParameter distortionTexture = new TextureParameter(null);
        public Vector2Parameter distortionTiling =
            new Vector2Parameter(new Vector2(0.01f, 0.01f));
        public Vector2Parameter distortionOffset = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter distortionScroll = new Vector2Parameter(Vector2.zero);
        public ClampedFloatParameter distortionStrength =
            new ClampedFloatParameter(0f, 0f, 0.25f);

        [Header("Primary Layer")]
        public BoolParameter primaryEnabled = new BoolParameter(true);
        public TextureParameter primaryTexture = new TextureParameter(null);
        public Vector3Parameter primaryCenter = new Vector3Parameter(Vector3.zero);
        public Vector3Parameter primaryRotation = new Vector3Parameter(Vector3.zero);
        public Vector3Parameter primarySize = new Vector3Parameter(new Vector3(200f, 100f, 200f));
        public Vector2Parameter primaryTiling = new Vector2Parameter(Vector2.one);
        public Vector2Parameter primaryOffset = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter primaryScroll = new Vector2Parameter(Vector2.zero);
        [Min(0f)]
        public FloatParameter primaryIntensity = new FloatParameter(0.35f);
        public ClampedFloatParameter primaryEdgeSoftness =
            new ClampedFloatParameter(0.02f, 0f, 0.5f);
        [ColorUsage(false, true)]
        public ColorParameter primaryShadowColor =
            new ColorParameter(new Color(0.55f, 0.6f, 0.68f, 1f));

        [Header("Secondary Layer")]
        public BoolParameter secondaryEnabled = new BoolParameter(false);
        public TextureParameter secondaryTexture = new TextureParameter(null);
        public Vector3Parameter secondaryCenter = new Vector3Parameter(Vector3.zero);
        public Vector3Parameter secondaryRotation = new Vector3Parameter(Vector3.zero);
        public Vector3Parameter secondarySize = new Vector3Parameter(new Vector3(260f, 120f, 260f));
        public Vector2Parameter secondaryTiling = new Vector2Parameter(Vector2.one);
        public Vector2Parameter secondaryOffset = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter secondaryScroll = new Vector2Parameter(Vector2.zero);
        [Min(0f)]
        public FloatParameter secondaryIntensity = new FloatParameter(0.2f);
        public ClampedFloatParameter secondaryEdgeSoftness =
            new ClampedFloatParameter(0.02f, 0f, 0.5f);
        [ColorUsage(false, true)]
        public ColorParameter secondaryShadowColor =
            new ColorParameter(new Color(0.6f, 0.64f, 0.7f, 1f));

        public bool IsActive()
        {
            return active
                && enable.value
                && (IsLayerActive(
                        primaryEnabled,
                        primaryTexture,
                        primaryIntensity)
                    || IsLayerActive(
                        secondaryEnabled,
                        secondaryTexture,
                        secondaryIntensity));
        }

        internal static bool IsLayerActive(
            BoolParameter enabled,
            TextureParameter texture,
            FloatParameter intensity)
        {
            return enabled != null
                && enabled.value
                && texture != null
                && texture.value != null
                && intensity != null
                && intensity.value > 0f;
        }
    }
}
