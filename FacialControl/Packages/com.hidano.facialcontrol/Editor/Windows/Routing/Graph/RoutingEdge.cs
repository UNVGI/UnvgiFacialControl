using System;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// Renders a routing edge with an editable weight knob anchored at the midpoint.
    /// </summary>
    public sealed class RoutingEdge : Edge
    {
        public const string WeightKnobName = "routing-edge-weight-knob";
        public const string WeightFieldName = "routing-edge-weight-field";
        private const float DragSensitivity = 0.01f;
        private const float KnobWidth = 72f;
        private const float KnobHeight = 22f;

        private readonly SerializedObject _serializedObject;
        private readonly IWiringSerializedMapper _wiringSerializedMapper;
        private readonly WeightDragManipulator _weightDragManipulator;
        private bool _isDraggingWeight;
        private float _currentWeight;

        public RoutingEdge(
            Port outputPort,
            Port inputPort,
            SerializedObject serializedObject,
            IWiringSerializedMapper wiringSerializedMapper,
            WiringEdgeData edgeData)
        {
            if (outputPort == null)
            {
                throw new ArgumentNullException(nameof(outputPort));
            }

            if (inputPort == null)
            {
                throw new ArgumentNullException(nameof(inputPort));
            }

            _serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            _wiringSerializedMapper = wiringSerializedMapper ?? throw new ArgumentNullException(nameof(wiringSerializedMapper));
            EdgeData = edgeData;
            _currentWeight = Mathf.Clamp01(edgeData.Weight);

            output = outputPort;
            input = inputPort;
            output.Connect(this);
            input.Connect(this);
            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);

            StyleSheet styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            WeightField = new FloatField
            {
                name = WeightFieldName,
                value = _currentWeight,
                isDelayed = true,
            };
            WeightField.style.width = 60f;
            WeightField.RegisterValueChangedCallback(OnWeightFieldChanged);

            WeightKnob = new VisualElement
            {
                name = WeightKnobName,
                pickingMode = PickingMode.Position,
            };
            WeightKnob.style.position = Position.Absolute;
            WeightKnob.style.width = KnobWidth;
            WeightKnob.style.height = KnobHeight;
            WeightKnob.style.flexDirection = FlexDirection.Row;
            WeightKnob.style.alignItems = Align.Center;
            WeightKnob.style.justifyContent = Justify.Center;
            WeightKnob.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.92f);
            WeightKnob.style.borderBottomLeftRadius = 10f;
            WeightKnob.style.borderBottomRightRadius = 10f;
            WeightKnob.style.borderTopLeftRadius = 10f;
            WeightKnob.style.borderTopRightRadius = 10f;
            WeightKnob.style.borderBottomWidth = 1f;
            WeightKnob.style.borderLeftWidth = 1f;
            WeightKnob.style.borderRightWidth = 1f;
            WeightKnob.style.borderTopWidth = 1f;
            WeightKnob.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
            WeightKnob.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
            WeightKnob.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
            WeightKnob.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
            WeightKnob.Add(WeightField);

            _weightDragManipulator = new WeightDragManipulator(this);
            WeightKnob.AddManipulator(_weightDragManipulator);
            Add(WeightKnob);

            RegisterCallback<GeometryChangedEvent>(_ => UpdateWeightKnobPosition());
            outputPort.RegisterCallback<GeometryChangedEvent>(_ => UpdateWeightKnobPosition());
            inputPort.RegisterCallback<GeometryChangedEvent>(_ => UpdateWeightKnobPosition());
            UpdateEdgeControl();
        }

        public WiringEdgeData EdgeData { get; }

        public VisualElement WeightKnob { get; }

        public FloatField WeightField { get; }

        public float CurrentWeight => _currentWeight;

        public void BeginWeightDrag()
        {
            if (_isDraggingWeight)
            {
                return;
            }

            _wiringSerializedMapper.BeginContinuousWeight(_serializedObject, EdgeData.LayerIndex, EdgeData.CanonicalId);
            _isDraggingWeight = true;
        }

        public void UpdateDraggedWeight(float weight)
        {
            float clampedWeight = Mathf.Clamp01(weight);
            _currentWeight = clampedWeight;
            WeightField.SetValueWithoutNotify(clampedWeight);
            _wiringSerializedMapper.SetWeightContinuous(_serializedObject, EdgeData.LayerIndex, EdgeData.CanonicalId, clampedWeight);
            UpdateWeightKnobPosition();
        }

        public void EndWeightDrag()
        {
            if (!_isDraggingWeight)
            {
                return;
            }

            _wiringSerializedMapper.EndContinuousWeight();
            _isDraggingWeight = false;
        }

        public override bool IsSelectable()
        {
            return false;
        }

        public override bool UpdateEdgeControl()
        {
            bool changed = base.UpdateEdgeControl();
            UpdateWeightKnobPosition();
            return changed;
        }

        private void OnWeightFieldChanged(ChangeEvent<float> evt)
        {
            float clampedWeight = Mathf.Clamp01(evt.newValue);
            _currentWeight = clampedWeight;
            WeightField.SetValueWithoutNotify(clampedWeight);
            _wiringSerializedMapper.SetWeight(_serializedObject, EdgeData.LayerIndex, EdgeData.CanonicalId, clampedWeight);
            UpdateWeightKnobPosition();
        }

        private void UpdateWeightKnobPosition()
        {
            if (WeightKnob == null || edgeControl == null)
            {
                return;
            }

            Vector2 midpoint = GetMidpoint();
            WeightKnob.style.left = midpoint.x - (KnobWidth * 0.5f);
            WeightKnob.style.top = midpoint.y - (KnobHeight * 0.5f);
        }

        private Vector2 GetMidpoint()
        {
            Rect outputBounds = output?.worldBound ?? default;
            Rect inputBounds = input?.worldBound ?? default;
            if (outputBounds.width <= 0f && inputBounds.width <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 origin = worldBound.position;
            Vector2 start = outputBounds.center - origin;
            Vector2 end = inputBounds.center - origin;
            return (start + end) * 0.5f;
        }

        private sealed class WeightDragManipulator : MouseManipulator
        {
            private readonly RoutingEdge _edge;
            private Vector2 _startPosition;
            private float _startWeight;
            private bool _isActive;

            public WeightDragManipulator(RoutingEdge edge)
            {
                _edge = edge ?? throw new ArgumentNullException(nameof(edge));
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDown);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            }

            private void OnMouseDown(MouseDownEvent evt)
            {
                if (_isActive || !CanStartManipulation(evt))
                {
                    return;
                }

                _isActive = true;
                _startPosition = evt.localMousePosition;
                _startWeight = _edge.CurrentWeight;
                _edge.BeginWeightDrag();
                target.CaptureMouse();
                evt.StopImmediatePropagation();
            }

            private void OnMouseMove(MouseMoveEvent evt)
            {
                if (!_isActive || !target.HasMouseCapture())
                {
                    return;
                }

                float delta = evt.localMousePosition.x - _startPosition.x;
                _edge.UpdateDraggedWeight(_startWeight + (delta * DragSensitivity));
                evt.StopImmediatePropagation();
            }

            private void OnMouseUp(MouseUpEvent evt)
            {
                if (!_isActive || !CanStopManipulation(evt))
                {
                    return;
                }

                _isActive = false;
                if (target.HasMouseCapture())
                {
                    target.ReleaseMouse();
                }

                _edge.EndWeightDrag();
                evt.StopImmediatePropagation();
            }
        }
    }
}
