using UnityEngine;
using Hidano.FacialControl.Adapters.Playable;

namespace Hidano.FacialControl.Samples
{
    /// <summary>
    /// アナログ入力 (右スティック → LeftEye/RightEye Euler、ARKit jawOpen / OSC float
    /// → mouth-open BlendShape) の入出力を OnGUI で目視確認するための PlayMode 専用 HUD。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 詳しい使い方は本サンプル同梱の README.md を参照。
    /// 本 HUD は <see cref="FacialController"/> の既存公開 API のみを利用し、
    /// 同 GameObject 上の <c>FacialCharacterInputExtension</c> によって駆動される
    /// BonePose / BlendShape の値を読み取るだけのオブザーバとして振る舞う（書込はしない）。
    /// </para>
    /// <para>
    /// 表情データの読込は新統合 SO (<c>FacialCharacterSO</c>) 経由で
    /// <c>FacialController.OnEnable</c> が StreamingAssets/FacialControl/{SO 名}/profile.json を
    /// 自動探索して行う想定 (3-B モデル)。アナログバインディングは SO の Inspector で編集する。
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("FacialControl/Samples/Analog Binding Demo HUD")]
    public class AnalogBindingDemoHUD : MonoBehaviour
    {
        [Tooltip("対象の FacialController。BlendShape の現在値読取に使用する。")]
        [SerializeField]
        private FacialController _facialController;

        [Tooltip("HUD で観測する LeftEye Transform (BonePose ターゲット)。")]
        [SerializeField]
        private Transform _leftEye;

        [Tooltip("HUD で観測する RightEye Transform (BonePose ターゲット)。")]
        [SerializeField]
        private Transform _rightEye;

        [Tooltip("観測対象の SkinnedMeshRenderer。設定がない場合は FacialController から取得する。")]
        [SerializeField]
        private SkinnedMeshRenderer _meshRenderer;

        [Tooltip("HUD に表示する mouth-open BlendShape 名 (デフォルト: jawOpen)。")]
        [SerializeField]
        private string _mouthOpenBlendShapeName = "jawOpen";

        [Tooltip("HUD で表示するアナログソース ID (informational)。")]
        [SerializeField]
        private string[] _displayedSourceIds = { "right_stick", "arkit_jaw_open" };

        private Vector2 _scroll;

        private void Awake()
        {
            // Game View が非 focus でも HUD が描画され続けるように。
            UnityEngine.Application.runInBackground = true;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 480, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("<b>Analog Binding Demo</b>", RichStyle());
            GUILayout.Label("右スティック → LeftEye/RightEye Euler、jawOpen → mouth-open BS");

            DrawControllerSection();
            GUILayout.Space(8);
            DrawBoneSection();
            GUILayout.Space(8);
            DrawBlendShapeSection();
            GUILayout.Space(8);
            DrawSourceIdSection();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawControllerSection()
        {
            GUILayout.Label("<b>FacialController</b>", RichStyle());
            if (_facialController == null)
            {
                GUILayout.Label("  (未割当)");
                return;
            }
            GUILayout.Label($"  IsInitialized: {_facialController.IsInitialized}");
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
