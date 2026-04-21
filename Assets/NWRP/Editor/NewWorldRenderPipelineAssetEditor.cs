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
        private SerializedProperty _mainLightShadowDebugProperty;

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
            _mainLightShadowDebugViewModeProperty = _mainLightShadowDebugProperty.FindPropertyRelative("debugViewMode");
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
                    "Cached shadow 当前已关闭，完整的主光阴影 Atlas 会对所有相机逐帧更新。",
                    MessageType.None
                );
                return;
            }

            EditorGUILayout.HelpBox(
                "Cached main light shadow 只对 Game Camera 生效。SceneView 和 Preview 相机会回退到实时主光阴影。",
                MessageType.None
            );

            if (useMediumPCF && _enableDynamicShadowOverlayProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "当 Medium PCF 与动态阴影叠加同时开启时，接收端可能会执行两次 9-tap compare filter。相比仅使用静态 Medium PCF，这条路径在移动端的开销会更高。",
                    MessageType.Info
                );
            }

            EditorGUILayout.HelpBox(
                "静态投影物体移动后不会自动重建 cached atlas。需要主动调用 MarkMainLightShadowCacheDirty()，或者让 Game Camera / 主光方向超过设定的失效阈值，才会触发重建。",
                MessageType.Info
            );

            if (_mainLightShadowDebugViewModeProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.MainLightShadowDebugViewMode.FinalShadowSourceTint)
            {
                EditorGUILayout.HelpBox(
                    "Final Shadow Source Tint 说明：蓝色 = 动态投影物体表面，绿色 = 静态投影物体表面。接收面的阴影保持原本的黑色，不再显示黄色重叠调试。",
                    MessageType.None
                );
                EditorGUILayout.HelpBox(
                    "Final Shadow Source Tint 仅对 Game Camera 生效。SceneView 和 Preview 相机会保持正常渲染，不参与这个调试视图。",
                    MessageType.Info
                );
                EditorGUILayout.HelpBox(
                    "Upload Main Light Cached Globals 不是阴影绘制 Pass。它只是把 cached shadow 用到的纹理和矩阵上传给后续材质采样，真正的采样发生在 Draw Opaque Objects 和 Draw Transparent Objects 阶段。",
                    MessageType.None
                );
                EditorGUILayout.HelpBox(
                    "可见投影物体的着色会遵循当前配置的 Static Caster Layer Mask 和 Dynamic Caster Layer Mask，但只对真正参与 ShadowCaster 路径的渲染器生效。没有投射阴影的 Unlit 物体会保持原来的 shader 表现。",
                    MessageType.None
                );
            }
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
