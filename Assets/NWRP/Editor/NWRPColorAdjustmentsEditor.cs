using NWRP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPColorAdjustments))]
    internal sealed class NWRPColorAdjustmentsEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _saturate;
        private SerializedDataParameter _brightness;
        private SerializedDataParameter _contrast;
        private SerializedDataParameter _daltonize;
        private SerializedDataParameter _sepia;
        private SerializedDataParameter _tintColor;
        private SerializedDataParameter _colorTempBlend;
        private SerializedDataParameter _colorTemp;

        public override void OnEnable()
        {
            _saturate = FindParameter("saturate");
            _brightness = FindParameter("brightness");
            _contrast = FindParameter("contrast");
            _daltonize = FindParameter("daltonize");
            _sepia = FindParameter("sepia");
            _tintColor = FindParameter("tintColor");
            _colorTempBlend = FindParameter("colorTempBlend");
            _colorTemp = FindParameter("colorTemp");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_saturate);
            PropertyField(_brightness);
            PropertyField(_contrast);
            PropertyField(_daltonize);
            PropertyField(_sepia);
            PropertyField(_tintColor);

            DrawHeader("White Balance");
            PropertyField(_colorTempBlend);
            PropertyField(_colorTemp);
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
