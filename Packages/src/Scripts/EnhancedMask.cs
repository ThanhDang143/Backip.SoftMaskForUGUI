using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Coffee.UISoftMask
{
    /// <summary>
    /// SoftMask.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class EnhancedMask : Mask, IMaskingShapeContainerOwner
    {
        [Tooltip("Enable alpha hit test.")]
        [SerializeField]
        private bool m_AlphaHitTest;

        [Tooltip("Enable anti-alias masking.")] [SerializeField]
        public bool m_AntiAliasing;

        [Tooltip("Enable anti-alias masking.")] [SerializeField] [Range(0f, 1f)]
        public float m_AntiAliasingThreshold = 0.5f;

        private MaskingShapeContainer _shapeContainer;

        /// <summary>
        /// Enable alpha hit test.
        /// </summary>
        public bool alphaHitTest
        {
            get => m_AlphaHitTest;
            set => m_AlphaHitTest = value;
        }

        public bool antiAliasing
        {
            get => m_AntiAliasing;
            set
            {
                if (m_AntiAliasing == value) return;
                m_AntiAliasing = value;

                if (!isActiveAndEnabled) return;
                if (m_AntiAliasing)
                {
                    UIExtraCallbacks.onBeforeCanvasRebuild += UpdateAntiAliasing;
                }
                else
                {
                    UIExtraCallbacks.onBeforeCanvasRebuild -= UpdateAntiAliasing;
                    UpdateAntiAliasing();
                }
            }
        }

        public float antiAliasingThreshold
        {
            get => m_AntiAliasingThreshold;
            set => m_AntiAliasingThreshold = value;
        }

        protected override void OnEnable()
        {
            _shapeContainer = GetComponent<MaskingShapeContainer>();

            if (m_AntiAliasing)
            {
                UIExtraCallbacks.onBeforeCanvasRebuild += UpdateAntiAliasing;
            }

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (m_AntiAliasing)
            {
                UIExtraCallbacks.onBeforeCanvasRebuild -= UpdateAntiAliasing;
                UpdateAntiAliasing();
            }

            _shapeContainer = null;

            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (!isActiveAndEnabled) return;
            if (m_AntiAliasing)
            {
                UIExtraCallbacks.onBeforeCanvasRebuild -= UpdateAntiAliasing;
                UIExtraCallbacks.onBeforeCanvasRebuild += UpdateAntiAliasing;
            }
            else
            {
                UIExtraCallbacks.onBeforeCanvasRebuild -= UpdateAntiAliasing;
                UpdateAntiAliasing();
            }
        }
#endif

        void IMaskingShapeContainerOwner.Register(MaskingShapeContainer container)
        {
            _shapeContainer = container;
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

            Profiler.BeginSample("(SM4UI)[MaskWithShapes] IsRaycastLocationValid > Base");
            valid = base.IsRaycastLocationValid(sp, eventCamera);
            Profiler.EndSample();

            if (!MaskEnabled())
            {
                FrameCache.Set(this, nameof(IsRaycastLocationValid), valid);
                return valid;
            }

            if (valid && alphaHitTest)
            {
                Profiler.BeginSample("(SM4UI)[MaskWithShapes] IsRaycastLocationValid > Alpha hit test");
                valid = Utils.AlphaHitTestValid(graphic, sp, eventCamera, 0.01f);
                Profiler.EndSample();
            }

            if (_shapeContainer)
            {
                Profiler.BeginSample("(SM4UI)[MaskWithShapes] IsRaycastLocationValid > Shapes");
                valid = _shapeContainer.IsInside(sp, eventCamera, valid);
                Profiler.EndSample();
            }

            FrameCache.Set(this, nameof(IsRaycastLocationValid), valid);
            return valid;
        }

        private void UpdateAntiAliasing()
        {
            if (!this || !graphic) return;

            var use = enabled && antiAliasing;
            Utils.UpdateAntiAlias(graphic, use, antiAliasingThreshold);
        }
    }
}
