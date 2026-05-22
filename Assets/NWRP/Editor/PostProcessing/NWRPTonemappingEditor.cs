using NWRP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPTonemapping))]
    internal sealed class NWRPTonemappingEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _mode;
        private SerializedDataParameter _preExposure;
        private SerializedDataParameter _postBrightness;
        private SerializedDataParameter _maxInputBrightness;
        private SerializedDataParameter _agxGamma;

        public override void OnEnable()
        {
            _mode = FindParameter("mode");
            _preExposure = FindParameter("preExposure");
            _postBrightness = FindParameter("postBrightness");
            _maxInputBrightness = FindParameter("maxInputBrightness");
            _agxGamma = FindParameter("agxGamma");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_mode);

            if (!_mode.overrideState.boolValue)
            {
                return;
            }

            NWRPTonemappingMode mode = (NWRPTonemappingMode)_mode.value.intValue;
            if (mode == NWRPTonemappingMode.None)
            {
                return;
            }

            PropertyField(_preExposure);
            PropertyField(_postBrightness);
            PropertyField(_maxInputBrightness);

            if (mode == NWRPTonemappingMode.AGX)
            {
                PropertyField(_agxGamma);
            }
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
