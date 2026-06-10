using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP.Editor
{
    [InitializeOnLoad]
    internal static class NWRPMaterialDefaults
    {
        internal const string StandardLitShaderName = "NewWorld/Lit/StandardLit";
        internal const string DefaultMaterialPath = "Assets/NWRP/Materials/M_NWRP_DefaultLit.mat";

        private const string AutoAssignEditorPrefKey = "NWRP.Editor.Materials.AutoAssignDefaultLit";
        private const string BuiltInStandardShaderName = "Standard";
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";
        private const string DefaultMaterialName = "Default-Material";
        private const string DefaultDiffuseMaterialName = "Default-Diffuse";
        private const string ReplaceUndoName = "Assign NWRP Default Lit Material";

        private static readonly HashSet<int> s_KnownRendererIds = new HashSet<int>();
        private static readonly HashSet<Renderer> s_PendingRenderers = new HashSet<Renderer>();
        private static bool s_AssignQueued;
        private static bool s_ScanQueued;
        private static bool s_HasBootstrappedKnownRenderers;

        static NWRPMaterialDefaults()
        {
            ObjectFactory.componentWasAdded += OnComponentWasAdded;
            EditorApplication.hierarchyChanged += QueueHierarchyScan;
            EditorApplication.delayCall += BootstrapKnownRenderers;
        }

        internal static bool AutoAssignEnabled
        {
            get => EditorPrefs.GetBool(AutoAssignEditorPrefKey, true);
            set => EditorPrefs.SetBool(AutoAssignEditorPrefKey, value);
        }

        internal static Material GetOrCreateDefaultMaterial()
        {
            return GetOrCreateStandardLitMaterial(DefaultMaterialPath);
        }

        internal static Material GetOrCreateStandardLitMaterial(string materialPath)
        {
            if (string.IsNullOrWhiteSpace(materialPath))
            {
                throw new ArgumentException("Material path must be a non-empty Assets path.", nameof(materialPath));
            }

            if (!materialPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Material path must be under Assets/: {materialPath}", nameof(materialPath));
            }

            Shader standardLitShader = Shader.Find(StandardLitShaderName);
            if (standardLitShader == null)
            {
                Debug.LogError($"NWRP default material cannot be created because shader '{StandardLitShaderName}' was not found.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                EnsureAssetFolder(Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));

                material = new Material(standardLitShader)
                {
                    name = Path.GetFileNameWithoutExtension(materialPath),
                    enableInstancing = true
                };
                ApplyDefaultLitValues(material);
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();
                return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }

            bool changed = false;
            if (material.shader != standardLitShader)
            {
                material.shader = standardLitShader;
                changed = true;
            }

            if (!material.enableInstancing)
            {
                material.enableInstancing = true;
                changed = true;
            }

            if (changed)
            {
                ApplyDefaultLitValues(material);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }

            return material;
        }

        internal static int ReplaceDefaultSlots(Renderer renderer, Material replacementMaterial, bool recordUndo)
        {
            if (renderer == null || replacementMaterial == null)
            {
                return 0;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                return 0;
            }

            int replacedSlots = 0;
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                if (!ShouldReplaceMaterial(sharedMaterials[i], replacementMaterial))
                {
                    continue;
                }

                sharedMaterials[i] = replacementMaterial;
                replacedSlots++;
            }

            if (replacedSlots == 0)
            {
                return 0;
            }

            if (recordUndo)
            {
                Undo.RecordObject(renderer, ReplaceUndoName);
            }

            renderer.sharedMaterials = sharedMaterials;
            EditorUtility.SetDirty(renderer);
            return replacedSlots;
        }

        [MenuItem("NWRP/Tools/Materials/Auto Assign Default Lit Material", false, 900)]
        private static void ToggleAutoAssignDefaultMaterial()
        {
            AutoAssignEnabled = !AutoAssignEnabled;
            Menu.SetChecked("NWRP/Tools/Materials/Auto Assign Default Lit Material", AutoAssignEnabled);

            if (AutoAssignEnabled)
            {
                QueueHierarchyScan();
            }
        }

        [MenuItem("NWRP/Tools/Materials/Auto Assign Default Lit Material", true)]
        private static bool ValidateToggleAutoAssignDefaultMaterial()
        {
            Menu.SetChecked("NWRP/Tools/Materials/Auto Assign Default Lit Material", AutoAssignEnabled);
            return true;
        }

        [MenuItem("NWRP/Tools/Materials/Select Default Lit Material", false, 901)]
        private static void SelectDefaultMaterial()
        {
            Material material = GetOrCreateDefaultMaterial();
            if (material == null)
            {
                return;
            }

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
        }

        private static void OnComponentWasAdded(Component component)
        {
            if (component is Renderer renderer)
            {
                QueueRenderer(renderer);
            }
        }

        private static void BootstrapKnownRenderers()
        {
            if (s_HasBootstrappedKnownRenderers)
            {
                return;
            }

            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (ShouldOwnRenderer(renderer))
                {
                    s_KnownRendererIds.Add(renderer.GetInstanceID());
                }
            }

            s_HasBootstrappedKnownRenderers = true;
        }

        private static void QueueHierarchyScan()
        {
            if (s_ScanQueued)
            {
                return;
            }

            s_ScanQueued = true;
            EditorApplication.delayCall += ScanForNewRenderers;
        }

        private static void ScanForNewRenderers()
        {
            s_ScanQueued = false;

            if (Application.isPlaying)
            {
                return;
            }

            if (!s_HasBootstrappedKnownRenderers)
            {
                BootstrapKnownRenderers();
                return;
            }

            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!ShouldOwnRenderer(renderer))
                {
                    continue;
                }

                int instanceId = renderer.GetInstanceID();
                if (s_KnownRendererIds.Contains(instanceId))
                {
                    continue;
                }

                s_KnownRendererIds.Add(instanceId);
                QueueRenderer(renderer);
            }
        }

        private static void QueueRenderer(Renderer renderer)
        {
            if (renderer == null || Application.isPlaying || !AutoAssignEnabled)
            {
                return;
            }

            s_PendingRenderers.Add(renderer);
            if (s_AssignQueued)
            {
                return;
            }

            s_AssignQueued = true;
            EditorApplication.delayCall += ProcessQueuedRenderers;
        }

        private static void ProcessQueuedRenderers()
        {
            s_AssignQueued = false;

            if (Application.isPlaying || !AutoAssignEnabled || !IsNWRPActive())
            {
                s_PendingRenderers.Clear();
                return;
            }

            Material defaultMaterial = GetOrCreateDefaultMaterial();
            if (defaultMaterial == null)
            {
                s_PendingRenderers.Clear();
                return;
            }

            foreach (Renderer renderer in s_PendingRenderers)
            {
                if (!ShouldOwnRenderer(renderer))
                {
                    continue;
                }

                s_KnownRendererIds.Add(renderer.GetInstanceID());
                if (ReplaceDefaultSlots(renderer, defaultMaterial, true) > 0)
                {
                    EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
                }
            }

            s_PendingRenderers.Clear();
        }

        private static bool ShouldOwnRenderer(Renderer renderer)
        {
            return renderer != null
                && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                && renderer.gameObject.scene.IsValid()
                && renderer.gameObject.scene.isLoaded
                && !EditorUtility.IsPersistent(renderer)
                && (renderer.hideFlags & HideFlags.NotEditable) == 0;
        }

        private static bool ShouldReplaceMaterial(Material material, Material replacementMaterial)
        {
            if (material == null)
            {
                return true;
            }

            if (material == replacementMaterial)
            {
                return false;
            }

            Shader shader = material.shader;
            string shaderName = shader != null ? shader.name : string.Empty;
            if (shaderName == StandardLitShaderName)
            {
                return false;
            }

            return shaderName == BuiltInStandardShaderName
                || shaderName == InternalErrorShaderName
                || shaderName == NWRPMaterialConverter.UrpLitShaderName
                || shaderName == NWRPMaterialConverter.UrpSimpleLitShaderName
                || material.name == DefaultMaterialName
                || material.name == DefaultDiffuseMaterialName;
        }

        private static bool IsNWRPActive()
        {
            return GraphicsSettings.currentRenderPipeline is NewWorldRenderPipelineAsset
                || QualitySettings.renderPipeline is NewWorldRenderPipelineAsset;
        }

        private static void ApplyDefaultLitValues(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.5f);
            }

            if (material.HasProperty("_ReceiveShadows"))
            {
                material.SetFloat("_ReceiveShadows", 1.0f);
            }

            if (material.HasProperty("_CastShadows"))
            {
                material.SetFloat("_CastShadows", 1.0f);
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }

                currentPath = nextPath;
            }
        }
    }
}
