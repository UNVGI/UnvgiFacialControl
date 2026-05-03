using UnityEngine;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Samples
{
    /// <summary>
    /// 同一レイヤーに複数の <c>ExpressionTrigger</c> 入力源 (controller-expr / keyboard-expr) を並置し、
    /// それぞれのウェイト比と独立トリガーで BlendShape 加重和がどう見えるかを目視検証するための
    /// PlayMode 専用 HUD。さらにアナログバインディング (左スティック → LeftEye/RightEye Euler、
    /// ARKit/OSC float → mouth-open BlendShape) の現在値も同 HUD で観測する。
    /// </summary>
    /// <remarks>
    /// 詳しい使い方は本サンプル同梱の README.md 参照。
    /// 本 HUD は FacialController の既存公開 API
    /// (<c>SetInputSourceWeight</c> / <c>TryGetExpressionTriggerSourceById</c> /
    /// <c>GetInputSourceWeightsSnapshot</c>) のみを利用し、書込はしないオブザーバ。
    /// 表情データの読込は新統合 SO (<c>FacialCharacterSO</c>) 経由で
    /// <c>FacialController.OnEnable</c> が StreamingAssets/FacialControl/{SO 名}/profile.json を
    /// 自動探索して行う想定 (3-B モデル)。アナログバインディングは SO の Inspector で編集する。
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("FacialControl/Samples/Multi Source Blend Demo HUD")]
    public class MultiSourceBlendDemoHUD : MonoBehaviour
    {
        [Tooltip("対象の FacialController")]
        [SerializeField]
        private FacialController _facialController;

        [Tooltip("HUD を表示するレイヤー index。既定: 0 (emotion)。")]
        [SerializeField]
        private int _layerIndex = 0;

        [Tooltip("Controller 入力源の source index (1 始まり)。")]
        [SerializeField]
        private int _controllerSourceIndex = 1;

        [Tooltip("Keyboard 入力源の source index (1 始まり)。")]
        [SerializeField]
        private int _keyboardSourceIndex = 2;

        [Tooltip("HUD で観測する LeftEye Transform (BonePose ターゲット)。アナログ機能未使用時は未割当で OK。")]
        [SerializeField]
        private Transform _leftEye;

        [Tooltip("HUD で観測する RightEye Transform (BonePose ターゲット)。アナログ機能未使用時は未割当で OK。")]
        [SerializeField]
        private Transform _rightEye;

        [Tooltip("観測対象の SkinnedMeshRenderer。未割当時は FacialController から探索する。")]
        [SerializeField]
        private SkinnedMeshRenderer _meshRenderer;

        [Tooltip("HUD に表示する mouth-open BlendShape 名 (デフォルト: jawOpen)。")]
        [SerializeField]
        private string _mouthOpenBlendShapeName = "jawOpen";

        [Tooltip("HUD で表示するアナログソース ID (informational)。")]
        [SerializeField]
        private string[] _displayedSourceIds = { "left_stick", "arkit_jaw_open" };

        private static readonly string[] s_expressionIds = { "smile", "anger", "surprise", "lipsync_a" };

        private float _controllerWeight = 0.5f;
        private float _keyboardWeight = 0.5f;
        private Vector2 _scroll;
        private bool _initialWeightsApplied;

        private void Awake()
        {
            // Game View が非 focus でも Aggregator が tick し続けるように。
            UnityEngine.Application.runInBackground = true;
        }

        private void Update()
        {
            // FacialController は OnEnable で SO + StreamingAssets JSON から非同期に初期化される
            // 場合があるため、初期化検出後 1 度だけウェイトを適用する。
            if (_initialWeightsApplied)
            {
                return;
            }
            if (_facialController == null || !_facialController.IsInitialized)
            {
                return;
            }

            ApplyInitialWeights();
            _initialWeightsApplied = true;
        }

        private void ApplyInitialWeights()
        {
            _facialController.SetInputSourceWeight(_layerIndex, _controllerSourceIndex, _controllerWeight);
            _facialController.SetInputSourceWeight(_layerIndex, _keyboardSourceIndex, _keyboardWeight);
        }

        private void OnGUI()
        {
            if (_facialController == null)
            {
                GUILayout.BeginArea(new Rect(10, 10, 400, 80));
                GUILayout.Label("MultiSourceBlendDemoHUD: FacialController 未設定");
                GUILayout.EndArea();
                return;
            }

            if (!_facialController.IsInitialized)
            {
                GUILayout.BeginArea(new Rect(10, 10, 400, 80));
                GUILayout.Label("MultiSourceBlendDemoHUD: FacialController 初期化前");
                GUILayout.EndArea();
                return;
            }

            var controllerSource = GetSource(Hidano.FacialControl.Adapters.InputSources.ExpressionTriggerInputSource.ControllerReservedId);
            var keyboardSource = GetSource(Hidano.FacialControl.Adapters.InputSources.ExpressionTriggerInputSource.KeyboardReservedId);

            GUILayout.BeginArea(new Rect(10, 10, 480, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("<b>Multi Source Blend Demo</b>", RichStyle());
            GUILayout.Label($"layer {_layerIndex} の 2 ソースを加重和で合成");

            DrawWeightSlider("Controller weight (source=1)",
                ref _controllerWeight, _controllerSourceIndex);
            DrawWeightSlider("Keyboard weight (source=2)",
                ref _keyboardWeight, _keyboardSourceIndex);

            GUILayout.Space(8);
            GUILayout.Label("<b>Controller 入力源 (controller-expr)</b>", RichStyle());
            DrawSourceTriggerRow(controllerSource, Hidano.FacialControl.Adapters.InputSources.ExpressionTriggerInputSource.ControllerReservedId);

            GUILayout.Space(6);
            GUILayout.Label("<b>Keyboard 入力源 (keyboard-expr)</b>", RichStyle());
            DrawSourceTriggerRow(keyboardSource, Hidano.FacialControl.Adapters.InputSources.ExpressionTriggerInputSource.KeyboardReservedId);

            GUILayout.Space(8);
            if (GUILayout.Button("全ソースの全 Expression を Off"))
            {
                ReleaseAll(controllerSource);
                ReleaseAll(keyboardSource);
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Active Expression</b>", RichStyle());
            GUILayout.Label(controllerSource == null
                ? "  controller: (source not registered)"
                : $"  controller: [{string.Join(", ", controllerSource.ActiveExpressionIds)}]");
            GUILayout.Label(keyboardSource == null
                ? "  keyboard: (source not registered)"
                : $"  keyboard: [{string.Join(", ", keyboardSource.ActiveExpressionIds)}]");

            GUILayout.Space(10);
            GUILayout.Label("<b>Weight Snapshot</b>", RichStyle());
            var snapshot = _facialController.GetInputSourceWeightsSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                GUILayout.Label(
                    $"  layer={entry.LayerIdx} source={entry.SourceId} weight={entry.Weight:0.00} saturated={entry.Saturated}");
            }

            GUILayout.Space(10);
            DrawBoneSection();
            GUILayout.Space(8);
            DrawBlendShapeSection();
            GUILayout.Space(8);
            DrawSourceIdSection();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawBoneSection()
        {
            GUILayout.Label("<b>BonePose 出力 (Eye Euler)</b>", RichStyle());
            DrawBone("  LeftEye", _leftEye);
            DrawBone("  RightEye", _rightEye);
        }

        private static void DrawBone(string label, Transform t)
        {
            if (t == null)
            {
                GUILayout.Label($"{label}: (未割当)");
                return;
            }
            var euler = t.localRotation.eulerAngles;
            GUILayout.Label(
                $"{label}: ({euler.x:0.0}, {euler.y:0.0}, {euler.z:0.0})");
        }

        private void DrawBlendShapeSection()
        {
            GUILayout.Label("<b>BlendShape 出力 (mouth-open)</b>", RichStyle());

            var renderer = ResolveRenderer();
            if (renderer == null)
            {
                GUILayout.Label("  (SkinnedMeshRenderer 未解決)");
                return;
            }

            var mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                GUILayout.Label("  (sharedMesh 未割当)");
                return;
            }

            int idx = mesh.GetBlendShapeIndex(_mouthOpenBlendShapeName);
            if (idx < 0)
            {
                GUILayout.Label($"  '{_mouthOpenBlendShapeName}' BlendShape が見つかりません");
                return;
            }

            float weight = renderer.GetBlendShapeWeight(idx);
            GUILayout.Label($"  {_mouthOpenBlendShapeName}: {weight:0.00}");
        }

        private void DrawSourceIdSection()
        {
            GUILayout.Label("<b>定義済みアナログ source ID (参考)</b>", RichStyle());
            if (_displayedSourceIds == null || _displayedSourceIds.Length == 0)
            {
                GUILayout.Label("  (未設定)");
                return;
            }
            for (int i = 0; i < _displayedSourceIds.Length; i++)
            {
                GUILayout.Label($"  - {_displayedSourceIds[i]}");
            }
        }

        private SkinnedMeshRenderer ResolveRenderer()
        {
            if (_meshRenderer != null) return _meshRenderer;
            if (_facialController == null) return null;
            return _facialController.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        private void DrawWeightSlider(string label, ref float weight, int sourceIndex)
        {
            GUILayout.Label($"{label}: {weight:0.00}");
            float next = GUILayout.HorizontalSlider(weight, 0f, 1f);
            if (!Mathf.Approximately(next, weight))
            {
                weight = next;
                _facialController.SetInputSourceWeight(_layerIndex, sourceIndex, weight);
            }
        }

        private void DrawSourceTriggerRow(ExpressionTriggerInputSourceBase source, string id)
        {
            if (source == null)
            {
                GUILayout.Label($"  {id} は profile.inputSources に未宣言。");
                return;
            }

            for (int i = 0; i < s_expressionIds.Length; i++)
            {
                string exprId = s_expressionIds[i];
                bool active = Contains(source.ActiveExpressionIds, exprId);
                GUILayout.BeginHorizontal();
                GUILayout.Label(exprId, GUILayout.Width(80));
                if (GUILayout.Button("On", GUILayout.Width(60)))
                {
                    source.TriggerOn(exprId);
                }
                if (GUILayout.Button("Off", GUILayout.Width(60)))
                {
                    source.TriggerOff(exprId);
                }
                GUILayout.Label(active ? "(active)" : "");
                GUILayout.EndHorizontal();
            }
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == id) return true;
            }
            return false;
        }

        private void ReleaseAll(ExpressionTriggerInputSourceBase source)
        {
            if (source == null) return;
            for (int i = 0; i < s_expressionIds.Length; i++)
            {
                source.TriggerOff(s_expressionIds[i]);
            }
        }

        private ExpressionTriggerInputSourceBase GetSource(string id)
        {
            _facialController.TryGetExpressionTriggerSourceById(id, out var source);
            return source;
        }

        private static GUIStyle s_richStyle;
        private static GUIStyle RichStyle()
        {
            if (s_richStyle == null)
            {
                s_richStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }
            return s_richStyle;
        }
    }
}
