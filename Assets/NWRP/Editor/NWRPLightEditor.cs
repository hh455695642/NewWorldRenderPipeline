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
        private static readonly GUIContent s_CullingMaskLabel = new GUIContent("Culling Mask");
        private static readonly GUIContent s_ShadowToggleLabel = new GUIContent("Shadow Type");
        private static readonly GUIContent s_ShadowStrengthLabel = new GUIContent("Shadow Strength");
        private static readonly GUIContent s_ShadowNearPlaneLabel = new GUIContent("Shadow Near Plane");

        private UnityEditor.Editor _fallbackEditor;

        private SerializedProperty _typeProperty;
        private SerializedProperty _shapeProperty;
        private SerializedProperty _colorProperty;
        private SerializedProperty _intensityProperty;
        private SerializedProperty _rangeProperty;
        private SerializedProperty _spotAngleProperty;
        private SerializedProperty _innerSpotAngleProperty;
        private SerializedProperty _cullingMaskProperty;
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
            if (!TryGetSimplifiedLightType(out LightType simplifiedType))
            {
                DrawFallbackInspector();
                return;
            }

            serializedObject.Update();
            NormalizeShadowType();

            EditorGUILayout.LabelField(GetHeaderLabel(simplifiedType), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This simplified inspector only exposes properties currently consumed by the NWRP mobile realtime light paths. " +
                "Cookie, flare, halo, baking extras, render mode, rendering layers, per-light shadow resolution, per-light hard/soft switches, per-light bias overrides, and custom culling overrides are intentionally hidden.",
                MessageType.None);

            DrawLightSection();
            EditorGUILayout.Space();
            DrawShadowSection(simplifiedType);
            EditorGUILayout.Space();
            DrawPipelineInfo(simplifiedType);

            serializedObject.ApplyModifiedProperties();
        }

        public override bool RequiresConstantRepaint()
        {
            if (TryGetSimplifiedLightType(out _))
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

            if (!TryGetSelectedSimplifiedLightType(out LightType selectedType))
            {
                EditorGUILayout.HelpBox(
                    "Switching away from Directional, Spot, or Point Light will restore the Unity default inspector on the next repaint.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.PropertyField(_colorProperty, s_ColorLabel);
            EditorGUILayout.PropertyField(_intensityProperty, s_IntensityLabel);

            if (selectedType == LightType.Point || selectedType == LightType.Spot)
            {
                EditorGUILayout.PropertyField(_rangeProperty, s_RangeLabel);
            }

            if (selectedType == LightType.Spot)
            {
                EditorGUILayout.PropertyField(_spotAngleProperty, s_SpotAngleLabel);
                EditorGUILayout.PropertyField(_innerSpotAngleProperty, s_InnerSpotAngleLabel);

                if (!IsConeShape())
                {
                    EditorGUILayout.HelpBox(
                        "The current NWRP punctual shadow path only supports cone spot lights. Non-cone spot shapes stay hidden in this simplified inspector.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.PropertyField(_cullingMaskProperty, s_CullingMaskLabel);
        }

        private void DrawShadowSection(LightType simplifiedType)
        {
            EditorGUILayout.LabelField("Shadows", EditorStyles.miniBoldLabel);

            bool shadowsEnabled = _shadowTypeProperty.enumValueIndex != (int)LightShadows.None;
            EditorGUI.showMixedValue = _shadowTypeProperty.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            bool newShadowsEnabled = EditorGUILayout.Toggle(s_ShadowToggleLabel, shadowsEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                _shadowTypeProperty.enumValueIndex = newShadowsEnabled
                    ? (int)LightShadows.Hard
                    : (int)LightShadows.None;
            }

            EditorGUI.showMixedValue = false;

            if (_shadowTypeProperty.hasMultipleDifferentValues
                || _shadowTypeProperty.enumValueIndex == (int)LightShadows.None)
            {
                return;
            }

            EditorGUILayout.PropertyField(_shadowStrengthProperty, s_ShadowStrengthLabel);
            EditorGUILayout.PropertyField(_shadowNearPlaneProperty, s_ShadowNearPlaneLabel);

            if (simplifiedType == LightType.Directional)
            {
                EditorGUILayout.HelpBox(
                    "Main Light shadow filtering and bias tuning are controlled on the active NWRP pipeline asset. Per-light Hard / Soft selection is intentionally collapsed to ON / OFF.",
                    MessageType.None);
                return;
            }

            string punctualMessage = simplifiedType == LightType.Point
                ? "Point lights consume six shadow slices in the shared atlas."
                : "Spot lights consume one shadow slice in the shared atlas.";
            EditorGUILayout.HelpBox(
                punctualMessage + " Additional punctual light shadows are hard-shadow only in this branch, and bias tuning is controlled on the active NWRP pipeline asset.",
                MessageType.None);
        }

        private void DrawPipelineInfo(LightType simplifiedType)
        {
            NewWorldRenderPipelineAsset pipelineAsset = GetActivePipelineAsset();
            if (pipelineAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "No active NWRP pipeline asset was found. The simplified light inspector is intended for the active NewWorld render pipeline.",
                    MessageType.Warning);
                return;
            }

            string infoMessage = simplifiedType == LightType.Directional
                ? "Main Light shadow filter mode, atlas resolution, cascade settings, and cached shadow options are controlled on the active NWRP pipeline asset."
                : "Additional punctual light shadow atlas size, requested tile resolution, budget, and bias are controlled on the active NWRP pipeline asset.";
            EditorGUILayout.HelpBox(infoMessage, MessageType.Info);

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
            _cullingMaskProperty = serializedObject.FindProperty("m_CullingMask");
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

        private void NormalizeShadowType()
        {
            if (_shadowTypeProperty == null
                || _shadowTypeProperty.hasMultipleDifferentValues
                || _shadowTypeProperty.enumValueIndex == (int)LightShadows.None
                || _shadowTypeProperty.enumValueIndex == (int)LightShadows.Hard)
            {
                return;
            }

            _shadowTypeProperty.enumValueIndex = (int)LightShadows.Hard;
        }

        private bool TryGetSimplifiedLightType(out LightType lightType)
        {
            lightType = default;

            if (targets == null || targets.Length == 0 || GetActivePipelineAsset() == null)
            {
                return false;
            }

            LightType firstLightType = default;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not Light light || !IsSupportedSimplifiedLightType(light.type))
                {
                    return false;
                }

                if (i == 0)
                {
                    firstLightType = light.type;
                    continue;
                }

                if (light.type != firstLightType)
                {
                    return false;
                }
            }

            lightType = firstLightType;
            return true;
        }

        private bool TryGetSelectedSimplifiedLightType(out LightType lightType)
        {
            lightType = default;
            if (_typeProperty == null || _typeProperty.hasMultipleDifferentValues)
            {
                return false;
            }

            lightType = (LightType)_typeProperty.enumValueIndex;
            return IsSupportedSimplifiedLightType(lightType);
        }

        private static bool IsSupportedSimplifiedLightType(LightType lightType)
        {
            return lightType == LightType.Directional
                || lightType == LightType.Spot
                || lightType == LightType.Point;
        }

        private bool IsConeShape()
        {
            return _shapeProperty == null
                || _shapeProperty.hasMultipleDifferentValues
                || _shapeProperty.enumValueIndex == 0;
        }

        private static string GetHeaderLabel(LightType lightType)
        {
            return lightType switch
            {
                LightType.Directional => "NWRP Directional Light",
                LightType.Point => "NWRP Point Light",
                _ => "NWRP Spot Light"
            };
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
