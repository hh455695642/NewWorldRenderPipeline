using UnityEditor;
using UnityEngine;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NewWorldRenderPipelineAsset))]
    public sealed class NewWorldRenderPipelineAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty _useSRPBatcherProperty;
        private SerializedProperty _useGPUInstancingProperty;
        private SerializedProperty _featureSettingsProperty;

        private SerializedProperty _mainLightShadowsProperty;
        private SerializedProperty _enableMainLightShadowsProperty;
        private SerializedProperty _enableCachedMainLightShadowsProperty;
        private SerializedProperty _mainLightShadowResolutionProperty;
        private SerializedProperty _mainLightShadowDistanceProperty;
        private SerializedProperty _mainLightShadowCascadeCountProperty;
        private SerializedProperty _mainLightShadowCascadeSplitProperty;
        private SerializedProperty _mainLightShadowBiasProperty;
        private SerializedProperty _mainLightShadowNormalBiasProperty;
        private SerializedProperty _mainLightShadowFilterModeProperty;
        private SerializedProperty _mainLightShadowFilterRadiusProperty;
        private SerializedProperty _mainLightShadowReceiverDepthBiasProperty;
        private SerializedProperty _mainLightShadowReceiverNormalBiasProperty;
        private SerializedProperty _mainLightShadowCasterCullModeProperty;
        private SerializedProperty _enableDynamicShadowOverlayProperty;
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
            _enableMainLightShadowsProperty = _mainLightShadowsProperty.FindPropertyRelative("enableMainLightShadows");
            _enableCachedMainLightShadowsProperty = _mainLightShadowsProperty.FindPropertyRelative("enableCachedMainLightShadows");
            _mainLightShadowResolutionProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowResolution");
            _mainLightShadowDistanceProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowDistance");
            _mainLightShadowCascadeCountProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowCascadeCount");
            _mainLightShadowCascadeSplitProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowCascadeSplit");
            _mainLightShadowBiasProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowBias");
            _mainLightShadowNormalBiasProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowNormalBias");
            _mainLightShadowFilterModeProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowFilterMode");
            _mainLightShadowFilterRadiusProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowFilterRadius");
            _mainLightShadowReceiverDepthBiasProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowReceiverDepthBias");
            _mainLightShadowReceiverNormalBiasProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowReceiverNormalBias");
            _mainLightShadowCasterCullModeProperty = _mainLightShadowsProperty.FindPropertyRelative("mainLightShadowCasterCullMode");
            _enableDynamicShadowOverlayProperty = _mainLightShadowsProperty.FindPropertyRelative("enableDynamicShadowOverlay");
            _staticCasterLayerMaskProperty = _mainLightShadowsProperty.FindPropertyRelative("staticCasterLayerMask");
            _dynamicCasterLayerMaskProperty = _mainLightShadowsProperty.FindPropertyRelative("dynamicCasterLayerMask");
            _cameraPositionInvalidationThresholdProperty = _mainLightShadowsProperty.FindPropertyRelative("cameraPositionInvalidationThreshold");
            _cameraRotationInvalidationThresholdProperty = _mainLightShadowsProperty.FindPropertyRelative("cameraRotationInvalidationThreshold");
            _lightDirectionInvalidationThresholdProperty = _mainLightShadowsProperty.FindPropertyRelative("lightDirectionInvalidationThreshold");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGeneralSettings();
            EditorGUILayout.Space();
            DrawMainLightShadowSettings();
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

        private void DrawMainLightShadowSettings()
        {
            EditorGUILayout.LabelField("Main Light Shadows", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableMainLightShadowsProperty, new GUIContent("Enable Main Light Shadow"));

            using (new EditorGUI.DisabledScope(!_enableMainLightShadowsProperty.boolValue))
            {
                EditorGUILayout.PropertyField(_mainLightShadowResolutionProperty);
                EditorGUILayout.PropertyField(_mainLightShadowDistanceProperty);
                EditorGUILayout.PropertyField(_mainLightShadowCascadeCountProperty);
                EditorGUILayout.PropertyField(_mainLightShadowCascadeSplitProperty);
                EditorGUILayout.PropertyField(_mainLightShadowBiasProperty);
                EditorGUILayout.PropertyField(_mainLightShadowNormalBiasProperty);
                EditorGUILayout.PropertyField(_mainLightShadowFilterModeProperty);
                bool showFilterRadius = _mainLightShadowFilterModeProperty.enumValueIndex
                    == (int)NewWorldRenderPipelineAsset.MainLightShadowFilterMode.MediumPCF;
                if (showFilterRadius)
                {
                    EditorGUILayout.PropertyField(_mainLightShadowFilterRadiusProperty);
                }
                EditorGUILayout.PropertyField(_mainLightShadowReceiverDepthBiasProperty);
                EditorGUILayout.PropertyField(_mainLightShadowReceiverNormalBiasProperty);
                EditorGUILayout.PropertyField(_mainLightShadowCasterCullModeProperty, new GUIContent("Shadow Caster Cull Mode"));
                EditorGUILayout.Space(2f);
                EditorGUILayout.PropertyField(_enableCachedMainLightShadowsProperty, new GUIContent("Enable Cached Shadow"));
            }

            if (!_enableMainLightShadowsProperty.boolValue)
            {
                EditorGUILayout.HelpBox("Main light shadows are fully disabled.", MessageType.Info);
                return;
            }

            bool useMediumPCF = _mainLightShadowFilterModeProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.MainLightShadowFilterMode.MediumPCF;

            if (!_enableCachedMainLightShadowsProperty.boolValue)
            {
                EditorGUILayout.HelpBox("Cached shadow is disabled. The full main light shadow atlas is updated every frame for all cameras.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Cached Shadow Controls", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Cached main light shadow is only used by Game Cameras. SceneView and Preview cameras fall back to realtime main light shadows.", MessageType.None);
            EditorGUILayout.PropertyField(_enableDynamicShadowOverlayProperty, new GUIContent("Enable Dynamic Shadow"));
            if (useMediumPCF && _enableDynamicShadowOverlayProperty.boolValue)
            {
                EditorGUILayout.HelpBox("Medium PCF with dynamic shadow overlay can evaluate two 9-tap compare filters on the receiver path. Mobile cost is higher than static-only Medium PCF.", MessageType.Info);
            }
            EditorGUILayout.PropertyField(_staticCasterLayerMaskProperty, new GUIContent("Static Caster Layer Mask"));

            using (new EditorGUI.DisabledScope(!_enableDynamicShadowOverlayProperty.boolValue))
            {
                EditorGUILayout.PropertyField(_dynamicCasterLayerMaskProperty, new GUIContent("Dynamic Caster Layer Mask"));
            }

            EditorGUILayout.PropertyField(_cameraPositionInvalidationThresholdProperty);
            EditorGUILayout.PropertyField(_cameraRotationInvalidationThresholdProperty);
            EditorGUILayout.PropertyField(_lightDirectionInvalidationThresholdProperty);
            EditorGUILayout.HelpBox("Static caster movement does not rebuild the cached atlas automatically. Rebuild it by calling MarkMainLightShadowCacheDirty() or by exceeding the Game Camera / main light invalidation thresholds.", MessageType.Info);
        }
    }
}
