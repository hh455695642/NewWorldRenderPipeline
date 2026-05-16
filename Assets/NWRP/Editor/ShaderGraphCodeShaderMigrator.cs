using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NWRP.Editor
{
    internal static class ShaderGraphCodeShaderMigrator
    {
        private const string ShaderGraphExtension = ".shadergraph";
        private const string CodeShaderExtension = ".shader";
        private const string CodeShaderFileId = "4800000";

        private static readonly string[] s_ShaderSearchRoots =
        {
            "Assets/Res/Effects/Shader",
            "Assets/NWRP/Shaders",
            "Assets"
        };

        private static readonly string[] s_TextReferenceExtensions =
        {
            ".mat",
            ".prefab",
            ".unity",
            ".asset"
        };

        private static readonly HashSet<string> s_PendingShaderGraphs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> s_PendingTextAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool s_DelayCallRegistered;

        [MenuItem("NWRP/Tools/Effects/Migrate ShaderGraph References To Code Shaders")]
        private static void MigrateAllShaderGraphs()
        {
            string[] paths = AssetDatabase.GetAllAssetPaths();
            List<string> shaderGraphs = new List<string>();

            foreach (string path in paths)
            {
                if (IsShaderGraphPath(path))
                {
                    shaderGraphs.Add(path);
                }
            }

            int migrated = Migrate(shaderGraphs, autoRun: false);
            int repaired = RepairCodeShaderFileIds(EnumerateAllTextReferenceAssetPaths());
            Debug.Log(
                $"NWRP shadergraph migration finished. Migrated {migrated} shadergraph asset(s), " +
                $"repaired {repaired} code-shader fileID reference asset(s).");
        }

        internal static void ScheduleAutoProcess(IEnumerable<string> importedOrMovedPaths)
        {
            foreach (string path in importedOrMovedPaths)
            {
                if (IsShaderGraphPath(path))
                {
                    s_PendingShaderGraphs.Add(path);
                }

                if (IsTextReferenceAssetPath(path))
                {
                    s_PendingTextAssets.Add(path);
                }
            }

            if ((s_PendingShaderGraphs.Count == 0 && s_PendingTextAssets.Count == 0) || s_DelayCallRegistered)
            {
                return;
            }

            s_DelayCallRegistered = true;
            EditorApplication.delayCall += RunScheduledMigration;
        }

        private static void RunScheduledMigration()
        {
            s_DelayCallRegistered = false;

            if (s_PendingShaderGraphs.Count == 0)
            {
                string[] textOnlyPaths = new string[s_PendingTextAssets.Count];
                s_PendingTextAssets.CopyTo(textOnlyPaths);
                s_PendingTextAssets.Clear();
                RepairCodeShaderFileIds(textOnlyPaths);
                return;
            }

            string[] paths = new string[s_PendingShaderGraphs.Count];
            string[] textPaths = new string[s_PendingTextAssets.Count];
            s_PendingShaderGraphs.CopyTo(paths);
            s_PendingTextAssets.CopyTo(textPaths);
            s_PendingShaderGraphs.Clear();
            s_PendingTextAssets.Clear();

            Migrate(paths, autoRun: true);
            RepairCodeShaderFileIds(textPaths);
        }

        private static int Migrate(IEnumerable<string> shaderGraphPaths, bool autoRun)
        {
            int migratedCount = 0;
            bool changedAssets = false;
            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (string shaderGraphPath in shaderGraphPaths)
                {
                    if (!IsShaderGraphPath(shaderGraphPath) || !File.Exists(shaderGraphPath))
                    {
                        continue;
                    }

                    string replacementPath = FindCodeShaderPath(shaderGraphPath);
                    if (string.IsNullOrEmpty(replacementPath))
                    {
                        if (!autoRun)
                        {
                            Debug.LogWarning($"No same-name code shader found for imported ShaderGraph: {shaderGraphPath}");
                        }
                        continue;
                    }

                    if (TryMigrateShaderGraph(shaderGraphPath, replacementPath, out int reboundMaterials, out int rewrittenTextAssets))
                    {
                        migratedCount++;
                        changedAssets = true;
                        Debug.Log(
                            $"Migrated ShaderGraph '{shaderGraphPath}' -> '{replacementPath}'. " +
                            $"Rebound materials: {reboundMaterials}, rewritten text assets: {rewrittenTextAssets}.");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (changedAssets)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return migratedCount;
        }

        private static bool TryMigrateShaderGraph(
            string shaderGraphPath,
            string replacementPath,
            out int reboundMaterials,
            out int rewrittenTextAssets)
        {
            reboundMaterials = 0;
            rewrittenTextAssets = 0;

            string shaderGraphGuid = AssetDatabase.AssetPathToGUID(shaderGraphPath);
            string replacementGuid = AssetDatabase.AssetPathToGUID(replacementPath);
            if (string.IsNullOrEmpty(shaderGraphGuid) || string.IsNullOrEmpty(replacementGuid))
            {
                Debug.LogWarning($"Cannot migrate ShaderGraph '{shaderGraphPath}' because one of the GUIDs is empty.");
                return false;
            }

            Shader replacementShader = AssetDatabase.LoadAssetAtPath<Shader>(replacementPath);
            if (replacementShader == null)
            {
                Debug.LogWarning($"Cannot migrate ShaderGraph '{shaderGraphPath}' because replacement shader failed to load: {replacementPath}");
                return false;
            }

            Shader shaderGraphShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderGraphPath);
            reboundMaterials = RebindLoadedMaterials(shaderGraphShader, replacementShader);
            rewrittenTextAssets = RewriteSerializedShaderReferences(shaderGraphGuid, replacementGuid);

            if (!AssetDatabase.DeleteAsset(shaderGraphPath))
            {
                Debug.LogWarning($"Migrated references but failed to delete old ShaderGraph: {shaderGraphPath}");
                return false;
            }

            return true;
        }

        private static int RebindLoadedMaterials(Shader oldShader, Shader replacementShader)
        {
            int reboundCount = 0;
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });

            foreach (string materialGuid in materialGuids)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(materialGuid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.shader != oldShader)
                {
                    continue;
                }

                material.shader = replacementShader;
                EditorUtility.SetDirty(material);
                reboundCount++;
            }

            return reboundCount;
        }

        private static int RewriteSerializedShaderReferences(string oldGuid, string replacementGuid)
        {
            int changedCount = 0;
            string assetsRoot = Application.dataPath;

            foreach (string filePath in Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(filePath);
                if (!HasTextReferenceExtension(extension))
                {
                    continue;
                }

                string text = File.ReadAllText(filePath);
                if (text.IndexOf(oldGuid, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string rewritten = RewriteShaderReferenceLine(text, oldGuid, replacementGuid);
                if (rewritten == text)
                {
                    continue;
                }

                File.WriteAllText(filePath, rewritten);
                changedCount++;
            }

            return changedCount;
        }

        private static string RewriteShaderReferenceLine(string text, string oldGuid, string replacementGuid)
        {
            string escapedGuid = Regex.Escape(oldGuid);
            string pattern = @"m_Shader:\s*\{\s*fileID:\s*-?\d+\s*,\s*guid:\s*" + escapedGuid + @"\s*,\s*type:\s*3\s*\}";
            string replacement = $"m_Shader: {{fileID: {CodeShaderFileId}, guid: {replacementGuid}, type: 3}}";
            return Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
        }

        private static int RepairCodeShaderFileIds(IEnumerable<string> candidatePaths)
        {
            int changedCount = 0;

            foreach (string assetPath in candidatePaths)
            {
                if (!IsTextReferenceAssetPath(assetPath) || !File.Exists(assetPath))
                {
                    continue;
                }

                string text = File.ReadAllText(assetPath);
                string rewritten = RewriteCodeShaderFileIds(text);
                if (rewritten == text)
                {
                    continue;
                }

                File.WriteAllText(assetPath, rewritten);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                changedCount++;
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return changedCount;
        }

        private static IEnumerable<string> EnumerateAllTextReferenceAssetPaths()
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (IsTextReferenceAssetPath(path))
                {
                    yield return path;
                }
            }
        }

        private static string RewriteCodeShaderFileIds(string text)
        {
            const string pattern = @"m_Shader:\s*\{\s*fileID:\s*(?<fileId>-?\d+)\s*,\s*guid:\s*(?<guid>[0-9a-fA-F]{32})\s*,\s*type:\s*3\s*\}";

            return Regex.Replace(
                text,
                pattern,
                match =>
                {
                    string fileId = match.Groups["fileId"].Value;
                    string guid = match.Groups["guid"].Value;
                    if (fileId == CodeShaderFileId)
                    {
                        return match.Value;
                    }

                    string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(shaderPath)
                        || !shaderPath.EndsWith(CodeShaderExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        return match.Value;
                    }

                    return $"m_Shader: {{fileID: {CodeShaderFileId}, guid: {guid}, type: 3}}";
                },
                RegexOptions.IgnoreCase);
        }

        private static string FindCodeShaderPath(string shaderGraphPath)
        {
            string directory = Path.GetDirectoryName(shaderGraphPath)?.Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(shaderGraphPath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(baseName))
            {
                return null;
            }

            string sameDirectoryPath = $"{directory}/{baseName}{CodeShaderExtension}";
            if (File.Exists(sameDirectoryPath))
            {
                return sameDirectoryPath;
            }

            string[] shaderGuids = AssetDatabase.FindAssets($"{baseName} t:Shader", s_ShaderSearchRoots);
            foreach (string shaderGuid in shaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(shaderGuid);
                if (!path.EndsWith(CodeShaderExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(Path.GetFileNameWithoutExtension(path), baseName, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            return null;
        }

        private static bool IsShaderGraphPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(ShaderGraphExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextReferenceAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                && HasTextReferenceExtension(Path.GetExtension(path));
        }

        private static bool HasTextReferenceExtension(string extension)
        {
            foreach (string textReferenceExtension in s_TextReferenceExtensions)
            {
                if (string.Equals(extension, textReferenceExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class ShaderGraphCodeShaderAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Length > 0)
            {
                ShaderGraphCodeShaderMigrator.ScheduleAutoProcess(importedAssets);
            }

            if (movedAssets.Length > 0)
            {
                ShaderGraphCodeShaderMigrator.ScheduleAutoProcess(movedAssets);
            }
        }
    }
}
