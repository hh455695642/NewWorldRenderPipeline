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

            if (ShouldDrawAgxGamma(mode))
            {
                PropertyField(_agxGamma);
            }

            if (!ShouldDrawExposureControls(mode))
            {
                return;
            }

            PropertyField(_maxInputBrightness);
            PropertyField(_preExposure);
            PropertyField(_postBrightness);
        }

        private static bool ShouldDrawExposureControls(NWRPTonemappingMode mode)
        {
            return mode != NWRPTonemappingMode.None &&
                mode != NWRPTonemappingMode.Linear;
        }

        private static bool ShouldDrawAgxGamma(NWRPTonemappingMode mode)
        {
            return mode == NWRPTonemappingMode.AGX;
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
