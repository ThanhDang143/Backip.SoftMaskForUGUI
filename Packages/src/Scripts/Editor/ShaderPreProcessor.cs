using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Coffee.UISoftMask
{
    public class ShaderPreProcessor : IPreprocessShaders
    {
        int IOrderedCallback.callbackOrder => 1;

        void IPreprocessShaders.OnProcessShader(Shader shader, ShaderSnippetData snippet,
            IList<ShaderCompilerData> data)
        {
            // If the shader is not SoftMask/softMaskable, do nothing.
            var type = GetShaderType(shader);
            if (type == ShaderType.None) return;

            // Remove the 'UI_SOFT_MASKABLE_EDITOR' shader variants.
            var editor = new ShaderKeyword(shader, "UI_SOFT_MASKABLE_EDITOR");
            StripUnusedVariantsIf(true, data, editor, true);

            // If the shader is separated soft-maskable, remove non-soft-maskable variants.
            var softMaskable = new ShaderKeyword(shader, "UI_SOFT_MASKABLE");
            StripUnusedVariantsIf(type == ShaderType.SeparatedSoftMaskable, data, softMaskable, false);

            if (!UISoftMaskProjectSettings.instance.m_StripShaderVariants) return;

            // If soft mask is disabled in the project, remove the all shader variants.
            var softMaskDisabled = !UISoftMaskProjectSettings.instance.m_SoftMaskEnabled;
            if (StripUnusedVariantsIf(softMaskDisabled, data)) return;

            // If stereo is disabled in the project, remove the 'UI_SOFT_MASKABLE_STEREO' shader variants.
            var stereoDisabled = !UISoftMaskProjectSettings.stereoEnabled;
            var stereo = new ShaderKeyword(shader, "UI_SOFT_MASKABLE_STEREO");
            StripUnusedVariantsIf(stereoDisabled, data, stereo, true);

            // Remove the shader variants that are not used in the project.
            var baseShader = Shader.Find(Regex.Replace(shader.name, @"(^Hidden/| \(SoftMaskable\)$)", ""));
            StripUnusedVariants(shader, data, GetUsedMaterials(baseShader));

            // Log
            if (snippet.shaderType == UnityEditor.Rendering.ShaderType.Fragment)
            {
                Console.WriteLine($"[{shader.name}] type=({type}) {data.Count} variants available:");
                for (var i = 0; i < data.Count; i++)
                {
                    var platform = data[i].shaderCompilerPlatform;
                    var keywords = GetShaderKeywords(shader, data[i], null);
                    Console.WriteLine($"  - {platform}: {keywords}");
                }

                Console.WriteLine();
            }
        }

        private static ShaderType GetShaderType(Shader shader)
        {
            if (!shader) return ShaderType.None;
            var name = shader.name;
            if (name == "Hidden/UI/SoftMask") return ShaderType.SoftMask;
            if (!name.EndsWith(" (SoftMaskable)")) return ShaderType.None;
            return name.StartsWith("Hidden/")
                ? ShaderType.SeparatedSoftMaskable
                : ShaderType.HybridSoftMaskable;
        }

        private static bool IsSoftMaskOrSoftMaskable(Shader shader)
        {
            if (!shader) return false;
            var name = shader.name;
            return name.EndsWith(" (SoftMaskable)")
                   || name == "Hidden/UI/SoftMask";
        }

        private static bool StripUnusedVariantsIf(bool condition, IList<ShaderCompilerData> data)
        {
            if (!condition) return false;

            data.Clear();
            return true;
        }

        private static void StripUnusedVariantsIf(bool condition, IList<ShaderCompilerData> data,
            ShaderKeyword keyword, bool enabled)
        {
            if (!condition) return;

            for (var i = data.Count - 1; i >= 0; --i)
            {
                if (data[i].shaderKeywordSet.IsEnabled(keyword) == enabled)
                {
                    data.RemoveAt(i);
                }
            }
        }

        private static Material[] GetUsedMaterials(Shader shader)
        {
            if (!shader) return null;

            return AssetDatabase.FindAssets("t:Material")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(x => x.shader == shader)
                .ToArray();
        }

        private static void StripUnusedVariants(Shader shader, IList<ShaderCompilerData> data, Material[] usedMaterials)
        {
            if (!shader || usedMaterials == null || data == null) return;

            var ignoredKeywords = GetMultiCompileKeywords(shader);
            var usedKeywordsArray = usedMaterials
                .Select(m => GetShaderKeywords(m, ignoredKeywords))
                .Distinct()
                .ToArray();

            for (var i = data.Count - 1; i >= 0; --i)
            {
                var keywordsInData = GetShaderKeywords(shader, data[i], ignoredKeywords);
                if (0 < keywordsInData.Length && !usedKeywordsArray.Contains(keywordsInData))
                {
                    data.RemoveAt(i);
                }
            }
        }

        private static string GetShaderKeywords(IEnumerable<string> keywords, IEnumerable<string> ignoredKeywords)
        {
            return string.Join("|", keywords
                .Where(k => ignoredKeywords == null || !ignoredKeywords.Contains(k))
                .OrderBy(k => k));
        }

        private static string GetShaderKeywords(Material mat, IEnumerable<string> ignoredKeywords)
        {
            return GetShaderKeywords(mat.shaderKeywords
                .Where(mat.IsKeywordEnabled), ignoredKeywords);
        }

        private static string GetShaderKeywords(Shader shader, ShaderCompilerData data,
            IEnumerable<string> ignoredKeywords)
        {
            return GetShaderKeywords(data.shaderKeywordSet.GetShaderKeywords()
                .Select(k => ShaderKeyword.GetKeywordName(shader, k)), ignoredKeywords);
        }

        private static string[] GetMultiCompileKeywords(Shader shader)
        {
            if (!shader) return Array.Empty<string>();

            var path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path)) return Array.Empty<string>();

            var keywords = new List<string>();
            foreach (var l in File.ReadAllLines(path))
            {
                var m = Regex.Match(l, @"#pragma[\s]+multi_compile(_local)?(.+)", RegexOptions.Compiled);
                if (!m.Success) continue;

                keywords.AddRange(m.Groups[2].Value.Split(' ', '\t'));
            }

            return keywords
                .Distinct()
                .Where(x => new ShaderKeyword(shader, x).IsValid())
                .ToArray();
        }

        private enum ShaderType
        {
            None,
            SoftMask,
            HybridSoftMaskable,
            SeparatedSoftMaskable
        }
    }
}
