using NWRP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPFog))]
    internal sealed class NWRPFogEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _enableFog;
        private SerializedDataParameter _mode;
        private SerializedDataParameter _color;
        private SerializedDataParameter _startDistance;
        private SerializedDataParameter _endDistance;
        private SerializedDataParameter _density;

        public override void OnEnable()
        {
            _enableFog = FindParameter("enableFog");
            _mode = FindParameter("mode");
            _color = FindParameter("color");
            _startDistance = FindParameter("startDistance");
            _endDistance = FindParameter("endDistance");
            _density = FindParameter("density");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_enableFog);
            PropertyField(_mode);
            PropertyField(_color);

            if (!_mode.overrideState.boolValue)
            {
                PropertyField(_startDistance);
                PropertyField(_endDistance);
                PropertyField(_density);
                return;
            }

            NWRPFogMode mode = (NWRPFogMode)_mode.value.intValue;
            if (mode == NWRPFogMode.Linear)
            {
                PropertyField(_startDistance);
                PropertyField(_endDistance);
            }
            else if (mode == NWRPFogMode.Exp || mode == NWRPFogMode.Exp2)
            {
                PropertyField(_density);
            }
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
