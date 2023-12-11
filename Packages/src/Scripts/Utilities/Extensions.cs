using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Coffee.UISoftMask
{
    /// <summary>
    /// Extension methods for Component class.
    /// </summary>
    internal static class ListExtensions
    {
        public static void RemoveAtFast<T>(this List<T> self, int index)
        {
            if (self == null) return;

            var lastIndex = self.Count - 1;
            self[index] = self[lastIndex];
            self.RemoveAt(lastIndex);
        }
    }

    /// <summary>
    /// Extension methods for Sprite class.
    /// </summary>
    internal static class SpriteExtensions
    {
#if UNITY_EDITOR
        private static readonly Type s_SpriteEditorExtensionType =
            Type.GetType("UnityEditor.Experimental.U2D.SpriteEditorExtension, UnityEditor")
            ?? Type.GetType("UnityEditor.U2D.SpriteEditorExtension, UnityEditor");

        private static readonly MethodInfo s_GetActiveAtlasTextureMethod = s_SpriteEditorExtensionType
            .GetMethod("GetActiveAtlasTexture", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo s_GetActiveAtlasMethod = s_SpriteEditorExtensionType
            .GetMethod("GetActiveAtlas", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Get the actual texture of a sprite in play mode or edit mode.
        /// </summary>
        public static Texture2D GetActualTexture(this Sprite self)
        {
            if (!self) return null;

            if (Application.isPlaying) return self.texture;

            var ret = s_GetActiveAtlasTextureMethod.Invoke(null, new object[] { self }) as Texture2D;
            return ret ? ret : self.texture;
        }

        /// <summary>
        /// Get the active sprite atlas of a sprite in play mode or edit mode.
        /// </summary>
        public static SpriteAtlas GetActiveAtlas(this Sprite self)
        {
            if (!self) return null;

            return s_GetActiveAtlasMethod.Invoke(null, new object[] { self }) as SpriteAtlas;
        }
#else
        /// <summary>
        /// Get the actual texture of a sprite in play mode.
        /// </summary>
        internal static Texture2D GetActualTexture(this Sprite self)
        {
            return self ? self.texture : null;
        }
#endif
    }

    /// <summary>
    /// Extension methods for Graphic class.
    /// </summary>
    internal static class GraphicExtensions
    {
        private static readonly Vector3[] s_WorldCorners = new Vector3[4];
        private static readonly Bounds s_ScreenBounds = new Bounds(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1, 1, 1));

        /// <summary>
        /// Check if a Graphic component is currently in the screen view.
        /// </summary>
        public static bool IsInScreen(this Graphic self)
        {
            if (!self || !self.canvas) return false;

            if (FrameCache.TryGet(self, nameof(IsInScreen), out bool result))
            {
                return result;
            }

            Profiler.BeginSample("(SM4UI)[GraphicExtensions] InScreen");
            var cam = self.canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? self.canvas.worldCamera
                : null;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            self.rectTransform.GetWorldCorners(s_WorldCorners);

            for (var i = 0; i < 4; i++)
            {
                if (cam)
                {
                    s_WorldCorners[i] = cam.WorldToViewportPoint(s_WorldCorners[i]);
                }
                else
                {
                    s_WorldCorners[i] = RectTransformUtility.WorldToScreenPoint(null, s_WorldCorners[i]);
                    s_WorldCorners[i].x /= Screen.width;
                    s_WorldCorners[i].y /= Screen.height;
                }

                s_WorldCorners[i].z = 0;
                min = Vector3.Min(s_WorldCorners[i], min);
                max = Vector3.Max(s_WorldCorners[i], max);
            }

            var bounds = new Bounds(min, Vector3.zero);
            bounds.Encapsulate(max);
            result = bounds.Intersects(s_ScreenBounds);
            FrameCache.Set(self, nameof(IsInScreen), result);
            Profiler.EndSample();

            return result;
        }

        /// <summary>
        /// Get the actual main texture of a Graphic component.
        /// </summary>
        public static Texture GetActualMainTexture(this Graphic self)
        {
            var image = self as Image;
            if (image == null) return self.mainTexture;

            var sprite = image.overrideSprite;
            return sprite ? sprite.GetActualTexture() : self.mainTexture;
        }
    }

    /// <summary>
    /// Extension methods for Component class.
    /// </summary>
    internal static class ComponentExtensions
    {
        /// <summary>
        /// Get or add a component of a specific type to a GameObject.
        /// </summary>
        public static T GetOrAddComponent<T>(this Component self) where T : Component
        {
            if (!self) return null;
            var c = self.GetComponent<T>();
            return c ? c : self.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Get the root component of a specific type in the hierarchy of a GameObject.
        /// </summary>
        public static T GetRootComponent<T>(this Component self) where T : Component
        {
            T component = null;
            var transform = self.transform;
            while (transform)
            {
                component = transform.GetComponent<T>() ?? component;
                transform = transform.parent;
            }

            return component;
        }

        /// <summary>
        /// Get a component of a specific type in the parent hierarchy of a GameObject.
        /// </summary>
        public static T GetComponentInParent<T>(this Component self, bool includeSelf, Transform stopAfter,
            Predicate<T> valid)
            where T : Component
        {
            var tr = includeSelf ? self.transform : self.transform.parent;
            while (tr)
            {
                var c = tr.GetComponent<T>();
                if (c && valid(c)) return c;
                if (tr == stopAfter) return null;
                tr = tr.parent;
            }

            return null;
        }

        /// <summary>
        /// Add a component of a specific type to the children of a GameObject.
        /// </summary>
        public static void AddComponentOnChildren<T>(this Component self, HideFlags hideFlags, bool includeSelf)
            where T : Component
        {
            if (self == null) return;

            Profiler.BeginSample("(SM4UI)[ComponentExtensions] AddComponentOnChildren > Self");
            if (includeSelf && !self.TryGetComponent<T>(out _))
            {
                var c = self.gameObject.AddComponent<T>();
                c.hideFlags = hideFlags;
            }

            Profiler.EndSample();

            Profiler.BeginSample("(SM4UI)[ComponentExtensions] AddComponentOnChildren > Child");
            var childCount = self.transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = self.transform.GetChild(i);
                if (child.TryGetComponent<T>(out _)) continue;

                var c = child.gameObject.AddComponent<T>();
                c.hideFlags = hideFlags;
            }

            Profiler.EndSample();
        }
    }

    /// <summary>
    /// Extension methods for Transform class.
    /// </summary>
    internal static class TransformExtensions
    {
        private static readonly Vector3[] s_Corners = new Vector3[4];

        /// <summary>
        /// Compare the hierarchy index of one transform with another transform.
        /// </summary>
        public static int CompareHierarchyIndex(this Transform self, Transform other, Transform stopAt)
        {
            if (self == other) return 0;

            Profiler.BeginSample("(SM4UI)[TransformExtensions] CompareHierarchyIndex > GetTransforms");
            var lTrs = self.GetTransforms(stopAt, ListPool<Transform>.Rent());
            var rTrs = other.GetTransforms(stopAt, ListPool<Transform>.Rent());
            Profiler.EndSample();

            Profiler.BeginSample("(SM4UI)[TransformExtensions] CompareHierarchyIndex > Calc");
            var loop = Mathf.Min(lTrs.Count, rTrs.Count);
            var result = 0;
            for (var i = 0; i < loop; ++i)
            {
                self = lTrs[lTrs.Count - i - 1];
                other = rTrs[rTrs.Count - i - 1];
                if (self == other) continue;

                result = self.GetSiblingIndex() - other.GetSiblingIndex();
                break;
            }

            Profiler.EndSample();

            Profiler.BeginSample("(SM4UI)[TransformExtensions] CompareHierarchyIndex > Return");
            ListPool<Transform>.Return(ref lTrs);
            ListPool<Transform>.Return(ref rTrs);
            Profiler.EndSample();

            return result;
        }

        private static List<Transform> GetTransforms(this Transform self, Transform stopAt, List<Transform> results)
        {
            results.Clear();
            while (self != stopAt)
            {
                results.Add(self);
                self = self.parent;
            }

            return results;
        }

        /// <summary>
        /// Check if a transform has changed.
        /// </summary>
        public static bool HasChanged(this Transform self, ref Matrix4x4 prev)
        {
            return self.HasChanged(null, ref prev);
        }

        /// <summary>
        /// Check if a transform has changed.
        /// </summary>
        public static bool HasChanged(this Transform self, Transform baseTransform, ref Matrix4x4 prev)
        {
            if (!self) return false;

            var hash = baseTransform ? baseTransform.GetHashCode() : 0;
            if (FrameCache.TryGet(self, nameof(HasChanged), hash, out bool result)) return result;

            var matrix = baseTransform
                ? baseTransform.worldToLocalMatrix * self.localToWorldMatrix
                : self.localToWorldMatrix;
            var current = matrix * Matrix4x4.Scale(Vector3.one * 10000);
            result = !Approximately(current, prev);
            FrameCache.Set(self, nameof(HasChanged), hash, result);
            if (result)
            {
                prev = current;
            }

            return result;
        }

        private static bool Approximately(Matrix4x4 self, Matrix4x4 other)
        {
            var epsilon = 1f / UISoftMaskProjectSettings.transformSensitivityBias;
            for (var i = 0; i < 16; i++)
            {
                if (epsilon < Mathf.Abs(self[i] - other[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static Bounds GetRelativeBounds(this Transform self, Transform child)
        {
            if (!self || !child)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var list = ListPool<RectTransform>.Rent();
            child.GetComponentsInChildren(false, list);
            if (list.Count == 0)
            {
                ListPool<RectTransform>.Return(ref list);
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var max = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var min = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var worldToLocalMatrix = self.worldToLocalMatrix;
            for (var i = 0; i < list.Count; i++)
            {
                list[i].GetWorldCorners(s_Corners);
                for (var j = 0; j < 4; j++)
                {
                    var lhs = worldToLocalMatrix.MultiplyPoint3x4(s_Corners[j]);
                    max = Vector3.Min(lhs, max);
                    min = Vector3.Max(lhs, min);
                }
            }

            ListPool<RectTransform>.Return(ref list);

            var rectTransformBounds = new Bounds(max, Vector3.zero);
            rectTransformBounds.Encapsulate(min);
            return rectTransformBounds;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Extension methods for Component class.
    /// </summary>
    internal static class ComponentConverter
    {
        /// <summary>
        /// Verify whether it can be converted to the specified component.
        /// </summary>
        internal static bool CanConvertTo<T>(this Object context) where T : MonoBehaviour
        {
            return context && context.GetType() != typeof(T);
        }

        /// <summary>
        /// Convert to the specified component.
        /// </summary>
        internal static void ConvertTo<T>(this Object context) where T : MonoBehaviour
        {
            var target = context as MonoBehaviour;
            if (target == null) return;

            var so = new SerializedObject(target);
            so.Update();

            var oldEnable = target.enabled;
            target.enabled = false;

            // Find MonoScript of the specified component.
            foreach (var script in Resources.FindObjectsOfTypeAll<MonoScript>())
            {
                if (script.GetClass() != typeof(T))
                {
                    continue;
                }

                // Set 'm_Script' to convert.
                so.FindProperty("m_Script").objectReferenceValue = script;
                so.ApplyModifiedProperties();
                break;
            }

            if (so.targetObject is MonoBehaviour mb)
            {
                mb.enabled = oldEnable;
            }
        }
    }
#endif
}
