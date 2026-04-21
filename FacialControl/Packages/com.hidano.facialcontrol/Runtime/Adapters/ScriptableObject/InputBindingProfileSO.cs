using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using InputBinding = Hidano.FacialControl.Domain.Models.InputBinding;

namespace Hidano.FacialControl.Adapters.ScriptableObject
{
    /// <summary>
    /// 入力源のカテゴリ分類。
    /// <see cref="InputBindingProfileSO"/> をコントローラ系 / キーボード系の
    /// いずれの Expression Trigger アダプタに振り分けるかを決定する。
    /// </summary>
    /// <remarks>
    /// Preview 段階の破壊的変更: 本フィールドを持たない既存 <see cref="InputBindingProfileSO"/>
    /// Asset は Unity の SerializedObject 機構により初回ロード時に既定値
    /// <see cref="InputSourceCategory.Controller"/> が暗黙的に付与される。
    /// キーボード専用のバインディングを持つ Asset は明示的に
    /// <see cref="InputSourceCategory.Keyboard"/> へ変更する必要がある
    /// （CHANGELOG / 移行ノート参照）。
    /// </remarks>
    public enum InputSourceCategory
    {
        Controller = 0,
        Keyboard = 1,
    }

    /// <summary>
    /// InputAction 名と Expression ID のバインディングを永続化する ScriptableObject。
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewInputBindingProfile",
        menuName = "FacialControl/Input Binding Profile",
        order = 1)]
    public class InputBindingProfileSO : UnityEngine.ScriptableObject
    {
        [Tooltip("バインド対象の InputActionAsset")]
        [SerializeField]
        private InputActionAsset _actionAsset;

        [Tooltip("対象 ActionMap の名前")]
        [SerializeField]
        private string _actionMapName = "Expression";

        // Preview 破壊的変更: 既存 Asset は本フィールドを持たないため、
        // 初回ロード時に既定値 Controller が暗黙的に付与される。
        // キーボード専用バインディングの Asset は Keyboard に明示変更が必要
        // （詳細は CHANGELOG / docs/ 配下の移行ノート参照）。
        [Tooltip("入力源のカテゴリ（Controller / Keyboard）")]
        [SerializeField]
        private InputSourceCategory _inputSourceCategory = InputSourceCategory.Controller;

        [Tooltip("ActionName と ExpressionId のバインディング一覧")]
        [SerializeField]
        private List<InputBindingEntry> _bindings = new List<InputBindingEntry>();

        /// <summary>
        /// 参照中の InputActionAsset。
        /// </summary>
        public InputActionAsset ActionAsset => _actionAsset;

        /// <summary>
        /// 対象 ActionMap の名前。
        /// </summary>
        public string ActionMapName => _actionMapName;

        /// <summary>
        /// 入力源カテゴリ。ControllerExpressionInputSource / KeyboardExpressionInputSource の
        /// いずれに本 SO のトリガーを振り分けるかを表す。
        /// </summary>
        public InputSourceCategory InputSourceCategory => _inputSourceCategory;

        /// <summary>
        /// シリアライズされたバインディングエントリ一覧を Domain モデルに変換して返す。
        /// _actionAsset が未設定の場合は空リストを返す。
        /// 空文字エントリは防御的にスキップする。
        /// </summary>
        public IReadOnlyList<InputBinding> GetBindings()
        {
            if (_actionAsset == null || _bindings == null)
            {
                return Array.Empty<InputBinding>();
            }

            var result = new List<InputBinding>(_bindings.Count);
            foreach (var entry in _bindings)
            {
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.actionName) || string.IsNullOrWhiteSpace(entry.expressionId))
                {
                    continue;
                }

                result.Add(new InputBinding(entry.actionName, entry.expressionId));
            }

            return result;
        }

        /// <summary>
        /// Best-effort 検証: Category=Controller に設定されているにもかかわらず、
        /// 対象 ActionMap のバインディングが全て Keyboard デバイスのみを参照している
        /// ケースを検出して警告ログを出す。Preview 破壊的変更で既定値 Controller が
        /// 暗黙付与される挙動により、ユーザーが気付かないまま想定外のアダプタ経路へ
        /// トリガーが流れる事故を防ぐためのガード。
        /// </summary>
        private void OnValidate()
        {
            if (_inputSourceCategory != InputSourceCategory.Controller)
            {
                return;
            }

            if (_actionAsset == null || string.IsNullOrEmpty(_actionMapName))
            {
                return;
            }

            var map = _actionAsset.FindActionMap(_actionMapName);
            if (map == null)
            {
                return;
            }

            int leafCount = 0;
            int keyboardCount = 0;
            foreach (var binding in map.bindings)
            {
                if (binding.isComposite)
                {
                    continue;
                }

                string path = binding.effectivePath;
                if (string.IsNullOrEmpty(path))
                {
                    path = binding.path;
                }

                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                leafCount++;
                if (path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase))
                {
                    keyboardCount++;
                }
            }

            if (leafCount > 0 && leafCount == keyboardCount)
            {
                Debug.LogWarning(
                    $"InputBindingProfileSO '{name}': InputSourceCategory が Controller に設定されていますが、" +
                    $"ActionMap '{_actionMapName}' のバインディングはすべて Keyboard デバイスを参照しています。" +
                    "意図した設定か確認してください（Keyboard 用アダプタへ振り分けるには InputSourceCategory を Keyboard に変更してください）。");
            }
        }

        /// <summary>
        /// Unity がシリアライズするためのバインディングエントリ。
        /// List&lt;T&gt; の要素としてシリアライズさせる都合上、struct ではなく class として定義する。
        /// </summary>
        [Serializable]
        public class InputBindingEntry
        {
            public string actionName;
            public string expressionId;
        }
    }
}
