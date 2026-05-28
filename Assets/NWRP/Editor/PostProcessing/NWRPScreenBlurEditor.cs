using NWRP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPScreenBlur))]
    internal sealed class NWRPScreenBlurEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _radius;
        private SerializedDataParameter _iterations;
        private SerializedDataParameter _injectionPoint;

        public override void OnEnable()
        {
            _radius = FindParameter("radius");
            _iterations = FindParameter("iterations");
            _injectionPoint = FindParameter("injectionPoint");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_radius);
            PropertyField(_iterations);
            PropertyField(_injectionPoint);
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
