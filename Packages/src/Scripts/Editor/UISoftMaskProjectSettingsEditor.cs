using UnityEditor;
using UnityEngine;

namespace Coffee.UISoftMask
{
    [CustomEditor(typeof(UISoftMaskProjectSettings))]
    internal class UISoftMaskProjectSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var prevEnabled = UISoftMaskProjectSettings.softMaskEnabled;
            base.OnInspectorGUI();
            if (prevEnabled != UISoftMaskProjectSettings.softMaskEnabled)
            {
                UISoftMaskProjectSettings.ResetAllSoftMasks();
            }

            // Draw SoftMask/SoftMaskable/TerminalShape Shaders;
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.PrefixLabel("Included Shaders");
                if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    UISoftMaskProjectSettings.instance.ReloadShaders(true);
                }
            }
            EditorGUILayout.EndHorizontal();

            foreach (var shader in AlwaysIncludedShadersProxy.GetShaders())
            {
                if (!UISoftMaskProjectSettings.CanIncludeShader(shader)) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(shader, typeof(Shader), false);
                if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    AlwaysIncludedShadersProxy.Remove(shader);
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
