using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Coffee.UISoftMask
{
    [ExecuteAlways]
    public class SoftMaskable : MonoBehaviour, IMaterialModifier, IMaskable
#if UNITY_EDITOR
        , ISerializationCallbackReceiver
#endif
    {
        private MaskableGraphic _graphic;
        private Material _maskableMaterial;
        private bool _shouldRecalculateStencil;
        private SoftMask _softMask;
        private int _softMaskDepth;
        private int _stencilBits;

        private bool isTerminal => _graphic is TerminalMaskingShape;

        private void OnEnable()
        {
            if (UISoftMaskProjectSettings.softMaskableBehavior == SoftMaskableBehavior.Automatic)
            {
                this.AddComponentOnChildren<SoftMaskable>(hideFlags, false);
            }

            hideFlags = UISoftMaskProjectSettings.behaviorHideFlags;

            SoftMaskUtils.onChangeBufferSize += SetMaterialDirtyIfNeeded;
#if UNITY_EDITOR
            UIExtraCallbacks.onAfterCanvasRebuild += UpdateSceneViewMatrix;
#endif
            _graphic = GetComponent<MaskableGraphic>();
            if (_graphic)
            {
                _graphic.RegisterDirtyMaterialCallback(RequestRecalculateStencil);
                _graphic.SetMaterialDirty();
            }
            else
            {
                UIExtraCallbacks.onBeforeCanvasRebuild += CheckGraphic;
            }
        }

        private void OnDisable()
        {
            SoftMaskUtils.onChangeBufferSize -= SetMaterialDirtyIfNeeded;
            UIExtraCallbacks.onBeforeCanvasRebuild -= CheckGraphic;
#if UNITY_EDITOR
            UIExtraCallbacks.onAfterCanvasRebuild -= UpdateSceneViewMatrix;
#endif

            if (_graphic)
            {
                _graphic.UnregisterDirtyMaterialCallback(RequestRecalculateStencil);
                _graphic.SetMaterialDirty();
            }

            _graphic = null;
            _softMask = null;
            MaterialRepository.Release(ref _maskableMaterial);
        }

        private void OnTransformChildrenChanged()
        {
            if (UISoftMaskProjectSettings.softMaskableBehavior == SoftMaskableBehavior.Automatic)
            {
                this.AddComponentOnChildren<SoftMaskable>(UISoftMaskProjectSettings.behaviorHideFlags, false);
            }
        }

        private void OnTransformParentChanged()
        {
            RequestRecalculateStencil();
        }

        void IMaskable.RecalculateMasking()
        {
            RequestRecalculateStencil();
        }

        Material IMaterialModifier.GetModifiedMaterial(Material baseMaterial)
        {
#if UNITY_EDITOR
            if (!UISoftMaskProjectSettings.softMaskEnabled)
            {
                MaterialRepository.Release(ref _maskableMaterial);
                return baseMaterial;
            }
#endif

            if (!isActiveAndEnabled || !_graphic || !_graphic.maskable || isTerminal || baseMaterial == null)
            {
                MaterialRepository.Release(ref _maskableMaterial);
                return baseMaterial;
            }

            if (_shouldRecalculateStencil)
            {
                _shouldRecalculateStencil = false;
                _stencilBits = GetStencilBitsAndSoftMask(transform, out _softMask);
                _softMaskDepth = _softMask ? _softMask.softMaskDepth : -1;
            }

            if (!_softMask || _softMaskDepth < 0 || 4 <= _softMaskDepth)
            {
                MaterialRepository.Release(ref _maskableMaterial);
                return baseMaterial;
            }

            Profiler.BeginSample("(SM4UI)[SoftMaskable] GetModifiedMaterial");

            var isStereo = Application.isPlaying && SoftMaskUtils.IsStereoCanvas(_graphic.canvas);
            var hash = new Hash128(
                (uint)baseMaterial.GetInstanceID(),
                (uint)_softMask.softMaskBuffer.GetInstanceID(),
                (uint)_stencilBits + (isStereo ? 1 << 8 : 0u),
                (uint)_softMaskDepth);
            MaterialRepository.Get(hash, ref _maskableMaterial,
                x => SoftMaskUtils.CreateSoftMaskable(x.baseMaterial, x.softMaskBuffer, x._softMaskDepth,
                    x._stencilBits, x.isStereo, UISoftMaskProjectSettings.fallbackBehavior),
                (baseMaterial, _softMask.softMaskBuffer, _softMaskDepth, _stencilBits, isStereo));
            Profiler.EndSample();

            return _maskableMaterial;
        }

        private void CheckGraphic()
        {
            if (_graphic) return;

            _graphic = GetComponent<MaskableGraphic>();
            if (!_graphic) return;

            // UIExtraCallbacks.onBeforeCanvasRebuild -= CheckGraphic;
            _graphic.RegisterDirtyMaterialCallback(RequestRecalculateStencil);

            gameObject.AddComponent<SoftMaskable>();
            Utils.DestroySafety(this);
        }

        private static int GetStencilBitsAndSoftMask(Transform transform, out SoftMask nearestSoftMask)
        {
            Profiler.BeginSample("(SM4UI)[SoftMaskable] GetStencilBitsAndSoftMask");
            nearestSoftMask = null;
            var stopAfter = MaskUtilities.FindRootSortOverrideCanvas(transform);
            if (transform == stopAfter)
            {
                Profiler.EndSample();
                return 0;
            }

            var depth = 0;
            var stencilBits = 0;
            var tr = transform.parent;
            while (tr)
            {
                var mask = tr.GetComponent<Mask>();
                if (mask && mask.MaskEnabled())
                {
                    stencilBits = 0 < depth++ ? stencilBits << 1 : 0;
                    if (UISoftMaskProjectSettings.softMaskEnabled && mask is SoftMask softMask)
                    {
                        if (!nearestSoftMask)
                        {
                            nearestSoftMask = softMask;
                        }
                    }
                    else
                    {
                        stencilBits++;
                    }
                }

                if (tr == stopAfter) break;
                tr = tr.parent;
            }

            Profiler.EndSample();
            return stencilBits;
        }

        private void SetMaterialDirtyIfNeeded()
        {
            if (_graphic && _maskableMaterial)
            {
                _graphic.SetMaterialDirty();
            }
        }

        private void RequestRecalculateStencil()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            _shouldRecalculateStencil = true;
        }


#if UNITY_EDITOR
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            hideFlags = UISoftMaskProjectSettings.behaviorHideFlags;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
        }

        private void OnValidate()
        {
            RequestRecalculateStencil();
        }

        private void UpdateSceneViewMatrix()
        {
            if (!_graphic || !_graphic.canvas || !_maskableMaterial) return;
            if (FrameCache.TryGet(_maskableMaterial, nameof(UpdateSceneViewMatrix), out bool _))
            {
                return;
            }

            var canvas = _graphic.canvas.rootCanvas;
            if (!FrameCache.TryGet(canvas, "GameVp", out Matrix4x4 gameVp) ||
                !FrameCache.TryGet(canvas, "GameTvp", out Matrix4x4 gameTvp))
            {
                Profiler.BeginSample("(SM4UI)[SoftMaskable] (Editor) UpdateSceneViewMatrix > Calc GameVp & GameTvp");
                var rt = canvas.transform as RectTransform;
                var cam = canvas.worldCamera;
                if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay && cam)
                {
                    var eye = SoftMaskUtils.IsStereoCanvas(canvas)
                        ? Camera.MonoOrStereoscopicEye.Left
                        : Camera.MonoOrStereoscopicEye.Mono;
                    SoftMaskUtils.GetViewProjectionMatrix(eye, canvas, out var vMatrix, out var pMatrix);
                    gameVp = gameTvp = pMatrix * vMatrix;
                }
                else if (rt != null)
                {
                    var pos = rt.position;
                    var scale = rt.localScale.x;
                    var size = rt.sizeDelta;
                    gameVp = Matrix4x4.TRS(new Vector3(0, 0, 0.5f), Quaternion.identity,
                        new Vector3(2 / size.x, 2 / size.y, 0.0005f * scale));
                    gameTvp = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity,
                        new Vector3(1 / pos.x, 1 / pos.y, -2 / 2000f)) * Matrix4x4.Translate(-pos);
                }
                else
                {
                    gameVp = gameTvp = Matrix4x4.identity;
                }

                FrameCache.Set(canvas, "GameVp", gameVp);
                FrameCache.Set(canvas, "GameTvp", gameTvp);
                Profiler.EndSample();
            }

            // Set view and projection matrices.
            Profiler.BeginSample("(SM4UI)[SoftMaskable] (Editor) UpdateSceneViewMatrix > Set matrices");
            _maskableMaterial.SetMatrix(SoftMaskUtils.s_GameVpId, gameVp);
            _maskableMaterial.SetMatrix(SoftMaskUtils.s_GameTvpId, gameTvp);
            Profiler.EndSample();

            // Calc Right eye matrices.
            if (SoftMaskUtils.IsStereoCanvas(canvas))
            {
                if (!FrameCache.TryGet(canvas, "GameVp2", out gameVp))
                {
                    Profiler.BeginSample("(SM4UI)[SoftMaskable] (Editor) UpdateSceneViewMatrix > Calc GameVp2");
                    var eye = Camera.MonoOrStereoscopicEye.Right;
                    SoftMaskUtils.GetViewProjectionMatrix(eye, canvas, out var vMatrix, out var pMatrix);
                    gameVp = pMatrix * vMatrix;

                    FrameCache.Set(canvas, "GameVp2", gameVp);
                    Profiler.EndSample();
                }

                _maskableMaterial.SetMatrix(SoftMaskUtils.s_GameVp2Id, gameVp);
                _maskableMaterial.SetMatrix(SoftMaskUtils.s_GameTvp2Id, gameVp);
            }

            FrameCache.Set(_maskableMaterial, nameof(UpdateSceneViewMatrix), true);
        }
#endif
    }
}
