﻿using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

namespace Coffee.UISoftMask
{
    public abstract class PreloadedProjectSettings : ScriptableObject
    {
#if UNITY_EDITOR
        protected static string GetDefaultName(Type type, bool nicify)
        {
            var typeName = type.Name.Replace("ProjectSettings", "");
            return nicify
                ? ObjectNames.NicifyVariableName(typeName)
                : typeName;
        }

        private static Object[] GetPreloadedSettings(Type type)
        {
            return PlayerSettings.GetPreloadedAssets()
                .Where(x => x && x.GetType() == type)
                .ToArray();
        }

        protected static PreloadedProjectSettings GetDefaultSettings(Type type)
        {
            return GetPreloadedSettings(type).FirstOrDefault() as PreloadedProjectSettings;
        }

        protected static void SetDefaultSettings(PreloadedProjectSettings asset)
        {
            var type = asset.GetType();
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            {
                if (!AssetDatabase.IsValidFolder("Assets/ProjectSettings"))
                {
                    AssetDatabase.CreateFolder("Assets", "ProjectSettings");
                }

                var assetPath = $"Assets/ProjectSettings/{GetDefaultName(type, false)}.asset";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            var projectSettings = GetPreloadedSettings(type);
            PlayerSettings.SetPreloadedAssets(preloadedAssets
                .Where(x => x)
                .Except(projectSettings.Except(new[] { asset }))
                .Append(asset)
                .Distinct()
                .ToArray());
        }

        private class Initializer : IPreprocessBuildWithReport
        {
            int IOrderedCallback.callbackOrder => 0;

            void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
            {
                Initialize();
            }

            [InitializeOnLoadMethod]
            [InitializeOnEnterPlayMode]
            private static void Initialize()
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
                foreach (var t in TypeCache.GetTypesDerivedFrom(typeof(PreloadedProjectSettings<>)))
                {
                    var defaultSettings = GetDefaultSettings(t);
                    if (!defaultSettings)
                    {
                        defaultSettings = t.GetProperty("instance", flags)
                            ?.GetValue(null, null) as PreloadedProjectSettings;
                        SetDefaultSettings(defaultSettings);
                    }
                    else if (GetPreloadedSettings(t).Length != 1)
                    {
                        SetDefaultSettings(defaultSettings);
                    }
                }

                EditorApplication.QueuePlayerLoopUpdate();
            }
        }
#endif
    }

    public abstract class PreloadedProjectSettings<T> : PreloadedProjectSettings
        where T : PreloadedProjectSettings<T>
    {
        private static T s_Instance;

#if UNITY_EDITOR
        private string _jsonText;

        public static T instance
        {
            get
            {
                if (s_Instance) return s_Instance;

                s_Instance = GetDefaultSettings(typeof(T)) as T;
                if (s_Instance) return s_Instance;

                s_Instance = CreateInstance<T>();
                SetDefaultSettings(s_Instance);
                return s_Instance;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _jsonText = EditorJsonUtility.ToJson(this);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    if (_jsonText != null)
                    {
                        EditorJsonUtility.FromJsonOverwrite(_jsonText, this);
                        _jsonText = null;
                    }

                    break;
            }
        }
#else
        public static T instance => s_Instance ? s_Instance : s_Instance = CreateInstance<T>();
#endif

        protected virtual void OnEnable()
        {
#if UNITY_EDITOR
            var isDefaultSettings = !s_Instance || s_Instance == this || GetDefaultSettings(typeof(T)) == this;
            if (!isDefaultSettings)
            {
                Debug.LogError($"[{typeof(T).Name}] Other instance already exists in preload assets.", this);
                return;
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

            if (s_Instance) return;
            s_Instance = this as T;
        }

        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            if (s_Instance != this) return;

            s_Instance = null;
        }

#if UNITY_EDITOR
        protected sealed class PreloadedProjectSettingsProvider : SettingsProvider
        {
            private Editor _editor;
            private PreloadedProjectSettings<T> _target;

            public PreloadedProjectSettingsProvider(string path) : base(path, SettingsScope.Project)
            {
            }

            public override void OnGUI(string searchContext)
            {
                if (!_target)
                {
                    if (_editor)
                    {
                        DestroyImmediate(_editor);
                        _editor = null;
                    }

                    _target = instance;
                    _editor = Editor.CreateEditor(_target);
                }

                _editor.OnInspectorGUI();
            }
        }
#endif
    }
}
