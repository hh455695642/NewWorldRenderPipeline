using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Light))]
    public sealed class NWRPLightEditor : UnityEditor.Editor
    {
        private static readonly BindingFlags s_InstanceAnyVisibility =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly GUIContent s_TypeLabel = new GUIContent("Type");
        private static readonly GUIContent s_ColorLabel = new GUIContent("Color");
        private static readonly GUIContent s_IntensityLabel = new GUIContent("Intensity");
        private static readonly GUIContent s_RangeLabel = new GUIContent("Range");
        private static readonly GUIContent s_SpotAngleLabel = new GUIContent("Spot Angle");
        private static readonly GUIContent s_InnerSpotAngleLabel = new GUIContent("Inner Spot Angle");
        private static readonly GUIContent s_DirectionalShadowsLabel = new GUIContent("Shadows");
        private static readonly GUIContent s_ShadowTypeLabel = new GUIContent("Shadow Type");
        private static readonly GUIContent s_ShadowStrengthLabel = new GUIContent("Shadow Strength");
        private static readonly GUIContent s_ShadowNearPlaneLabel = new GUIContent("Shadow Near Plane");
        private static readonly GUIContent s_ShapeLabel = new GUIContent("Shape");
        private static readonly GUIContent[] s_DirectionalShadowOptions =
        {
            new GUIContent("Off"),
            new GUIContent("On")
        };

        private static readonly int[] s_DirectionalShadowOptionValues =
        {
            0,
            1
        };

        private static readonly GUIContent[] s_ShadowTypeOptions =
        {
            new GUIContent("No Shadows"),
            new GUIContent("Hard Shadows")
        };

        private static readonly int[] s_ShadowTypeOptionValues =
        {
            (int)LightShadows.None,
            (int)LightShadows.Hard
        };

        private UnityEditor.Editor _fallbackEditor;

        private SerializedProperty _typeProperty;
        private SerializedProperty _shapeProperty;
        private SerializedProperty _colorProperty;
        private SerializedProperty _intensityProperty;
        private SerializedProperty _rangeProperty;
        private SerializedProperty _spotAngleProperty;
        private SerializedProperty _innerSpotAngleProperty;
        private SerializedProperty _shadowTypeProperty;
        private SerializedProperty _shadowStrengthProperty;
        private SerializedProperty _shadowNearPlaneProperty;

        private void OnEnable()
        {
            CacheProperties();
            CreateFallbackEditor();
        }

        private void OnDisable()
        {
            if (_fallbackEditor != null)
            {
                DestroyImmediate(_fallbackEditor);
                _fallbackEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            if (!ShouldUseSimplifiedSupportedLightInspector())
            {
                DrawFallbackInspector();
                return;
            }

            serializedObject.Update();

            EditorGUILayout.LabelField(GetInspectorTitle(), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(GetInspectorDescription(), MessageType.None);

            DrawLightSection();
            EditorGUILayout.Space();
            DrawShadowSection();
            EditorGUILayout.Space();
            DrawPipelineInfo();

            serializedObject.ApplyModifiedProperties();
        }

        public override bool RequiresConstantRepaint()
        {
            if (ShouldUseSimplifiedSupportedLightInspector())
            {
                return false;
            }

            return _fallbackEditor != null && _fallbackEditor.RequiresConstantRepaint();
        }

        private void OnSceneGUI()
        {
            InvokeFallbackEditorMethod("OnSceneGUI");
        }

        private void DrawLightSection()
        {
            EditorGUILayout.LabelField("Light", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_typeProperty, s_TypeLabel);

            if (!IsSupportedTypeSelected())
            {
                EditorGUILayout.HelpBox(
                    "Switching away from Directional, Spot, or Point Light will restore the Unity default inspector on the next repaint.",
                    MessageType.Info);
                return;
            }

            if (IsSpotTypeSelected())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_shapeProperty, s_ShapeLabel);
                }
            }

            EditorGUILayout.PropertyField(_colorProperty, s_ColorLabel);
            EditorGUILayout.PropertyField(_intensityProperty, s_IntensityLabel);

            if (IsLocalTypeSelected())
            {
                EditorGUILayout.PropertyField(_rangeProperty, s_RangeLabel);
            }

            if (IsSpotTypeSelected())
            {
                EditorGUILayout.PropertyField(_spotAngleProperty, s_SpotAngleLabel);
                EditorGUILayout.PropertyField(_innerSpotAngleProperty, s_InnerSpotAngleLabel);

                if (!IsConeShape())
                {
                    EditorGUILayout.HelpBox(
                        "The current additional spot-light implementation only supports cone-shaped spot lights. Other spot shapes are intentionally hidden from normal authoring.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawShadowSection()
        {
            EditorGUILayout.LabelField("Shadows", EditorStyles.miniBoldLabel);
            NormalizeUnsupportedShadowTypes();
            DrawShadowControl();

            if (_shadowTypeProperty.hasMultipleDifferentValues
                || _shadowTypeProperty.enumValueIndex == (int)LightShadows.None)
            {
                return;
            }

            EditorGUILayout.PropertyField(_shadowStrengthProperty, s_ShadowStrengthLabel);
            EditorGUILayout.HelpBox(
                IsDirectionalTypeSelected()
                    ? "Main-light bias and receiver filtering are controlled globally on the active NWRP pipeline asset. This light only controls whether the main light casts shadows."
                    : "Shadow Bias and Shadow Normal Bias are controlled globally on the active NWRP pipeline asset. Per-light bias overrides are not supported in the current implementation.",
                MessageType.Info);
            EditorGUILayout.PropertyField(_shadowNearPlaneProperty, s_ShadowNearPlaneLabel);
        }

        private void DrawShadowControl()
        {
            if (IsDirectionalTypeSelected())
            {
                DrawDirectionalShadowToggle();
                return;
            }

            DrawLocalShadowTypePopup();
        }

        private void DrawDirectionalShadowToggle()
        {
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = _shadowTypeProperty.hasMultipleDifferentValues;

            int currentShadowEnabled = _shadowTypeProperty.enumValueIndex == (int)LightShadows.None ? 0 : 1;

            EditorGUI.BeginChangeCheck();
            int selectedShadowEnabled = EditorGUILayout.IntPopup(
                s_DirectionalShadowsLabel,
                currentShadowEnabled,
                s_DirectionalShadowOptions,
                s_DirectionalShadowOptionValues);

            if (EditorGUI.EndChangeCheck())
            {
                _shadowTypeProperty.enumValueIndex = selectedShadowEnabled == 0
                    ? (int)LightShadows.None
                    : (int)LightShadows.Hard;
            }

            EditorGUI.showMixedValue = previousMixedValue;
        }

        private void DrawLocalShadowTypePopup()
        {
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = _shadowTypeProperty.hasMultipleDifferentValues;

            int currentShadowType = _shadowTypeProperty.enumValueIndex == (int)LightShadows.None
                ? (int)LightShadows.None
                : (int)LightShadows.Hard;

            EditorGUI.BeginChangeCheck();
            int selectedShadowType = EditorGUILayout.IntPopup(
                s_ShadowTypeLabel,
                currentShadowType,
                s_ShadowTypeOptions,
                s_ShadowTypeOptionValues);

            if (EditorGUI.EndChangeCheck())
            {
                _shadowTypeProperty.enumValueIndex = selectedShadowType;
            }

            EditorGUI.showMixedValue = previousMixedValue;
        }

        private void DrawPipelineInfo()
        {
            NewWorldRenderPipelineAsset pipelineAsset = GetActivePipelineAsset();
            if (pipelineAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "No active NWRP pipeline asset was found. The simplified local-light inspector is intended for the active NewWorld render pipeline.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                IsDirectionalTypeSelected()
                    ? "Cascade setup, shadow distance, atlas resolution, and main-light shadow bias settings are controlled on the active NWRP pipeline asset, not per light."
                    : "Shadow tile resolution, atlas budget, and additional-light shadow bias settings are controlled on the active NWRP pipeline asset, not per light.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Active Pipeline", pipelineAsset, typeof(NewWorldRenderPipelineAsset), false);
                }

                if (GUILayout.Button("Select", GUILayout.Width(60f)))
                {
                    Selection.activeObject = pipelineAsset;
                    EditorGUIUtility.PingObject(pipelineAsset);
                }
            }
        }

        private void DrawFallbackInspector()
        {
            CreateFallbackEditor();

            if (_fallbackEditor != null)
            {
                _fallbackEditor.OnInspectorGUI();
                return;
            }

            DrawDefaultInspector();
        }

        private void CacheProperties()
        {
            _typeProperty = serializedObject.FindProperty("m_Type");
            _shapeProperty = serializedObject.FindProperty("m_Shape");
            _colorProperty = serializedObject.FindProperty("m_Color");
            _intensityProperty = serializedObject.FindProperty("m_Intensity");
            _rangeProperty = serializedObject.FindProperty("m_Range");
            _spotAngleProperty = serializedObject.FindProperty("m_SpotAngle");
            _innerSpotAngleProperty = serializedObject.FindProperty("m_InnerSpotAngle");
            _shadowTypeProperty = serializedObject.FindProperty("m_Shadows.m_Type");
            _shadowStrengthProperty = serializedObject.FindProperty("m_Shadows.m_Strength");
            _shadowNearPlaneProperty = serializedObject.FindProperty("m_Shadows.m_NearPlane");
        }

        private void CreateFallbackEditor()
        {
            if (_fallbackEditor != null)
            {
                return;
            }

            _fallbackEditor = CreateEditor(targets, typeof(UnityEditor.LightEditor));
        }

        private void InvokeFallbackEditorMethod(string methodName)
        {
            CreateFallbackEditor();
            if (_fallbackEditor == null)
            {
                return;
            }

            MethodInfo method = _fallbackEditor.GetType().GetMethod(methodName, s_InstanceAnyVisibility);
            method?.Invoke(_fallbackEditor, null);
        }

        private bool ShouldUseSimplifiedSupportedLightInspector()
        {
            if (targets == null || targets.Length == 0)
            {
                return false;
            }

            if (GetActivePipelineAsset() == null)
            {
                return false;
            }

            LightType? supportedLightType = null;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not Light light
                    || !SupportsSimplifiedInspector(light.type))
                {
                    return false;
                }

                if (supportedLightType == null)
                {
                    supportedLightType = light.type;
                    continue;
                }

                if (supportedLightType.Value != light.type)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLocalTypeSelected()
        {
            return _typeProperty.hasMultipleDifferentValues
                || _typeProperty.enumValueIndex == (int)LightType.Spot
                || _typeProperty.enumValueIndex == (int)LightType.Point;
        }

        private bool IsDirectionalTypeSelected()
        {
            return _typeProperty.hasMultipleDifferentValues
                || _typeProperty.enumValueIndex == (int)LightType.Directional;
        }

        private bool IsSupportedTypeSelected()
        {
            return _typeProperty.hasMultipleDifferentValues
                || SupportsSimplifiedInspector((LightType)_typeProperty.enumValueIndex);
        }

        private bool IsSpotTypeSelected()
        {
            return _typeProperty.hasMultipleDifferentValues
                || _typeProperty.enumValueIndex == (int)LightType.Spot;
        }

        private bool IsConeShape()
        {
            return _shapeProperty == null
                || _shapeProperty.hasMultipleDifferentValues
                || _shapeProperty.enumValueIndex == 0;
        }

        private void NormalizeUnsupportedShadowTypes()
        {
            Light[] lightsToNormalize = null;
            int lightCount = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not Light light || light.shadows != LightShadows.Soft)
                {
                    continue;
                }

                lightsToNormalize ??= new Light[targets.Length];
                lightsToNormalize[lightCount++] = light;
            }

            if (lightCount == 0)
            {
                return;
            }

            Object[] undoTargets = new Object[lightCount];
            for (int i = 0; i < lightCount; i++)
            {
                undoTargets[i] = lightsToNormalize[i];
            }

            Undo.RecordObjects(undoTargets, "Clamp Unsupported NWRP Light Shadow Type");
            for (int i = 0; i < lightCount; i++)
            {
                Light light = lightsToNormalize[i];
                light.shadows = LightShadows.Hard;
                EditorUtility.SetDirty(light);
            }

            serializedObject.Update();
        }

        private string GetInspectorTitle()
        {
            return IsDirectionalTypeSelected()
                ? "NWRP Directional Light"
                : "NWRP Local Light";
        }

        private string GetInspectorDescription()
        {
            if (IsDirectionalTypeSelected())
            {
                return "This simplified inspector only exposes properties currently consumed by the NWRP mobile main-light path. " +
                    "Cookie, flare, halo, baking, render mode, culling mask, rendering layers, indirect multiplier, per-light shadow bias, soft-shadow settings, and custom culling overrides are hidden to avoid misleading artists.";
            }

            return "This simplified inspector only exposes properties currently consumed by the NWRP mobile local-light path. " +
                "Cookie, flare, halo, baking, render mode, culling mask, rendering layers, indirect multiplier, per-light shadow resolution, soft-shadow settings, and custom culling overrides are hidden to avoid misleading artists.";
        }

        private static bool SupportsSimplifiedInspector(LightType lightType)
        {
            return lightType == LightType.Directional
                || lightType == LightType.Spot
                || lightType == LightType.Point;
        }

        private static NewWorldRenderPipelineAsset GetActivePipelineAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is NewWorldRenderPipelineAsset currentPipeline)
            {
                return currentPipeline;
            }

            return QualitySettings.renderPipeline as NewWorldRenderPipelineAsset;
        }
    }
}
