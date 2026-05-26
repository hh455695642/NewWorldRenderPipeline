using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPCameraData))]
    public sealed class NWRPCameraDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _renderPostProcessingProperty;
        private SerializedProperty _renderScaleModeProperty;
        private SerializedProperty _renderScaleOverrideProperty;
        private SerializedProperty _volumeLayerMaskProperty;
        private SerializedProperty _volumeTriggerProperty;
        private SerializedProperty _rendererIndexProperty;

        private void OnEnable()
        {
            _renderPostProcessingProperty =
                serializedObject.FindProperty("m_RenderPostProcessing");
            _renderScaleModeProperty =
                serializedObject.FindProperty("renderScaleMode");
            _renderScaleOverrideProperty =
                serializedObject.FindProperty("renderScaleOverride");
            _volumeLayerMaskProperty =
                serializedObject.FindProperty("volumeLayerMask");
            _volumeTriggerProperty =
                serializedObject.FindProperty("volumeTrigger");
            _rendererIndexProperty =
                serializedObject.FindProperty("m_RendererIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawRendererSelection();
            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(
                _renderPostProcessingProperty,
                new GUIContent("Render Post Processing"));
            EditorGUILayout.PropertyField(
                _renderScaleModeProperty,
                new GUIContent("Render Scale Mode"));
            if (_renderScaleModeProperty.enumValueIndex
                == (int)NWRPCameraData.RenderScaleMode.Override)
            {
                EditorGUILayout.PropertyField(
                    _renderScaleOverrideProperty,
                    new GUIContent("Render Scale Override"));
            }

            EditorGUILayout.PropertyField(
                _volumeLayerMaskProperty,
                new GUIContent("Volume Layer Mask"));
            EditorGUILayout.PropertyField(
                _volumeTriggerProperty,
                new GUIContent("Volume Trigger"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRendererSelection()
        {
            NewWorldRenderPipelineAsset asset = GetActivePipelineAsset();
            if (asset == null)
            {
                EditorGUILayout.PropertyField(
                    _rendererIndexProperty,
                    new GUIContent("Renderer"));
                EditorGUILayout.HelpBox(
                    "No active NWRP asset. Renderer selection will resolve when NWRP is active.",
                    MessageType.Info);
                return;
            }

            int selectedRendererOption = _rendererIndexProperty.intValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = _rendererIndexProperty.hasMultipleDifferentValues;
            int selectedRenderer = EditorGUILayout.IntPopup(
                new GUIContent("Renderer"),
                selectedRendererOption,
                asset.rendererDisplayList,
                asset.rendererIndexList);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                _rendererIndexProperty.intValue = selectedRenderer;
            }

            if (!asset.ValidateRendererDataList())
            {
                EditorGUILayout.HelpBox(
                    "The active NWRP asset has no valid Renderer Data entries.",
                    MessageType.Error);
            }
            else if (!asset.ValidateRendererData(selectedRendererOption))
            {
                EditorGUILayout.HelpBox(
                    "Selected Renderer Data is missing. This camera will fall back to the default renderer.",
                    MessageType.Warning);
            }
        }

        private static NewWorldRenderPipelineAsset GetActivePipelineAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is NewWorldRenderPipelineAsset current)
            {
                return current;
            }

            return QualitySettings.renderPipeline as NewWorldRenderPipelineAsset;
        }
    }
}
