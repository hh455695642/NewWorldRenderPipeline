using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPFeature), true)]
    [CanEditMultipleObjects]
    public sealed class NWRPFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4f);
            DrawRendererDataUsage();
        }

        private void DrawRendererDataUsage()
        {
            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox(
                    "Select one NWRP Feature asset to inspect renderer data usage.",
                    MessageType.Info);
                return;
            }

            if (target is not NWRPFeature feature)
            {
                return;
            }

            NewWorldRenderPipelineAsset asset = GetActivePipelineAsset();
            if (asset == null)
            {
                EditorGUILayout.HelpBox(
                    "No active NWRP asset. This feature will run only after it is referenced by a renderer data used by an active NWRP asset.",
                    MessageType.Info);
                return;
            }

            if (!asset.ValidateRendererDataList())
            {
                EditorGUILayout.HelpBox(
                    "The active NWRP asset has no valid Renderer Data entries. Add this feature to a Renderer Data Explicit Features list before expecting it to run.",
                    MessageType.Warning);
                return;
            }

            if (TryGetRendererDataReferences(asset, feature, out string renderers))
            {
                EditorGUILayout.HelpBox(
                    $"Referenced by Renderer Data: {renderers}. This feature runs for cameras that resolve to those renderers when the feature is enabled.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "This feature asset is not referenced by any Renderer Data in the active NWRP asset. Creating it alone does not enqueue render passes; add it to a Renderer Data Explicit Features list.",
                MessageType.Warning);
        }

        private static NewWorldRenderPipelineAsset GetActivePipelineAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is NewWorldRenderPipelineAsset current)
            {
                return current;
            }

            return QualitySettings.renderPipeline as NewWorldRenderPipelineAsset;
        }

        private static bool TryGetRendererDataReferences(
            NewWorldRenderPipelineAsset asset,
            NWRPFeature feature,
            out string renderers)
        {
            List<string> matches = new List<string>();
            int defaultIndex = asset.DefaultRendererIndex;
            NWRPRendererData[] rendererDataList = asset.rendererDataList;
            if (rendererDataList == null)
            {
                renderers = string.Empty;
                return false;
            }

            for (int i = 0; i < rendererDataList.Length; i++)
            {
                NWRPRendererData rendererData = rendererDataList[i];
                if (rendererData == null || !ContainsFeature(rendererData, feature))
                {
                    continue;
                }

                string rendererName = string.IsNullOrEmpty(rendererData.name)
                    ? "Unnamed Renderer Data"
                    : rendererData.name;
                string suffix = i == defaultIndex ? " (Default)" : string.Empty;
                matches.Add($"{i}: {rendererName}{suffix}");
            }

            renderers = string.Join(", ", matches);
            return matches.Count > 0;
        }

        private static bool ContainsFeature(
            NWRPRendererData rendererData,
            NWRPFeature feature)
        {
            List<NWRPFeature> features = rendererData.Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] == feature)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
