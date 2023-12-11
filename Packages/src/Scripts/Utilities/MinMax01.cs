using System;
using UnityEditor;
using UnityEngine;

namespace Coffee.UISoftMask
{
    [Serializable]
    public struct MinMax01
    {
        [SerializeField]
        private float m_Min;

        [SerializeField]
        private float m_Max;

        public MinMax01(float min, float max)
        {
            m_Min = Mathf.Clamp01(Mathf.Min(min, max));
            m_Max = Mathf.Clamp01(Mathf.Max(min, max));
        }

        public float min
        {
            get => m_Min;
            set
            {
                m_Min = Mathf.Clamp01(value);
                m_Max = Mathf.Max(value, m_Max);
            }
        }

        public float max
        {
            get => m_Max;
            set
            {
                m_Max = Mathf.Clamp01(value);
                m_Min = Mathf.Min(value, m_Min);
            }
        }

        public bool Approximately(MinMax01 other)
        {
            return Mathf.Approximately(m_Min, other.m_Min) && Mathf.Approximately(m_Max, other.m_Max);
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MinMax01))]
    public class MinMaxRangeDrawer : PropertyDrawer
    {
        private const float k_NumWidth = 50;
        private const float k_Space = 5;
        private SerializedProperty _max;
        private SerializedProperty _min;

        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, prop);

            if (_min == null)
            {
                prop.Next(true);
                _min = prop.Copy();
                prop.Next(true);
                _max = prop.Copy();
            }

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var min = _min.floatValue;
            var max = _max.floatValue;

            EditorGUI.BeginChangeCheck();

            var rect = new Rect(position.x, position.y, k_NumWidth, position.height);
            min = Mathf.Clamp(EditorGUI.FloatField(rect, min), 0, max);

            rect.x += rect.width + k_Space;
            rect.width = position.width - k_NumWidth * 2 - k_Space * 2;
            EditorGUI.MinMaxSlider(rect, ref min, ref max, 0, 1);

            rect.x += rect.width + k_Space;
            rect.width = k_NumWidth;
            max = Mathf.Clamp(EditorGUI.FloatField(rect, max), min, 1);

            if (EditorGUI.EndChangeCheck())
            {
                _min.floatValue = min;
                _max.floatValue = max;
            }

            EditorGUI.EndProperty();
        }


        public static bool Draw(Rect position, GUIContent label, ref float minValue, ref float maxValue)
        {
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            EditorGUI.BeginChangeCheck();

            var rect = new Rect(position.x, position.y, k_NumWidth, position.height);
            minValue = Mathf.Clamp(EditorGUI.FloatField(rect, minValue), 0, maxValue);

            rect.x += rect.width + k_Space;
            rect.width = position.width - k_NumWidth * 2 - k_Space * 2;
            EditorGUI.MinMaxSlider(rect, ref minValue, ref maxValue, 0, 1);

            rect.x += rect.width + k_Space;
            rect.width = k_NumWidth;
            maxValue = Mathf.Clamp(EditorGUI.FloatField(rect, maxValue), minValue, 1);

            return EditorGUI.EndChangeCheck();
        }
    }
#endif
}
