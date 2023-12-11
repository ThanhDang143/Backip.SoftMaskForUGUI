﻿using System;
using UnityEngine;

namespace Coffee.UISoftMask
{
    /// <summary>
    /// triggers an event when the view projection matrix of a Canvas in World Space render mode changes.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [ExecuteAlways]
    [AddComponentMenu("")]
    public class CanvasViewChangeTrigger : MonoBehaviour
    {
        private Canvas _canvas;
        private int _lastCameraVpHash;

        /// <summary>
        /// Called when the component is enabled.
        /// </summary>
        private void OnEnable()
        {
            hideFlags = UISoftMaskProjectSettings.behaviorHideFlags;
            _canvas = GetComponent<Canvas>();
            UIExtraCallbacks.onBeforeCanvasRebuild += CheckViewProjectionMatrix;
        }

        /// <summary>
        /// Called when the component is disabled.
        /// </summary>
        private void OnDisable()
        {
            UIExtraCallbacks.onBeforeCanvasRebuild -= CheckViewProjectionMatrix;
        }

        /// <summary>
        /// Called when the component is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            _canvas = null;
            onViewChange = null;
        }

        /// <summary>
        /// Event that is triggered when the view projection matrix changes.
        /// </summary>
        public event Action onViewChange;

        private void CheckViewProjectionMatrix()
        {
            if (!_canvas || _canvas.renderMode != RenderMode.WorldSpace) return;

            // Get the view and projection matrix of the Canvas.
            var prevHash = _lastCameraVpHash;
            SoftMaskUtils.GetViewProjectionMatrix(Camera.MonoOrStereoscopicEye.Mono, _canvas, out var vpMatrix);
            _lastCameraVpHash = vpMatrix.GetHashCode();

            // The matrix has changed.
            if (prevHash != _lastCameraVpHash)
            {
                Logging.Log(this, "ViewProjection changed.");
                onViewChange?.Invoke();
            }
        }

        /// <summary>
        /// get or add a CanvasViewChangeTrigger component in the root Canvas.
        /// </summary>
        public static CanvasViewChangeTrigger Find(Transform transform)
        {
            // Find the root Canvas component.
            var rootCanvas = transform.GetRootComponent<Canvas>();

            // Get the CanvasViewChangeTrigger component if found, or add.
            return rootCanvas ? rootCanvas.GetOrAddComponent<CanvasViewChangeTrigger>() : null;
        }
    }
}
