using UnityEditor;
using UnityEngine;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NewWorldRenderPipelineAsset))]
    public sealed class NewWorldRenderPipelineAssetEditor : UnityEditor.Editor
    {
        private const string kMainLightSectionStateKey =
            "NWRP.NewWorldRenderPipelineAssetEditor.MainLightSectionExpanded";
        private const string kAdditionalLightSectionStateKey =
            "NWRP.NewWorldRenderPipelineAssetEditor.AdditionalLightSectionExpanded";

        private SerializedProperty _useSRPBatcherProperty;
        private SerializedProperty _useGPUInstancingProperty;
        private SerializedProperty _featureSettingsProperty;

        private SerializedProperty _mainLightShadowsProperty;
        private SerializedProperty _mainLightShadowTogglesProperty;
        private SerializedProperty _mainLightShadowDistanceProperty;
        private SerializedProperty _mainLightShadowAtlasProperty;
        private SerializedProperty _mainLightShadowBiasProperty;
        private SerializedProperty _mainLightShadowCachedProperty;
        private SerializedProperty _mainLightShadowDebugProperty;
        private SerializedProperty _additionalLightShadowsProperty;
        private SerializedProperty _additionalLightShadowTogglesProperty;
        private SerializedProperty _additionalLightShadowBudgetProperty;
        private SerializedProperty _additionalLightShadowAtlasProperty;
        private SerializedProperty _additionalLightShadowBiasProperty;

        private SerializedProperty _enableMainLightShadowsProperty;
        private SerializedProperty _enableCachedMainLightShadowsProperty;
        private SerializedProperty _enableDynamicShadowOverlayProperty;
        private SerializedProperty _mainLightShadowDistanceValueProperty;
        private SerializedProperty _mainLightShadowCascadeCountProperty;
        private SerializedProperty _mainLightShadowCascadeSplitProperty;
        private SerializedProperty _mainLightShadowResolutionProperty;
        private SerializedProperty _mainLightShadowFilterModeProperty;
        private SerializedProperty _mainLightShadowFilterRadiusProperty;
        private SerializedProperty _mainLightShadowBiasValueProperty;
        private SerializedProperty _mainLightShadowNormalBiasProperty;
        private SerializedProperty _mainLightShadowReceiverDepthBiasProperty;
        private SerializedProperty _mainLightShadowReceiverNormalBiasProperty;
        private SerializedProperty _mainLightShadowCasterCullModeProperty;
        private SerializedProperty _staticCasterLayerMaskProperty;
        private SerializedProperty _dynamicCasterLayerMaskProperty;
        private SerializedProperty _cameraPositionInvalidationThresholdProperty;
        private SerializedProperty _cameraRotationInvalidationThresholdProperty;
        private SerializedProperty _lightDirectionInvalidationThresholdProperty;
        private SerializedProperty _mainLightShadowDebugViewModeProperty;
        private SerializedProperty _enableAdditionalLightShadowsProperty;
        private SerializedProperty _maxShadowedAdditionalLightsProperty;
        private SerializedProperty _additionalLightShadowResolutionProperty;
        private SerializedProperty _additionalLightShadowAtlasMaxSizeProperty;
        private SerializedProperty _additionalLightShadowDistanceProperty;
        private SerializedProperty _additionalLightShadowBiasValueProperty;
        private SerializedProperty _additionalLightShadowNormalBiasProperty;
        private SerializedProperty _additionalLightShadowCasterCullModeProperty;

        private void OnEnable()
        {
            _useSRPBatcherProperty = serializedObject.FindProperty("useSRPBatcher");
            _useGPUInstancingProperty = serializedObject.FindProperty("useGPUInstancing");
            _featureSettingsProperty = serializedObject.FindProperty("featureSettings");

            _mainLightShadowsProperty = serializedObject.FindProperty("mainLightShadows");
            _mainLightShadowTogglesProperty = _mainLightShadowsProperty.FindPropertyRelative("toggles");
            _mainLightShadowDistanceProperty = _mainLightShadowsProperty.FindPropertyRelative("distance");
            _mainLightShadowAtlasProperty = _mainLightShadowsProperty.FindPropertyRelative("atlas");
            _mainLightShadowBiasProperty = _mainLightShadowsProperty.FindPropertyRelative("bias");
            _mainLightShadowCachedProperty = _mainLightShadowsProperty.FindPropertyRelative("cached");
            _mainLightShadowDebugProperty = _mainLightShadowsProperty.FindPropertyRelative("debug");
            _additionalLightShadowsProperty = serializedObject.FindProperty("additionalLightShadows");
            _additionalLightShadowTogglesProperty = _additionalLightShadowsProperty.FindPropertyRelative("toggles");
            _additionalLightShadowBudgetProperty = _additionalLightShadowsProperty.FindPropertyRelative("budget");
            _additionalLightShadowAtlasProperty = _additionalLightShadowsProperty.FindPropertyRelative("atlas");
            _additionalLightShadowBiasProperty = _additionalLightShadowsProperty.FindPropertyRelative("bias");

            _enableMainLightShadowsProperty =
                _mainLightShadowTogglesProperty.FindPropertyRelative("enableMainLightShadows");
            _enableCachedMainLightShadowsProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("enableCachedMainLightShadows");
            _enableDynamicShadowOverlayProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("enableDynamicShadowOverlay");

            _mainLightShadowDistanceValueProperty =
                _mainLightShadowDistanceProperty.FindPropertyRelative("mainLightShadowDistance");
            _mainLightShadowCascadeCountProperty =
                _mainLightShadowDistanceProperty.FindPropertyRelative("mainLightShadowCascadeCount");
            _mainLightShadowCascadeSplitProperty =
                _mainLightShadowDistanceProperty.FindPropertyRelative("mainLightShadowCascadeSplit");

            _mainLightShadowResolutionProperty =
                _mainLightShadowAtlasProperty.FindPropertyRelative("mainLightShadowResolution");
            _mainLightShadowFilterModeProperty =
                _mainLightShadowAtlasProperty.FindPropertyRelative("mainLightShadowFilterMode");
            _mainLightShadowFilterRadiusProperty =
                _mainLightShadowAtlasProperty.FindPropertyRelative("mainLightShadowFilterRadius");

            _mainLightShadowBiasValueProperty =
                _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowBias");
            _mainLightShadowNormalBiasProperty =
                _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowNormalBias");
            _mainLightShadowReceiverDepthBiasProperty =
                _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowReceiverDepthBias");
            _mainLightShadowReceiverNormalBiasProperty =
                _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowReceiverNormalBias");
            _mainLightShadowCasterCullModeProperty =
                _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowCasterCullMode");

            _staticCasterLayerMaskProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("staticCasterLayerMask");
            _dynamicCasterLayerMaskProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("dynamicCasterLayerMask");
            _cameraPositionInvalidationThresholdProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("cameraPositionInvalidationThreshold");
            _cameraRotationInvalidationThresholdProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("cameraRotationInvalidationThreshold");
            _lightDirectionInvalidationThresholdProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("lightDirectionInvalidationThreshold");
            _mainLightShadowDebugViewModeProperty =
                _mainLightShadowDebugProperty.FindPropertyRelative("debugViewMode");
            _enableAdditionalLightShadowsProperty =
                _additionalLightShadowTogglesProperty.FindPropertyRelative("enableAdditionalLightShadows");
            _maxShadowedAdditionalLightsProperty =
                _additionalLightShadowBudgetProperty.FindPropertyRelative("maxShadowedAdditionalLights");
            _additionalLightShadowResolutionProperty =
                _additionalLightShadowAtlasProperty.FindPropertyRelative("additionalLightShadowResolution");
            _additionalLightShadowAtlasMaxSizeProperty =
                _additionalLightShadowAtlasProperty.FindPropertyRelative("additionalLightShadowAtlasMaxSize");
            _additionalLightShadowDistanceProperty =
                _additionalLightShadowAtlasProperty.FindPropertyRelative("additionalLightShadowDistance");
            _additionalLightShadowBiasValueProperty =
                _additionalLightShadowBiasProperty.FindPropertyRelative("additionalLightShadowBias");
            _additionalLightShadowNormalBiasProperty =
                _additionalLightShadowBiasProperty.FindPropertyRelative("additionalLightShadowNormalBias");
            _additionalLightShadowCasterCullModeProperty =
                _additionalLightShadowBiasProperty.FindPropertyRelative("additionalLightShadowCasterCullMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGeneralSettings();
            EditorGUILayout.Space();
            DrawShadowSettings();
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_featureSettingsProperty, true);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useSRPBatcherProperty);
            EditorGUILayout.PropertyField(_useGPUInstancingProperty);
        }

        private void DrawShadowSettings()
        {
            EditorGUILayout.LabelField("Shadow Settings", EditorStyles.boldLabel);
            DrawFoldoutSection(kMainLightSectionStateKey, "Main Light", DrawMainLightShadowSettings);
            DrawFoldoutSection(
                kAdditionalLightSectionStateKey,
                "Additional Punctual Light",
                DrawAdditionalLightShadowSettings);
        }

        private void DrawMainLightShadowSettings()
        {
            DrawSubsectionLabel("Toggle");
            EditorGUILayout.PropertyField(
                _enableMainLightShadowsProperty,
                new GUIContent("Enable Main Light Shadow"));

            if (!_enableMainLightShadowsProperty.boolValue)
            {
                EditorGUILayout.HelpBox("Main light shadows are fully disabled.", MessageType.Info);
                return;
            }

            bool useMediumPCF = _mainLightShadowFilterModeProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.MainLightShadowFilterMode.MediumPCF;

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Distance / Cascade");
            EditorGUILayout.PropertyField(_mainLightShadowDistanceValueProperty);
            EditorGUILayout.PropertyField(_mainLightShadowCascadeCountProperty);
            EditorGUILayout.PropertyField(_mainLightShadowCascadeSplitProperty);

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Atlas / Resolution");
            EditorGUILayout.PropertyField(_mainLightShadowResolutionProperty);
            EditorGUILayout.PropertyField(_mainLightShadowFilterModeProperty);
            if (useMediumPCF)
            {
                EditorGUILayout.PropertyField(_mainLightShadowFilterRadiusProperty);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Bias");
            EditorGUILayout.PropertyField(_mainLightShadowBiasValueProperty);
            EditorGUILayout.PropertyField(_mainLightShadowNormalBiasProperty);
            EditorGUILayout.PropertyField(_mainLightShadowReceiverDepthBiasProperty);
            EditorGUILayout.PropertyField(_mainLightShadowReceiverNormalBiasProperty);
            EditorGUILayout.PropertyField(
                _mainLightShadowCasterCullModeProperty,
                new GUIContent("Shadow Caster Cull Mode"));

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Cached Shadow");
            EditorGUILayout.PropertyField(
                _enableCachedMainLightShadowsProperty,
                new GUIContent("Enable Cached Shadow"));

            if (_enableCachedMainLightShadowsProperty.boolValue)
            {
                EditorGUILayout.PropertyField(
                    _enableDynamicShadowOverlayProperty,
                    new GUIContent("Enable Dynamic Shadow"));
                EditorGUILayout.PropertyField(
                    _staticCasterLayerMaskProperty,
                    new GUIContent("Static Caster Layer Mask"));
                EditorGUILayout.PropertyField(_cameraPositionInvalidationThresholdProperty);
                EditorGUILayout.PropertyField(_cameraRotationInvalidationThresholdProperty);
                EditorGUILayout.PropertyField(_lightDirectionInvalidationThresholdProperty);

                if (_enableDynamicShadowOverlayProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        _dynamicCasterLayerMaskProperty,
                        new GUIContent("Dynamic Caster Layer Mask"));
                }
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Debug View");
            EditorGUILayout.PropertyField(
                _mainLightShadowDebugViewModeProperty,
                new GUIContent("Final Shadow Source Tint"));

            DrawMainLightShadowInfo(useMediumPCF);
        }

        private void DrawMainLightShadowInfo(bool useMediumPCF)
        {
            if (!_enableCachedMainLightShadowsProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Cached main light shadows are currently disabled, so the full main light shadow atlas is refreshed every frame for every camera.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "Cached main light shadows only apply to Game Cameras. SceneView and Preview cameras still render realtime main light shadows.",
                MessageType.None);

            if (useMediumPCF && _enableDynamicShadowOverlayProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "When Medium PCF and the dynamic shadow overlay are both enabled, receivers may run two 9-tap compare filters. This costs more on mobile than using the cached static path alone.",
                    MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "Moving static shadow casters does not rebuild the cached atlas automatically. Call MarkMainLightShadowCacheDirty() or move the Game Camera or main light far enough to cross the invalidation thresholds.",
                MessageType.Info);

            if (_mainLightShadowDebugViewModeProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.MainLightShadowDebugViewMode.FinalShadowSourceTint)
            {
                EditorGUILayout.HelpBox(
                    "Final Shadow Source Tint legend: blue marks dynamic caster surfaces and green marks static caster surfaces. Receiver shadows stay black so the tint only explains the source path.",
                    MessageType.None);
                EditorGUILayout.HelpBox(
                    "Final Shadow Source Tint only affects Game Cameras. SceneView and Preview cameras keep the normal shaded result.",
                    MessageType.Info);
                EditorGUILayout.HelpBox(
                    "Upload Main Light Cached Globals is not a shadow drawing pass. It only uploads the cached shadow textures and matrices for later material sampling in the opaque and transparent draw stages.",
                    MessageType.None);
                EditorGUILayout.HelpBox(
                    "Visible caster tinting follows the current Static Caster Layer Mask and Dynamic Caster Layer Mask, but only renderers that actually participate in the ShadowCaster path are affected. Unlit objects that do not cast shadows keep their normal shading.",
                    MessageType.None);
            }
        }

        private void DrawAdditionalLightShadowSettings()
        {
            DrawSubsectionLabel("Toggle");
            EditorGUILayout.PropertyField(
                _enableAdditionalLightShadowsProperty,
                new GUIContent("Enable Additional Punctual Light Shadows"));

            if (!_enableAdditionalLightShadowsProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Additional punctual light realtime shadows are disabled.",
                    MessageType.Info);
                return;
            }

            DrawSubsectionLabel("Budget");
            EditorGUILayout.PropertyField(
                _maxShadowedAdditionalLightsProperty,
                new GUIContent("Max Shadowed Punctual Lights"));

            DrawSubsectionLabel("Atlas / Distance");
            EditorGUILayout.PropertyField(
                _additionalLightShadowAtlasMaxSizeProperty,
                new GUIContent("Atlas Max Size"));
            EditorGUILayout.PropertyField(
                _additionalLightShadowResolutionProperty,
                new GUIContent("Requested Tile Resolution"));
            EditorGUILayout.PropertyField(
                _additionalLightShadowDistanceProperty,
                new GUIContent("Max Shadow Distance"));

            DrawSubsectionLabel("Bias");
            EditorGUILayout.PropertyField(_additionalLightShadowBiasValueProperty);
            EditorGUILayout.PropertyField(_additionalLightShadowNormalBiasProperty);
            EditorGUILayout.PropertyField(
                _additionalLightShadowCasterCullModeProperty,
                new GUIContent("Shadow Caster Cull Mode"));

            EditorGUILayout.HelpBox(
                "Spot lights consume one shadow slice and point lights consume six shadow slices in the shared atlas. Requested Tile Resolution controls per-slice quality; Atlas Max Size caps the total mobile shadow texture budget. The additional punctual light path is hard-shadow only in this branch.",
                MessageType.None);
        }

        private static void DrawFoldoutSection(string stateKey, string label, System.Action drawContent)
        {
            bool isExpanded = SessionState.GetBool(stateKey, true);
            isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(isExpanded, label);
            SessionState.SetBool(stateKey, isExpanded);

            if (isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    drawContent?.Invoke();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawSubsectionLabel(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        }
    }
}
