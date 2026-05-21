using System.IO;
using System.Reflection;
using NUnit.Framework;
using NWRP.Editor;
using NWRP.Runtime.Passes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Tests
{
    public sealed class ValleyHeightFogVolumeTests
    {
        [Test]
        public void PipelineAssetDoesNotExposeValleyHeightFogToggle()
        {
            PropertyInfo property = typeof(NewWorldRenderPipelineAsset).GetProperty(
                "EnableValleyHeightFog",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNull(property);
        }

        [Test]
        public void FrameDataExposesValleyHeightFogState()
        {
            FieldInfo componentField = typeof(NWRPFrameData).GetField("valleyHeightFog");
            FieldInfo activeField = typeof(NWRPFrameData).GetField("valleyHeightFogActive");

            Assert.IsNotNull(componentField);
            Assert.IsNotNull(activeField);
            Assert.AreEqual(typeof(NWRPValleyHeightFog), componentField.FieldType);
            Assert.AreEqual(typeof(bool), activeField.FieldType);
        }

        [Test]
        public void VolumeEnableIsRuntimeActivationSwitch()
        {
            NWRPValleyHeightFog fog = new NWRPValleyHeightFog();
            fog.active = true;

            fog.enable.value = false;
            Assert.IsFalse(fog.IsActive());

            fog.enable.value = true;
            Assert.IsTrue(fog.IsActive());
        }

        [Test]
        public void VolumeOwnsUrpHeightFogParametersAndDefaults()
        {
            AssertVolumeFieldExists("enable", typeof(BoolParameter));
            AssertVolumeFieldExists("mode", typeof(NWRPValleyHeightFogModeParameter));
            AssertVolumeFieldExists("fogColor", typeof(ColorParameter));
            AssertVolumeFieldExists("fogBaseHeight", typeof(FloatParameter));
            AssertVolumeFieldExists("heightDensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("fogStart", typeof(FloatParameter));
            AssertVolumeFieldExists("fogLength", typeof(FloatParameter));
            AssertVolumeFieldExists("noiseScale", typeof(FloatParameter));
            AssertVolumeFieldExists("noiseIntensity", typeof(FloatParameter));
            AssertVolumeFieldExists("noiseSpeed", typeof(FloatParameter));
            AssertVolumeFieldExists("noiseRoughness", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("noisePersistance", typeof(ClampedFloatParameter));

            NWRPValleyHeightFog fog = new NWRPValleyHeightFog();
            Assert.AreEqual(NWRPValleyHeightFogMode.SingleLayer, fog.mode.value);
            Assert.AreEqual(new Color(0.8f, 0.9f, 1f, 1f), fog.fogColor.value);
            Assert.AreEqual(50f, fog.fogBaseHeight.value);
            Assert.AreEqual(0.3f, fog.heightDensity.value);
            Assert.AreEqual(250f, fog.fogStart.value);
            Assert.AreEqual(100f, fog.fogLength.value);
            Assert.AreEqual(0.005f, fog.noiseScale.value);
            Assert.AreEqual(20f, fog.noiseIntensity.value);
            Assert.AreEqual(0.1f, fog.noiseSpeed.value);
            Assert.AreEqual(2f, fog.noiseRoughness.value);
            Assert.AreEqual(0.5f, fog.noisePersistance.value);

            Assert.IsNull(typeof(NWRPValleyHeightFog).GetField("intensity"));
        }

        [Test]
        public void VolumeOwnsThreeLayerFogParametersAndDefaults()
        {
            AssertVolumeFieldExists("bottomHeight", typeof(FloatParameter));
            AssertVolumeFieldExists("bottomFade", typeof(FloatParameter));
            AssertVolumeFieldExists("bottomDensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("bottomIntensity", typeof(FloatParameter));
            AssertVolumeFieldExists("bottomNoiseScale", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("bottomNoiseIntensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("midHeight", typeof(FloatParameter));
            AssertVolumeFieldExists("midFade", typeof(FloatParameter));
            AssertVolumeFieldExists("midDensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("midIntensity", typeof(FloatParameter));
            AssertVolumeFieldExists("midNoiseScale", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("midNoiseIntensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("topIntensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("topDensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("topNoiseScale", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("topNoiseIntensity", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("threeLayerNoiseSpeed", typeof(FloatParameter));
            AssertVolumeFieldExists("threeLayerNoiseRoughness", typeof(ClampedFloatParameter));
            AssertVolumeFieldExists("threeLayerNoisePersistance", typeof(ClampedFloatParameter));

            NWRPValleyHeightFog fog = new NWRPValleyHeightFog();
            Assert.AreEqual(10f, fog.bottomHeight.value);
            Assert.AreEqual(6f, fog.bottomFade.value);
            Assert.AreEqual(0.012f, fog.bottomDensity.value);
            Assert.AreEqual(0.8f, fog.bottomIntensity.value);
            Assert.AreEqual(0.12f, fog.bottomNoiseScale.value);
            Assert.AreEqual(1f, fog.bottomNoiseIntensity.value);
            Assert.AreEqual(300f, fog.midHeight.value);
            Assert.AreEqual(60f, fog.midFade.value);
            Assert.AreEqual(0.003f, fog.midDensity.value);
            Assert.AreEqual(0.5f, fog.midIntensity.value);
            Assert.AreEqual(0.003f, fog.midNoiseScale.value);
            Assert.AreEqual(1.1f, fog.midNoiseIntensity.value);
            Assert.AreEqual(0f, fog.topIntensity.value);
            Assert.AreEqual(0.0005f, fog.topDensity.value);
            Assert.AreEqual(0.005f, fog.topNoiseScale.value);
            Assert.AreEqual(1.5f, fog.topNoiseIntensity.value);
            Assert.AreEqual(0.15f, fog.threeLayerNoiseSpeed.value);
            Assert.AreEqual(2f, fog.threeLayerNoiseRoughness.value);
            Assert.AreEqual(0.35f, fog.threeLayerNoisePersistance.value);
        }

        [Test]
        public void ValleyHeightFogFeatureRequestsColorAndDepthWhenActive()
        {
            NewWorldRenderPipelineAsset asset =
                ScriptableObject.CreateInstance<NewWorldRenderPipelineAsset>();
            asset.supportsPostProcessing = true;

            NWRPFrameData frameData = new NWRPFrameData
            {
                asset = asset,
                postProcessingEnabled = true,
                valleyHeightFog = new NWRPValleyHeightFog(),
                valleyHeightFogActive = true
            };

            ValleyHeightFogFeature feature =
                ScriptableObject.CreateInstance<ValleyHeightFogFeature>();

            try
            {
                Assert.IsTrue(feature.TryGetFrameTargetRequirements(
                    ref frameData,
                    out NWRPFrameTargetRequirements requirements));
                Assert.IsTrue(requirements.requiresIntermediateColor);
                Assert.IsTrue(requirements.requiresDepthTexture);
            }
            finally
            {
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ValleyHeightFogFeatureDoesNotRequestTargetsWhenInactive()
        {
            NewWorldRenderPipelineAsset asset =
                ScriptableObject.CreateInstance<NewWorldRenderPipelineAsset>();
            asset.supportsPostProcessing = true;

            NWRPFrameData frameData = new NWRPFrameData
            {
                asset = asset,
                postProcessingEnabled = true,
                valleyHeightFog = new NWRPValleyHeightFog(),
                valleyHeightFogActive = false
            };

            ValleyHeightFogFeature feature =
                ScriptableObject.CreateInstance<ValleyHeightFogFeature>();

            try
            {
                Assert.IsFalse(feature.TryGetFrameTargetRequirements(
                    ref frameData,
                    out NWRPFrameTargetRequirements requirements));
                Assert.IsFalse(requirements.requiresIntermediateColor);
                Assert.IsFalse(requirements.requiresDepthTexture);
            }
            finally
            {
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ValleyHeightFogPassRunsAfterTransparent()
        {
            ValleyHeightFogPass pass = new ValleyHeightFogPass();

            Assert.AreEqual(NWRPPassEvent.AfterTransparent, pass.passEvent);
        }

        [Test]
        public void ValleyHeightFogPassSelectsShaderPassFromVolumeMode()
        {
            NWRPValleyHeightFog fog = new NWRPValleyHeightFog();

            fog.mode.value = NWRPValleyHeightFogMode.SingleLayer;
            Assert.AreEqual(
                ValleyHeightFogPass.SingleLayerShaderPass,
                ValleyHeightFogPass.GetShaderPassIndex(fog));

            fog.mode.value = NWRPValleyHeightFogMode.ThreeLayer;
            Assert.AreEqual(
                ValleyHeightFogPass.ThreeLayerShaderPass,
                ValleyHeightFogPass.GetShaderPassIndex(fog));
        }

        [Test]
        public void AssetEditorCreatesAndAddsValleyHeightFogFeatureSubAsset()
        {
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/NWRP/Tests/EditMode/TempNewWorldRP.asset");
            NewWorldRenderPipelineAsset asset =
                ScriptableObject.CreateInstance<NewWorldRenderPipelineAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);

            try
            {
                ValleyHeightFogFeature feature =
                    NewWorldRenderPipelineAssetEditor.EnsureValleyHeightFogFeature(asset);

                Assert.IsNotNull(feature);
                Assert.Contains(feature, asset.Features);
                Assert.AreEqual(assetPath, AssetDatabase.GetAssetPath(feature));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void ValleyHeightFogShaderUsesNwrpAlgorithmPathOnly()
        {
            string shaderPath = Path.Combine(
                Application.dataPath,
                "NWRP/Shaders/PostProcess/NWRP_ValleyHeightFog.shader");
            string shader = File.ReadAllText(shaderPath);

            Assert.IsFalse(shader.Contains("_CameraOpaqueTexture"));
            Assert.IsFalse(shader.Contains("multi_compile"));
            Assert.IsFalse(shader.Contains("shader_feature"));
            Assert.IsFalse(shader.Contains("render-pipelines.universal"));
            Assert.IsFalse(shader.Contains("worldDebugColor"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogColor"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogHeightParams"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogDistanceParams"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogNoiseParams"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogBottomParams"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogMidParams"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogTopParams"));
            Assert.IsTrue(shader.Contains("_NWRPValleyHeightFogThreeLayerNoiseParams"));
            Assert.IsTrue(shader.Contains("Name \"Valley Height Fog 3 Layer\""));
            Assert.IsTrue(shader.Contains("#pragma fragment FragThreeLayer"));
        }

        private static void AssertVolumeFieldExists(string fieldName, System.Type expectedType)
        {
            FieldInfo field = typeof(NWRPValleyHeightFog).GetField(fieldName);
            Assert.IsNotNull(field, fieldName);
            Assert.AreEqual(expectedType, field.FieldType, fieldName);
        }
    }
}
