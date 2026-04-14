using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using InputBinding = Hidano.FacialControl.Domain.Models.InputBinding;

namespace Hidano.FacialControl.Adapters.ScriptableObject
{
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
