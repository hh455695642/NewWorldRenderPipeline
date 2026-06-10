using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NWRP.Editor
{
    internal static class NWRPMaterialConverter
    {
        internal const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        internal const string UrpSimpleLitShaderName = "Universal Render Pipeline/Simple Lit";

        private const string ConvertAllMenuPath = "NWRP/Tools/Materials/Convert All URP Lit Materials To NWRP StandardLit";
        private const string ConvertSelectedMenuPath = "NWRP/Tools/Materials/Convert Selected URP Lit Materials To NWRP StandardLit";
        private const string ConvertAllSimpleLitMenuPath = "NWRP/Tools/Materials/Convert All URP Simple Lit Materials To NWRP StandardLit";
        private const string ConvertSelectedSimpleLitMenuPath = "NWRP/Tools/Materials/Convert Selected URP Simple Lit Materials To NWRP StandardLit";
        private const string TargetShaderName = NWRPMaterialDefaults.StandardLitShaderName;
        private const string UndoName = "Convert URP Materials To NWRP StandardLit";

        private static readonly SourceShaderProfile s_UrpLitProfile = new SourceShaderProfile(
            UrpLitShaderName,
            "URP Lit",
            "_MetallicGlossMap",
            forceDielectricMetallic: false);

        private static readonly SourceShaderProfile s_UrpSimpleLitProfile = new SourceShaderProfile(
            UrpSimpleLitShaderName,
            "URP Simple Lit",
            null,
            forceDielectricMetallic: true);

        private static readonly SourceShaderProfile[] s_AllSourceProfiles =
        {
            s_UrpLitProfile,
            s_UrpSimpleLitProfile
        };

        [MenuItem(ConvertSelectedMenuPath, false, 920)]
        private static void ConvertSelectedUrpLitMaterials()
        {
            Material[] selectedMaterials = Selection.GetFiltered<Material>(SelectionMode.Assets | SelectionMode.DeepAssets);
            ConvertMaterialsWithDialog(selectedMaterials, s_UrpLitProfile, "Convert selected opaque URP Lit material assets?");
        }

        [MenuItem(ConvertSelectedMenuPath, true)]
        private static bool ValidateConvertSelectedUrpLitMaterials()
        {
            return Selection.GetFiltered<Material>(SelectionMode.Assets | SelectionMode.DeepAssets).Length > 0;
        }

        [MenuItem(ConvertAllMenuPath, false, 921)]
        internal static void ConvertAllProjectMaterialsWithDialog()
        {
            Material[] materials = FindAllProjectMaterials();
            ConvertMaterialsWithDialog(materials, s_UrpLitProfile, "Convert all opaque URP Lit material assets under Assets/?");
        }

        [MenuItem(ConvertSelectedSimpleLitMenuPath, false, 922)]
        private static void ConvertSelectedUrpSimpleLitMaterials()
        {
            Material[] selectedMaterials = Selection.GetFiltered<Material>(SelectionMode.Assets | SelectionMode.DeepAssets);
            ConvertMaterialsWithDialog(selectedMaterials, s_UrpSimpleLitProfile, "Convert selected opaque URP Simple Lit material assets?");
        }

        [MenuItem(ConvertSelectedSimpleLitMenuPath, true)]
        private static bool ValidateConvertSelectedUrpSimpleLitMaterials()
        {
            return Selection.GetFiltered<Material>(SelectionMode.Assets | SelectionMode.DeepAssets).Length > 0;
        }

        [MenuItem(ConvertAllSimpleLitMenuPath, false, 923)]
        internal static void ConvertAllProjectSimpleLitMaterialsWithDialog()
        {
            Material[] materials = FindAllProjectMaterials();
            ConvertMaterialsWithDialog(materials, s_UrpSimpleLitProfile, "Convert all opaque URP Simple Lit material assets under Assets/?");
        }

        internal static ConversionSummary ConvertAllProjectMaterials(bool recordUndo)
        {
            Shader targetShader = Shader.Find(TargetShaderName);
            if (targetShader == null)
            {
                Debug.LogError($"Cannot convert URP materials because shader '{TargetShaderName}' was not found.");
                return default;
            }

            return ConvertMaterials(FindAllProjectMaterials(), targetShader, recordUndo, s_UrpLitProfile);
        }

        internal static ConversionSummary ConvertMaterials(Material[] materials, Shader targetShader, bool recordUndo)
        {
            return ConvertMaterials(materials, targetShader, recordUndo, null);
        }

        private static ConversionSummary ConvertMaterials(Material[] materials, Shader targetShader, bool recordUndo, SourceShaderProfile? sourceFilter)
        {
            if (materials == null || materials.Length == 0)
            {
                return default;
            }

            if (targetShader == null)
            {
                Debug.LogError($"Cannot convert URP materials because shader '{TargetShaderName}' was not found.");
                return new ConversionSummary(materials.Length, 0, materials.Length, 0);
            }

            int totalCount = 0;
            int convertedCount = 0;
            int skippedCount = 0;
            int unsupportedCount = 0;
            HashSet<Material> visitedMaterials = new HashSet<Material>();

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !visitedMaterials.Add(material))
                {
                    continue;
                }

                totalCount++;
                if (!TryGetSourceProfile(material, sourceFilter, out SourceShaderProfile sourceProfile))
                {
                    skippedCount++;
                    continue;
                }

                if (HasUnsupportedSurfaceMode(material, out string skipReason))
                {
                    unsupportedCount++;
                    Debug.LogWarning($"Skipped URP Lit material '{AssetDatabase.GetAssetPath(material)}': {skipReason}");
                    continue;
                }

                UrpLitSnapshot snapshot = UrpLitSnapshot.Capture(material, sourceProfile);
                if (recordUndo)
                {
                    Undo.RecordObject(material, UndoName);
                }

                material.shader = targetShader;
                snapshot.ApplyTo(material);
                material.shaderKeywords = Array.Empty<string>();
                EditorUtility.SetDirty(material);
                convertedCount++;
            }

            return new ConversionSummary(totalCount, convertedCount, skippedCount, unsupportedCount);
        }

        private static void ConvertMaterialsWithDialog(Material[] materials, SourceShaderProfile sourceProfile, string prompt)
        {
            Shader targetShader = Shader.Find(TargetShaderName);
            if (targetShader == null)
            {
                EditorUtility.DisplayDialog("NWRP Material Convert", $"Shader '{TargetShaderName}' was not found.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "NWRP Material Convert",
                    prompt + $"\n\nTransparent and alpha-clipped {sourceProfile.DisplayName} materials are skipped because the target StandardLit shader is opaque-only.",
                    "Convert",
                    "Cancel"))
            {
                return;
            }

            ConversionSummary summary = ConvertMaterials(materials, targetShader, recordUndo: true, sourceProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "NWRP Material Convert",
                $"Converted: {summary.ConvertedCount}\nSkipped non-matching: {summary.SkippedCount}\nSkipped unsupported: {summary.UnsupportedCount}",
                "OK");
        }

        private static Material[] FindAllProjectMaterials()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            List<Material> materials = new List<Material>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    materials.Add(material);
                }
            }

            return materials.ToArray();
        }

        private static bool TryGetSourceProfile(Material material, SourceShaderProfile? sourceFilter, out SourceShaderProfile sourceProfile)
        {
            sourceProfile = default;
            if (material == null || material.shader == null)
            {
                return false;
            }

            if (sourceFilter.HasValue)
            {
                sourceProfile = sourceFilter.Value;
                return material.shader.name == sourceProfile.ShaderName;
            }

            for (int i = 0; i < s_AllSourceProfiles.Length; i++)
            {
                SourceShaderProfile profile = s_AllSourceProfiles[i];
                if (material.shader.name == profile.ShaderName)
                {
                    sourceProfile = profile;
                    return true;
                }
            }

            return false;
        }

        private static bool HasUnsupportedSurfaceMode(Material material, out string reason)
        {
            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f)
            {
                reason = "transparent surface mode has no equivalent in NewWorld/Lit/StandardLit.";
                return true;
            }

            if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
            {
                reason = "alpha clipping has no equivalent in NewWorld/Lit/StandardLit.";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        internal readonly struct ConversionSummary
        {
            public ConversionSummary(int totalCount, int convertedCount, int skippedCount, int unsupportedCount)
            {
                TotalCount = totalCount;
                ConvertedCount = convertedCount;
                SkippedCount = skippedCount;
                UnsupportedCount = unsupportedCount;
            }

            public int TotalCount { get; }
            public int ConvertedCount { get; }
            public int SkippedCount { get; }
            public int UnsupportedCount { get; }
        }

        private readonly struct SourceShaderProfile
        {
            public SourceShaderProfile(string shaderName, string displayName, string maskMapPropertyName, bool forceDielectricMetallic)
            {
                ShaderName = shaderName;
                DisplayName = displayName;
                MaskMapPropertyName = maskMapPropertyName;
                ForceDielectricMetallic = forceDielectricMetallic;
            }

            public string ShaderName { get; }
            public string DisplayName { get; }
            public string MaskMapPropertyName { get; }
            public bool ForceDielectricMetallic { get; }
        }

        private readonly struct UrpLitSnapshot
        {
            private readonly bool m_HasBaseColor;
            private readonly Color m_BaseColor;
            private readonly bool m_HasMetallic;
            private readonly float m_Metallic;
            private readonly bool m_HasSmoothness;
            private readonly float m_Smoothness;
            private readonly bool m_HasOcclusionStrength;
            private readonly float m_OcclusionStrength;
            private readonly bool m_HasNormalStrength;
            private readonly float m_NormalStrength;
            private readonly bool m_HasEmissionColor;
            private readonly Color m_EmissionColor;
            private readonly bool m_EnableInstancing;
            private readonly TextureSlot m_BaseMap;
            private readonly TextureSlot m_MaskMap;
            private readonly TextureSlot m_NormalMap;
            private readonly TextureSlot m_EmissiveMap;

            private UrpLitSnapshot(
                bool hasBaseColor,
                Color baseColor,
                bool hasMetallic,
                float metallic,
                bool hasSmoothness,
                float smoothness,
                bool hasOcclusionStrength,
                float occlusionStrength,
                bool hasNormalStrength,
                float normalStrength,
                bool hasEmissionColor,
                Color emissionColor,
                bool enableInstancing,
                TextureSlot baseMap,
                TextureSlot maskMap,
                TextureSlot normalMap,
                TextureSlot emissiveMap)
            {
                m_HasBaseColor = hasBaseColor;
                m_BaseColor = baseColor;
                m_HasMetallic = hasMetallic;
                m_Metallic = metallic;
                m_HasSmoothness = hasSmoothness;
                m_Smoothness = smoothness;
                m_HasOcclusionStrength = hasOcclusionStrength;
                m_OcclusionStrength = occlusionStrength;
                m_HasNormalStrength = hasNormalStrength;
                m_NormalStrength = normalStrength;
                m_HasEmissionColor = hasEmissionColor;
                m_EmissionColor = emissionColor;
                m_EnableInstancing = enableInstancing;
                m_BaseMap = baseMap;
                m_MaskMap = maskMap;
                m_NormalMap = normalMap;
                m_EmissiveMap = emissiveMap;
            }

            public static UrpLitSnapshot Capture(Material material, SourceShaderProfile sourceProfile)
            {
                bool hasMetallic = material.HasProperty("_Metallic") || sourceProfile.ForceDielectricMetallic;
                return new UrpLitSnapshot(
                    material.HasProperty("_BaseColor"),
                    material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white,
                    hasMetallic,
                    material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0.0f,
                    material.HasProperty("_Smoothness"),
                    material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.5f,
                    material.HasProperty("_OcclusionStrength"),
                    material.HasProperty("_OcclusionStrength") ? material.GetFloat("_OcclusionStrength") : 1.0f,
                    material.HasProperty("_BumpScale"),
                    material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1.0f,
                    material.HasProperty("_EmissionColor"),
                    material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black,
                    material.enableInstancing,
                    TextureSlot.Capture(material, "_BaseMap"),
                    TextureSlot.Capture(material, sourceProfile.MaskMapPropertyName),
                    TextureSlot.Capture(material, "_BumpMap"),
                    TextureSlot.Capture(material, "_EmissionMap"));
            }

            public void ApplyTo(Material material)
            {
                if (m_HasBaseColor && material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", m_BaseColor);
                }

                if (m_HasMetallic && material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", m_Metallic);
                }

                if (m_HasSmoothness && material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", m_Smoothness);
                }

                if (m_HasOcclusionStrength && material.HasProperty("_OcclusionStrength"))
                {
                    material.SetFloat("_OcclusionStrength", m_OcclusionStrength);
                }

                if (m_HasNormalStrength && material.HasProperty("_NormalStrength"))
                {
                    material.SetFloat("_NormalStrength", m_NormalStrength);
                }

                if (m_HasEmissionColor && material.HasProperty("_EmissiveColor"))
                {
                    material.SetColor("_EmissiveColor", m_EmissionColor);
                }

                m_BaseMap.ApplyTo(material, "_BaseMap");
                m_MaskMap.ApplyTo(material, "_MaskMap");
                m_NormalMap.ApplyTo(material, "_NormalMap");
                m_EmissiveMap.ApplyTo(material, "_EmissiveMap");
                material.enableInstancing = m_EnableInstancing;
            }
        }

        private readonly struct TextureSlot
        {
            private readonly bool m_HasTexture;
            private readonly Texture m_Texture;
            private readonly Vector2 m_Scale;
            private readonly Vector2 m_Offset;

            private TextureSlot(bool hasTexture, Texture texture, Vector2 scale, Vector2 offset)
            {
                m_HasTexture = hasTexture;
                m_Texture = texture;
                m_Scale = scale;
                m_Offset = offset;
            }

            public static TextureSlot Capture(Material material, string propertyName)
            {
                if (string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
                {
                    return default;
                }

                return new TextureSlot(
                    true,
                    material.GetTexture(propertyName),
                    material.GetTextureScale(propertyName),
                    material.GetTextureOffset(propertyName));
            }

            public void ApplyTo(Material material, string propertyName)
            {
                if (!m_HasTexture || !material.HasProperty(propertyName))
                {
                    return;
                }

                material.SetTexture(propertyName, m_Texture);
                material.SetTextureScale(propertyName, m_Scale);
                material.SetTextureOffset(propertyName, m_Offset);
            }
        }
    }
}
