using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPRendererData))]
    public sealed class NWRPRendererDataEditor : UnityEditor.Editor
    {
        private const string kFeatureEnabledPropertyName = "isEnabled";
        private const string kValleyHeightFogFeatureName = "Valley Height Fog Feature";
        private const string kValleyHeightFogOverlayFeatureName = "Valley Height Fog Overlay Feature";
        private const string kCloudShadowProjectorFeatureName = "Cloud Shadow Projector Feature";
        private const string kScreenBlurFeatureName = "Screen Blur Feature";
        private const float kFeatureRowDragHandleWidth = 18f;

        private SerializedProperty _filteringProperty;
        private SerializedProperty _opaqueLayerMaskProperty;
        private SerializedProperty _transparentLayerMaskProperty;
        private SerializedProperty _featureSettingsProperty;
        private SerializedProperty _featureListProperty;
        private SerializedProperty _featureOutlineProperty;
        private SerializedProperty _featureOpaqueTextureProperty;
        private SerializedProperty _featureDepthTextureProperty;
        private SerializedProperty _featureVegetationIndirectShadowProperty;
        private SerializedProperty _enableOutlineProperty;
        private SerializedProperty _enableOpaqueTextureProperty;
        private SerializedProperty _enableDepthTextureProperty;
        private SerializedProperty _copyDepthModeProperty;
        private SerializedProperty _enableVegetationIndirectTreeShadowsProperty;
        private ReorderableList _featureReorderableList;
        private readonly HashSet<Object> _expandedFeatures = new HashSet<Object>();

        private void OnEnable()
        {
            _filteringProperty = serializedObject.FindProperty("filtering");
            _opaqueLayerMaskProperty =
                _filteringProperty.FindPropertyRelative("opaqueLayerMask");
            _transparentLayerMaskProperty =
                _filteringProperty.FindPropertyRelative("transparentLayerMask");
            _featureSettingsProperty = serializedObject.FindProperty("featureSettings");
            _featureOutlineProperty =
                _featureSettingsProperty.FindPropertyRelative("outline");
            _featureOpaqueTextureProperty =
                _featureSettingsProperty.FindPropertyRelative("opaqueTexture");
            _featureDepthTextureProperty =
                _featureSettingsProperty.FindPropertyRelative("depthTexture");
            _featureVegetationIndirectShadowProperty =
                _featureSettingsProperty.FindPropertyRelative("vegetationIndirectShadows");
            _featureListProperty =
                _featureSettingsProperty.FindPropertyRelative("features");

            _enableOutlineProperty =
                _featureOutlineProperty.FindPropertyRelative("enableOutline");
            _enableOpaqueTextureProperty =
                _featureOpaqueTextureProperty.FindPropertyRelative("enableOpaqueTexture");
            _enableDepthTextureProperty =
                _featureDepthTextureProperty.FindPropertyRelative("enableDepthTexture");
            _copyDepthModeProperty =
                _featureDepthTextureProperty.FindPropertyRelative("copyDepthMode");
            _enableVegetationIndirectTreeShadowsProperty =
                _featureVegetationIndirectShadowProperty.FindPropertyRelative(
                    "enableVegetationIndirectTreeShadows");

            CreateFeatureList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Filtering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _opaqueLayerMaskProperty,
                new GUIContent(
                    "Opaque Layer Mask",
                    "Controls which opaque layers this renderer draws."));
            EditorGUILayout.PropertyField(
                _transparentLayerMaskProperty,
                new GUIContent(
                    "Transparent Layer Mask",
                    "Controls which transparent layers this renderer draws."));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Feature Settings", EditorStyles.boldLabel);
            DrawSubsectionLabel("Outline");
            EditorGUILayout.PropertyField(
                _enableOutlineProperty,
                new GUIContent("Enable Built-in Outline"));

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Opaque Texture");
            EditorGUILayout.PropertyField(
                _enableOpaqueTextureProperty,
                new GUIContent("Enable Camera Opaque Texture"));
            if (_enableOpaqueTextureProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Copies opaque color to _CameraOpaqueTexture before transparent rendering. Mobile cost is one full-screen copy and one full-resolution color RT.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Depth Texture");
            EditorGUILayout.PropertyField(
                _enableDepthTextureProperty,
                new GUIContent("Enable Camera Depth Texture"));
            if (_enableDepthTextureProperty.boolValue)
            {
                EditorGUILayout.PropertyField(
                    _copyDepthModeProperty,
                    new GUIContent("Camera Depth Texture Mode"));
                EditorGUILayout.HelpBox(
                    "Copies or pre-renders opaque depth to _CameraDepthTexture. After Opaques is required when transparent materials sample scene depth; Force Prepass depends on DepthOnly passes.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Vegetation Indirect Shadows");
            EditorGUILayout.PropertyField(
                _enableVegetationIndirectTreeShadowsProperty,
                new GUIContent("Enable Vegetation Indirect Tree Shadows"));
            if (_enableVegetationIndirectTreeShadowsProperty.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Adds GPU indirect Tree/TreeLeaf ShadowCaster draws to the main-light shadow atlas. Additional light shadows stay on the regular renderer path.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(2f);
            DrawSubsectionLabel("Explicit Features");
            EditorGUILayout.HelpBox(
                "Only NWRPFeature assets referenced here are enqueued by this renderer data. Use Add Feature so renderer-local feature assets are owned and deleted with this list.",
                MessageType.Info);
            _featureReorderableList.DoLayoutList();
            DrawAddFeatureButton();

            serializedObject.ApplyModifiedProperties();
        }

        private void CreateFeatureList()
        {
            _featureReorderableList = new ReorderableList(
                serializedObject,
                _featureListProperty,
                true,
                true,
                false,
                false)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Features"),
                drawElementCallback = DrawFeatureElement,
                elementHeightCallback = GetFeatureElementHeight,
                onReorderCallback = _ => EditorUtility.SetDirty(target)
            };
        }

        private void DrawFeatureElement(
            Rect rect,
            int index,
            bool isActive,
            bool isFocused)
        {
            SerializedProperty element =
                _featureListProperty.GetArrayElementAtIndex(index);
            NWRPFeature feature = element.objectReferenceValue as NWRPFeature;
            rect.y += 2f;

            Rect rowRect = new Rect(
                rect.x,
                rect.y,
                rect.width,
                EditorGUIUtility.singleLineHeight);
            DrawFeatureHeader(rowRect, element, feature, index);

            if (feature != null && IsExpanded(feature))
            {
                Rect settingsRect = new Rect(
                    rect.x + kFeatureRowDragHandleWidth + 22f,
                    rowRect.yMax + 4f,
                    rect.width - kFeatureRowDragHandleWidth - 22f,
                    GetFeatureSettingsHeight(feature));
                DrawFeatureSettings(settingsRect, feature);
            }
        }

        private void DrawFeatureHeader(
            Rect rect,
            SerializedProperty element,
            NWRPFeature feature,
            int index)
        {
            float contentX = rect.x + kFeatureRowDragHandleWidth;

            Rect foldoutRect = new Rect(
                contentX,
                rect.y,
                16f,
                EditorGUIUtility.singleLineHeight);
            if (feature != null)
            {
                bool expanded = EditorGUI.Foldout(
                    foldoutRect,
                    IsExpanded(feature),
                    GUIContent.none,
                    true);
                SetExpanded(feature, expanded);
            }

            Rect toggleRect = new Rect(
                contentX + 20f,
                rect.y,
                18f,
                EditorGUIUtility.singleLineHeight);
            DrawFeatureEnabledToggle(toggleRect, feature);

            Rect objectRect = new Rect(
                contentX + 42f,
                rect.y,
                rect.width - kFeatureRowDragHandleWidth - 136f,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            Object assigned = EditorGUI.ObjectField(
                objectRect,
                element.objectReferenceValue,
                typeof(NWRPFeature),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                NWRPFeature assignedFeature = assigned as NWRPFeature;
                if (CanAssignFeatureAt(assignedFeature, index))
                {
                    element.objectReferenceValue = assignedFeature;
                    if (assignedFeature != null)
                    {
                        SetExpanded(assignedFeature, true);
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Duplicate Feature",
                        "This renderer data already contains a "
                            + GetDuplicateFeatureDisplayName(assignedFeature)
                            + " feature.",
                        "Close");
                }
            }

            Rect selectRect = new Rect(
                rect.x + rect.width - 88f,
                rect.y,
                52f,
                EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(feature == null))
            {
                if (GUI.Button(selectRect, "Select"))
                {
                    Selection.activeObject = feature;
                }
            }

            Rect removeRect = new Rect(
                rect.x + rect.width - 30f,
                rect.y,
                28f,
                EditorGUIUtility.singleLineHeight);
            if (GUI.Button(removeRect, "-"))
            {
                serializedObject.ApplyModifiedProperties();
                RemoveFeatureAt(target as NWRPRendererData, index);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawFeatureEnabledToggle(Rect rect, NWRPFeature feature)
        {
            if (feature == null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.Toggle(rect, false);
                }
                return;
            }

            SerializedObject featureObject = new SerializedObject(feature);
            SerializedProperty enabledProperty =
                featureObject.FindProperty(kFeatureEnabledPropertyName);
            if (enabledProperty == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(rect, enabledProperty, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                featureObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(feature);
            }
        }

        private float GetFeatureElementHeight(int index)
        {
            if (index < 0 || index >= _featureListProperty.arraySize)
            {
                return EditorGUIUtility.singleLineHeight + 4f;
            }

            SerializedProperty element =
                _featureListProperty.GetArrayElementAtIndex(index);
            NWRPFeature feature = element.objectReferenceValue as NWRPFeature;
            float height = EditorGUIUtility.singleLineHeight + 4f;
            if (feature != null && IsExpanded(feature))
            {
                height += GetFeatureSettingsHeight(feature) + 4f;
            }

            return height;
        }

        private static float GetFeatureSettingsHeight(NWRPFeature feature)
        {
            int propertyCount = 0;
            float height = 0f;
            SerializedObject featureObject = new SerializedObject(feature);
            SerializedProperty iterator = featureObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (ShouldSkipFeatureProperty(iterator))
                {
                    continue;
                }

                propertyCount++;
                height += EditorGUI.GetPropertyHeight(iterator, true)
                    + EditorGUIUtility.standardVerticalSpacing;
            }

            if (propertyCount == 0)
            {
                return EditorGUIUtility.singleLineHeight * 2.5f;
            }

            return height;
        }

        private static void DrawFeatureSettings(Rect rect, NWRPFeature feature)
        {
            SerializedObject featureObject = new SerializedObject(feature);
            SerializedProperty iterator = featureObject.GetIterator();
            bool enterChildren = true;
            int propertyCount = 0;
            float y = rect.y;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (ShouldSkipFeatureProperty(iterator))
                {
                    continue;
                }

                float height = EditorGUI.GetPropertyHeight(iterator, true);
                Rect propertyRect = new Rect(rect.x, y, rect.width, height);
                EditorGUI.PropertyField(propertyRect, iterator, true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
                propertyCount++;
            }

            if (propertyCount == 0)
            {
                string message =
                    feature is ValleyHeightFogFeature
                    || feature is CloudShadowProjectorFeature
                    || feature is NWRPScreenBlurFeature
                        ? "No renderer-local settings. This feature's parameters are controlled by Volumes."
                        : "No renderer-local settings.";
                EditorGUI.HelpBox(rect, message, MessageType.Info);
            }

            featureObject.ApplyModifiedProperties();
        }

        private static bool ShouldSkipFeatureProperty(SerializedProperty property)
        {
            return property.name == "m_Script"
                || property.name == kFeatureEnabledPropertyName;
        }

        private void DrawAddFeatureButton()
        {
            NWRPRendererData rendererData = target as NWRPRendererData;
            string assetPath = AssetDatabase.GetAssetPath(rendererData);
            bool canCreateSubAsset =
                rendererData != null && !string.IsNullOrEmpty(assetPath);

            using (new EditorGUI.DisabledScope(!canCreateSubAsset))
            {
                if (GUILayout.Button("Add Feature"))
                {
                    GenericMenu menu = new GenericMenu();
                    bool hasValleyHeightFog =
                        IndexOfFeature<ValleyHeightFogFeature>(rendererData) >= 0;
                    bool hasValleyHeightFogOverlay =
                        IndexOfFeature<ValleyHeightFogOverlayFeature>(rendererData) >= 0;
                    bool hasCloudShadowProjector =
                        IndexOfFeature<CloudShadowProjectorFeature>(rendererData) >= 0;
                    bool hasScreenBlur =
                        IndexOfFeature<NWRPScreenBlurFeature>(rendererData) >= 0;
                    if (hasValleyHeightFog)
                    {
                        menu.AddDisabledItem(
                            new GUIContent("Valley Height Fog"));
                    }
                    else
                    {
                        menu.AddItem(
                            new GUIContent("Valley Height Fog"),
                            false,
                            AddValleyHeightFogFeatureFromMenu);
                    }

                    if (hasValleyHeightFogOverlay)
                    {
                        menu.AddDisabledItem(
                            new GUIContent("Valley Height Fog Overlay"));
                    }
                    else
                    {
                        menu.AddItem(
                            new GUIContent("Valley Height Fog Overlay"),
                            false,
                            AddValleyHeightFogOverlayFeatureFromMenu);
                    }

                    if (hasCloudShadowProjector)
                    {
                        menu.AddDisabledItem(
                            new GUIContent("Cloud Shadow Projector"));
                    }
                    else
                    {
                        menu.AddItem(
                            new GUIContent("Cloud Shadow Projector"),
                            false,
                            AddCloudShadowProjectorFeatureFromMenu);
                    }

                    if (hasScreenBlur)
                    {
                        menu.AddDisabledItem(
                            new GUIContent("Screen Blur"));
                    }
                    else
                    {
                        menu.AddItem(
                            new GUIContent("Screen Blur"),
                            false,
                            AddScreenBlurFeatureFromMenu);
                    }

                    menu.ShowAsContext();
                }
            }

            if (!canCreateSubAsset)
            {
                EditorGUILayout.HelpBox(
                    "Save this Renderer Data asset before adding renderer-local feature sub-assets.",
                    MessageType.Warning);
            }
        }

        private void AddValleyHeightFogFeatureFromMenu()
        {
            serializedObject.ApplyModifiedProperties();
            NWRPRendererData rendererData = target as NWRPRendererData;
            ValleyHeightFogFeature feature = AddValleyHeightFogFeature(rendererData);
            serializedObject.Update();
            if (feature == null)
            {
                return;
            }

            int index = IndexOfFeature(rendererData, feature);
            if (index >= 0)
            {
                _featureReorderableList.index = index;
                SetExpanded(feature, true);
            }
        }

        private void AddValleyHeightFogOverlayFeatureFromMenu()
        {
            serializedObject.ApplyModifiedProperties();
            NWRPRendererData rendererData = target as NWRPRendererData;
            ValleyHeightFogOverlayFeature feature =
                AddValleyHeightFogOverlayFeature(rendererData);
            serializedObject.Update();
            if (feature == null)
            {
                return;
            }

            int index = IndexOfFeature(rendererData, feature);
            if (index >= 0)
            {
                _featureReorderableList.index = index;
                SetExpanded(feature, true);
            }
        }

        private void AddCloudShadowProjectorFeatureFromMenu()
        {
            serializedObject.ApplyModifiedProperties();
            NWRPRendererData rendererData = target as NWRPRendererData;
            CloudShadowProjectorFeature feature =
                AddCloudShadowProjectorFeature(rendererData);
            serializedObject.Update();
            if (feature == null)
            {
                return;
            }

            int index = IndexOfFeature(rendererData, feature);
            if (index >= 0)
            {
                _featureReorderableList.index = index;
                SetExpanded(feature, true);
            }
        }

        private void AddScreenBlurFeatureFromMenu()
        {
            serializedObject.ApplyModifiedProperties();
            NWRPRendererData rendererData = target as NWRPRendererData;
            NWRPScreenBlurFeature feature = AddScreenBlurFeature(rendererData);
            serializedObject.Update();
            if (feature == null)
            {
                return;
            }

            int index = IndexOfFeature(rendererData, feature);
            if (index >= 0)
            {
                _featureReorderableList.index = index;
                SetExpanded(feature, true);
            }
        }

        internal static ValleyHeightFogOverlayFeature AddValleyHeightFogOverlayFeature(
            NWRPRendererData rendererData)
        {
            if (rendererData == null)
            {
                return null;
            }

            int existingIndex =
                IndexOfFeature<ValleyHeightFogOverlayFeature>(rendererData);
            if (existingIndex >= 0)
            {
                return rendererData.Features[existingIndex]
                    as ValleyHeightFogOverlayFeature;
            }

            string assetPath = AssetDatabase.GetAssetPath(rendererData);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Undo.RecordObject(rendererData, "Add Valley Height Fog Overlay Feature");
            ValleyHeightFogOverlayFeature feature =
                ScriptableObject.CreateInstance<ValleyHeightFogOverlayFeature>();
            feature.name = kValleyHeightFogOverlayFeatureName;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            Undo.RegisterCreatedObjectUndo(
                feature,
                "Add Valley Height Fog Overlay Feature");

            rendererData.Features.Add(feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return feature;
        }

        internal static ValleyHeightFogFeature AddValleyHeightFogFeature(
            NWRPRendererData rendererData)
        {
            if (rendererData == null)
            {
                return null;
            }

            int existingIndex =
                IndexOfFeature<ValleyHeightFogFeature>(rendererData);
            if (existingIndex >= 0)
            {
                return rendererData.Features[existingIndex]
                    as ValleyHeightFogFeature;
            }

            string assetPath = AssetDatabase.GetAssetPath(rendererData);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Undo.RecordObject(rendererData, "Add Valley Height Fog Feature");
            ValleyHeightFogFeature feature =
                ScriptableObject.CreateInstance<ValleyHeightFogFeature>();
            feature.name = kValleyHeightFogFeatureName;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            Undo.RegisterCreatedObjectUndo(
                feature,
                "Add Valley Height Fog Feature");

            rendererData.Features.Add(feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return feature;
        }

        internal static NWRPScreenBlurFeature AddScreenBlurFeature(
            NWRPRendererData rendererData)
        {
            if (rendererData == null)
            {
                return null;
            }

            int existingIndex =
                IndexOfFeature<NWRPScreenBlurFeature>(rendererData);
            if (existingIndex >= 0)
            {
                return rendererData.Features[existingIndex]
                    as NWRPScreenBlurFeature;
            }

            string assetPath = AssetDatabase.GetAssetPath(rendererData);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Undo.RecordObject(rendererData, "Add Screen Blur Feature");
            NWRPScreenBlurFeature feature =
                ScriptableObject.CreateInstance<NWRPScreenBlurFeature>();
            feature.name = kScreenBlurFeatureName;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            Undo.RegisterCreatedObjectUndo(
                feature,
                "Add Screen Blur Feature");

            rendererData.Features.Add(feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return feature;
        }

        internal static CloudShadowProjectorFeature AddCloudShadowProjectorFeature(
            NWRPRendererData rendererData)
        {
            if (rendererData == null)
            {
                return null;
            }

            int existingIndex =
                IndexOfFeature<CloudShadowProjectorFeature>(rendererData);
            if (existingIndex >= 0)
            {
                return rendererData.Features[existingIndex]
                    as CloudShadowProjectorFeature;
            }

            string assetPath = AssetDatabase.GetAssetPath(rendererData);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Undo.RecordObject(rendererData, "Add Cloud Shadow Projector Feature");
            CloudShadowProjectorFeature feature =
                ScriptableObject.CreateInstance<CloudShadowProjectorFeature>();
            feature.name = kCloudShadowProjectorFeatureName;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            Undo.RegisterCreatedObjectUndo(
                feature,
                "Add Cloud Shadow Projector Feature");

            rendererData.Features.Add(feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return feature;
        }

        internal static void RemoveFeatureAt(
            NWRPRendererData rendererData,
            int index)
        {
            if (rendererData == null
                || index < 0
                || index >= rendererData.Features.Count)
            {
                return;
            }

            NWRPFeature feature = rendererData.Features[index];
            Undo.RecordObject(rendererData, "Remove NWRP Feature");
            rendererData.Features.RemoveAt(index);
            EditorUtility.SetDirty(rendererData);

            if (ShouldDestroyOwnedFeature(rendererData, feature))
            {
                string assetPath = AssetDatabase.GetAssetPath(rendererData);
                Undo.DestroyObjectImmediate(feature);
                AssetDatabase.SaveAssets();
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private bool CanAssignFeatureAt(NWRPFeature feature, int index)
        {
            if (feature == null)
            {
                return true;
            }

            NWRPRendererData rendererData = target as NWRPRendererData;
            if (feature is ValleyHeightFogFeature)
            {
                int existingIndex =
                    IndexOfFeature<ValleyHeightFogFeature>(rendererData);
                return existingIndex < 0 || existingIndex == index;
            }

            if (feature is ValleyHeightFogOverlayFeature)
            {
                int existingIndex =
                    IndexOfFeature<ValleyHeightFogOverlayFeature>(rendererData);
                return existingIndex < 0 || existingIndex == index;
            }

            if (feature is CloudShadowProjectorFeature)
            {
                int existingIndex =
                    IndexOfFeature<CloudShadowProjectorFeature>(rendererData);
                return existingIndex < 0 || existingIndex == index;
            }

            if (feature is NWRPScreenBlurFeature)
            {
                int existingIndex =
                    IndexOfFeature<NWRPScreenBlurFeature>(rendererData);
                return existingIndex < 0 || existingIndex == index;
            }

            return true;
        }

        private static string GetDuplicateFeatureDisplayName(NWRPFeature feature)
        {
            if (feature is NWRPScreenBlurFeature)
            {
                return "Screen Blur";
            }

            if (feature is ValleyHeightFogOverlayFeature)
            {
                return "Valley Height Fog Overlay";
            }

            if (feature is CloudShadowProjectorFeature)
            {
                return "Cloud Shadow Projector";
            }

            return "Valley Height Fog";
        }

        private static bool ShouldDestroyOwnedFeature(
            NWRPRendererData rendererData,
            NWRPFeature feature)
        {
            if (rendererData == null
                || feature == null
                || !AssetDatabase.IsSubAsset(feature))
            {
                return false;
            }

            string rendererDataPath = AssetDatabase.GetAssetPath(rendererData);
            string featurePath = AssetDatabase.GetAssetPath(feature);
            if (string.IsNullOrEmpty(rendererDataPath)
                || rendererDataPath != featurePath)
            {
                return false;
            }

            return !IsFeatureReferencedByRendererDataAtPath(
                feature,
                featurePath);
        }

        private static bool IsFeatureReferencedByRendererDataAtPath(
            NWRPFeature feature,
            string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not NWRPRendererData rendererData)
                {
                    continue;
                }

                if (IndexOfFeature(rendererData, feature) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsExpanded(Object feature)
        {
            return feature != null && _expandedFeatures.Contains(feature);
        }

        private void SetExpanded(Object feature, bool expanded)
        {
            if (feature == null)
            {
                return;
            }

            if (expanded)
            {
                _expandedFeatures.Add(feature);
            }
            else
            {
                _expandedFeatures.Remove(feature);
            }
        }

        private static string GetFeatureDisplayName(NWRPFeature feature)
        {
            return feature != null && !string.IsNullOrEmpty(feature.name)
                ? feature.name
                : "Missing Feature";
        }

        private static int IndexOfFeature<T>(NWRPRendererData rendererData)
            where T : NWRPFeature
        {
            if (rendererData == null)
            {
                return -1;
            }

            List<NWRPFeature> features = rendererData.Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] is T)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfFeature(
            NWRPRendererData rendererData,
            NWRPFeature feature)
        {
            if (rendererData == null || feature == null)
            {
                return -1;
            }

            List<NWRPFeature> features = rendererData.Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] == feature)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DrawSubsectionLabel(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        }
    }
}
