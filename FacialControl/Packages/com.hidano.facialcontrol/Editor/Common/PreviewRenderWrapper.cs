using System;
using SceneViewStyleCameraController;
using SceneViewStyleCameraController.Handlers;
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

        private int _dragButton = -1;
        private bool _dragAlt;

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
            var pivotPoint = CalculatePivotPoint(_previewInstance, bounds);
            var pivotDistance = bounds.extents.magnitude * 2f;
            var rotation = Quaternion.Euler(0f, 180f, 0f);
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
            var scrollDelta = evt.type == EventType.ScrollWheel ? evt.delta : Vector2.zero;
            var frame = new PreviewInputFrame(
                evt.type,
                evt.button,
                evt.mousePosition,
                evt.delta,
                scrollDelta,
                evt.alt);

            var changed = HandleInput(rect, frame);
            if (changed)
                evt.Use();
            return changed;
        }

        public bool HandleInput(Rect rect, PreviewInputFrame frame)
        {
            if (frame.EventType == EventType.MouseDown)
            {
                if (rect.Contains(frame.MousePosition))
                {
                    _dragButton = frame.Button;
                    _dragAlt = frame.Alt;
                }
                return false;
            }

            if (frame.EventType == EventType.MouseUp)
            {
                if (_dragButton == frame.Button)
                    _dragButton = -1;
                return false;
            }

            bool isCapturedDrag = frame.EventType == EventType.MouseDrag && _dragButton >= 0;
            int button = isCapturedDrag ? _dragButton : frame.Button;
            bool alt = isCapturedDrag ? _dragAlt : frame.Alt;

            if (!isCapturedDrag && !rect.Contains(frame.MousePosition))
                return false;

            var previous = _state;

            var verticalFlippedDelta = new Vector2(frame.Delta.x, -frame.Delta.y);

            switch (frame.EventType)
            {
                case EventType.MouseDrag when button == 0 && alt:
                    _state = OrbitHandler.Apply(_state, verticalFlippedDelta, OrbitSensitivity, MinPivotDistance);
                    break;
                case EventType.MouseDrag when button == 2:
                    _state = PanHandler.Apply(_state, verticalFlippedDelta, PanSensitivity, MinPivotDistance);
                    break;
                case EventType.ScrollWheel:
                    _state = DollyHandler.Apply(_state, frame.ScrollDelta.y, DollyScrollSensitivity, MinPivotDistance);
                    break;
                case EventType.MouseDrag when button == 1 && alt:
                    _state = DollyHandler.Apply(_state, -frame.Delta.y, DollyDragSensitivity, MinPivotDistance);
                    break;
                default:
                    return false;
            }

            return _state.position != previous.position
                || _state.rotation != previous.rotation
                || _state.pivotPoint != previous.pivotPoint
                || _state.pivotDistance != previous.pivotDistance;
        }

        public void ResetCamera()
        {
            _state = _initialState;
        }

        public static Vector3 CalculatePivotPoint(GameObject go, Bounds fallbackBounds)
        {
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    var headHeight = headBone.position.y;
                    return new Vector3(fallbackBounds.center.x, headHeight, fallbackBounds.center.z);
                }
            }

            return fallbackBounds.center;
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
