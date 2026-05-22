using NWRP;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPBloom))]
    internal sealed class NWRPBloomEditor : VolumeComponentEditor
    {
        private static class Styles
        {
            public static readonly GUIContent[] WeightLabels =
            {
                EditorGUIUtility.TrTextContent("Weight 0"),
                EditorGUIUtility.TrTextContent("Weight 1"),
                EditorGUIUtility.TrTextContent("Weight 2"),
                EditorGUIUtility.TrTextContent("Weight 3"),
                EditorGUIUtility.TrTextContent("Weight 4"),
                EditorGUIUtility.TrTextContent("Weight 5")
            };

            public static readonly GUIContent[] BoostLabels =
            {
                EditorGUIUtility.TrTextContent("Boost 0"),
                EditorGUIUtility.TrTextContent("Boost 1"),
                EditorGUIUtility.TrTextContent("Boost 2"),
                EditorGUIUtility.TrTextContent("Boost 3"),
                EditorGUIUtility.TrTextContent("Boost 4"),
                EditorGUIUtility.TrTextContent("Boost 5")
            };

            public static readonly GUIContent[] TintLabels =
            {
                EditorGUIUtility.TrTextContent("Tint 0"),
                EditorGUIUtility.TrTextContent("Tint 1"),
                EditorGUIUtility.TrTextContent("Tint 2"),
                EditorGUIUtility.TrTextContent("Tint 3"),
                EditorGUIUtility.TrTextContent("Tint 4"),
                EditorGUIUtility.TrTextContent("Tint 5")
            };
        }

        private SerializedDataParameter _intensity;
        private SerializedDataParameter _threshold;
        private SerializedDataParameter _conservativeThreshold;
        private SerializedDataParameter _spread;
        private SerializedDataParameter _maxBrightness;
        private SerializedDataParameter _tint;
        private SerializedDataParameter _antiflicker;
        private SerializedDataParameter _resolution;
        private SerializedDataParameter _quickerBlur;
        private SerializedDataParameter _customize;
        private readonly SerializedDataParameter[] _weights = new SerializedDataParameter[6];
        private readonly SerializedDataParameter[] _boosts = new SerializedDataParameter[6];
        private readonly SerializedDataParameter[] _tints = new SerializedDataParameter[6];
        private SerializedDataParameter _lensDirtIntensity;
        private SerializedDataParameter _lensDirtThreshold;
        private SerializedDataParameter _lensDirtTexture;
        private SerializedDataParameter _lensDirtSpread;

        public override void OnEnable()
        {
            _intensity = FindParameter("intensity");
            _threshold = FindParameter("threshold");
            _conservativeThreshold = FindParameter("conservativeThreshold");
            _spread = FindParameter("spread");
            _maxBrightness = FindParameter("maxBrightness");
            _tint = FindParameter("tint");
            _antiflicker = FindParameter("antiflicker");
            _resolution = FindParameter("resolution");
            _quickerBlur = FindParameter("quickerBlur");
            _customize = FindParameter("customize");

            _weights[0] = FindParameter("weight0");
            _weights[1] = FindParameter("weight1");
            _weights[2] = FindParameter("weight2");
            _weights[3] = FindParameter("weight3");
            _weights[4] = FindParameter("weight4");
            _weights[5] = FindParameter("weight5");

            _boosts[0] = FindParameter("boost0");
            _boosts[1] = FindParameter("boost1");
            _boosts[2] = FindParameter("boost2");
            _boosts[3] = FindParameter("boost3");
            _boosts[4] = FindParameter("boost4");
            _boosts[5] = FindParameter("boost5");

            _tints[0] = FindParameter("tint0");
            _tints[1] = FindParameter("tint1");
            _tints[2] = FindParameter("tint2");
            _tints[3] = FindParameter("tint3");
            _tints[4] = FindParameter("tint4");
            _tints[5] = FindParameter("tint5");

            _lensDirtIntensity = FindParameter("lensDirtIntensity");
            _lensDirtThreshold = FindParameter("lensDirtThreshold");
            _lensDirtTexture = FindParameter("lensDirtTexture");
            _lensDirtSpread = FindParameter("lensDirtSpread");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_intensity);
            PropertyField(_threshold);
            PropertyField(_conservativeThreshold);
            PropertyField(_spread);
            PropertyField(_maxBrightness);
            PropertyField(_tint);
            PropertyField(_antiflicker);
            PropertyField(_resolution);
            PropertyField(_quickerBlur);
            PropertyField(_customize);

            if (_customize.overrideState.boolValue && _customize.value.boolValue)
            {
                DrawHeader("Custom Layer Controls");
                EditorGUI.indentLevel++;
                for (int i = 0; i < 6; i++)
                {
                    PropertyField(_weights[i], Styles.WeightLabels[i]);
                    PropertyField(_boosts[i], Styles.BoostLabels[i]);
                    PropertyField(_tints[i], Styles.TintLabels[i]);
                }
                EditorGUI.indentLevel--;
            }

            PropertyField(_lensDirtIntensity);
            PropertyField(_lensDirtThreshold);
            PropertyField(_lensDirtTexture);
            PropertyField(_lensDirtSpread);
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
