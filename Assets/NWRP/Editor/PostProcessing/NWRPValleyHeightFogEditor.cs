using NWRP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NWRP.Editor
{
    [CustomEditor(typeof(NWRPValleyHeightFog))]
    internal sealed class NWRPValleyHeightFogEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _enable;
        private SerializedDataParameter _mode;
        private SerializedProperty _modeValue;
        private SerializedDataParameter _fogColor;
        private SerializedDataParameter _fogBaseHeight;
        private SerializedDataParameter _heightDensity;
        private SerializedDataParameter _fogStart;
        private SerializedDataParameter _fogLength;
        private SerializedDataParameter _noiseScale;
        private SerializedDataParameter _noiseIntensity;
        private SerializedDataParameter _noiseSpeed;
        private SerializedDataParameter _noiseRoughness;
        private SerializedDataParameter _noisePersistance;
        private SerializedDataParameter _bottomHeight;
        private SerializedDataParameter _bottomFade;
        private SerializedDataParameter _bottomDensity;
        private SerializedDataParameter _bottomIntensity;
        private SerializedDataParameter _bottomNoiseScale;
        private SerializedDataParameter _bottomNoiseIntensity;
        private SerializedDataParameter _midHeight;
        private SerializedDataParameter _midFade;
        private SerializedDataParameter _midDensity;
        private SerializedDataParameter _midIntensity;
        private SerializedDataParameter _midNoiseScale;
        private SerializedDataParameter _midNoiseIntensity;
        private SerializedDataParameter _topIntensity;
        private SerializedDataParameter _topDensity;
        private SerializedDataParameter _topNoiseScale;
        private SerializedDataParameter _topNoiseIntensity;
        private SerializedDataParameter _threeLayerNoiseSpeed;
        private SerializedDataParameter _threeLayerNoiseRoughness;
        private SerializedDataParameter _threeLayerNoisePersistance;

        public override void OnEnable()
        {
            _enable = FindParameter("enable");
            _mode = FindParameter("mode");
            _modeValue = serializedObject
                .FindProperty("mode")
                .FindPropertyRelative("m_Value");
            _fogColor = FindParameter("fogColor");
            _fogBaseHeight = FindParameter("fogBaseHeight");
            _heightDensity = FindParameter("heightDensity");
            _fogStart = FindParameter("fogStart");
            _fogLength = FindParameter("fogLength");
            _noiseScale = FindParameter("noiseScale");
            _noiseIntensity = FindParameter("noiseIntensity");
            _noiseSpeed = FindParameter("noiseSpeed");
            _noiseRoughness = FindParameter("noiseRoughness");
            _noisePersistance = FindParameter("noisePersistance");
            _bottomHeight = FindParameter("bottomHeight");
            _bottomFade = FindParameter("bottomFade");
            _bottomDensity = FindParameter("bottomDensity");
            _bottomIntensity = FindParameter("bottomIntensity");
            _bottomNoiseScale = FindParameter("bottomNoiseScale");
            _bottomNoiseIntensity = FindParameter("bottomNoiseIntensity");
            _midHeight = FindParameter("midHeight");
            _midFade = FindParameter("midFade");
            _midDensity = FindParameter("midDensity");
            _midIntensity = FindParameter("midIntensity");
            _midNoiseScale = FindParameter("midNoiseScale");
            _midNoiseIntensity = FindParameter("midNoiseIntensity");
            _topIntensity = FindParameter("topIntensity");
            _topDensity = FindParameter("topDensity");
            _topNoiseScale = FindParameter("topNoiseScale");
            _topNoiseIntensity = FindParameter("topNoiseIntensity");
            _threeLayerNoiseSpeed = FindParameter("threeLayerNoiseSpeed");
            _threeLayerNoiseRoughness = FindParameter("threeLayerNoiseRoughness");
            _threeLayerNoisePersistance = FindParameter("threeLayerNoisePersistance");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_enable);
            PropertyField(_mode);

            NWRPValleyHeightFogMode mode =
                (NWRPValleyHeightFogMode)_modeValue.enumValueIndex;
            if (mode == NWRPValleyHeightFogMode.ThreeLayer)
            {
                DrawThreeLayerInspector();
                return;
            }

            DrawSingleLayerInspector();
        }

        private void DrawSingleLayerInspector()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Fog Base", EditorStyles.boldLabel);
            PropertyField(_fogColor);
            PropertyField(_fogBaseHeight);
            PropertyField(_heightDensity);
            PropertyField(_fogStart);
            PropertyField(_fogLength);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Fog Noise", EditorStyles.boldLabel);
            PropertyField(_noiseScale);
            PropertyField(_noiseIntensity);
            PropertyField(_noiseSpeed);
            PropertyField(_noiseRoughness);
            PropertyField(_noisePersistance);
        }

        private void DrawThreeLayerInspector()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Fog Base", EditorStyles.boldLabel);
            PropertyField(_fogColor);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Bottom Fog Layer", EditorStyles.boldLabel);
            PropertyField(_bottomHeight);
            PropertyField(_bottomFade);
            PropertyField(_bottomDensity);
            PropertyField(_bottomIntensity);
            PropertyField(_bottomNoiseScale);
            PropertyField(_bottomNoiseIntensity);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Mid Fog Layer", EditorStyles.boldLabel);
            PropertyField(_midHeight);
            PropertyField(_midFade);
            PropertyField(_midDensity);
            PropertyField(_midIntensity);
            PropertyField(_midNoiseScale);
            PropertyField(_midNoiseIntensity);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Top Fog Layer", EditorStyles.boldLabel);
            PropertyField(_topIntensity);
            PropertyField(_topDensity);
            PropertyField(_topNoiseScale);
            PropertyField(_topNoiseIntensity);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Distance Fog", EditorStyles.boldLabel);
            PropertyField(_fogStart);
            PropertyField(_fogLength);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Noise Settings", EditorStyles.boldLabel);
            PropertyField(_threeLayerNoiseSpeed);
            PropertyField(_threeLayerNoiseRoughness);
            PropertyField(_threeLayerNoisePersistance);
        }

        private SerializedDataParameter FindParameter(string propertyName)
        {
            return Unpack(serializedObject.FindProperty(propertyName));
        }
    }
}
