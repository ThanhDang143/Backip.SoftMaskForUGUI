using UnityEditor;
using UnityEditor.UI;
using UnityEngine.UI;

namespace Coffee.UISoftMask
{
    [CustomEditor(typeof(EnhancedMask), true)]
    [CanEditMultipleObjects]
    public class EnhancedMaskEditor : MaskEditor
    {
        private SerializedProperty _alphaHitTest;
        private SerializedProperty _antiAliasing;
        private SerializedProperty _antiAliasingThreshold;

        protected override void OnEnable()
        {
            base.OnEnable();

            _alphaHitTest = serializedObject.FindProperty("m_AlphaHitTest");
            _antiAliasing = serializedObject.FindProperty("m_AntiAliasing");
            _antiAliasingThreshold = serializedObject.FindProperty("m_AntiAliasingThreshold");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var current = target as EnhancedMask;
            if (!current) return;

            EditorGUILayout.PropertyField(_alphaHitTest);
            if (_alphaHitTest.boolValue)
            {
                Utils.DrawAlphaHitTestWarning(current.graphic);
            }

            EditorGUILayout.PropertyField(_antiAliasing);

            EditorGUI.BeginDisabledGroup(!_antiAliasing.boolValue);
            EditorGUILayout.PropertyField(_antiAliasingThreshold);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        //%%%% Context menu for editor %%%%
        [MenuItem("CONTEXT/" + nameof(Mask) + "/Convert To " + nameof(EnhancedMask), true)]
        private static bool _ConvertToSoftMask(MenuCommand command)
        {
            return command.context.CanConvertTo<EnhancedMask>();
        }

        [MenuItem("CONTEXT/" + nameof(Mask) + "/Convert To " + nameof(EnhancedMask), false)]
        private static void ConvertToSoftMask(MenuCommand command)
        {
            command.context.ConvertTo<EnhancedMask>();
        }

        [MenuItem("CONTEXT/" + nameof(EnhancedMask) + "/Convert To " + nameof(Mask), true)]
        private static bool _ConvertToMask(MenuCommand command)
        {
            return command.context.CanConvertTo<Mask>();
        }

        [MenuItem("CONTEXT/" + nameof(EnhancedMask) + "/Convert To " + nameof(Mask), false)]
        private static void ConvertToMask(MenuCommand command)
        {
            command.context.ConvertTo<Mask>();
        }
    }
}
