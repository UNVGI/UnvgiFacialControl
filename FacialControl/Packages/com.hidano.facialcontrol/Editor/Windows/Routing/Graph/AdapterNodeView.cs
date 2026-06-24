using System;
using System.Collections.Generic;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Editor.Windows.Routing.Graph
{
    /// <summary>
    /// 1 つの AdapterBinding を表すノード。複数の入力源を名前付き出力ポートとして並べ、
    /// 「アダプター」バッジで概念を明示する。
    /// </summary>
    public sealed class AdapterNodeView : Node
    {
        public const string AutoWireButtonText = "overlay レイヤーへ一括配線";
        public const string TypeBadgeName = "routing-adapter-type-badge";
        public const string TypeBadgeText = "アダプター";
        private static readonly UnityEngine.Color TitleBarColor = new UnityEngine.Color(0.20f, 0.18f, 0.30f, 1f);
        private static readonly UnityEngine.Color TypeBadgeColor = new UnityEngine.Color(0.80f, 0.74f, 1f, 1f);

        private readonly Action<string> _onAutoWireRequested;
        private readonly Dictionary<string, Port> _outputPorts = new Dictionary<string, Port>(StringComparer.Ordinal);

        public AdapterNodeView(AdapterNodeData data, Action<string> onAutoWireRequested)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            _onAutoWireRequested = onAutoWireRequested;

            name = $"routing-adapter-node-{Data.BindingSlug}";
            title = string.IsNullOrEmpty(Data.DisplayName) ? "Adapter" : Data.DisplayName;

            StyleSheet styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);

            // アダプターであることを一目で分かるようにタイトルバーを着色し「アダプター」バッジを付与する。
            titleContainer.style.backgroundColor = TitleBarColor;
            TypeBadge = new Label(TypeBadgeText) { name = TypeBadgeName };
            TypeBadge.style.color = TypeBadgeColor;
            TypeBadge.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            TypeBadge.style.marginRight = 6f;
            titleButtonContainer.Add(TypeBadge);

            // 「overlay レイヤーへ一括配線」は default-layer-inputs 系 binding でのみ意味を持つ。
            if (Data.SupportsAutoWire)
            {
                AutoWireButton = new Button(NotifyAutoWireRequested)
                {
                    text = AutoWireButtonText,
                };
                AutoWireButton.AddToClassList(FacialControlStyles.ActionButton);
                titleButtonContainer.Add(AutoWireButton);
            }

            for (int i = 0; i < Data.Outputs.Count; i++)
            {
                AdapterOutputData output = Data.Outputs[i];
                Port port = InstantiatePort(
                    Orientation.Horizontal,
                    Direction.Output,
                    Port.Capacity.Multi,
                    typeof(float));
                port.portName = output.Label;
                port.tooltip = output.CanonicalId;
                port.userData = output.CanonicalId;
                _outputPorts[output.CanonicalId] = port;
                outputContainer.Add(port);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public AdapterNodeData Data { get; }

        public Button AutoWireButton { get; }

        public Label TypeBadge { get; }

        /// <summary>
        /// 指定 canonical id に対応する出力ポートを返す。存在しなければ null。
        /// </summary>
        public Port GetOutputPort(string canonicalId)
        {
            return canonicalId != null && _outputPorts.TryGetValue(canonicalId, out Port port) ? port : null;
        }

        private void NotifyAutoWireRequested()
        {
            _onAutoWireRequested?.Invoke(Data.BindingSlug);
        }
    }
}
