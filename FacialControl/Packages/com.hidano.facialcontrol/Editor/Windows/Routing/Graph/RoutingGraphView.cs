using System;
using System.Collections.Generic;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// ルーティング UI の GraphView ルート要素。
    /// </summary>
    public sealed class RoutingGraphView : GraphView
    {
        private readonly List<SourceNodeView> _sourceNodeViews = new List<SourceNodeView>();

        public RoutingGraphView()
        {
            name = "routing-graph-view";

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
        }

        public IReadOnlyList<SourceNodeView> SourceNodeViews => _sourceNodeViews;

        public void SetSourceNodes(
            IReadOnlyList<SourceNodeDescriptor> sourceNodes,
            Action<SourceNodeDescriptor> onAutoWireRequested)
        {
            if (sourceNodes == null)
            {
                throw new ArgumentNullException(nameof(sourceNodes));
            }

            ClearSourceNodes();

            for (int i = 0; i < sourceNodes.Count; i++)
            {
                var sourceNodeView = new SourceNodeView(sourceNodes[i], onAutoWireRequested);
                sourceNodeView.SetPosition(new Rect(32f, 32f + (i * 140f), 240f, 96f));
                _sourceNodeViews.Add(sourceNodeView);
                AddElement(sourceNodeView);
            }
        }

        private void ClearSourceNodes()
        {
            for (int i = 0; i < _sourceNodeViews.Count; i++)
            {
                RemoveElement(_sourceNodeViews[i]);
            }

            _sourceNodeViews.Clear();
        }
    }
}
