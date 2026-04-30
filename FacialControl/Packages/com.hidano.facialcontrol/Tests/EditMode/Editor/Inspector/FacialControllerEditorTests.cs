using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Editor.Inspector;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Inspector
{
    /// <summary>
    /// Phase 5.4 (inspector-and-data-model-redesign):
    /// <see cref="FacialControllerEditor"/> の概要表示が新モデルに合わせて
    /// BonePose 文字列を含まず、Schema / Layer 数 / Expression 数 / Snapshot 数を
    /// 表示することを検証する。
    /// </summary>
    [TestFixture]
    public class FacialControllerEditorTests
    {
        private sealed class TestCharacterSO : FacialCharacterProfileSO { }

        private GameObject _go;
        private FacialController _controller;
        private TestCharacterSO _so;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("FacialControllerEditorTestsHost");
            _go.AddComponent<Animator>();
            _controller = _go.AddComponent<FacialController>();
            _so = ScriptableObject.CreateInstance<TestCharacterSO>();
            _so.name = "FacialControllerEditorTestsCharacter";
            _so.SchemaVersion = "2.0";
            _controller.CharacterSO = _so;
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }
            if (_so != null)
            {
                Object.DestroyImmediate(_so);
                _so = null;
            }
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
                _go = null;
            }
        }

        private VisualElement CreateInspectorRoot()
        {
            _editor = UnityEditor.Editor.CreateEditor(_controller, typeof(FacialControllerEditor));
            Assert.IsNotNull(_editor, "FacialControllerEditor を生成できませんでした。");
            var root = _editor.CreateInspectorGUI();
            Assert.IsNotNull(root, "CreateInspectorGUI が null を返しました。");
            // ラベル初期化スケジュールを即時実行する
            root.schedule.Execute(() => { }).ExecuteLater(0);
            return root;
        }

        [Test]
        public void OverviewLabels_DoNotContainBonePoseString()
        {
            // BonePose を持たない素のデータでも、概要ラベルテキストに "BonePose" が含まれないこと
            _so.Expressions.Add(new ExpressionSerializable { id = "expr-a", name = "Smile", layer = "emotion" });

            var root = CreateInspectorRoot();

            var schemaLabel = root.Q<Label>(name: FacialControllerEditor.SchemaVersionLabelName);
            var layerCountLabel = root.Q<Label>(name: FacialControllerEditor.LayerCountLabelName);
            var expressionCountLabel = root.Q<Label>(name: FacialControllerEditor.ExpressionCountLabelName);
            var snapshotCountLabel = root.Q<Label>(name: FacialControllerEditor.SnapshotCountLabelName);

            Assert.IsNotNull(schemaLabel, "スキーマバージョン Label が見つかりません。");
            Assert.IsNotNull(layerCountLabel, "レイヤー数 Label が見つかりません。");
            Assert.IsNotNull(expressionCountLabel, "Expression 数 Label が見つかりません。");
            Assert.IsNotNull(snapshotCountLabel,
                "Phase 5.4 で導入した Snapshot 数 Label が見つかりません。BonePose 表示の置き換え先として必須です。");

            string[] texts = new[]
            {
                schemaLabel.text ?? string.Empty,
                layerCountLabel.text ?? string.Empty,
                expressionCountLabel.text ?? string.Empty,
                snapshotCountLabel.text ?? string.Empty,
            };
            foreach (var t in texts)
            {
                StringAssert.DoesNotContain("BonePose", t,
                    $"概要ラベルに BonePose 文字列が含まれてはいけません (Phase 5.4): \"{t}\"");
                StringAssert.DoesNotContain("ボーン", t,
                    $"概要ラベルにボーン関連文字列が含まれてはいけません (Phase 5.4): \"{t}\"");
            }
        }

        [Test]
        public void OverviewLabels_HaveSnapshotCountLabel_UsingFormatConst()
        {
            var root = CreateInspectorRoot();

            var snapshotCountLabel = root.Q<Label>(name: FacialControllerEditor.SnapshotCountLabelName);
            Assert.IsNotNull(snapshotCountLabel, "Snapshot 数 Label が必要です。");

            // フォーマット定数の prefix が表示テキストに含まれること（"Snapshot 数: "）
            int braceIndex = FacialControllerEditor.SnapshotCountLabelFormat.IndexOf('{');
            string prefix = braceIndex > 0
                ? FacialControllerEditor.SnapshotCountLabelFormat.Substring(0, braceIndex)
                : FacialControllerEditor.SnapshotCountLabelFormat;
            StringAssert.StartsWith(prefix, snapshotCountLabel.text ?? string.Empty,
                "Snapshot 数 Label のテキストはフォーマット定数 prefix から始まるべきです。");
        }

        [Test]
        public void SnapshotCount_ReflectsCachedSnapshotPresence()
        {
            // cachedSnapshot を 1 件持つ Expression と持たない Expression を混在させる
            _so.Expressions.Add(new ExpressionSerializable
            {
                id = "expr-with-snap",
                name = "Smile",
                layer = "emotion",
                cachedSnapshot = new ExpressionSnapshotDto(),
            });
            _so.Expressions.Add(new ExpressionSerializable
            {
                id = "expr-no-snap",
                name = "Anger",
                layer = "emotion",
                cachedSnapshot = null,
            });

            var root = CreateInspectorRoot();

            var snapshotCountLabel = root.Q<Label>(name: FacialControllerEditor.SnapshotCountLabelName);
            Assert.IsNotNull(snapshotCountLabel);

            string expected = string.Format(FacialControllerEditor.SnapshotCountLabelFormat, 1);
            Assert.AreEqual(expected, snapshotCountLabel.text,
                "cachedSnapshot を持つ Expression の数が Snapshot 数として表示されるべきです。");
        }
    }
}
