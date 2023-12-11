using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Coffee.UISoftMask
{
    /// <summary>
    /// SoftMask.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class SoftMask : Mask, IMeshModifier, IMaskable, IMaskingShapeContainerOwner
    {
        private static readonly Camera.MonoOrStereoscopicEye[] s_MonoEyes = { Camera.MonoOrStereoscopicEye.Mono };

        private static readonly Camera.MonoOrStereoscopicEye[] s_StereoEyes =
            { Camera.MonoOrStereoscopicEye.Left, Camera.MonoOrStereoscopicEye.Right };

        [Tooltip("Enable alpha hit test.")]
        [SerializeField]
        public bool m_AlphaHitTest;

        [Tooltip("The threshold for soft masking.")]
        [SerializeField]
        public MinMax01 m_Threshold = new MinMax01(0, 1f);

        private readonly List<SoftMask> _children = new List<SoftMask>();

        private CommandBuffer _cb;
        private bool _hasSoftMaskBufferDrawn;
        private Mesh _mesh;
        private MaterialPropertyBlock _mpb;
        private SoftMask _parent;
        private Matrix4x4 _prevTransformMatrix;
        private Canvas _rootCanvas;
        private MaskingShapeContainer _shapeContainer;
        internal RenderTexture _softMaskBuffer;
        private CanvasViewChangeTrigger _viewChangeTrigger;

        /// <summary>
        /// Enable alpha hit test.
        /// </summary>
        public bool alphaHitTest
        {
            get => m_AlphaHitTest;
            set => m_AlphaHitTest = value;
        }

        /// <summary>
        /// The soft mask depth.
        /// </summary>
        public int softMaskDepth
        {
            get
            {
                var depth = -1;
                for (var current = this; current; current = current._parent)
                {
                    if (current.MaskEnabled())
                    {
                        depth++;
                    }
                }

                return depth;
            }
        }

        public bool hasSoftMaskBuffer => _softMaskBuffer;

        /// <summary>
        /// The soft mask buffer.
        /// </summary>
        public RenderTexture softMaskBuffer
        {
            get
            {
                if (UISoftMaskProjectSettings.softMaskEnabled && MaskEnabled())
                {
                    var id = GetInstanceID();
                    var size = RenderTextureRepository.GetScreenSize();
                    var rate = (int)UISoftMaskProjectSettings.downSamplingRate;
                    return RenderTextureRepository.Get(id, size, rate, ref _softMaskBuffer, false);
                }

                RenderTextureRepository.Release(ref _softMaskBuffer);
                return null;
            }
        }

        /// <summary>
        /// The threshold for soft masking.
        /// </summary>
        public MinMax01 threshold
        {
            get => m_Threshold;
            set
            {
                if (m_Threshold.Approximately(value)) return;

                m_Threshold = value;
                SetSoftMaskDirty();
            }
        }

        /// <summary>
        /// Clear color for the soft mask buffer.
        /// </summary>
        public Color clearColor
        {
            get;
            set;
        }

        public bool isDirty
        {
            get;
            private set;
        }

        /// <summary>
        /// Called when the component is enabled.
        /// </summary>
        protected override void OnEnable()
        {
            UIExtraCallbacks.onBeforeCanvasRebuild += CheckTransformChanged;
            UIExtraCallbacks.onAfterCanvasRebuild += RenderSoftMaskBuffer;
            SoftMaskUtils.onChangeBufferSize += SetDirtyAndNotify;

            if (graphic)
            {
                graphic.RegisterDirtyMaterialCallback(UpdateParentSoftMask);
                graphic.RegisterDirtyVerticesCallback(SetSoftMaskDirty);
                graphic.SetVerticesDirty();
            }

            if (UISoftMaskProjectSettings.softMaskableBehavior == SoftMaskableBehavior.Automatic)
            {
                this.AddComponentOnChildren<SoftMaskable>(UISoftMaskProjectSettings.behaviorHideFlags, true);
            }

            OnCanvasHierarchyChanged();
            _shapeContainer = GetComponent<MaskingShapeContainer>();

            base.OnEnable();
        }

        /// <summary>
        /// Called when the component is disabled.
        /// </summary>
        protected override void OnDisable()
        {
            UIExtraCallbacks.onBeforeCanvasRebuild -= CheckTransformChanged;
            UIExtraCallbacks.onAfterCanvasRebuild -= RenderSoftMaskBuffer;
            SoftMaskUtils.onChangeBufferSize -= SetDirtyAndNotify;

            if (graphic)
            {
                graphic.UnregisterDirtyMaterialCallback(UpdateParentSoftMask);
                graphic.UnregisterDirtyVerticesCallback(SetSoftMaskDirty);
                graphic.SetVerticesDirty();
            }

            UpdateParentSoftMask(null);
            _children.Clear();

            SoftMaskUtils.meshPool.Return(ref _mesh);
            SoftMaskUtils.materialPropertyBlockPool.Return(ref _mpb);
            SoftMaskUtils.commandBufferPool.Return(ref _cb);
            RenderTextureRepository.Release(ref _softMaskBuffer);

            UpdateCanvasViewChangeTrigger(null);
            _rootCanvas = null;
            _shapeContainer = null;

            base.OnDisable();
        }

        /// <summary>
        /// Called when the state of the parent Canvas is changed.
        /// </summary>
        protected override void OnCanvasHierarchyChanged()
        {
            if (!isActiveAndEnabled) return;
            _rootCanvas = this.GetRootComponent<Canvas>();
            UpdateCanvasViewChangeTrigger(null);
        }

        /// <summary>
        /// Call from unity if animation properties have changed.
        /// </summary>
        protected override void OnDidApplyAnimationProperties()
        {
            SetSoftMaskDirty();
        }

        /// <summary>
        /// This callback is called if an associated RectTransform has its dimensions changed. The call is also made to all child
        /// rect transforms, even if the child transform itself doesn't change - as it could have, depending on its anchoring.
        /// </summary>
        protected override void OnRectTransformDimensionsChange()
        {
            SetSoftMaskDirty();
        }

        protected void OnTransformChildrenChanged()
        {
            if (!isActiveAndEnabled) return;
            if (UISoftMaskProjectSettings.softMaskableBehavior == SoftMaskableBehavior.Automatic)
            {
                this.AddComponentOnChildren<SoftMaskable>(UISoftMaskProjectSettings.behaviorHideFlags, false);
            }
        }

        protected override void OnTransformParentChanged()
        {
            UpdateParentSoftMask();
            UpdateCanvasViewChangeTrigger(CanvasViewChangeTrigger.Find(transform));
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetSoftMaskDirty();
        }
#endif

        void IMaskable.RecalculateMasking()
        {
            SetSoftMaskDirty();
            if (!UISoftMaskProjectSettings.softMaskEnabled && _softMaskBuffer)
            {
                RenderTextureRepository.Release(ref _softMaskBuffer);
            }
        }

        void IMaskingShapeContainerOwner.Register(MaskingShapeContainer container)
        {
            _shapeContainer = container;
        }

        void IMeshModifier.ModifyMesh(Mesh mesh)
        {
        }

        void IMeshModifier.ModifyMesh(VertexHelper verts)
        {
            if (!MaskEnabled()) return;

            Profiler.BeginSample("(SM4UI)[SoftMask] ModifyMesh");
            if (!_mesh)
            {
                _mesh = SoftMaskUtils.meshPool.Rent();
            }

            verts.FillMesh(_mesh);
            _mesh.RecalculateBounds();

            Profiler.EndSample();

            Logging.Log(this, " >>>> Graphic mesh is modified.");
        }

        private void CheckTransformChanged()
        {
            if (transform.HasChanged(ref _prevTransformMatrix))
            {
                SetSoftMaskDirty();
            }

            if (!_viewChangeTrigger && _rootCanvas && _rootCanvas.renderMode == RenderMode.WorldSpace)
            {
                UpdateCanvasViewChangeTrigger(CanvasViewChangeTrigger.Find(transform));
                SetSoftMaskDirty();
            }
        }

        private void UpdateCanvasViewChangeTrigger(CanvasViewChangeTrigger trigger)
        {
            if (_viewChangeTrigger != trigger)
            {
                Logging.Log(this, $"UpdateCanvasViewChangeTrigger: {_viewChangeTrigger} -> {trigger}");

                if (_viewChangeTrigger)
                {
                    _viewChangeTrigger.onViewChange -= SetSoftMaskDirty;
                }

                if (trigger)
                {
                    trigger.onViewChange += SetSoftMaskDirty;
                }
            }

            _viewChangeTrigger = trigger;
        }

        public override bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (FrameCache.TryGet(this, nameof(IsRaycastLocationValid), out bool valid))
            {
                return valid;
            }

            if (!isActiveAndEnabled)
            {
                FrameCache.Set(this, nameof(IsRaycastLocationValid), true);
                return true;
            }

            // Check parent
            if (_parent && !_parent.IsRaycastLocationValid(sp, eventCamera))
            {
                FrameCache.Set(this, nameof(IsRaycastLocationValid), false);
                return false;
            }

            Profiler.BeginSample("(SM4UI)[SoftMask] IsRaycastLocationValid > Base");
            valid = base.IsRaycastLocationValid(sp, eventCamera);
            Profiler.EndSample();

            if (!MaskEnabled() || !UISoftMaskProjectSettings.softMaskEnabled)
            {
                FrameCache.Set(this, nameof(IsRaycastLocationValid), valid);
                return valid;
            }

            if (valid && alphaHitTest)
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] IsRaycastLocationValid > Alpha hit test");
                valid = Utils.AlphaHitTestValid(graphic, sp, eventCamera, 0.01f);
                Profiler.EndSample();
            }

            if (_shapeContainer)
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] IsRaycastLocationValid > Shapes");
                valid |= _shapeContainer.IsInside(sp, eventCamera, false, 0.5f);
                Profiler.EndSample();
            }

            FrameCache.Set(this, nameof(IsRaycastLocationValid), valid);
            return valid;
        }

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            if (UISoftMaskProjectSettings.softMaskEnabled && MaskEnabled())
            {
                return showMaskGraphic ? baseMaterial : null;
            }

            return base.GetModifiedMaterial(baseMaterial);
        }

        private void SetDirtyAndNotify()
        {
            SetSoftMaskDirty();
            MaskUtilities.NotifyStencilStateChanged(this);
        }

        public void SetSoftMaskDirty()
        {
            if (isDirty) return;

            Logging.LogIf(!isDirty, this, $"! SetSoftMaskDirty {GetInstanceID()}");
            isDirty = true;
            for (var i = _children.Count - 1; i >= 0; i--)
            {
                if (_children[i])
                {
                    _children[i].SetSoftMaskDirty();
                }
                else
                {
                    _children.RemoveAt(i);
                }
            }
        }

        private void UpdateParentSoftMask()
        {
            if (MaskEnabled())
            {
                var stopAfter = MaskUtilities.FindRootSortOverrideCanvas(transform);
                var parentSoftMask = transform.GetComponentInParent<SoftMask>(false, stopAfter, x => x.MaskEnabled());
                UpdateParentSoftMask(parentSoftMask);
            }
            else
            {
                UpdateParentSoftMask(null);
            }
        }

        private void UpdateParentSoftMask(SoftMask newParent)
        {
            if (_parent && _parent._children.Contains(this))
            {
                _parent._children.Remove(this);
            }

            if (newParent && !newParent._children.Contains(this))
            {
                newParent._children.Add(this);
            }

            if (_parent != newParent)
            {
                SetSoftMaskDirty();
            }

            _parent = newParent;
        }

        private bool InScreen()
        {
            if (graphic && graphic.IsInScreen()) return true;
            if (_shapeContainer && _shapeContainer.InScreen()) return true;

            return false;
        }

        private void RenderSoftMaskBuffer()
        {
            if (!UISoftMaskProjectSettings.softMaskEnabled) return;

            if (FrameCache.TryGet(this, nameof(RenderSoftMaskBuffer), out bool _)) return;
            FrameCache.Set(this, nameof(RenderSoftMaskBuffer), true);

            if (!isDirty) return;
            isDirty = false;

            if (_parent)
            {
                _parent.RenderSoftMaskBuffer();
            }

            var depth = softMaskDepth;
            if (depth < 0 || 4 <= depth) return;

            if (_cb == null)
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Rent cb");
                _cb = SoftMaskUtils.commandBufferPool.Rent();
                _cb.name = "[SoftMask] SoftMaskBuffer";
                Profiler.EndSample();
            }

            if (_mpb == null)
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Rent mpb");
                _mpb = SoftMaskUtils.materialPropertyBlockPool.Rent();
                _mpb.Clear();
                Profiler.EndSample();
            }

            if (!InScreen())
            {
                if (_hasSoftMaskBufferDrawn)
                {
                    Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Clear");
                    SoftMaskUtils.InitCommandBuffer(_cb, softMaskDepth, softMaskBuffer, clearColor);
                    Graphics.ExecuteCommandBuffer(_cb);
                    Profiler.EndSample();
                }

                _hasSoftMaskBufferDrawn = false;
                return;
            }

            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Init command buffer");
                SoftMaskUtils.InitCommandBuffer(_cb, softMaskDepth, softMaskBuffer, clearColor);
                Profiler.EndSample();
            }

            var eyes = SoftMaskUtils.IsStereoCanvas(graphic.canvas) ? s_StereoEyes : s_MonoEyes;
            for (var i = 0; i < eyes.Length; i++)
            {
                RenderSoftMaskBuffer(eyes[i]);
            }

            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Execute command buffer");
                Graphics.ExecuteCommandBuffer(_cb);
                _hasSoftMaskBufferDrawn = true;
                Logging.Log(this, $" >>>> SoftMaskBuffer '{softMaskBuffer.name}' will render.");
                Profiler.EndSample();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        private void RenderSoftMaskBuffer(Camera.MonoOrStereoscopicEye eye)
        {
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Set vp matrices");
                SoftMaskUtils.SetVpMatricesCommandBuffer(eye, _cb, graphic.canvas);
                SoftMaskUtils.ApplyMaterialPropertyBlock(_mpb, softMaskDepth, graphic.mainTexture, m_Threshold);
                Profiler.EndSample();
            }

            if (eye != Camera.MonoOrStereoscopicEye.Right && _parent)
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Copy texture from parent");
                if (_parent.softMaskBuffer)
                {
                    _cb.CopyTexture(_parent.softMaskBuffer, softMaskBuffer);
                }

                Profiler.EndSample();
            }

            if (eye != Camera.MonoOrStereoscopicEye.Mono)
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Set viewport");
                var w = softMaskBuffer.width * 0.5f;
                var h = softMaskBuffer.height;
                _cb.SetViewport(new Rect(w * (int)eye, 0f, w, h));
                Profiler.EndSample();
            }

            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Draw mesh");
                var softMaterial = SoftMaskUtils.GetSoftMaskingMaterial(MaskingShape.MaskingMethod.Additive);
                _cb.DrawMesh(_mesh, transform.localToWorldMatrix, softMaterial, 0, 0, _mpb);
                Profiler.EndSample();
            }

            if (_shapeContainer)
            {
                Profiler.BeginSample("(SM4UI)[SoftMask] RenderSoftMaskBuffer > Draw shapes");
                _shapeContainer.DrawSoftMaskBuffer(_cb, softMaskDepth);
                Profiler.EndSample();
            }
        }
    }
}
