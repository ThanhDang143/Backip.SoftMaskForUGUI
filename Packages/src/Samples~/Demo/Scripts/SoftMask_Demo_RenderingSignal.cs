using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
#endif

namespace Coffee.UISoftMask.Demos
{
    [ExecuteAlways]
    public class SoftMask_Demo_RenderingSignal : RawImage
    {
        [SerializeField] private SoftMask m_SoftMask;

        protected override void OnEnable()
        {
            base.OnEnable();
            UIExtraCallbacks.onBeforeCanvasRebuild += CheckTransformChange;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UIExtraCallbacks.onBeforeCanvasRebuild -= CheckTransformChange;
        }

        private void CheckTransformChange()
        {
            if (canvasRenderer && m_SoftMask)
            {
                canvasRenderer.SetAlpha(m_SoftMask.isDirty ? 1 : 0);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SoftMask_Demo_RenderingSignal))]
    internal class SoftMask_Demo_RenderingSignalEditor : RawImageEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SoftMask"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
