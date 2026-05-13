using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    /// <summary>
    /// Unified post-processing pass for NWRP.
    ///
    /// Bloom is executed as an internal pyramid before the final composite so the
    /// external frame graph stays Transparent -> NWRP PostProcess -> DebugOverlay/FinalBlit.
    /// </summary>
    public sealed class PostProcessPass : NWRPPass
    {
        private const string k_TonemappingShaderName = "Hidden/NWRP/PostProcess/Tonemapping";
        private const string k_BloomShaderName = "Hidden/NWRP/PostProcess/Bloom";
        private const int k_BloomLastMip = 5;
        private const int k_BloomMipCount = k_BloomLastMip + 1;

        private static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private readonly BloomMipData[] _bloomMips = new BloomMipData[k_BloomMipCount];
        private readonly int _bloomTempBlurId = Shader.PropertyToID("_NWRPBloomTempBlur");
        private readonly int _bloomComposeId = Shader.PropertyToID("_NWRPBloomCustomCompose");

        private Material _tonemappingMaterial;
        private Material _bloomMaterial;
        private Texture _defaultLensDirtTexture;

        private enum BloomPass
        {
            Luminance = 0,
            LuminanceAntiFlicker = 1,
            BlurHorizontal = 2,
            BlurVertical = 3,
            Resample = 4,
            ResampleAndCombine = 5,
            BloomCompose = 6
        }

        private struct BloomMipData
        {
            public int down;
            public int up;
            public int width;
            public int height;
        }

        private struct BloomResources
        {
            public bool allocated;
            public bool hasBloomTexture;
            public bool hasDirtSourceTexture;
            public bool usedCustomCompose;
            public int finalTexture;
            public int dirtSourceTexture;
        }

        public PostProcessPass()
            : base(NWRPPassEvent.PostProcess, "NWRP PostProcess")
        {
            for (int i = 0; i < _bloomMips.Length; i++)
            {
                _bloomMips[i].down = Shader.PropertyToID("_NWRPBloomMipDown" + i);
                _bloomMips[i].up = Shader.PropertyToID("_NWRPBloomMipUp" + i);
            }
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            if (!PostProcessFeature.HasAnyActivePostProcess(ref frameData)
                || frameData.targets.cameraColorHandle == null
                || frameData.camera == null)
            {
                return;
            }

            BloomResources bloomResources = default;
            try
            {
                if (PostProcessFeature.IsBloomActive(ref frameData))
                {
                    bloomResources = ExecuteBloom(ref frameData);
                }

                ExecuteFinalComposite(ref frameData, bloomResources);
            }
            finally
            {
                ReleaseBloomResources(frameData.cmd, bloomResources);
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_tonemappingMaterial);
            CoreUtils.Destroy(_bloomMaterial);
            _tonemappingMaterial = null;
            _bloomMaterial = null;
            _defaultLensDirtTexture = null;
        }

        private BloomResources ExecuteBloom(ref NWRPFrameData frameData)
        {
            BloomResources resources = default;
            NWRPBloom bloom = frameData.bloom;
            RTHandle source = frameData.targets.cameraColorHandle;
            RenderTexture sourceTexture = source != null ? source.rt : null;
            if (bloom == null || source == null || sourceTexture == null || !EnsureBloomMaterial())
            {
                return resources;
            }

            CommandBuffer cmd = frameData.cmd;
            int sourceWidth = Mathf.Max(sourceTexture.width, 1);
            int sourceHeight = Mathf.Max(sourceTexture.height, 1);
            float aspectRatio = (float)sourceHeight / sourceWidth;
            int baseSize = GetBloomBaseSize(sourceWidth, bloom.resolution.value);
            RenderTextureDescriptor bloomDescriptor = CreateBloomDescriptor(baseSize, aspectRatio);

            for (int i = 0; i < k_BloomMipCount; i++)
            {
                BloomMipData mip = _bloomMips[i];
                mip.width = Mathf.Max(1, bloomDescriptor.width);
                mip.height = Mathf.Max(1, bloomDescriptor.height);
                _bloomMips[i] = mip;

                bloomDescriptor.width = mip.width;
                bloomDescriptor.height = mip.height;
                cmd.GetTemporaryRT(mip.down, bloomDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(mip.up, bloomDescriptor, FilterMode.Bilinear);

                bloomDescriptor.width = Mathf.Max(1, bloomDescriptor.width / 2);
                bloomDescriptor.height = Mathf.Max(1, bloomDescriptor.height / 2);
            }

            resources.allocated = true;

            UploadBloomConstants(cmd, bloom);
            SetBloomTexelSize(cmd, sourceWidth, sourceHeight);
            BlitToTarget(
                cmd,
                source,
                _bloomMips[0].down,
                _bloomMips[0].width,
                _bloomMips[0].height,
                _bloomMaterial,
                bloom.antiflicker.value
                    ? (int)BloomPass.LuminanceAntiFlicker
                    : (int)BloomPass.Luminance);

            if (bloom.quickerBlur.value)
            {
                for (int i = 0; i < k_BloomLastMip; i++)
                {
                    BlurDownsampling(
                        cmd,
                        _bloomMips[i].down,
                        _bloomMips[i].width,
                        _bloomMips[i].height,
                        _bloomMips[i + 1].down,
                        _bloomMips[i + 1].width,
                        _bloomMips[i + 1].height);
                }
            }
            else
            {
                for (int i = 0; i < k_BloomLastMip; i++)
                {
                    SetBloomTexelSize(cmd, _bloomMips[i].width, _bloomMips[i].height);
                    BlitToTarget(
                        cmd,
                        _bloomMips[i].down,
                        _bloomMips[i + 1].down,
                        _bloomMips[i + 1].width,
                        _bloomMips[i + 1].height,
                        _bloomMaterial,
                        (int)BloomPass.Resample);
                    BlurInPlace(
                        cmd,
                        _bloomMips[i + 1].down,
                        _bloomMips[i + 1].width,
                        _bloomMips[i + 1].height,
                        1f);
                }
            }

            int bloomTexture = _bloomMips[k_BloomLastMip].down;
            for (int i = k_BloomLastMip; i > 0; i--)
            {
                cmd.SetGlobalTexture(
                    NWRPShaderIds.BloomCombineTexture,
                    _bloomMips[i - 1].down);
                SetBloomTexelSize(cmd, _bloomMips[i].width, _bloomMips[i].height);
                BlitToTarget(
                    cmd,
                    bloomTexture,
                    _bloomMips[i - 1].up,
                    _bloomMips[i - 1].width,
                    _bloomMips[i - 1].height,
                    _bloomMaterial,
                    (int)BloomPass.ResampleAndCombine);
                bloomTexture = _bloomMips[i - 1].up;
            }

            if (bloom.customize.value && bloom.intensity.value > 0f)
            {
                RenderTextureDescriptor composeDescriptor =
                    CreateBloomDescriptor(_bloomMips[0].width, aspectRatio);
                composeDescriptor.width = _bloomMips[0].width;
                composeDescriptor.height = _bloomMips[0].height;
                cmd.GetTemporaryRT(_bloomComposeId, composeDescriptor, FilterMode.Bilinear);
                resources.usedCustomCompose = true;

                cmd.SetGlobalTexture(NWRPShaderIds.BloomTexture, _bloomMips[0].up);
                cmd.SetGlobalTexture(NWRPShaderIds.BloomTexture1, _bloomMips[1].up);
                cmd.SetGlobalTexture(NWRPShaderIds.BloomTexture2, _bloomMips[2].up);
                cmd.SetGlobalTexture(NWRPShaderIds.BloomTexture3, _bloomMips[3].up);
                cmd.SetGlobalTexture(NWRPShaderIds.BloomTexture4, _bloomMips[4].up);

                SetBloomTexelSize(cmd, _bloomMips[k_BloomLastMip].width, _bloomMips[k_BloomLastMip].height);
                BlitToTarget(
                    cmd,
                    _bloomMips[k_BloomLastMip].down,
                    _bloomComposeId,
                    _bloomMips[0].width,
                    _bloomMips[0].height,
                    _bloomMaterial,
                    (int)BloomPass.BloomCompose);
                bloomTexture = _bloomComposeId;
            }

            resources.hasBloomTexture = true;
            resources.finalTexture = bloomTexture;
            resources.hasDirtSourceTexture = bloom.lensDirtIntensity.value > 0f;
            resources.dirtSourceTexture = GetLensDirtSourceTexture(bloom.lensDirtSpread.value);
            return resources;
        }

        private void ExecuteFinalComposite(
            ref NWRPFrameData frameData,
            BloomResources bloomResources)
        {
            if (!EnsureTonemappingMaterial())
            {
                return;
            }

            bool tonemappingActive = PostProcessFeature.IsTonemappingActive(ref frameData);
            int passIndex = tonemappingActive
                ? GetTonemappingPassIndex(frameData.tonemapping.mode.value)
                : 0;
            if (passIndex < 0)
            {
                return;
            }

            CommandBuffer cmd = frameData.cmd;
            RTHandle source = frameData.targets.cameraColorHandle;
            Rect cameraViewport = NWRPRenderer.GetCameraViewport(frameData.camera);
            RenderBufferLoadAction loadAction =
                NWRPRenderer.IsDefaultViewport(frameData.camera, cameraViewport)
                    ? RenderBufferLoadAction.DontCare
                    : RenderBufferLoadAction.Load;

            UploadTonemappingConstants(cmd, frameData.tonemapping, tonemappingActive, frameData.bloom);
            UploadFinalBloomConstants(cmd, frameData.bloom, bloomResources);
            UploadColorAdjustmentConstants(
                cmd,
                frameData.colorAdjustments,
                PostProcessFeature.IsColorAdjustmentsActive(ref frameData));
            UploadVignetteConstants(
                cmd,
                frameData.camera,
                frameData.vignette,
                PostProcessFeature.IsVignetteActive(ref frameData));

            CoreUtils.SetRenderTarget(
                cmd,
                frameData.targets.backBufferColor,
                loadAction,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            cmd.SetViewport(cameraViewport);

            Blitter.BlitTexture(
                cmd,
                source,
                NWRPRenderer.GetFinalBlitScaleBias(frameData.camera, source),
                _tonemappingMaterial,
                passIndex);

            frameData.targets.cameraColorPresented = true;
        }

        private void BlurInPlace(
            CommandBuffer cmd,
            int source,
            int width,
            int height,
            float blurScale)
        {
            RenderTextureDescriptor descriptor = CreateBloomDescriptor(width, (float)height / width);
            descriptor.width = width;
            descriptor.height = height;
            cmd.GetTemporaryRT(_bloomTempBlurId, descriptor, FilterMode.Bilinear);

            cmd.SetGlobalFloat(NWRPShaderIds.BloomBlurScale, blurScale);
            SetBloomTexelSize(cmd, width, height);
            BlitToTarget(
                cmd,
                source,
                _bloomTempBlurId,
                width,
                height,
                _bloomMaterial,
                (int)BloomPass.BlurHorizontal);

            SetBloomTexelSize(cmd, width, height);
            BlitToTarget(
                cmd,
                _bloomTempBlurId,
                source,
                width,
                height,
                _bloomMaterial,
                (int)BloomPass.BlurVertical);

            cmd.ReleaseTemporaryRT(_bloomTempBlurId);
        }

        private void BlurDownsampling(
            CommandBuffer cmd,
            int source,
            int sourceWidth,
            int sourceHeight,
            int destination,
            int destinationWidth,
            int destinationHeight)
        {
            RenderTextureDescriptor descriptor =
                CreateBloomDescriptor(destinationWidth, (float)destinationHeight / destinationWidth);
            descriptor.width = destinationWidth;
            descriptor.height = destinationHeight;
            cmd.GetTemporaryRT(_bloomTempBlurId, descriptor, FilterMode.Bilinear);

            cmd.SetGlobalFloat(NWRPShaderIds.BloomBlurScale, 4f);
            SetBloomTexelSize(cmd, sourceWidth, sourceHeight);
            BlitToTarget(
                cmd,
                source,
                _bloomTempBlurId,
                destinationWidth,
                destinationHeight,
                _bloomMaterial,
                (int)BloomPass.BlurHorizontal);

            cmd.SetGlobalFloat(NWRPShaderIds.BloomBlurScale, 1f);
            SetBloomTexelSize(cmd, destinationWidth, destinationHeight);
            BlitToTarget(
                cmd,
                _bloomTempBlurId,
                destination,
                destinationWidth,
                destinationHeight,
                _bloomMaterial,
                (int)BloomPass.BlurVertical);

            cmd.ReleaseTemporaryRT(_bloomTempBlurId);
        }

        private static void BlitToTarget(
            CommandBuffer cmd,
            RTHandle source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            Material material,
            int passIndex)
        {
            CoreUtils.SetRenderTarget(
                cmd,
                destination,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            cmd.SetViewport(new Rect(0f, 0f, width, height));
            Blitter.BlitTexture(cmd, source, s_FullScaleBias, material, passIndex);
        }

        private static void BlitToTarget(
            CommandBuffer cmd,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            Material material,
            int passIndex)
        {
            CoreUtils.SetRenderTarget(
                cmd,
                destination,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                ClearFlag.None,
                Color.clear);
            cmd.SetViewport(new Rect(0f, 0f, width, height));
            Blitter.BlitTexture(cmd, source, s_FullScaleBias, material, passIndex);
        }

        private static void UploadBloomConstants(CommandBuffer cmd, NWRPBloom bloom)
        {
            float threshold = Mathf.Max(0f, bloom.threshold.value);
            float maxBrightness = Mathf.Max(0f, bloom.maxBrightness.value);
            cmd.SetGlobalVector(
                NWRPShaderIds.BloomThresholdParams,
                new Vector4(
                    threshold,
                    bloom.conservativeThreshold.value ? 1f : 0f,
                    maxBrightness,
                    0f));
            cmd.SetGlobalColor(NWRPShaderIds.BloomTint, bloom.tint.value);
            cmd.SetGlobalFloat(
                NWRPShaderIds.BloomSpread,
                Mathf.Lerp(0.05f, 0.95f, bloom.spread.value));

            float weightSum = 0.00001f
                + bloom.weight0.value
                + bloom.weight1.value
                + bloom.weight2.value
                + bloom.weight3.value
                + bloom.weight4.value
                + bloom.weight5.value;
            cmd.SetGlobalVector(
                NWRPShaderIds.BloomWeights,
                new Vector4(
                    bloom.weight0.value / weightSum + bloom.boost0.value,
                    bloom.weight1.value / weightSum + bloom.boost1.value,
                    bloom.weight2.value / weightSum + bloom.boost2.value,
                    bloom.weight3.value / weightSum + bloom.boost3.value));
            cmd.SetGlobalVector(
                NWRPShaderIds.BloomWeights2,
                new Vector4(
                    bloom.weight4.value / weightSum + bloom.boost4.value,
                    bloom.weight5.value / weightSum + bloom.boost5.value,
                    0f,
                    weightSum));

            cmd.SetGlobalColor(NWRPShaderIds.BloomTint0, bloom.tint0.value);
            cmd.SetGlobalColor(NWRPShaderIds.BloomTint1, bloom.tint1.value);
            cmd.SetGlobalColor(NWRPShaderIds.BloomTint2, bloom.tint2.value);
            cmd.SetGlobalColor(NWRPShaderIds.BloomTint3, bloom.tint3.value);
            cmd.SetGlobalColor(NWRPShaderIds.BloomTint4, bloom.tint4.value);
            cmd.SetGlobalColor(NWRPShaderIds.BloomTint5, bloom.tint5.value);
        }

        private static void UploadTonemappingConstants(
            CommandBuffer cmd,
            NWRPTonemapping tonemapping,
            bool tonemappingActive,
            NWRPBloom bloom)
        {
            if (tonemappingActive && tonemapping != null)
            {
                cmd.SetGlobalVector(
                    NWRPShaderIds.TonemapParams,
                    new Vector4(
                        Mathf.Max(0f, tonemapping.preExposure.value),
                        Mathf.Max(0f, tonemapping.postBrightness.value),
                        Mathf.Max(0f, tonemapping.maxInputBrightness.value),
                        Mathf.Max(0f, tonemapping.agxGamma.value)));
                return;
            }

            cmd.SetGlobalVector(
                NWRPShaderIds.TonemapParams,
                new Vector4(
                    1f,
                    1f,
                    Mathf.Max(1000f, bloom != null ? bloom.maxBrightness.value : 1000f),
                    2.5f));
        }

        private void UploadFinalBloomConstants(
            CommandBuffer cmd,
            NWRPBloom bloom,
            BloomResources resources)
        {
            float bloomIntensity = 0f;
            float dirtIntensity = 0f;
            Texture dirtTexture = Texture2D.blackTexture;
            if (bloom != null && resources.hasBloomTexture)
            {
                bloomIntensity = Mathf.Max(0f, bloom.intensity.value);
                if (bloom.lensDirtIntensity.value > 0f && resources.hasDirtSourceTexture)
                {
                    dirtTexture = GetLensDirtTexture(bloom);
                    if (dirtTexture != null)
                    {
                        dirtIntensity = bloom.lensDirtIntensity.value * bloom.lensDirtIntensity.value;
                    }
                }
            }

            cmd.SetGlobalVector(
                NWRPShaderIds.BloomCompositeParams,
                new Vector4(
                    bloomIntensity,
                    dirtIntensity,
                    bloom != null ? bloom.lensDirtThreshold.value : 0f,
                    Mathf.Max(bloomIntensity, 1f)));
            if (resources.hasBloomTexture)
            {
                cmd.SetGlobalTexture(NWRPShaderIds.BloomTexture, resources.finalTexture);
            }
            else
            {
                cmd.SetGlobalTexture(NWRPShaderIds.BloomTexture, Texture2D.blackTexture);
            }

            if (resources.hasDirtSourceTexture)
            {
                cmd.SetGlobalTexture(NWRPShaderIds.BloomDirtSourceTexture, resources.dirtSourceTexture);
            }
            else
            {
                cmd.SetGlobalTexture(NWRPShaderIds.BloomDirtSourceTexture, Texture2D.blackTexture);
            }

            cmd.SetGlobalTexture(NWRPShaderIds.BloomDirtTexture, dirtTexture);
        }

        private static void UploadColorAdjustmentConstants(
            CommandBuffer cmd,
            NWRPColorAdjustments colorAdjustments,
            bool colorAdjustmentsActive)
        {
            float sepia = 0f;
            float daltonize = 0f;
            float saturate = 0f;
            float brightness = 1f;
            float contrast = 1f;
            float temperature = 6550f;
            float temperatureBlend = 0f;
            Color tintColor = new Color(1f, 1f, 1f, 0f);
            float active = 0f;

            if (colorAdjustmentsActive && colorAdjustments != null)
            {
                sepia = GetFloat(colorAdjustments.sepia, 0f);
                daltonize = GetFloat(colorAdjustments.daltonize, 0f);
                saturate = GetFloat(colorAdjustments.saturate, 0f);
                brightness = GetFloat(colorAdjustments.brightness, 1f);
                contrast = GetFloat(colorAdjustments.contrast, 1f);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    contrast = 1f + (contrast - 1f) / 2.2f;
                }

                temperature = GetFloat(colorAdjustments.colorTemp, 6550f);
                temperatureBlend = GetFloat(colorAdjustments.colorTempBlend, 0f);
                tintColor = GetColor(colorAdjustments.tintColor, tintColor);
                active = 1f;
            }

            cmd.SetGlobalVector(
                NWRPShaderIds.ColorAdjustParams,
                new Vector4(
                    Mathf.Clamp01(sepia),
                    Mathf.Clamp(daltonize, 0f, 2f),
                    Mathf.Clamp(saturate, -2f, 3f),
                    Mathf.Clamp(brightness, 0f, 2f)));
            cmd.SetGlobalVector(
                NWRPShaderIds.ColorAdjustParams2,
                new Vector4(
                    Mathf.Clamp(contrast, 0.5f, 1.5f),
                    Mathf.Clamp(temperature, 1000f, 40000f),
                    Mathf.Clamp01(temperatureBlend),
                    active));
            cmd.SetGlobalColor(NWRPShaderIds.ColorAdjustTint, tintColor);
        }

        private static void UploadVignetteConstants(
            CommandBuffer cmd,
            Camera camera,
            NWRPVignette vignette,
            bool vignetteActive)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            Color color = Color.clear;
            float outerRing = 1f;
            float innerRing = 1f;
            float fade = 0f;
            float aspectX = 1f;
            float aspectY = 1f;
            float active = 0f;

            if (vignetteActive && vignette != null)
            {
                center = GetVector2(vignette.center, center);
                color = GetColor(vignette.color, new Color(0f, 0f, 0f, 1f));
                outerRing = 1f - GetFloat(vignette.outerRing, 0f);
                innerRing = 1f - GetFloat(vignette.innerRing, 1f);
                fade = GetFloat(vignette.fade, 0f);

                if (GetBool(vignette.circularShape, false))
                {
                    float cameraAspect = Mathf.Max(camera != null ? camera.aspect : 1f, 0.0001f);
                    if (GetVignetteFitMode(vignette.fitMode) == NWRPVignetteFitMode.FitToWidth)
                    {
                        aspectY = 1f / cameraAspect;
                    }
                    else
                    {
                        aspectX = cameraAspect;
                    }
                }
                else
                {
                    aspectY = GetFloat(vignette.aspectRatio, 1f);
                }

                active = 1f;
            }

            cmd.SetGlobalColor(NWRPShaderIds.VignetteColor, color);
            cmd.SetGlobalVector(
                NWRPShaderIds.VignetteParams,
                new Vector4(
                    center.x,
                    center.y,
                    Mathf.Max(0.0001f, aspectY),
                    outerRing));
            cmd.SetGlobalVector(
                NWRPShaderIds.VignetteParams2,
                new Vector4(
                    Mathf.Max(0.0001f, aspectX),
                    innerRing,
                    Mathf.Clamp01(fade),
                    active));
        }

        private static float GetFloat(FloatParameter parameter, float fallback)
        {
            return parameter != null && parameter.overrideState
                ? parameter.value
                : fallback;
        }

        private static bool GetBool(BoolParameter parameter, bool fallback)
        {
            return parameter != null && parameter.overrideState
                ? parameter.value
                : fallback;
        }

        private static Vector2 GetVector2(Vector2Parameter parameter, Vector2 fallback)
        {
            return parameter != null && parameter.overrideState
                ? parameter.value
                : fallback;
        }

        private static Color GetColor(ColorParameter parameter, Color fallback)
        {
            return parameter != null && parameter.overrideState
                ? parameter.value
                : fallback;
        }

        private static NWRPVignetteFitMode GetVignetteFitMode(
            NWRPVignetteFitModeParameter parameter)
        {
            return parameter != null && parameter.overrideState
                ? parameter.value
                : NWRPVignetteFitMode.FitToWidth;
        }

        private Texture GetLensDirtTexture(NWRPBloom bloom)
        {
            Texture texture = bloom.lensDirtTexture.value;
            if (texture != null)
            {
                return texture;
            }

            if (_defaultLensDirtTexture == null)
            {
                _defaultLensDirtTexture = Resources.Load<Texture2D>("Textures/lensDirt");
            }

            return _defaultLensDirtTexture;
        }

        private int GetLensDirtSourceTexture(int lensDirtSpread)
        {
            int index = Mathf.Clamp(lensDirtSpread, 0, k_BloomLastMip);
            return index >= k_BloomLastMip
                ? _bloomMips[k_BloomLastMip].down
                : _bloomMips[index].up;
        }

        private static int GetBloomBaseSize(int sourceWidth, int resolution)
        {
            int size = (int)(Mathf.Lerp(512f, sourceWidth, resolution / 10f) / 4f) * 4;
            return Mathf.Clamp(size, 4, Mathf.Max(sourceWidth, 4));
        }

        private static RenderTextureDescriptor CreateBloomDescriptor(int width, float aspectRatio)
        {
            int safeWidth = Mathf.Max(width, 1);
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                safeWidth,
                Mathf.Max(1, Mathf.RoundToInt(safeWidth * aspectRatio)),
                RenderTextureFormat.ARGBHalf,
                0)
            {
                depthBufferBits = 0,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1,
                bindMS = false,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false
            };
            return descriptor;
        }

        private static void SetBloomTexelSize(CommandBuffer cmd, int width, int height)
        {
            float safeWidth = Mathf.Max(width, 1);
            float safeHeight = Mathf.Max(height, 1);
            cmd.SetGlobalVector(
                NWRPShaderIds.BloomTexelSize,
                new Vector4(1f / safeWidth, 1f / safeHeight, safeWidth, safeHeight));
        }

        private void ReleaseBloomResources(CommandBuffer cmd, BloomResources resources)
        {
            if (!resources.allocated || cmd == null)
            {
                return;
            }

            for (int i = 0; i < _bloomMips.Length; i++)
            {
                cmd.ReleaseTemporaryRT(_bloomMips[i].down);
                cmd.ReleaseTemporaryRT(_bloomMips[i].up);
            }

            if (resources.usedCustomCompose)
            {
                cmd.ReleaseTemporaryRT(_bloomComposeId);
            }
        }

        private bool EnsureTonemappingMaterial()
        {
            if (_tonemappingMaterial != null)
            {
                return true;
            }

            Shader shader = Shader.Find(k_TonemappingShaderName);
            if (shader == null)
            {
                Debug.LogError("NWRP tonemapping requires Hidden/NWRP/PostProcess/Tonemapping.");
                return false;
            }

            _tonemappingMaterial = CoreUtils.CreateEngineMaterial(shader);
            return _tonemappingMaterial != null;
        }

        private bool EnsureBloomMaterial()
        {
            if (_bloomMaterial != null)
            {
                return true;
            }

            Shader shader = Shader.Find(k_BloomShaderName);
            if (shader == null)
            {
                Debug.LogError("NWRP bloom requires Hidden/NWRP/PostProcess/Bloom.");
                return false;
            }

            _bloomMaterial = CoreUtils.CreateEngineMaterial(shader);
            return _bloomMaterial != null;
        }

        private static int GetTonemappingPassIndex(NWRPTonemappingMode mode)
        {
            switch (mode)
            {
                case NWRPTonemappingMode.Linear:
                    return 0;
                case NWRPTonemappingMode.ACES:
                    return 1;
                case NWRPTonemappingMode.ACESFitted:
                    return 2;
                case NWRPTonemappingMode.AGX:
                    return 3;
                default:
                    return -1;
            }
        }
    }
}
