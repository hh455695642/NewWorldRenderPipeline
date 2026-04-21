using UnityEditor;
using UnityEngine;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NewWorldRenderPipelineAsset))]
    public sealed class NewWorldRenderPipelineAssetEditor : UnityEditor.Editor
    {
        private const string kMainLightSectionStateKey =
            "NWRP.NewWorldRenderPipelineAssetEditor.MainLightSectionExpanded";

        private SerializedProperty _useSRPBatcherProperty;
        private SerializedProperty _useGPUInstancingProperty;
        private SerializedProperty _featureSettingsProperty;

        private SerializedProperty _mainLightShadowsProperty;
        private SerializedProperty _mainLightShadowTogglesProperty;
        private SerializedProperty _mainLightShadowDistanceProperty;
        private SerializedProperty _mainLightShadowAtlasProperty;
        private SerializedProperty _mainLightShadowBiasProperty;
        private SerializedProperty _mainLightShadowCachedProperty;

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

            _enableMainLightShadowsProperty = _mainLightShadowTogglesProperty.FindPropertyRelative("enableMainLightShadows");
            _enableCachedMainLightShadowsProperty = _mainLightShadowCachedProperty.FindPropertyRelative("enableCachedMainLightShadows");
            _enableDynamicShadowOverlayProperty = _mainLightShadowCachedProperty.FindPropertyRelative("enableDynamicShadowOverlay");

            _mainLightShadowDistanceValueProperty = _mainLightShadowDistanceProperty.FindPropertyRelative("mainLightShadowDistance");
            _mainLightShadowCascadeCountProperty = _mainLightShadowDistanceProperty.FindPropertyRelative("mainLightShadowCascadeCount");
            _mainLightShadowCascadeSplitProperty = _mainLightShadowDistanceProperty.FindPropertyRelative("mainLightShadowCascadeSplit");

            _mainLightShadowResolutionProperty = _mainLightShadowAtlasProperty.FindPropertyRelative("mainLightShadowResolution");
            _mainLightShadowFilterModeProperty = _mainLightShadowAtlasProperty.FindPropertyRelative("mainLightShadowFilterMode");
            _mainLightShadowFilterRadiusProperty = _mainLightShadowAtlasProperty.FindPropertyRelative("mainLightShadowFilterRadius");

            _mainLightShadowBiasValueProperty = _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowBias");
            _mainLightShadowNormalBiasProperty = _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowNormalBias");
            _mainLightShadowReceiverDepthBiasProperty = _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowReceiverDepthBias");
            _mainLightShadowReceiverNormalBiasProperty = _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowReceiverNormalBias");
            _mainLightShadowCasterCullModeProperty = _mainLightShadowBiasProperty.FindPropertyRelative("mainLightShadowCasterCullMode");

            _staticCasterLayerMaskProperty = _mainLightShadowCachedProperty.FindPropertyRelative("staticCasterLayerMask");
            _dynamicCasterLayerMaskProperty = _mainLightShadowCachedProperty.FindPropertyRelative("dynamicCasterLayerMask");
            _cameraPositionInvalidationThresholdProperty = _mainLightShadowCachedProperty.FindPropertyRelative("cameraPositionInvalidationThreshold");
            _cameraRotationInvalidationThresholdProperty = _mainLightShadowCachedProperty.FindPropertyRelative("cameraRotationInvalidationThreshold");
            _lightDirectionInvalidationThresholdProperty = _mainLightShadowCachedProperty.FindPropertyRelative("lightDirectionInvalidationThreshold");
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
        }

        private void DrawMainLightShadowSettings()
        {
            DrawSubsectionLabel("Toggle");
            EditorGUILayout.PropertyField(_enableMainLightShadowsProperty, new GUIContent("Enable Main Light Shadow"));

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
            EditorGUILayout.PropertyField(_mainLightShadowCasterCullModeProperty, new GUIContent("Shadow Caster Cull Mode"));

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Cached Shadow");
            EditorGUILayout.PropertyField(_enableCachedMainLightShadowsProperty, new GUIContent("Enable Cached Shadow"));

            if (_enableCachedMainLightShadowsProperty.boolValue)
            {
                EditorGUILayout.PropertyField(_enableDynamicShadowOverlayProperty, new GUIContent("Enable Dynamic Shadow"));
                EditorGUILayout.PropertyField(_staticCasterLayerMaskProperty, new GUIContent("Static Caster Layer Mask"));
                EditorGUILayout.PropertyField(_cameraPositionInvalidationThresholdProperty);
                EditorGUILayout.PropertyField(_cameraRotationInvalidationThresholdProperty);
                EditorGUILayout.PropertyField(_lightDirectionInvalidationThresholdProperty);

                if (_enableDynamicShadowOverlayProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(_dynamicCasterLayerMaskProperty, new GUIContent("Dynamic Caster Layer Mask"));
                }
            }

            DrawMainLightShadowInfo(useMediumPCF);
        }

        private void DrawMainLightShadowInfo(bool useMediumPCF)
        {
            if (!_enableCachedMainLightShadowsProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Cached shadow is disabled. The full main light shadow atlas is updated every frame for all cameras.",
                    MessageType.None
                );
                return;
            }

            EditorGUILayout.HelpBox(
                "Cached main light shadow is only used by Game Cameras. SceneView and Preview cameras fall back to realtime main light shadows.",
                MessageType.None
            );

            if (useMediumPCF && _enableDynamicShadowOverlayProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Medium PCF with dynamic shadow overlay can evaluate two 9-tap compare filters on the receiver path. Mobile cost is higher than static-only Medium PCF.",
                    MessageType.Info
                );
            }

            EditorGUILayout.HelpBox(
                "Static caster movement does not rebuild the cached atlas automatically. Rebuild it by calling MarkMainLightShadowCacheDirty() or by exceeding the Game Camera / main light invalidation thresholds.",
                MessageType.Info
            );
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
