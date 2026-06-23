using System;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// Renders an invalid-id edge without an output port as a read-only red dashed wire.
    /// </summary>
    public sealed class DanglingEdge : Edge
    {
        public const string IdBadgeName = "routing-dangling-edge-id-badge";
        private static readonly Color WireColor = new Color(1f, 0.29f, 0.29f, 0.95f);
        private const float StartOffsetX = 168f;
        private const float StartOffsetY = 20f;
        private const float DashLength = 8f;
        private const float DashGap = 5f;
        private const float LineWidth = 3f;
        private const float LayoutPadding = 24f;

        private readonly DashedWireElement _dashedWire;
        private readonly Label _idBadge;
        private bool _isInitialized;

        public DanglingEdge(Port inputPort, DanglingEdgeData edgeData)
        {
            if (inputPort == null)
            {
                throw new ArgumentNullException(nameof(inputPort));
            }

            EdgeData = edgeData;
            input = inputPort;
            input.Connect(this);
            capabilities &= ~(Capabilities.Selectable | Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);

            StyleSheet styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            pickingMode = PickingMode.Ignore;
            if (edgeControl != null)
            {
                edgeControl.pickingMode = PickingMode.Ignore;
                edgeControl.style.display = DisplayStyle.None;
            }

            _dashedWire = new DashedWireElement();
            _dashedWire.pickingMode = PickingMode.Ignore;
            _dashedWire.style.position = Position.Absolute;
            _dashedWire.style.left = 0f;
            _dashedWire.style.top = 0f;
            _dashedWire.style.right = 0f;
            _dashedWire.style.bottom = 0f;
            Add(_dashedWire);

            _idBadge = new Label(edgeData.Id)
            {
                name = IdBadgeName,
                tooltip = edgeData.Id,
                pickingMode = PickingMode.Ignore,
            };
            _idBadge.style.position = Position.Absolute;
            _idBadge.style.color = WireColor;
            _idBadge.style.backgroundColor = new Color(0.15f, 0.02f, 0.02f, 0.9f);
            _idBadge.style.borderBottomColor = WireColor;
            _idBadge.style.borderLeftColor = WireColor;
            _idBadge.style.borderRightColor = WireColor;
            _idBadge.style.borderTopColor = WireColor;
            _idBadge.style.borderBottomWidth = 1f;
            _idBadge.style.borderLeftWidth = 1f;
            _idBadge.style.borderRightWidth = 1f;
            _idBadge.style.borderTopWidth = 1f;
            _idBadge.style.borderBottomLeftRadius = 8f;
            _idBadge.style.borderBottomRightRadius = 8f;
            _idBadge.style.borderTopLeftRadius = 8f;
            _idBadge.style.borderTopRightRadius = 8f;
            _idBadge.style.paddingLeft = 6f;
            _idBadge.style.paddingRight = 6f;
            _idBadge.style.paddingTop = 2f;
            _idBadge.style.paddingBottom = 2f;
            _idBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(_idBadge);

            RegisterCallback<GeometryChangedEvent>(_ => UpdateVisuals());
            inputPort.RegisterCallback<GeometryChangedEvent>(_ => UpdateVisuals());
            _isInitialized = true;
            UpdateEdgeControl();
        }

        public DanglingEdgeData EdgeData { get; }

        public Label IdBadge => _idBadge;

        public Color CurrentWireColor => WireColor;

        public override bool IsSelectable()
        {
            return false;
        }

        public override bool UpdateEdgeControl()
        {
            if (!_isInitialized || input == null)
            {
                return false;
            }

            UpdateVisuals();
            return true;
        }

        private void UpdateVisuals()
        {
            if (input == null)
            {
                return;
            }

            Vector2 endWorld = input.worldBound.center;
            Vector2 startWorld = endWorld + GetAnchorOffset();
            Vector2 min = Vector2.Min(startWorld, endWorld) - new Vector2(LayoutPadding, LayoutPadding);
            Vector2 max = Vector2.Max(startWorld, endWorld) + new Vector2(LayoutPadding, LayoutPadding);

            style.left = min.x;
            style.top = min.y;
            style.width = Mathf.Max(1f, max.x - min.x);
            style.height = Mathf.Max(1f, max.y - min.y);

            Vector2 localStart = startWorld - min;
            Vector2 localEnd = endWorld - min;

            _dashedWire.SetEndpoints(localStart, localEnd, WireColor, DashLength, DashGap, LineWidth);

            Vector2 midpoint = (localStart + localEnd) * 0.5f;
            _idBadge.style.left = midpoint.x - 52f;
            _idBadge.style.top = midpoint.y - 26f;
        }

        private Vector2 GetAnchorOffset()
        {
            float declarationOffset = Mathf.Min(EdgeData.DeclarationIndex, 6) * StartOffsetY;
            return new Vector2(-StartOffsetX, -declarationOffset);
        }

        private sealed class DashedWireElement : VisualElement
        {
            private Vector2 _start;
            private Vector2 _end;
            private Color _color;
            private float _dashLength;
            private float _dashGap;
            private float _lineWidth;

            public DashedWireElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetEndpoints(
                Vector2 start,
                Vector2 end,
                Color color,
                float dashLength,
                float dashGap,
                float lineWidth)
            {
                _start = start;
                _end = end;
                _color = color;
                _dashLength = dashLength;
                _dashGap = dashGap;
                _lineWidth = lineWidth;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Painter2D painter2D = context.painter2D;
                painter2D.strokeColor = _color;
                painter2D.lineWidth = _lineWidth;
                painter2D.lineCap = LineCap.Round;
                painter2D.lineJoin = LineJoin.Round;

                Vector2 delta = _end - _start;
                float length = delta.magnitude;
                if (length <= Mathf.Epsilon)
                {
                    return;
                }

                Vector2 direction = delta / length;
                float distance = 0f;
                while (distance < length)
                {
                    float next = Mathf.Min(length, distance + _dashLength);
                    painter2D.BeginPath();
                    painter2D.MoveTo(_start + (direction * distance));
                    painter2D.LineTo(_start + (direction * next));
                    painter2D.Stroke();
                    distance = next + _dashGap;
                }
            }
        }
    }
}
