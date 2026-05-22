using NWRP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPAntiAliasing))]
    internal sealed class NWRPAntiAliasingEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _mode;
        private SerializedDataParameter _fixedThreshold;
        private SerializedDataParameter _relativeThreshold;
        private SerializedDataParameter _subpixelBlending;

        public override void OnEnable()
        {
            _mode = FindParameter("mode");
            _fixedThreshold = FindParameter("fixedThreshold");
            _relativeThreshold = FindParameter("relativeThreshold");
            _subpixelBlending = FindParameter("subpixelBlending");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_mode);

            if (!_mode.overrideState.boolValue
                || (NWRPAntiAliasingMode)_mode.value.intValue != NWRPAntiAliasingMode.FXAA)
            {
                return;
            }

            PropertyField(_fixedThreshold);
            PropertyField(_relativeThreshold);
            PropertyField(_subpixelBlending);
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
