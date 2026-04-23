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
        private static readonly GUIContent s_ShadowTypeLabel = new GUIContent("Shadow Type");
        private static readonly GUIContent s_ShadowStrengthLabel = new GUIContent("Shadow Strength");
        private static readonly GUIContent s_ShadowBiasLabel = new GUIContent("Shadow Bias");
        private static readonly GUIContent s_ShadowNormalBiasLabel = new GUIContent("Shadow Normal Bias");
        private static readonly GUIContent s_ShadowNearPlaneLabel = new GUIContent("Shadow Near Plane");
        private static readonly GUIContent s_ShapeLabel = new GUIContent("Shape");

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
        private SerializedProperty _shadowBiasProperty;
        private SerializedProperty _shadowNormalBiasProperty;
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
            if (!ShouldUseSimplifiedSpotInspector())
            {
                DrawFallbackInspector();
                return;
            }

            serializedObject.Update();

            EditorGUILayout.LabelField("NWRP Spot Light", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This simplified inspector only exposes properties currently consumed by the NWRP mobile spot-light path. " +
                "Cookie, flare, halo, baking, render mode, rendering layers, per-light shadow resolution, soft-shadow shape settings, and custom culling overrides are hidden to avoid misleading artists.",
                MessageType.None);

            DrawLightSection();
            EditorGUILayout.Space();
            DrawShadowSection();
            EditorGUILayout.Space();
            DrawPipelineInfo();

            serializedObject.ApplyModifiedProperties();
        }

        public override bool RequiresConstantRepaint()
        {
            if (ShouldUseSimplifiedSpotInspector())
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

            if (!IsSpotTypeSelected())
            {
                EditorGUILayout.HelpBox(
                    "Switching away from Spot Light will restore the Unity default inspector on the next repaint.",
                    MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_shapeProperty, s_ShapeLabel);
            }

            EditorGUILayout.PropertyField(_colorProperty, s_ColorLabel);
            EditorGUILayout.PropertyField(_intensityProperty, s_IntensityLabel);
            EditorGUILayout.PropertyField(_rangeProperty, s_RangeLabel);
            EditorGUILayout.PropertyField(_spotAngleProperty, s_SpotAngleLabel);
            EditorGUILayout.PropertyField(_innerSpotAngleProperty, s_InnerSpotAngleLabel);
            EditorGUILayout.PropertyField(_cullingMaskProperty, s_CullingMaskLabel);

            if (!IsConeShape())
            {
                EditorGUILayout.HelpBox(
                    "The current additional spot-light implementation only supports cone-shaped spot lights. Other spot shapes are intentionally hidden from normal authoring.",
                    MessageType.Warning);
            }
        }

        private void DrawShadowSection()
        {
            EditorGUILayout.LabelField("Shadows", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_shadowTypeProperty, s_ShadowTypeLabel);

            if (_shadowTypeProperty.hasMultipleDifferentValues
                || _shadowTypeProperty.enumValueIndex == (int)LightShadows.None)
            {
                return;
            }

            EditorGUILayout.PropertyField(_shadowStrengthProperty, s_ShadowStrengthLabel);
            EditorGUILayout.HelpBox(
                "Shadow Bias and Shadow Normal Bias are controlled globally on the active NWRP pipeline asset. Per-light bias overrides are not supported in the current implementation.",
                MessageType.Info);
            EditorGUILayout.PropertyField(_shadowNearPlaneProperty, s_ShadowNearPlaneLabel);
        }

        private void DrawPipelineInfo()
        {
            NewWorldRenderPipelineAsset pipelineAsset = GetActivePipelineAsset();
            if (pipelineAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "No active NWRP pipeline asset was found. The simplified spot-light inspector is intended for the active NewWorld render pipeline.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Shadow atlas resolution and the maximum number of shadowed additional spot lights are controlled on the active NWRP pipeline asset, not per light.",
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
            _cullingMaskProperty = serializedObject.FindProperty("m_CullingMask");
            _shadowTypeProperty = serializedObject.FindProperty("m_Shadows.m_Type");
            _shadowStrengthProperty = serializedObject.FindProperty("m_Shadows.m_Strength");
            _shadowBiasProperty = serializedObject.FindProperty("m_Shadows.m_Bias");
            _shadowNormalBiasProperty = serializedObject.FindProperty("m_Shadows.m_NormalBias");
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

        private bool ShouldUseSimplifiedSpotInspector()
        {
            if (targets == null || targets.Length == 0)
            {
                return false;
            }

            if (GetActivePipelineAsset() == null)
            {
                return false;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not Light light || light.type != LightType.Spot)
                {
                    return false;
                }
            }

            return true;
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