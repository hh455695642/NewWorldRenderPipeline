using NWRP;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPVignette))]
    internal sealed class NWRPVignetteEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _outerRing;
        private SerializedDataParameter _innerRing;
        private SerializedDataParameter _fade;
        private SerializedDataParameter _center;
        private SerializedDataParameter _color;
        private SerializedDataParameter _circularShape;
        private SerializedDataParameter _fitMode;
        private SerializedDataParameter _aspectRatio;

        public override void OnEnable()
        {
            _outerRing = FindParameter("outerRing");
            _innerRing = FindParameter("innerRing");
            _fade = FindParameter("fade");
            _center = FindParameter("center");
            _color = FindParameter("color");
            _circularShape = FindParameter("circularShape");
            _fitMode = FindParameter("fitMode");
            _aspectRatio = FindParameter("aspectRatio");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_outerRing);
            PropertyField(_innerRing);
            PropertyField(_fade);
            PropertyField(_center);
            PropertyField(_color);
            PropertyField(_circularShape);

            bool circularOverride = _circularShape.overrideState.boolValue;
            bool circularEnabled = circularOverride && _circularShape.value.boolValue;
            using (new EditorGUI.DisabledScope(!circularOverride))
            {
                if (circularEnabled)
                {
                    PropertyField(_fitMode);
                }
                else
                {
                    PropertyField(_aspectRatio);
                }
            }
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
