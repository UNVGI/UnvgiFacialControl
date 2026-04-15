using System;
using SceneViewStyleCameraController;
using UnityEditor;
using UnityEngine;

namespace Hidano.FacialControl.Editor.Common
{
    public class PreviewRenderWrapper : IDisposable
    {
        public const float DefaultFov = 30f;
        public const float DefaultNearClip = 0.01f;
        public const float DefaultFarClip = 100f;
        public const float DefaultLightIntensity = 1.2f;
        public static readonly Quaternion DefaultLightRotation = Quaternion.Euler(30f, -30f, 0f);
        public static readonly Color DefaultBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        private const float OrbitSensitivity = 0.5f;
        private const float PanSensitivity = 0.002f;
        private const float DollyScrollSensitivity = 0.1f;
        private const float DollyDragSensitivity = 0.02f;
        private const float MinPivotDistance = 0.1f;

        private PreviewRenderUtility _previewRenderUtility;
        private GameObject _previewInstance;
        private bool _disposed;

        private CameraState _state;
        private CameraState _initialState;

        public bool IsInitialized => _previewRenderUtility != null && _previewInstance != null;

        public GameObject PreviewInstance => _previewInstance;

        public void Setup(GameObject sourceObject)
        {
            Cleanup();

            if (sourceObject == null)
                return;

            _previewRenderUtility = new PreviewRenderUtility();

            _previewRenderUtility.camera.fieldOfView = DefaultFov;
            _previewRenderUtility.camera.nearClipPlane = DefaultNearClip;
            _previewRenderUtility.camera.farClipPlane = DefaultFarClip;
            _previewRenderUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewRenderUtility.camera.backgroundColor = DefaultBackgroundColor;

            _previewRenderUtility.lights[0].intensity = DefaultLightIntensity;
            _previewRenderUtility.lights[0].transform.rotation = DefaultLightRotation;

            _previewInstance = UnityEngine.Object.Instantiate(sourceObject);
            _previewInstance.hideFlags = HideFlags.HideAndDontSave;
            _previewInstance.transform.position = Vector3.zero;
            _previewInstance.transform.rotation = Quaternion.identity;

            _previewRenderUtility.AddSingleGO(_previewInstance);

            var bounds = CalculateBounds(_previewInstance);
            var pivotPoint = bounds.center;
            var pivotDistance = bounds.extents.magnitude * 2f;
            var rotation = Quaternion.identity;
            var position = pivotPoint - rotation * Vector3.forward * pivotDistance;

            _initialState = new CameraState(position, rotation, pivotPoint, pivotDistance);
            _state = _initialState;
        }

        public void Cleanup()
        {
            if (_previewInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }

            if (_previewRenderUtility != null)
            {
                _previewRenderUtility.Cleanup();
                _previewRenderUtility = null;
            }
        }

        public void Render(Rect rect)
        {
            if (_previewRenderUtility == null || _previewInstance == null)
                return;

            _previewRenderUtility.camera.transform.position = _state.position;
            _previewRenderUtility.camera.transform.rotation = _state.rotation;

            _previewRenderUtility.BeginPreview(rect, GUIStyle.none);
            _previewRenderUtility.Render(true, true);
            var texture = _previewRenderUtility.EndPreview();

            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        public bool HandleInput(Rect rect)
        {
            var evt = Event.current;

            if (!rect.Contains(evt.mousePosition))
                return false;

            return false;
        }

        public static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Cleanup();
                _disposed = true;
            }
        }
    }
}
