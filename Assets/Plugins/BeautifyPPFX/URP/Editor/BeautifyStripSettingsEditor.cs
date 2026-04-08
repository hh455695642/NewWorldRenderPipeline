using UnityEngine;
using UnityEditor;

namespace Beautify.Universal {

    [CustomEditor(typeof(BeautifyStripSettings))]
    public class BeautifyStripSettingsEditor : Editor {

        SerializedProperty stripBeautifyTonemappingACES, stripBeautifyTonemappingACESFitted, stripBeautifyTonemappingAGX;
        SerializedProperty stripBeautifyDithering;
        SerializedProperty stripBeautifyLUT, stripBeautifyLUT3D, stripBeautifyColorTweaks;
        SerializedProperty stripBeautifySMH;
        SerializedProperty stripBeautifyBloom, stripBeautifyLensDirt, stripBeautifyChromaticAberration;
        SerializedProperty stripBeautifyDoF, stripBeautifyDoFTransparentSupport;
        SerializedProperty stripBeautifyEyeAdaptation;
        SerializedProperty stripBeautifyVignetting, stripBeautifyVignettingMask;
        SerializedProperty stripBeautifyOutline;
        SerializedProperty stripBeautifyFilmGrain;
        SerializedProperty stripUnityFilmGrain, stripUnityDithering, stripUnityTonemapping;
        SerializedProperty stripUnityBloom, stripUnityChromaticAberration;
        SerializedProperty stripUnityDistortion, stripUnityDebugVariants;

        void OnEnable() {
            try {
            stripBeautifyTonemappingACES = serializedObject.FindProperty("stripBeautifyTonemappingACES");
            stripBeautifyTonemappingACESFitted = serializedObject.FindProperty("stripBeautifyTonemappingACESFitted");
            stripBeautifyTonemappingAGX = serializedObject.FindProperty("stripBeautifyTonemappingAGX");
            stripBeautifyDithering = serializedObject.FindProperty("stripBeautifyDithering");
            stripBeautifyLUT = serializedObject.FindProperty("stripBeautifyLUT");
            stripBeautifyLUT3D = serializedObject.FindProperty("stripBeautifyLUT3D");
            stripBeautifyColorTweaks = serializedObject.FindProperty("stripBeautifyColorTweaks");
            stripBeautifySMH = serializedObject.FindProperty("stripBeautifySMH");
            stripBeautifyBloom = serializedObject.FindProperty("stripBeautifyBloom");
            stripBeautifyLensDirt = serializedObject.FindProperty("stripBeautifyLensDirt");
            stripBeautifyChromaticAberration = serializedObject.FindProperty("stripBeautifyChromaticAberration");
            stripBeautifyDoF = serializedObject.FindProperty("stripBeautifyDoF");
            stripBeautifyDoFTransparentSupport = serializedObject.FindProperty("stripBeautifyDoFTransparentSupport");
            stripBeautifyEyeAdaptation = serializedObject.FindProperty("stripBeautifyEyeAdaptation");
            stripBeautifyVignetting = serializedObject.FindProperty("stripBeautifyVignetting");
            stripBeautifyVignettingMask = serializedObject.FindProperty("stripBeautifyVignettingMask");
            stripBeautifyOutline = serializedObject.FindProperty("stripBeautifyOutline");
            stripBeautifyFilmGrain = serializedObject.FindProperty("stripBeautifyFilmGrain");
            stripUnityFilmGrain = serializedObject.FindProperty("stripUnityFilmGrain");
            stripUnityDithering = serializedObject.FindProperty("stripUnityDithering");
            stripUnityTonemapping = serializedObject.FindProperty("stripUnityTonemapping");
            stripUnityBloom = serializedObject.FindProperty("stripUnityBloom");
            stripUnityChromaticAberration = serializedObject.FindProperty("stripUnityChromaticAberration");
            stripUnityDistortion = serializedObject.FindProperty("stripUnityDistortion");
            stripUnityDebugVariants = serializedObject.FindProperty("stripUnityDebugVariants");
            } catch {
            }
        }

