using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
#endif

namespace NWRP.Editor
{
    [InitializeOnLoad]
    internal static class NWRPMaterialMainToolbar
    {
        private const string ButtonName = "NWRPConvertUrpLitMaterialsButton";
        private const string ButtonText = "URP Lit -> NWRP";
        private const string ButtonTooltip = "Convert all opaque URP Lit material assets under Assets to NewWorld/Lit/StandardLit.";

        private static readonly Type s_ToolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
        private static bool s_ButtonAdded;

        static NWRPMaterialMainToolbar()
        {
            EditorApplication.update += TryAddButton;
        }

        private static void TryAddButton()
        {
            if (s_ButtonAdded || s_ToolbarType == null)
            {
                EditorApplication.update -= TryAddButton;
                return;
            }

            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(s_ToolbarType);
            if (toolbars == null || toolbars.Length == 0)
            {
                return;
            }

            FieldInfo rootField = s_ToolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(rootField?.GetValue(toolbars[0]) is VisualElement root))
            {
                return;
            }

            VisualElement rightZone = root.Q("ToolbarZoneRightAlign");
            if (rightZone == null || rightZone.Q<Button>(ButtonName) != null)
            {
                return;
            }

            Button button = new Button(NWRPMaterialConverter.ConvertAllProjectMaterialsWithDialog)
            {
                name = ButtonName,
                text = ButtonText,
                tooltip = ButtonTooltip
            };
            button.style.marginLeft = 4;
            button.style.marginRight = 4;
            rightZone.Add(button);

            s_ButtonAdded = true;
            EditorApplication.update -= TryAddButton;
        }
    }

#if UNITY_2021_2_OR_NEWER
    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class NWRPConvertUrpLitToolbarButton : EditorToolbarButton
    {
        internal const string Id = "NWRP/ConvertURPLitMaterials";

        public NWRPConvertUrpLitToolbarButton()
        {
            text = "URP Lit -> NWRP";
            tooltip = "Convert all opaque URP Lit material assets under Assets to NewWorld/Lit/StandardLit.";
            clicked += NWRPMaterialConverter.ConvertAllProjectMaterialsWithDialog;
        }
    }

    [Overlay(typeof(SceneView), "NWRP Materials", true)]
    internal sealed class NWRPMaterialToolbarOverlay : ToolbarOverlay
    {
        public NWRPMaterialToolbarOverlay()
            : base(NWRPConvertUrpLitToolbarButton.Id)
        {
        }
    }
#endif
}
