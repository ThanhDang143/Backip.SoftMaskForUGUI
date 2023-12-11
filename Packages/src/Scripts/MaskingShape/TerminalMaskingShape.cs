﻿using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Coffee.UISoftMask
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class TerminalMaskingShape : MaskableGraphic, ILayoutElement, ILayoutIgnorer
    {
        private static Material s_SharedTerminalMaterial;
        private Mask _mask;

        public override bool raycastTarget
        {
            get => false;
            set { }
        }

        protected override void OnEnable()
        {
            if (!s_SharedTerminalMaterial)
            {
                s_SharedTerminalMaterial = new Material(Shader.Find("Hidden/UI/TerminalMaskingShape"))
                {
                    hideFlags = HideFlags.DontSave
                };
            }

            material = s_SharedTerminalMaterial;
            _mask = transform.parent.GetComponent<Mask>();

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_mask && _mask.MaskEnabled())
            {
                _mask.graphic.SetMaterialDirty();
            }
        }

        void ILayoutElement.CalculateLayoutInputHorizontal()
        {
        }

        void ILayoutElement.CalculateLayoutInputVertical()
        {
        }

        float ILayoutElement.minWidth => 0;

        float ILayoutElement.preferredWidth => 0;

        float ILayoutElement.flexibleWidth => 0;

        float ILayoutElement.minHeight => 0;

        float ILayoutElement.preferredHeight => 0;

        float ILayoutElement.flexibleHeight => 0;

        int ILayoutElement.layoutPriority => 0;

        bool ILayoutIgnorer.ignoreLayout => true;

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!IsActive())
            {
                StencilMaterial.Remove(m_MaskMaterial);
                m_MaskMaterial = null;
                return null;
            }

            var stencilDepth = Utils.GetStencilDepthAndMask(transform, false, out var mask);
            if (stencilDepth <= 0 || _mask != mask)
            {
                StencilMaterial.Remove(m_MaskMaterial);
                m_MaskMaterial = null;
                return null;
            }

            var desiredStencilBit = 1 << (stencilDepth - 1);
            var maskMat = StencilMaterial.Add(baseMaterial, desiredStencilBit, StencilOp.Zero,
                CompareFunction.Equal, 0, desiredStencilBit, desiredStencilBit);

            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = maskMat;

            return maskMat;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!IsActive()) return;

            // Full-screen rendering.
            Profiler.BeginSample("(SM4UI)[TerminalMaskingShape)] OnPopulateMesh");
            vh.AddVert(new Vector3(-999999, -999999), new Color32(255, 255, 255, 255), new Vector2(0, 0));
            vh.AddVert(new Vector3(-999999, +999999), new Color32(255, 255, 255, 255), new Vector2(0, 1));
            vh.AddVert(new Vector3(+999999, +999999), new Color32(255, 255, 255, 255), new Vector2(1, 1));
            vh.AddVert(new Vector3(+999999, -999999), new Color32(255, 255, 255, 255), new Vector2(1, 0));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
            Profiler.EndSample();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(TerminalMaskingShape))]
    internal class TerminalMaskingShapeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
        }
    }
#endif
}