        public override void OnInspectorGUI() {

            serializedObject.Update();

            void DrawStripToggle(SerializedProperty property, string label) {
                EditorGUILayout.BeginHorizontal();
                property.boolValue = GUILayout.Toggle(property.boolValue, "", GUILayout.Width(20));
                GUILayout.Label(label);
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Autoselect Unused Beautify Features", EditorStyles.miniButton)) {
                if (EditorUtility.DisplayDialog("Autoselect Unused Beautify Features", "This will disable features not used in the current scene. Do you want to proceed?", "Yes", "No")) {

#if UNITY_2023_1_OR_NEWER
                    var beautifyVolumes = FindObjectsByType<Beautify>(FindObjectsSortMode.None);
#else
                    var beautifyVolumes = FindObjectsOfType<Beautify>();
#endif

                    bool isFeatureUsed(System.Func<Beautify, bool> predicate) {
                        foreach (var volume in beautifyVolumes) {
                            if (predicate(volume)) {
                                return true;
                            }
                        }
                        return false;
                    }

                    stripBeautifyDithering.boolValue = !isFeatureUsed(b => b.ditherIntensity.value > 0f);
                    stripBeautifyTonemappingACES.boolValue = !isFeatureUsed(b => b.tonemap.value == Beautify.TonemapOperator.ACES);
                    stripBeautifyTonemappingACESFitted.boolValue = !isFeatureUsed(b => b.tonemap.value == Beautify.TonemapOperator.ACESFitted);
                    stripBeautifyTonemappingAGX.boolValue = !isFeatureUsed(b => b.tonemap.value == Beautify.TonemapOperator.AGX);
                    stripBeautifyLUT.boolValue = !isFeatureUsed(b => b.lut.value && b.lutIntensity.value > 0 && b.lutTexture.value != null && !(b.lutTexture.value is Texture3D));
                    stripBeautifyLUT3D.boolValue = !isFeatureUsed(b => b.lut.value && b.lutIntensity.value > 0 && b.lutTexture.value is Texture3D);
                    stripBeautifyColorTweaks.boolValue = !isFeatureUsed(b => b.colorTempBlend.value > 0);
                    stripBeautifySMH.boolValue = !isFeatureUsed(b => b.smhShadows.value != Color.white || b.smhMidtones.value != Color.white || b.smhHighlights.value != Color.white);
                    stripBeautifyBloom.boolValue = !isFeatureUsed(b => b.bloomIntensity.value > 0f);
                    stripBeautifyLensDirt.boolValue = !isFeatureUsed(b => b.lensDirtIntensity.value > 0);
                    stripBeautifyChromaticAberration.boolValue = !isFeatureUsed(b => b.chromaticAberrationIntensity.value > 0f);
                    stripBeautifyDoF.boolValue = !isFeatureUsed(b => b.depthOfField.value);
                    stripBeautifyDoFTransparentSupport.boolValue = !isFeatureUsed(b => b.depthOfFieldTransparentSupport.value);
                    stripBeautifyEyeAdaptation.boolValue = !isFeatureUsed(b => b.eyeAdaptation.value);
                    stripBeautifyVignetting.boolValue = !isFeatureUsed(b => b.vignettingOuterRing.value > 0f);
                    stripBeautifyVignettingMask.boolValue = !isFeatureUsed(b => b.vignettingOuterRing.value > 0f && b.vignettingMask.value != null);
                    stripBeautifyOutline.boolValue = !isFeatureUsed(b => b.outline.value);
                    stripBeautifyFilmGrain.boolValue = !isFeatureUsed(b => b.filmGrainEnabled.value && (b.filmGrainIntensity.value > 0f || b.filmGrainDirtSpotsAmount.value > 0f || b.filmGrainScratchesAmount.value > 0f));
                }
            }

            // Image Enhancement section
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Image Enhancement", EditorStyles.boldLabel);
            if (GUILayout.Button("Toggle All", EditorStyles.miniButton, GUILayout.Width(80))) {
                bool allStripped = stripBeautifyDithering.boolValue;
                stripBeautifyDithering.boolValue = !allStripped;
            }
            EditorGUILayout.EndHorizontal();
            DrawStripToggle(stripBeautifyDithering, "Strip Dithering");

            // Tonemapping section
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tonemapping", EditorStyles.boldLabel);
            if (GUILayout.Button("Toggle All", EditorStyles.miniButton, GUILayout.Width(80))) {
                bool allStripped = stripBeautifyTonemappingACES.boolValue &&
                                 stripBeautifyTonemappingACESFitted.boolValue &&
                                 stripBeautifyTonemappingAGX.boolValue;
                stripBeautifyTonemappingACES.boolValue = !allStripped;
                stripBeautifyTonemappingACESFitted.boolValue = !allStripped;
                stripBeautifyTonemappingAGX.boolValue = !allStripped;
            }
            EditorGUILayout.EndHorizontal();
            DrawStripToggle(stripBeautifyTonemappingACES, "Strip ACES Tonemapping");
            DrawStripToggle(stripBeautifyTonemappingACESFitted, "Strip ACES Fitted Tonemapping");
            DrawStripToggle(stripBeautifyTonemappingAGX, "Strip AGX Tonemapping");

            // Color Grading section
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Color Grading", EditorStyles.boldLabel);
            if (GUILayout.Button("Toggle All", EditorStyles.miniButton, GUILayout.Width(80))) {
                bool allStripped = stripBeautifyLUT.boolValue &&
                                 stripBeautifyLUT3D.boolValue &&
                                 stripBeautifyColorTweaks.boolValue &&
                                 stripBeautifySMH.boolValue;
                stripBeautifyLUT.boolValue = !allStripped;
                stripBeautifyLUT3D.boolValue = !allStripped;
                stripBeautifyColorTweaks.boolValue = !allStripped;
                stripBeautifySMH.boolValue = !allStripped;
            }
            EditorGUILayout.EndHorizontal();
            DrawStripToggle(stripBeautifyLUT, "Strip LUT");
            DrawStripToggle(stripBeautifyLUT3D, "Strip LUT 3D");
            DrawStripToggle(stripBeautifyColorTweaks, new GUIContent("Strip Color Tweaks", "Refers to white balance / color temperature").text);
            DrawStripToggle(stripBeautifySMH, "Strip Shadows Midtones Highlights");

            // Effects section
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
            if (GUILayout.Button("Toggle All", EditorStyles.miniButton, GUILayout.Width(80))) {
                bool allStripped = stripBeautifyBloom.boolValue &&
                                 stripBeautifyLensDirt.boolValue &&
                                 stripBeautifyChromaticAberration.boolValue &&
                                 stripBeautifyDoF.boolValue &&
                                 stripBeautifyDoFTransparentSupport.boolValue &&
                                 stripBeautifyEyeAdaptation.boolValue &&
                                 stripBeautifyVignetting.boolValue &&
                                 stripBeautifyVignettingMask.boolValue &&
                                 stripBeautifyOutline.boolValue &&
                                 stripBeautifyFilmGrain.boolValue;
                stripBeautifyBloom.boolValue = !allStripped;
                stripBeautifyLensDirt.boolValue = !allStripped;
                stripBeautifyChromaticAberration.boolValue = !allStripped;
                stripBeautifyDoF.boolValue = !allStripped;
                stripBeautifyDoFTransparentSupport.boolValue = !allStripped;
                stripBeautifyEyeAdaptation.boolValue = !allStripped;
                stripBeautifyVignetting.boolValue = !allStripped;
                stripBeautifyVignettingMask.boolValue = !allStripped;
                stripBeautifyOutline.boolValue = !allStripped;
                stripBeautifyFilmGrain.boolValue = !allStripped;
            }
            EditorGUILayout.EndHorizontal();
            DrawStripToggle(stripBeautifyBloom, "Strip Bloom & Sun Flares");
            DrawStripToggle(stripBeautifyLensDirt, "Strip Lens Dirt");
            DrawStripToggle(stripBeautifyChromaticAberration, "Strip Chromatic Aberration");
            DrawStripToggle(stripBeautifyDoF, "Strip Depth of Field");
            DrawStripToggle(stripBeautifyDoFTransparentSupport, "Strip DoF Transparent Support");
            DrawStripToggle(stripBeautifyEyeAdaptation, "Strip Eye Adaptation");
            DrawStripToggle(stripBeautifyVignetting, "Strip Vignetting");
            DrawStripToggle(stripBeautifyVignettingMask, "Strip Vignetting Mask");
            DrawStripToggle(stripBeautifyOutline, "Strip Outline");
            DrawStripToggle(stripBeautifyFilmGrain, "Strip Film Grain");

            EditorGUILayout.Separator();
            // Unity Post Processing section
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Unity Post Processing Stripping", EditorStyles.boldLabel);
            if (GUILayout.Button("Toggle All", EditorStyles.miniButton, GUILayout.Width(80))) {
                bool allStripped = stripUnityFilmGrain.boolValue &&
                                 stripUnityDithering.boolValue &&
                                 stripUnityTonemapping.boolValue &&
                                 stripUnityBloom.boolValue &&
                                 stripUnityChromaticAberration.boolValue &&
                                 stripUnityDistortion.boolValue &&
                                 stripUnityDebugVariants.boolValue;
                stripUnityFilmGrain.boolValue = !allStripped;
                stripUnityDithering.boolValue = !allStripped;
                stripUnityTonemapping.boolValue = !allStripped;
                stripUnityBloom.boolValue = !allStripped;
                stripUnityChromaticAberration.boolValue = !allStripped;
                stripUnityDistortion.boolValue = !allStripped;
                stripUnityDebugVariants.boolValue = !allStripped;
            }
            EditorGUILayout.EndHorizontal();
            DrawStripToggle(stripUnityFilmGrain, "Strip Film Grain");
            DrawStripToggle(stripUnityDithering, "Strip Dithering");
            DrawStripToggle(stripUnityTonemapping, "Strip Tonemapping");
            DrawStripToggle(stripUnityBloom, "Strip Bloom");
            DrawStripToggle(stripUnityChromaticAberration, "Strip Chromatic Aberration");
            DrawStripToggle(stripUnityDistortion, "Strip Distortion");
            DrawStripToggle(stripUnityDebugVariants, "Strip Debug Variants");

            if (serializedObject.ApplyModifiedProperties()) {
                BeautifyRendererFeature.StripBeautifyFeatures();
            }
        }
    }
}
