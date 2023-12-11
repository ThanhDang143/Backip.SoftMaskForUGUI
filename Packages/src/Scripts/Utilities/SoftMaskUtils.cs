using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Coffee.UISoftMask
{
    /// <summary>
    /// Utility class containing various functions and resources for soft masking.
    /// </summary>
    internal static class SoftMaskUtils
    {
        /// <summary>
        /// Object pool for CommandBuffer instances.
        /// </summary>
        public static readonly ObjectPool<CommandBuffer> commandBufferPool =
            new ObjectPool<CommandBuffer>(
                () => new CommandBuffer(),
                x => x != null,
                x => x.Clear());

        /// <summary>
        /// Object pool for MaterialPropertyBlock instances.
        /// </summary>
        public static readonly ObjectPool<MaterialPropertyBlock> materialPropertyBlockPool =
            new ObjectPool<MaterialPropertyBlock>(
                () => new MaterialPropertyBlock(),
                x => x != null,
                x => x.Clear());

        /// <summary>
        /// Object pool for Mesh instances.
        /// </summary>
        public static readonly ObjectPool<Mesh> meshPool = new ObjectPool<Mesh>(
            () =>
            {
                var mesh = new Mesh
                {
                    hideFlags = HideFlags.DontSave
                };
                mesh.MarkDynamic();
                return mesh;
            },
            mesh => mesh,
            mesh =>
            {
                if (mesh)
                {
                    mesh.Clear();
                }
            });

        private static Material s_SoftMaskingMaterial;
        private static Material s_SoftMaskingMaterialSub;
        private static readonly int s_ColorMaskId = Shader.PropertyToID("_ColorMask");
        private static readonly int s_MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int s_ThresholdMinId = Shader.PropertyToID("_ThresholdMin");
        private static readonly int s_ThresholdMaxId = Shader.PropertyToID("_ThresholdMax");
        private static readonly int s_SoftMaskTexId = Shader.PropertyToID("_SoftMaskTex");
        private static readonly int s_SoftMaskColorId = Shader.PropertyToID("_SoftMaskColor");
        private static readonly int s_StencilReadMaskId = Shader.PropertyToID("_StencilReadMask");
        private static readonly int s_BlendOp = Shader.PropertyToID("_BlendOp");
        private static Vector2Int s_BufferSize;
        private static int s_Count;
        private static readonly FastAction s_OnChangeBufferSize = new FastAction();

        private static readonly string[] s_SoftMaskableShaderNameFormats =
        {
            "{0}",
            "Hidden/{0} (SoftMaskable)",
            "{0} (SoftMaskable)"
        };

        private static readonly Dictionary<int, string> s_SoftMaskableShaderNames = new Dictionary<int, string>();

        /// <summary>
        /// Event that gets triggered when the buffer size changes.
        /// </summary>
        public static event Action onChangeBufferSize
        {
            add => s_OnChangeBufferSize.Add(value);
            remove => s_OnChangeBufferSize.Remove(value);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void InitializeOnLoadMethod()
        {
            UIExtraCallbacks.onBeforeCanvasRebuild += () =>
            {
                var size = RenderTextureRepository.GetScreenSize();
                if (s_BufferSize == size) return;
                s_BufferSize = size;
                s_OnChangeBufferSize.Invoke();
            };
        }


        /// <summary>
        /// Initializes a command buffer for rendering.
        /// </summary>
        public static void InitCommandBuffer(CommandBuffer cb, int depth, RenderTexture rt, Color clearColor)
        {
            Profiler.BeginSample("(SM4UI)[SoftMaskUtils] InitCommandBuffer");
            cb.Clear();
            cb.SetRenderTarget(rt);
            if (depth == 0)
            {
                cb.ClearRenderTarget(true, true, clearColor);
            }

            Profiler.EndSample();
        }

        public static bool IsStereoCanvas(Canvas canvas)
        {
            if (UISoftMaskProjectSettings.useStereoMock && Application.isPlaying)
            {
                return true;
            }
#if UNITY_MODULE_VR
            if (FrameCache.TryGet<bool>(canvas, nameof(IsStereoCanvas), out var stereo)) return stereo;

            stereo =
                UISoftMaskProjectSettings.stereoEnabled
                && canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null
                && XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName);
            FrameCache.Set(canvas, nameof(IsStereoCanvas), stereo);
            return stereo;
#else
            return false;
#endif
        }

        /// <summary>
        /// Sets view and projection matrices in a command buffer.
        /// </summary>
        public static void SetVpMatricesCommandBuffer(Camera.MonoOrStereoscopicEye eye, CommandBuffer cb, Canvas canvas)
        {
            // Set view and projection matrices.
            Profiler.BeginSample("(SM4UI)[SoftMaskUtils] SetVpMatricesCommandBuffer");
            GetViewProjectionMatrix(eye, canvas.rootCanvas, out var viewMatrix, out var projectionMatrix);
            cb.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            Profiler.EndSample();
        }

        /// <summary>
        /// Gets the view-projection matrix for a Canvas.
        /// </summary>
        public static void GetViewProjectionMatrix(Camera.MonoOrStereoscopicEye eye, Canvas canvas,
            out Matrix4x4 vpMatrix)
        {
            if (FrameCache.TryGet(canvas, nameof(GetViewProjectionMatrix), out vpMatrix)) return;

            GetViewProjectionMatrix(eye, canvas, out var viewMatrix, out var projectionMatrix);
            vpMatrix = viewMatrix * projectionMatrix;
            FrameCache.Set(canvas, nameof(GetViewProjectionMatrix), vpMatrix);
        }

        /// <summary>
        /// Gets the view and projection matrices for a Canvas.
        /// </summary>
        public static void GetViewProjectionMatrix(Camera.MonoOrStereoscopicEye eye, Canvas canvas,
            out Matrix4x4 vMatrix, out Matrix4x4 pMatrix)
        {
            if (FrameCache.TryGet(canvas, "GetViewMatrix", (int)eye, out vMatrix) &&
                FrameCache.TryGet(canvas, "GetProjectionMatrix", (int)eye, out pMatrix))
            {
                return;
            }

            // Get view and projection matrices.
            Profiler.BeginSample("(SM4UI)[SoftMaskUtils] GetViewProjectionMatrix");
            var rootCanvas = canvas.rootCanvas;
            var cam = rootCanvas.worldCamera;
            if (rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && cam)
            {
                if (eye == Camera.MonoOrStereoscopicEye.Mono)
                {
                    vMatrix = cam.worldToCameraMatrix;
                    pMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
                }
                else
                {
                    pMatrix = cam.GetStereoProjectionMatrix((Camera.StereoscopicEye)eye);
                    vMatrix = cam.GetStereoViewMatrix((Camera.StereoscopicEye)eye);
                    pMatrix = GL.GetGPUProjectionMatrix(pMatrix, false);
                }
            }
            else
            {
                var pos = rootCanvas.transform.position;
                vMatrix = Matrix4x4.TRS(
                    new Vector3(-pos.x, -pos.y, -1000),
                    Quaternion.identity,
                    new Vector3(1, 1, -1f));
                pMatrix = Matrix4x4.TRS(
                    new Vector3(0, 0, -1),
                    Quaternion.identity,
                    new Vector3(1 / pos.x, 1 / pos.y, -2 / 10000f));
            }

            FrameCache.Set(canvas, "GetViewMatrix", (int)eye, vMatrix);
            FrameCache.Set(canvas, "GetProjectionMatrix", (int)eye, pMatrix);

            Profiler.EndSample();
        }

        /// <summary>
        /// Applies properties to a MaterialPropertyBlock for soft masking.
        /// </summary>
        public static void ApplyMaterialPropertyBlock(MaterialPropertyBlock mpb, int depth, Texture texture,
            MinMax01 threshold)
        {
            Profiler.BeginSample("(SM4UI)[SoftMaskUtils] ApplyMaterialPropertyBlock");
            var colorMask = Vector4.zero;
            colorMask[depth] = 1;
            mpb.SetVector(s_ColorMaskId, colorMask);
            mpb.SetTexture(s_MainTexId, texture);
            mpb.SetFloat(s_ThresholdMinId, threshold.min);
            mpb.SetFloat(s_ThresholdMaxId, threshold.max);
            Profiler.EndSample();
        }

        /// <summary>
        /// Gets the soft masking material based on the masking method.
        /// </summary>
        public static Material GetSoftMaskingMaterial(MaskingShape.MaskingMethod method)
        {
            return method == MaskingShape.MaskingMethod.Additive
                ? GetSoftMaskingMaterial(ref s_SoftMaskingMaterial, BlendOp.Add)
                : GetSoftMaskingMaterial(ref s_SoftMaskingMaterialSub, BlendOp.ReverseSubtract);
        }

        /// <summary>
        /// Gets or creates the soft masking material with a specific blend operation.
        /// </summary>
        private static Material GetSoftMaskingMaterial(ref Material mat, BlendOp op)
        {
            if (mat) return mat;

            mat = new Material(Shader.Find("Hidden/UI/SoftMask"))
            {
                hideFlags = HideFlags.DontSave
            };
            mat.SetInt(s_BlendOp, (int)op);
            return mat;
        }

        public static Material CreateSoftMaskable(
            Material baseMat,
            Texture softMaskBuffer,
            int softMaskDepth,
            int stencilBits,
            bool isStereo,
            FallbackBehavior fallbackBehavior)
        {
            Profiler.BeginSample("(SM4UI)[SoftMaskableMaterial] Create");
            var mat = new Material(baseMat)
            {
                shader = GetSoftMaskableShader(baseMat.shader, fallbackBehavior),
                hideFlags = HideFlags.HideAndDontSave
            };

            mat.SetTexture(s_SoftMaskTexId, softMaskBuffer);
            mat.SetInt(s_StencilReadMaskId, stencilBits);
            mat.SetVector(s_SoftMaskColorId, new Vector4(
                0 <= softMaskDepth ? 1 : 0,
                1 <= softMaskDepth ? 1 : 0,
                2 <= softMaskDepth ? 1 : 0,
                3 <= softMaskDepth ? 1 : 0
            ));

            if (isStereo)
            {
                mat.EnableKeyword("UI_SOFT_MASKABLE_STEREO");
            }

#if UNITY_EDITOR
            mat.EnableKeyword("UI_SOFT_MASKABLE_EDITOR");
#else
            mat.EnableKeyword("UI_SOFT_MASKABLE");
#endif
            Profiler.EndSample();
            return mat;
        }

        public static Shader GetSoftMaskableShader(Shader baseShader, FallbackBehavior fallback)
        {
            Profiler.BeginSample("(SM4UI)[SoftMaskUtils] GetSoftMaskableShader > From cache");
            var id = baseShader.GetInstanceID();
            if (s_SoftMaskableShaderNames.TryGetValue(id, out var shaderName))
            {
                var shader = Shader.Find(shaderName);
                Profiler.EndSample();

                return shader;
            }

            Profiler.EndSample();

            Profiler.BeginSample("(SM4UI)[SoftMaskableMaterial] GetSoftMaskableShader > Find soft maskable shader");
            shaderName = baseShader.name;
            for (var i = 0; i < s_SoftMaskableShaderNameFormats.Length; i++)
            {
                var name = string.Format(s_SoftMaskableShaderNameFormats[i], shaderName);
                if (!name.EndsWith(" (SoftMaskable)", StringComparison.Ordinal)) continue;

                var shader = Shader.Find(name);
                if (!shader) continue;

                s_SoftMaskableShaderNames.Add(id, name);
                Profiler.EndSample();
                return shader;
            }

            Profiler.EndSample();

            Profiler.BeginSample("(SM4UI)[SoftMaskableMaterial] GetSoftMaskableShader > Fallback");
            switch (fallback)
            {
                case FallbackBehavior.DefaultSoftMaskable:
                {
                    s_SoftMaskableShaderNames.Add(id, "Hidden/UI/Default (SoftMaskable)");
                    var shader = Shader.Find("Hidden/UI/Default (SoftMaskable)");
                    Profiler.EndSample();
                    return shader;
                }
                case FallbackBehavior.None:
                {
                    s_SoftMaskableShaderNames.Add(id, shaderName);
                    Profiler.EndSample();
                    return baseShader;
                }
                default:
                    Profiler.EndSample();
                    throw new ArgumentOutOfRangeException(nameof(fallback), fallback, null);
            }
        }

#if UNITY_EDITOR
        internal static readonly int s_GameVpId = Shader.PropertyToID("_GameVP");
        internal static readonly int s_GameTvpId = Shader.PropertyToID("_GameTVP");
        internal static readonly int s_GameVp2Id = Shader.PropertyToID("_GameVP_2");
        internal static readonly int s_GameTvp2Id = Shader.PropertyToID("_GameTVP_2");
#endif
    }
}
