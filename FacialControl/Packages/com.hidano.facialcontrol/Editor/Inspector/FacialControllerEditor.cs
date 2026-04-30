using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Editor.Common;

namespace Hidano.FacialControl.Editor.Inspector
{
    /// <summary>
    /// FacialController のカスタム Inspector。
    /// UI Toolkit で実装し、統合 SO 参照、SkinnedMeshRenderer リスト、
    /// プロファイル概要を表示する。OSC 設定はサブパッケージ
    /// <c>com.hidano.facialcontrol.osc</c> の専用 MonoBehaviour 側に移管されている。
    /// </summary>
    [CustomEditor(typeof(FacialController))]
    public class FacialControllerEditor : UnityEditor.Editor
    {
        private const string ProfileSectionLabel = "キャラクター SO";
        private const string RenderersSectionLabel = "SkinnedMeshRenderer";
        private const string ProfileInfoSectionLabel = "プロファイル情報";

        private Label _layerCountLabel;
        private Label _expressionCountLabel;
        private Label _schemaVersionLabel;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            // ========================================
            // 統合 SO セクション
            // ========================================
            var profileFoldout = new Foldout { text = ProfileSectionLabel, value = true };
            var profileField = new PropertyField(serializedObject.FindProperty("_characterSO"));
            profileField.RegisterValueChangeCallback(_ => UpdateProfileInfo());
            profileFoldout.Add(profileField);
            root.Add(profileFoldout);

            // ========================================
            // SkinnedMeshRenderer セクション
            // ========================================
            var renderersFoldout = new Foldout { text = RenderersSectionLabel, value = true };
            var renderersField = new PropertyField(serializedObject.FindProperty("_skinnedMeshRenderers"));
            renderersField.tooltip = "空の場合は子オブジェクトから自動検索されます";
            renderersFoldout.Add(renderersField);
            root.Add(renderersFoldout);

            // ========================================
            // プロファイル情報セクション（読み取り専用）
            // ========================================
            var infoFoldout = new Foldout { text = ProfileInfoSectionLabel, value = true };

            _schemaVersionLabel = new Label("スキーマバージョン: ---");
            _schemaVersionLabel.AddToClassList(FacialControlStyles.InfoLabel);
            infoFoldout.Add(_schemaVersionLabel);

            _layerCountLabel = new Label("レイヤー数: ---");
            _layerCountLabel.AddToClassList(FacialControlStyles.InfoLabel);
            infoFoldout.Add(_layerCountLabel);

            _expressionCountLabel = new Label("Expression 数: ---");
            _expressionCountLabel.AddToClassList(FacialControlStyles.InfoLabel);
            infoFoldout.Add(_expressionCountLabel);

            root.Add(infoFoldout);

            // 初回更新
            root.schedule.Execute(UpdateProfileInfo);

            return root;
        }

        /// <summary>
        /// CharacterSO から概要情報を読み取って表示を更新する。
        /// </summary>
        private void UpdateProfileInfo()
        {
            var controller = target as FacialController;
            if (controller == null)
                return;

            var so = controller.CharacterSO;
            if (so != null)
            {
                string version = !string.IsNullOrEmpty(so.SchemaVersion)
                    ? so.SchemaVersion
                    : "---";
                int layers = so.Layers != null ? so.Layers.Count : 0;
                int expressions = so.Expressions != null ? so.Expressions.Count : 0;

                if (_schemaVersionLabel != null)
                    _schemaVersionLabel.text = $"スキーマバージョン: {version}";
                if (_layerCountLabel != null)
                    _layerCountLabel.text = $"レイヤー数: {layers}";
                if (_expressionCountLabel != null)
                    _expressionCountLabel.text = $"Expression 数: {expressions}";
            }
            else
            {
                if (_schemaVersionLabel != null)
                    _schemaVersionLabel.text = "スキーマバージョン: ---";
                if (_layerCountLabel != null)
                    _layerCountLabel.text = "レイヤー数: ---";
                if (_expressionCountLabel != null)
                    _expressionCountLabel.text = "Expression 数: ---";
            }
        }
    }
}
