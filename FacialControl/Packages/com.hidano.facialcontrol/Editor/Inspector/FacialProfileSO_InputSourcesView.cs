using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Editor.Inspector
{
    /// <summary>
    /// FacialProfileSO Inspector に表示する、入力源ウェイトの読取専用ビュー (Req 8.6)。
    /// Play Mode 中に <see cref="FacialController"/> が提供する診断スナップショット
    /// (<see cref="FacialController.GetInputSourceWeightsSnapshot"/>) を
    /// UI Toolkit の <see cref="ListView"/> で表示する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Play Mode 外ではプレースホルダを表示し、Play Mode 中は現在の <see cref="FacialController"/>
    /// 参照をキャッシュして <see cref="EditorApplication.update"/> 経由で約 100ms 間隔に
    /// 健全性確認を行う。これにより Inspector の repaint は O(1) で済み、
    /// 大規模シーンでも <c>FindObjectOfType</c> が毎回走るのを防ぐ。
    /// </para>
    /// <para>
    /// 旧 preview.1 まで Editor 拡張で実施していた InputBindingProfileSO 由来の
    /// Category=Controller × Keyboard-only バインド検出は、
    /// <c>com.hidano.facialcontrol.input</c> サブパッケージに移管済み（preview.2）。
    /// </para>
    /// </remarks>
    internal sealed class FacialProfileSO_InputSourcesView : IDisposable
    {
        private const double RecheckIntervalSeconds = 0.1; // 約 100ms

        private readonly FacialProfileSO _target;

        private readonly VisualElement _root;
        private readonly Label _placeholderLabel;
        private readonly ListView _listView;

        private readonly List<LayerSourceWeightEntry> _entries = new List<LayerSourceWeightEntry>();

        private FacialController _cachedController;
        private double _lastRecheckTime;

        public VisualElement RootElement => _root;

        public FacialProfileSO_InputSourcesView(FacialProfileSO target)
        {
            _target = target;

            _root = new VisualElement();

            _placeholderLabel = new Label("Play Mode に入ると、現在の FacialController から入力源ウェイトのスナップショットを表示します。");
            _placeholderLabel.style.whiteSpace = WhiteSpace.Normal;
            _placeholderLabel.style.marginTop = 2;
            _placeholderLabel.style.marginBottom = 2;
            _root.Add(_placeholderLabel);

            _listView = new ListView
            {
                fixedItemHeight = 18f,
                selectionType = SelectionType.None,
                showBorder = true,
                itemsSource = _entries,
                makeItem = MakeItem,
                bindItem = BindItem,
            };
            _listView.style.display = DisplayStyle.None;
            _listView.style.minHeight = 40f;
            _listView.style.maxHeight = 200f;
            _root.Add(_listView);

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            RefreshSnapshot(forceRecheck: true);
        }

        public void Dispose()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _cachedController = null;
        }

        private static VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var layerLabel = new Label();
            layerLabel.name = "layer";
            layerLabel.style.width = 64;
            row.Add(layerLabel);

            var sourceLabel = new Label();
            sourceLabel.name = "source";
            sourceLabel.style.flexGrow = 1;
            row.Add(sourceLabel);

            var weightLabel = new Label();
            weightLabel.name = "weight";
            weightLabel.style.width = 72;
            weightLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(weightLabel);

            var statusLabel = new Label();
            statusLabel.name = "status";
            statusLabel.style.width = 120;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(statusLabel);

            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _entries.Count)
                return;

            var entry = _entries[index];
            var layerLabel = element.Q<Label>("layer");
            var sourceLabel = element.Q<Label>("source");
            var weightLabel = element.Q<Label>("weight");
            var statusLabel = element.Q<Label>("status");

            if (layerLabel != null)
                layerLabel.text = $"Layer {entry.LayerIdx}";
            if (sourceLabel != null)
                sourceLabel.text = entry.SourceId.Value ?? "(unknown)";
            if (weightLabel != null)
                weightLabel.text = entry.Weight.ToString("F3");
            if (statusLabel != null)
            {
                string status = entry.IsValid ? "valid" : "invalid";
                if (entry.Saturated)
                {
                    status += " / saturated";
                }
                statusLabel.text = status;
                statusLabel.style.color = entry.IsValid
                    ? (entry.Saturated ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.5f, 0.5f, 0.5f))
                    : new Color(0.8f, 0.3f, 0.3f);
            }
        }

        private void OnEditorUpdate()
        {
            if (_root == null)
                return;

            // 切替コストを抑えるため約 100ms 間隔に間引き (タスク 9 の O(1) repaint 要件)。
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastRecheckTime < RecheckIntervalSeconds)
            {
                return;
            }
            _lastRecheckTime = now;

            RefreshSnapshot(forceRecheck: false);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            _cachedController = null;
            RefreshSnapshot(forceRecheck: true);
        }

        private void RefreshSnapshot(bool forceRecheck)
        {
            if (!EditorApplication.isPlaying)
            {
                _cachedController = null;
                _placeholderLabel.style.display = DisplayStyle.Flex;
                _listView.style.display = DisplayStyle.None;
                return;
            }

            if (_cachedController == null || !IsCachedControllerValid())
            {
                _cachedController = FindMatchingController();
            }

            if (_cachedController == null)
            {
                _placeholderLabel.text = "FacialController が見つかりません (現在の ProfileSO を参照している FacialController をシーンに配置してください)。";
                _placeholderLabel.style.display = DisplayStyle.Flex;
                _listView.style.display = DisplayStyle.None;
                return;
            }

            var snapshot = _cachedController.GetInputSourceWeightsSnapshot();
            _entries.Clear();
            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    _entries.Add(snapshot[i]);
                }
            }

            if (_entries.Count == 0)
            {
                _placeholderLabel.text = "スナップショットは空です (初期化直後かプロファイル未ロードの可能性があります)。";
                _placeholderLabel.style.display = DisplayStyle.Flex;
                _listView.style.display = DisplayStyle.None;
                return;
            }

            _placeholderLabel.style.display = DisplayStyle.None;
            _listView.style.display = DisplayStyle.Flex;
            _listView.Rebuild();
        }

        private bool IsCachedControllerValid()
        {
            if (_cachedController == null)
                return false;

            // Unity のライフサイクルで破棄されたオブジェクトは null 相当になる。
            if (!_cachedController)
                return false;

            return _cachedController.ProfileSO == _target;
        }

        private FacialController FindMatchingController()
        {
#if UNITY_2022_2_OR_NEWER
            var controllers = UnityEngine.Object.FindObjectsByType<FacialController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#else
            var controllers = UnityEngine.Object.FindObjectsOfType<FacialController>(true);
#endif
            for (int i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller != null && controller.ProfileSO == _target)
                {
                    return controller;
                }
            }
            return null;
        }

    }
}
