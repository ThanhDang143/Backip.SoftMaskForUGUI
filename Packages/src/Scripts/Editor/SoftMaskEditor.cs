using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Coffee.UISoftMask
{
    [CustomEditor(typeof(SoftMask), true)]
    [CanEditMultipleObjects]
    public class SoftMaskEditor : Editor
    {
        private const string k_PrefsPreview = "k_PrefsPreview";
        private const int k_PreviewSize = 220;
        private SerializedProperty _alphaHitTest;
        private bool _preview;
        private SerializedProperty _showMaskGraphic;
        private SerializedProperty _threshold;

        protected void OnEnable()
        {
            _showMaskGraphic = serializedObject.FindProperty("m_ShowMaskGraphic");
            _alphaHitTest = serializedObject.FindProperty("m_AlphaHitTest");
            _threshold = serializedObject.FindProperty("m_Threshold");
            _preview = EditorPrefs.GetBool(k_PrefsPreview, false);
        }

        public override void OnInspectorGUI()
        {
            var current = target as SoftMask;
            if (current == null) return;

            if (!current.graphic || !current.graphic.IsActive())
            {
                EditorGUILayout.HelpBox("Masking disabled due to Graphic component being disabled.",
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(_showMaskGraphic);
            EditorGUILayout.PropertyField(_alphaHitTest);
            EditorGUILayout.PropertyField(_threshold);

            serializedObject.ApplyModifiedProperties();

            // Fix 'UIMask' issue.
            if (current.graphic is Image currentImage && IsMaskUI(currentImage.sprite))
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(
                    "SoftMask does not recommend to use 'UIMask' sprite as a source image.\n" +
                    "(It contains only small alpha pixels.)\n" +
                    "Do you want to use 'UISprite' instead?",
                    MessageType.Warning);
                if (GUILayout.Button("Fix"))
                {
                    currentImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                }

                GUILayout.EndHorizontal();
            }

            // Preview soft mask buffer.
            DrawSoftMaskBuffer();
        }

        private static bool IsMaskUI(Object obj)
        {
            return obj
                   && obj.name == "UIMask"
                   && AssetDatabase.GetAssetPath(obj) == "Resources/unity_builtin_extra";
        }

        private void DrawSoftMaskBuffer()
        {
            var current = target as SoftMask;
            if (current == null || !current.MaskEnabled()) return;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                if (_preview != (_preview = EditorGUILayout.ToggleLeft("Preview Soft Mask Buffer", _preview)))
                {
                    EditorPrefs.SetBool(k_PrefsPreview, _preview);
                }

                if (_preview)
                {
                    var tex = current._softMaskBuffer;
                    var depth = current.softMaskDepth;
                    var colorMask = GetColorMask(depth);

                    if (tex)
                    {
                        GUILayout.Label($"{tex.name} (Depth: {depth} {colorMask})");
                        var aspectRatio = (float)tex.width / tex.height;
                        EditorGUI.DrawPreviewTexture(
                            GUILayoutUtility.GetRect(k_PreviewSize, k_PreviewSize / aspectRatio), tex, null,
                            ScaleMode.ScaleToFit, aspectRatio, 0, colorMask);
                    }
                }
            }
            GUILayout.EndVertical();
        }

        private static ColorWriteMask GetColorMask(int depth)
        {
            switch (depth)
            {
                case 0: return ColorWriteMask.Red;
                case 1: return ColorWriteMask.Red | ColorWriteMask.Green;
                case 2: return ColorWriteMask.Red | ColorWriteMask.Green | ColorWriteMask.Blue;
                default: return ColorWriteMask.All;
            }
        }

        //%%%% Context menu for editor %%%%
        [MenuItem("CONTEXT/" + nameof(Mask) + "/Convert To " + nameof(SoftMask), true)]
        private static bool _ConvertToSoftMask(MenuCommand command)
        {
            return command.context.CanConvertTo<SoftMask>();
        }

        [MenuItem("CONTEXT/" + nameof(Mask) + "/Convert To " + nameof(SoftMask), false)]
        private static void ConvertToSoftMask(MenuCommand command)
        {
            command.context.ConvertTo<SoftMask>();
        }

        [MenuItem("CONTEXT/" + nameof(SoftMask) + "/Convert To " + nameof(Mask), true)]
        private static bool _ConvertToMask(MenuCommand command)
        {
            return command.context.CanConvertTo<Mask>();
        }

        [MenuItem("CONTEXT/" + nameof(SoftMask) + "/Convert To " + nameof(Mask), false)]
        private static void ConvertToMask(MenuCommand command)
        {
            command.context.ConvertTo<Mask>();
        }
    }
}
