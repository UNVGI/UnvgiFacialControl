using UnityEditor.Experimental.GraphView;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// ルーティング UI の GraphView 薄層の配置先。
    /// </summary>
    public sealed class RoutingGraphView : GraphView
    {
        public RoutingGraphView()
        {
            name = "routing-graph-view";
        }
    }
}
