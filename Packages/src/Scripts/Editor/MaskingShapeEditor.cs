using UnityEditor;

namespace Coffee.UISoftMask
{
    [CustomEditor(typeof(MaskingShape), true)]
    [CanEditMultipleObjects]
    public class MaskingShapeEditor : Editor
    {
        private SerializedProperty _alphaHitTest;
        private SerializedProperty _antiAliasing;
        private SerializedProperty _antiAliasingThreshold;
        private SerializedProperty _method;
        private SerializedProperty _showMaskGraphic;
        private SerializedProperty _softnessRange;

        protected void OnEnable()
        {
            _method = serializedObject.FindProperty("m_MaskingMethod");
            _showMaskGraphic = serializedObject.FindProperty("m_ShowMaskGraphic");
            _alphaHitTest = serializedObject.FindProperty("m_AlphaHitTest");
            _antiAliasing = serializedObject.FindProperty("m_AntiAliasing");
            _antiAliasingThreshold = serializedObject.FindProperty("m_AntiAliasingThreshold");
            _softnessRange = serializedObject.FindProperty("m_SoftnessRange");
        }

        public override void OnInspectorGUI()
        {
            var current = target as MaskingShape;
            if (!current) return;

            EditorGUILayout.PropertyField(_method);
            EditorGUILayout.PropertyField(_showMaskGraphic);
            EditorGUILayout.PropertyField(_alphaHitTest);

            Utils.GetStencilDepthAndMask(current.transform, false, out var mask);
            // AntiAliasing is only available in Mask
            EditorGUI.BeginDisabledGroup(mask is SoftMask);
            {
                EditorGUILayout.PropertyField(_antiAliasing);
                EditorGUI.BeginDisabledGroup(!_antiAliasing.boolValue);
                {
                    EditorGUILayout.PropertyField(_antiAliasingThreshold);
                }
            }
            EditorGUI.EndDisabledGroup();

            // Softness is only available in SoftMask
            EditorGUI.BeginDisabledGroup(!(mask is SoftMask));
            {
                EditorGUILayout.PropertyField(_softnessRange);
            }
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            // Draw alpha hit test warning
            if (current.alphaHitTest)
            {
                Utils.DrawAlphaHitTestWarning(current.graphic);
            }
        }
    }
}
