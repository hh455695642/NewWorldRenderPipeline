using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
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
        private const string kDefaultRendererDataName = "NWRP Default Renderer";
        private const string kGeneratedRendererDataPrefix = "NWRP Renderer Data ";

        private SerializedProperty _useSRPBatcherProperty;
        private SerializedProperty _useGPUInstancingProperty;
        private SerializedProperty _supportsHDRProperty;
        private SerializedProperty _hdrColorBufferPrecisionProperty;
        private SerializedProperty _supportsPostProcessingProperty;
        private SerializedProperty _enableRenderScaleProperty;
        private SerializedProperty _renderScaleProperty;
        private SerializedProperty _renderScaleFilterModeProperty;
        private SerializedProperty _mobileBandwidthProperty;
        private SerializedProperty _enableMobileFullscreenBudgetProperty;
        private SerializedProperty _mobileBloomMaxMipCountProperty;
        private SerializedProperty _mobileBloomMaxBaseSizeProperty;
        private SerializedProperty _mobileMaxAdditionalLightsProperty;
        private SerializedProperty _logFrameDebugStatsProperty;
        private SerializedProperty _rendererDataListProperty;
        private SerializedProperty _defaultRendererIndexProperty;
        private SerializedProperty _featureSettingsProperty;
        private SerializedProperty _featureListProperty;
        private SerializedProperty _featureOutlineProperty;
        private SerializedProperty _featureOpaqueTextureProperty;
        private SerializedProperty _featureDepthTextureProperty;
        private SerializedProperty _featureVegetationIndirectRenderingProperty;
        private SerializedProperty _featureVegetationIndirectShadowProperty;

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
        private SerializedProperty _additionalLightShadowFilterProperty;

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
        private SerializedProperty _enableCameraMotionInvalidationProperty;
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
        private SerializedProperty _additionalLightShadowFilterModeProperty;
        private SerializedProperty _additionalLightShadowFilterRadiusProperty;
        private SerializedProperty _enableOutlineProperty;
        private SerializedProperty _opaqueTexturePolicyProperty;
        private SerializedProperty _depthTexturePolicyProperty;
        private SerializedProperty _copyDepthModeProperty;
        private SerializedProperty _enableVegetationIndirectRenderingProperty;
        private SerializedProperty _enableVegetationIndirectTreeShadowsProperty;
        private ReorderableList _rendererDataList;

        private void OnEnable()
        {
            EnsureDefaultRendererDataForInspector(target as NewWorldRenderPipelineAsset);
            serializedObject.Update();

            _useSRPBatcherProperty = serializedObject.FindProperty("useSRPBatcher");
            _useGPUInstancingProperty = serializedObject.FindProperty("useGPUInstancing");
            _supportsHDRProperty = serializedObject.FindProperty("supportsHDR");
            _hdrColorBufferPrecisionProperty = serializedObject.FindProperty("hdrColorBufferPrecision");
            _supportsPostProcessingProperty = serializedObject.FindProperty("supportsPostProcessing");
            _enableRenderScaleProperty = serializedObject.FindProperty("enableRenderScale");
            _renderScaleProperty = serializedObject.FindProperty("renderScale");
            _renderScaleFilterModeProperty = serializedObject.FindProperty("renderScaleFilterMode");
            _mobileBandwidthProperty = serializedObject.FindProperty("mobileBandwidth");
            if (_mobileBandwidthProperty != null)
            {
                _enableMobileFullscreenBudgetProperty =
                    _mobileBandwidthProperty.FindPropertyRelative("enableMobileFullscreenBudget");
                _mobileBloomMaxMipCountProperty =
                    _mobileBandwidthProperty.FindPropertyRelative("bloomMaxMipCount");
                _mobileBloomMaxBaseSizeProperty =
                    _mobileBandwidthProperty.FindPropertyRelative("bloomMaxBaseSize");
                _mobileMaxAdditionalLightsProperty =
                    _mobileBandwidthProperty.FindPropertyRelative("maxAdditionalLights");
                _logFrameDebugStatsProperty =
                    _mobileBandwidthProperty.FindPropertyRelative("logFrameDebugStats");
            }
            _rendererDataListProperty = serializedObject.FindProperty("rendererDataList");
            _defaultRendererIndexProperty = serializedObject.FindProperty("defaultRendererIndex");
            CreateRendererDataList();

            _featureSettingsProperty = serializedObject.FindProperty("featureSettings");
            _featureOutlineProperty = _featureSettingsProperty.FindPropertyRelative("outline");
            _featureOpaqueTextureProperty = _featureSettingsProperty.FindPropertyRelative("opaqueTexture");
            _featureDepthTextureProperty = _featureSettingsProperty.FindPropertyRelative("depthTexture");
            _featureVegetationIndirectRenderingProperty =
                _featureSettingsProperty.FindPropertyRelative("vegetationIndirectRendering");
            _featureVegetationIndirectShadowProperty =
                _featureSettingsProperty.FindPropertyRelative("vegetationIndirectShadows");
            _featureListProperty = _featureSettingsProperty.FindPropertyRelative("features");

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
            _additionalLightShadowFilterProperty = _additionalLightShadowsProperty.FindPropertyRelative("filter");

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
            _enableCameraMotionInvalidationProperty =
                _mainLightShadowCachedProperty.FindPropertyRelative("enableCameraMotionInvalidation");
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
            _additionalLightShadowFilterModeProperty =
                _additionalLightShadowFilterProperty.FindPropertyRelative("additionalLightShadowFilterMode");
            _additionalLightShadowFilterRadiusProperty =
                _additionalLightShadowFilterProperty.FindPropertyRelative("additionalLightShadowFilterRadius");
            _enableOutlineProperty =
                _featureOutlineProperty.FindPropertyRelative("enableOutline");
            _opaqueTexturePolicyProperty =
                _featureOpaqueTextureProperty.FindPropertyRelative("texturePolicy");
            _depthTexturePolicyProperty =
                _featureDepthTextureProperty.FindPropertyRelative("texturePolicy");
            _copyDepthModeProperty =
                _featureDepthTextureProperty.FindPropertyRelative("copyDepthMode");
            if (_featureVegetationIndirectRenderingProperty != null)
            {
                _enableVegetationIndirectRenderingProperty =
                    _featureVegetationIndirectRenderingProperty.FindPropertyRelative(
                        "enableVegetationIndirectRendering");
            }

            if (_featureVegetationIndirectShadowProperty != null)
            {
                _enableVegetationIndirectTreeShadowsProperty =
                    _featureVegetationIndirectShadowProperty.FindPropertyRelative(
                        "enableVegetationIndirectTreeShadows");
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGeneralSettings();
            EditorGUILayout.Space();
            DrawShadowSettings();
            EditorGUILayout.Space();
            DrawRendererSettings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useSRPBatcherProperty);
            EditorGUILayout.PropertyField(_useGPUInstancingProperty);
            EditorGUILayout.PropertyField(
                _supportsHDRProperty,
                new GUIContent("Supports HDR"));
            using (new EditorGUI.DisabledScope(!_supportsHDRProperty.boolValue))
            {
                EditorGUILayout.PropertyField(
                    _hdrColorBufferPrecisionProperty,
                    new GUIContent("HDR Color Buffer Precision"));
            }

            if (_supportsHDRProperty.boolValue
                && _hdrColorBufferPrecisionProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.HDRColorBufferPrecision._64Bits)
            {
                EditorGUILayout.HelpBox(
                    "64-bit HDR uses a wider color buffer when supported. Prefer 32-bit on mobile unless banding is visible and profiling accepts the bandwidth cost.",
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(
                _supportsPostProcessingProperty,
                new GUIContent("Supports Post Processing"));
            if (!_supportsPostProcessingProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "NWRP camera post-processing passes are disabled at the pipeline capability level. Non-post-process Volume effects such as forward fog can still be sampled.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Render Scale");
            EditorGUILayout.PropertyField(
                _enableRenderScaleProperty,
                new GUIContent("Enable Render Scale"));
            using (new EditorGUI.DisabledScope(!_enableRenderScaleProperty.boolValue))
            {
                EditorGUILayout.PropertyField(
                    _renderScaleProperty,
                    new GUIContent("Render Scale"));
                EditorGUILayout.PropertyField(
                    _renderScaleFilterModeProperty,
                    new GUIContent("Upscale Filter"));
            }

            if (_enableRenderScaleProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Eligible Game cameras render into a scaled intermediate color/depth target. Mark UI cameras as Force Native on NWRPCameraData to keep Screen Space Camera UI sharp.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Mobile Bandwidth");
            if (_mobileBandwidthProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "The pipeline asset has not serialized the Mobile Bandwidth block yet. Reimport or resave the asset after script compilation.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(
                _enableMobileFullscreenBudgetProperty,
                new GUIContent("Enable Mobile Fullscreen Budget"));
            using (new EditorGUI.DisabledScope(!_enableMobileFullscreenBudgetProperty.boolValue))
            {
                EditorGUILayout.PropertyField(
                    _mobileBloomMaxMipCountProperty,
                    new GUIContent("Bloom Max Mips"));
                EditorGUILayout.PropertyField(
                    _mobileBloomMaxBaseSizeProperty,
                    new GUIContent("Bloom Max Base Size"));
                EditorGUILayout.PropertyField(
                    _mobileMaxAdditionalLightsProperty,
                    new GUIContent("Max Additional Lights"));
            }
            EditorGUILayout.PropertyField(
                _logFrameDebugStatsProperty,
                new GUIContent("Log Frame Debug Stats"));

            if (_enableMobileFullscreenBudgetProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Caps fullscreen bloom allocations for tile-based mobile GPUs. Keep this enabled for Android/iOS performance passes.",
                    MessageType.Info);
            }

            DrawMobileBandwidthRiskSummary();
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

        private void DrawRendererSettings()
        {
            EditorGUILayout.LabelField("Renderer List", EditorStyles.boldLabel);
            _rendererDataList?.DoLayoutList();

            NewWorldRenderPipelineAsset asset = target as NewWorldRenderPipelineAsset;
            if (asset == null)
            {
                return;
            }

            if (!asset.ValidateRendererDataList())
            {
                EditorGUILayout.HelpBox(
                    "No valid NWRP Renderer Data is assigned. Runtime will fall back to legacy Feature Settings until a renderer data asset is assigned.",
                    MessageType.Error);
            }
            else if (!asset.ValidateRendererData(-1))
            {
                EditorGUILayout.HelpBox(
                    "Default Renderer is missing. NWRP will fall back to the first valid renderer data entry.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Renderer Data owns feature/pass toggles and explicit NWRPFeature lists. Select a renderer data asset to edit its Feature Settings.",
                MessageType.None);
        }

        private void CreateRendererDataList()
        {
            _rendererDataList = new ReorderableList(
                serializedObject,
                _rendererDataListProperty,
                true,
                true,
                true,
                true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Renderers"),
                drawElementCallback = DrawRendererDataElement,
                onAddCallback = AddRendererDataElement,
                onRemoveCallback = RemoveRendererDataElement,
                onCanRemoveCallback = list => list.count > 1,
                onReorderCallbackWithDetails = (_, oldIndex, newIndex) =>
                    UpdateDefaultRendererIndexOnReorder(oldIndex, newIndex)
            };
        }

        private void DrawRendererDataElement(
            Rect rect,
            int index,
            bool isActive,
            bool isFocused)
        {
            rect.y += 2f;
            SerializedProperty element =
                _rendererDataListProperty.GetArrayElementAtIndex(index);
            Rect indexRect = new Rect(
                rect.x,
                rect.y,
                22f,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(indexRect, index.ToString());

            Rect objectRect = new Rect(
                rect.x + 24f,
                rect.y,
                rect.width - 150f,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(objectRect, element, GUIContent.none);

            Rect defaultRect = new Rect(
                rect.x + rect.width - 122f,
                rect.y,
                78f,
                EditorGUIUtility.singleLineHeight);
            bool isDefault = index == _defaultRendererIndexProperty.intValue;
            using (new EditorGUI.DisabledScope(isDefault))
            {
                if (GUI.Button(defaultRect, isDefault ? "Default" : "Set Default"))
                {
                    _defaultRendererIndexProperty.intValue = index;
                    EditorUtility.SetDirty(target);
                }
            }

            Rect selectRect = new Rect(
                rect.x + rect.width - 40f,
                rect.y,
                40f,
                EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(element.objectReferenceValue == null))
            {
                if (GUI.Button(selectRect, "Select"))
                {
                    Selection.activeObject = element.objectReferenceValue;
                }
            }
        }

        private void AddRendererDataElement(ReorderableList list)
        {
            serializedObject.ApplyModifiedProperties();

            NewWorldRenderPipelineAsset asset = target as NewWorldRenderPipelineAsset;
            NWRPRendererData rendererData = CreateRendererDataSubAsset(
                asset,
                $"NWRP Renderer Data {list.count}");

            serializedObject.Update();
            int newIndex = _rendererDataListProperty.arraySize;
            _rendererDataListProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty element =
                _rendererDataListProperty.GetArrayElementAtIndex(newIndex);
            element.objectReferenceValue = rendererData;
            _rendererDataList.index = newIndex;
            EditorUtility.SetDirty(target);
            serializedObject.ApplyModifiedProperties();
            SaveAndImportAsset(asset);
            serializedObject.Update();
        }

        private void RemoveRendererDataElement(ReorderableList list)
        {
            int removeIndex = list.index;
            if (removeIndex == _defaultRendererIndexProperty.intValue)
            {
                EditorUtility.DisplayDialog(
                    "Default Renderer",
                    "Cannot remove the default renderer. Set another renderer as default first.",
                    "Close");
                return;
            }

            Object rendererDataToRemove = _rendererDataListProperty
                .GetArrayElementAtIndex(removeIndex)
                .objectReferenceValue;
            NWRPRendererData ownedRendererDataToDestroy =
                IsOwnedRendererDataSubAsset(rendererDataToRemove)
                    ? rendererDataToRemove as NWRPRendererData
                    : null;

            Undo.RecordObject(target, $"Remove renderer at index {removeIndex}");
            int oldSize = _rendererDataListProperty.arraySize;
            _rendererDataListProperty.DeleteArrayElementAtIndex(removeIndex);
            if (_rendererDataListProperty.arraySize == oldSize)
            {
                _rendererDataListProperty.DeleteArrayElementAtIndex(removeIndex);
            }

            if (_defaultRendererIndexProperty.intValue > removeIndex)
            {
                _defaultRendererIndexProperty.intValue--;
            }

            _defaultRendererIndexProperty.intValue = Mathf.Clamp(
                _defaultRendererIndexProperty.intValue,
                0,
                Mathf.Max(_rendererDataListProperty.arraySize - 1, 0));
            EditorUtility.SetDirty(target);

            if (ownedRendererDataToDestroy != null)
            {
                serializedObject.ApplyModifiedProperties();
                DestroyOwnedRendererDataSubAsset(
                    target as NewWorldRenderPipelineAsset,
                    ownedRendererDataToDestroy);
                serializedObject.Update();
            }
        }

        private void UpdateDefaultRendererIndexOnReorder(int oldIndex, int newIndex)
        {
            int defaultIndex = _defaultRendererIndexProperty.intValue;
            if (defaultIndex == oldIndex)
            {
                _defaultRendererIndexProperty.intValue = newIndex;
            }
            else if (oldIndex < defaultIndex && newIndex >= defaultIndex)
            {
                _defaultRendererIndexProperty.intValue--;
            }
            else if (oldIndex > defaultIndex && newIndex <= defaultIndex)
            {
                _defaultRendererIndexProperty.intValue++;
            }

            EditorUtility.SetDirty(target);
        }

        private void DrawFeatureSettings()
        {
            EditorGUILayout.LabelField("Feature Settings", EditorStyles.boldLabel);
            DrawSubsectionLabel("Outline");
            EditorGUILayout.PropertyField(
                _enableOutlineProperty,
                new GUIContent("Enable Built-in Outline"));

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Opaque Texture");
            EditorGUILayout.PropertyField(
                _opaqueTexturePolicyProperty,
                new GUIContent("Camera Opaque Texture Policy"));
            if (IsTexturePolicyForce(_opaqueTexturePolicyProperty))
            {
                EditorGUILayout.HelpBox(
                    "Copies opaque color to _CameraOpaqueTexture before transparent rendering. Mobile cost is one full-screen copy and one full-resolution color RT.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Depth Texture");
            EditorGUILayout.PropertyField(
                _depthTexturePolicyProperty,
                new GUIContent("Camera Depth Texture Policy"));
            if (!IsTexturePolicyOff(_depthTexturePolicyProperty))
            {
                EditorGUILayout.PropertyField(
                    _copyDepthModeProperty,
                    new GUIContent("Camera Depth Texture Mode"));
            }

            if (IsTexturePolicyForce(_depthTexturePolicyProperty))
            {
                EditorGUILayout.HelpBox(
                    "Copies or pre-renders opaque depth to _CameraDepthTexture. After Opaques is required when transparent materials sample scene depth; Force Prepass depends on DepthOnly passes.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Vegetation Indirect Rendering");
            if (_enableVegetationIndirectRenderingProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "The pipeline asset has not serialized the Vegetation Indirect Rendering block yet. Reimport or resave the asset after script compilation.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.PropertyField(
                    _enableVegetationIndirectRenderingProperty,
                    new GUIContent("Enable Vegetation Indirect Rendering"));
                if (_enableVegetationIndirectRenderingProperty.boolValue)
                {
                    if (_enableVegetationIndirectTreeShadowsProperty == null)
                    {
                        EditorGUILayout.HelpBox(
                            "The pipeline asset has not serialized the Vegetation Indirect Shadows block yet. Reimport or resave the asset after script compilation.",
                            MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(
                            _enableVegetationIndirectTreeShadowsProperty,
                            new GUIContent("Enable Vegetation Indirect Tree Shadows"));
                        if (_enableVegetationIndirectTreeShadowsProperty.boolValue)
                        {
                            EditorGUILayout.HelpBox(
                                "Adds GPU indirect Tree/TreeLeaf ShadowCaster draws to the main-light shadow atlas. Additional light shadows stay on the regular renderer path.",
                                MessageType.Info);
                        }
                    }

                    EditorGUILayout.HelpBox(
                        "Scene VegetationIndirectRenderer components use GPU culling and Graphics.RenderMeshIndirect for visible vegetation. Disable this to keep source MeshRenderers as the fallback path.",
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Explicit Features");
            EditorGUILayout.PropertyField(_featureListProperty, true);
        }

        internal static NWRPRendererData EnsureDefaultRendererDataForInspector(
            NewWorldRenderPipelineAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            if (asset.ValidateRendererDataList())
            {
                NWRPRendererData namedDefault =
                    FindRendererDataSubAsset(assetPath, kDefaultRendererDataName);
                if (RepairNamedDefaultRendererReference(asset, namedDefault))
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceUpdate);
                }

                NWRPRendererData existing = asset.GetRendererData(-1);
                EditorUtility.SetDirty(asset);
                return existing;
            }

            Undo.RecordObject(asset, "Create NWRP Default Renderer Data");
            NWRPRendererData rendererData =
                FindRendererDataSubAsset(assetPath, kDefaultRendererDataName)
                ?? FindRendererDataSubAsset(assetPath)
                ?? CreateRendererDataSubAsset(asset, kDefaultRendererDataName);
            if (rendererData == null)
            {
                return null;
            }

            rendererData.CopyFeatureSettingsFrom(asset.featureSettings);
            asset.rendererDataList = new[] { rendererData };
            asset.defaultRendererIndex = 0;

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return rendererData;
        }

        private static NWRPRendererData CreateRendererDataSubAsset(
            NewWorldRenderPipelineAsset asset,
            string rendererName)
        {
            if (asset == null)
            {
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            NWRPRendererData rendererData =
                ScriptableObject.CreateInstance<NWRPRendererData>();
            rendererData.name = rendererName;
            AssetDatabase.AddObjectToAsset(rendererData, asset);
            Undo.RegisterCreatedObjectUndo(rendererData, $"Create {rendererName}");
            EditorUtility.SetDirty(rendererData);
            return rendererData;
        }

        private bool IsOwnedRendererDataSubAsset(Object rendererData)
        {
            return IsOwnedRendererDataSubAsset(target, rendererData);
        }

        private static bool IsOwnedRendererDataSubAsset(
            Object owner,
            Object rendererData)
        {
            if (rendererData == null || owner == null)
            {
                return false;
            }

            return rendererData is NWRPRendererData
                && AssetDatabase.IsSubAsset(rendererData)
                && AssetDatabase.GetAssetPath(rendererData)
                    == AssetDatabase.GetAssetPath(owner);
        }

        internal static void DestroyOwnedRendererDataSubAsset(
            NewWorldRenderPipelineAsset asset,
            NWRPRendererData rendererData)
        {
            if (!IsOwnedRendererDataSubAsset(asset, rendererData))
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            List<NWRPFeature> ownedFeatures =
                CollectDestroyableOwnedFeatures(rendererData, assetPath);
            for (int i = 0; i < ownedFeatures.Count; i++)
            {
                Undo.DestroyObjectImmediate(ownedFeatures[i]);
            }

            Undo.DestroyObjectImmediate(rendererData);
            SaveAndImportAsset(assetPath);
        }

        private static List<NWRPFeature> CollectDestroyableOwnedFeatures(
            NWRPRendererData rendererData,
            string assetPath)
        {
            List<NWRPFeature> ownedFeatures = new List<NWRPFeature>();
            List<NWRPFeature> features = rendererData.Features;
            for (int i = 0; i < features.Count; i++)
            {
                NWRPFeature feature = features[i];
                if (feature == null
                    || !AssetDatabase.IsSubAsset(feature)
                    || AssetDatabase.GetAssetPath(feature) != assetPath
                    || IsFeatureReferencedByOtherRendererDataAtPath(
                        feature,
                        assetPath,
                        rendererData)
                    || ownedFeatures.Contains(feature))
                {
                    continue;
                }

                ownedFeatures.Add(feature);
            }

            return ownedFeatures;
        }

        private static bool IsFeatureReferencedByOtherRendererDataAtPath(
            NWRPFeature feature,
            string assetPath,
            NWRPRendererData rendererDataToSkip)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not NWRPRendererData rendererData
                    || rendererData == rendererDataToSkip)
                {
                    continue;
                }

                if (rendererData.Features.Contains(feature))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SaveAndImportAsset(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            SaveAndImportAsset(AssetDatabase.GetAssetPath(asset));
        }

        private static void SaveAndImportAsset(string assetPath)
        {
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate);
            }
        }

        private static bool RepairNamedDefaultRendererReference(
            NewWorldRenderPipelineAsset asset,
            NWRPRendererData namedDefault)
        {
            if (asset == null
                || namedDefault == null
                || asset.rendererDataList == null
                || asset.rendererDataList.Length == 0)
            {
                return false;
            }

            int namedDefaultIndex =
                IndexOfRendererData(asset.rendererDataList, namedDefault);
            int defaultIndex = Mathf.Clamp(
                asset.defaultRendererIndex,
                0,
                asset.rendererDataList.Length - 1);
            if (namedDefaultIndex == defaultIndex)
            {
                return false;
            }

            NWRPRendererData currentDefault =
                asset.rendererDataList[defaultIndex];
            if (currentDefault == null
                || !currentDefault.name.StartsWith(
                    kGeneratedRendererDataPrefix,
                    System.StringComparison.Ordinal))
            {
                return false;
            }

            if (namedDefaultIndex >= 0)
            {
                asset.defaultRendererIndex = namedDefaultIndex;
                return true;
            }

            asset.rendererDataList[defaultIndex] = namedDefault;
            AppendRendererData(asset, currentDefault);
            asset.defaultRendererIndex = defaultIndex;
            return true;
        }

        private static int IndexOfRendererData(
            NWRPRendererData[] rendererDataList,
            NWRPRendererData rendererData)
        {
            for (int i = 0; i < rendererDataList.Length; i++)
            {
                if (rendererDataList[i] == rendererData)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AppendRendererData(
            NewWorldRenderPipelineAsset asset,
            NWRPRendererData rendererData)
        {
            if (asset == null || rendererData == null)
            {
                return;
            }

            if (IndexOfRendererData(asset.rendererDataList, rendererData) >= 0)
            {
                return;
            }

            int oldLength = asset.rendererDataList.Length;
            System.Array.Resize(ref asset.rendererDataList, oldLength + 1);
            asset.rendererDataList[oldLength] = rendererData;
        }

        private static NWRPRendererData FindRendererDataSubAsset(
            string assetPath,
            string rendererName = null)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is NWRPRendererData rendererData)
                {
                    if (string.IsNullOrEmpty(rendererName)
                        || rendererData.name == rendererName)
                    {
                        return rendererData;
                    }
                }
            }

            return null;
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
                EditorGUILayout.PropertyField(
                    _enableCameraMotionInvalidationProperty,
                    new GUIContent("Camera Motion Invalidates Cache"));
                if (_enableCameraMotionInvalidationProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(_cameraPositionInvalidationThresholdProperty);
                    EditorGUILayout.PropertyField(_cameraRotationInvalidationThresholdProperty);
                }

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
                    "When Medium PCF and the dynamic shadow overlay are both enabled, receivers still sample the combined main-light atlas once. The extra mobile cost is the per-frame atlas copy and dynamic caster shadow draw.",
                    MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "Moving static shadow casters does not rebuild the cached atlas automatically. Call MarkMainLightShadowCacheDirty() when the cached static region must refresh.",
                MessageType.Info);

            if (_enableCameraMotionInvalidationProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Camera Motion Invalidates Cache can make cached dynamic shadows visibly jump while the Game Camera moves. Keep it disabled for stable cached dynamic overlays.",
                    MessageType.Warning);
            }

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

            bool useMediumPCF = _additionalLightShadowFilterModeProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.AdditionalLightShadowFilterMode.MediumPCF;

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

            DrawSubsectionLabel("Filter");
            EditorGUILayout.PropertyField(
                _additionalLightShadowFilterModeProperty,
                new GUIContent("Shadow Filter Mode"));
            if (useMediumPCF)
            {
                EditorGUILayout.PropertyField(
                    _additionalLightShadowFilterRadiusProperty,
                    new GUIContent("Shadow Filter Radius"));
            }

            DrawSubsectionLabel("Bias");
            EditorGUILayout.PropertyField(_additionalLightShadowBiasValueProperty);
            EditorGUILayout.PropertyField(_additionalLightShadowNormalBiasProperty);
            EditorGUILayout.PropertyField(
                _additionalLightShadowCasterCullModeProperty,
                new GUIContent("Shadow Caster Cull Mode"));

            EditorGUILayout.HelpBox(
                "Spot lights consume one shadow slice and point lights consume six shadow slices in the shared atlas. Requested Tile Resolution controls per-slice quality; Atlas Max Size caps the total mobile shadow texture budget. Medium PCF costs 9 shadow compares per shadowed punctual light receiver sample.",
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

        private void DrawMobileBandwidthRiskSummary()
        {
            List<string> riskItems = new List<string>();
            if (_supportsHDRProperty.boolValue)
            {
                riskItems.Add("HDR color");
            }

            if (_supportsPostProcessingProperty.boolValue)
            {
                riskItems.Add("post-processing");
            }

            if (_enableRenderScaleProperty.boolValue)
            {
                riskItems.Add("render scale intermediate");
            }

            if (IsTexturePolicyForce(_opaqueTexturePolicyProperty))
            {
                riskItems.Add("forced opaque texture");
            }

            if (IsTexturePolicyForce(_depthTexturePolicyProperty))
            {
                riskItems.Add("forced depth texture");
            }

            if (_mainLightShadowFilterModeProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.MainLightShadowFilterMode.MediumPCF)
            {
                riskItems.Add("main-light Medium PCF");
            }

            if (_enableAdditionalLightShadowsProperty.boolValue)
            {
                riskItems.Add("additional light shadows");
            }

            if (_additionalLightShadowFilterModeProperty.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.AdditionalLightShadowFilterMode.MediumPCF)
            {
                riskItems.Add("additional-light Medium PCF");
            }

            if (riskItems.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Estimated mobile bandwidth risk: "
                    + string.Join(", ", riskItems)
                    + ". Keep these explicit for lookdev only, then verify with Frame Debugger/RenderDoc on device.",
                MessageType.Warning);
        }

        private static bool IsTexturePolicyForce(SerializedProperty property)
        {
            return property != null
                && property.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.CameraTexturePolicy.Force;
        }

        private static bool IsTexturePolicyOff(SerializedProperty property)
        {
            return property == null
                || property.enumValueIndex
                == (int)NewWorldRenderPipelineAsset.CameraTexturePolicy.Off;
        }
    }
}
