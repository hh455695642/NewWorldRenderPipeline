using UnityEditor;
using UnityEngine;

namespace NWRP.Editor
{
    /// <summary>
    /// NewWorld Shader 自定义材质面板。
    ///
    /// 功能：
    ///   - 按分组折叠显示属性（Surface / Mask / Normal / Emission）
    ///   - 贴图槽与对应参数在同一行显示
    ///   - 隐藏默认的 Render Queue / Double Sided GI 等非必要选项
    ///   - 支持所有 NewWorld Shader（通过 CustomEditor 指定）
    ///
    /// 用法：在 Shader 末尾添加  CustomEditor "NWRP.Editor.NewWorldShaderGUI"
    /// </summary>
    public class NewWorldShaderGUI : ShaderGUI
    {
        // 折叠状态
        private bool _surfaceFoldout = true;
        private bool _maskFoldout    = true;
        private bool _normalFoldout  = true;
        private bool _emissionFoldout = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material material = materialEditor.target as Material;

            // ── 标题 ────────────────────────────────────────────
            EditorGUILayout.LabelField("New World Render Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(material.shader.name, EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // ── 检测属性是否存在，自适应不同 Shader ──────────────
            bool hasBaseMap    = FindProperty("_BaseMap", properties, false) != null;
            bool hasMaskMap    = FindProperty("_MaskMap", properties, false) != null;
            bool hasNormalMap  = FindProperty("_NormalMap", properties, false) != null;
            bool hasEmissive   = FindProperty("_EmissiveMap", properties, false) != null;
            bool hasSpecular   = FindProperty("_SpecularColor", properties, false) != null;

            // ── Surface ─────────────────────────────────────────
            _surfaceFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_surfaceFoldout, "Surface");
            if (_surfaceFoldout)
            {
                EditorGUI.indentLevel++;

                // BaseColor 始终显示
                var baseColor = FindProperty("_BaseColor", properties, false);
                if (baseColor != null)
                {
                    if (hasBaseMap)
                    {
                        var baseMap = FindProperty("_BaseMap", properties);
                        materialEditor.TexturePropertySingleLine(
                            new GUIContent("Base Map", "RGB = Albedo"),
                            baseMap, baseColor
                        );
                        materialEditor.TextureScaleOffsetProperty(baseMap);
                    }
                    else
                    {
                        materialEditor.ShaderProperty(baseColor, "Base Color");
                    }
                }

                // Albedo（简单 PBR Shader 用 _Albedo 而非 _BaseColor）
                var albedo = FindProperty("_Albedo", properties, false);
                if (albedo != null)
                    materialEditor.ShaderProperty(albedo, "Albedo");

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Mask / PBR 参数 ─────────────────────────────────
            bool hasPBRParams = FindProperty("_Metallic", properties, false) != null
                             || FindProperty("_Smoothness", properties, false) != null
                             || FindProperty("_Roughness", properties, false) != null;

            if (hasPBRParams || hasMaskMap)
            {
                _maskFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_maskFoldout, "PBR / Mask");
                if (_maskFoldout)
                {
                    EditorGUI.indentLevel++;

                    if (hasMaskMap)
                    {
                        var maskMap = FindProperty("_MaskMap", properties);
                        materialEditor.TexturePropertySingleLine(
                            new GUIContent("Mask Map", "R=Metallic  G=AO  A=Smoothness"),
                            maskMap
                        );
                    }

                    DrawPropertyIfExists(materialEditor, properties, "_Metallic", "Metallic");
                    DrawPropertyIfExists(materialEditor, properties, "_Smoothness", "Smoothness");
                    DrawPropertyIfExists(materialEditor, properties, "_Roughness", "Roughness");
                    DrawPropertyIfExists(materialEditor, properties, "_OcclusionStrength", "AO Strength");

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            // ── Specular（Phong / BlinnPhong Shader） ────────────
            if (hasSpecular)
            {
                var specColor = FindProperty("_SpecularColor", properties);
                materialEditor.ShaderProperty(specColor, "Specular Color");
            }

            // ── Normal ──────────────────────────────────────────
            if (hasNormalMap)
            {
                _normalFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_normalFoldout, "Normal Map");
                if (_normalFoldout)
                {
                    EditorGUI.indentLevel++;

                    var normalMap = FindProperty("_NormalMap", properties);
                    var normalStrength = FindProperty("_NormalStrength", properties, false);
                    materialEditor.TexturePropertySingleLine(
                        new GUIContent("Normal Map", "Tangent Space Normal"),
                        normalMap, normalStrength
                    );

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            // ── Emission ────────────────────────────────────────
            if (hasEmissive)
            {
                _emissionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_emissionFoldout, "Emission");
                if (_emissionFoldout)
                {
                    EditorGUI.indentLevel++;

                    var emissiveMap = FindProperty("_EmissiveMap", properties);
                    var emissiveColor = FindProperty("_EmissiveColor", properties, false);
                    materialEditor.TexturePropertySingleLine(
                        new GUIContent("Emissive Map", "RGB = Emission"),
                        emissiveMap, emissiveColor
                    );

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            // ── 其他未分类属性（兜底：NPR Shader 的专有属性等） ──
            EditorGUILayout.Space(6);
            bool hasUncategorized = false;

            foreach (var prop in properties)
            {
                if ((prop.flags & MaterialProperty.PropFlags.HideInInspector) != 0)
                    continue;

                // 跳过已经在上面分组中绘制过的属性
                if (IsHandledProperty(prop.name))
                    continue;

                if (!hasUncategorized)
                {
                    EditorGUILayout.LabelField("Other", EditorStyles.boldLabel);
                    hasUncategorized = true;
                }

                materialEditor.ShaderProperty(prop, prop.displayName);
            }

            // ── 不显示默认的 Render Queue / DSGI ─────────────────
            // 有意省略 materialEditor.RenderQueueField()
            // 有意省略 materialEditor.DoubleSidedGIField()
            // 有意省略 materialEditor.EnableInstancingField()
        }

        // 辅助：如果属性存在则绘制
        private void DrawPropertyIfExists(MaterialEditor editor, MaterialProperty[] props,
                                           string name, string label)
        {
            var prop = FindProperty(name, props, false);
            if (prop != null)
                editor.ShaderProperty(prop, label);
        }

        // 已分组处理的属性名集合
        private static bool IsHandledProperty(string name)
        {
            switch (name)
            {
                case "_BaseColor":
                case "_BaseMap":
                case "_Albedo":
                case "_MaskMap":
                case "_Metallic":
                case "_Smoothness":
                case "_Roughness":
                case "_OcclusionStrength":
                case "_SpecularColor":
                case "_NormalMap":
                case "_NormalStrength":
                case "_EmissiveMap":
                case "_EmissiveColor":
                    return true;
                default:
                    return false;
            }
        }

        // 查找属性（带静默失败选项）
        private static MaterialProperty FindProperty(string name, MaterialProperty[] props, bool mandatory)
        {
            foreach (var prop in props)
            {
                if (prop.name == name)
                    return prop;
            }
            if (mandatory)
                throw new System.ArgumentException($"Property '{name}' not found in material properties.");
            return null;
        }
    }
}
