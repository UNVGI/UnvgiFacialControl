using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;
using DomainInputBinding = Hidano.FacialControl.Domain.Models.InputBinding;

namespace Hidano.FacialControl.Adapters.Input
{
    /// <summary>
    /// <see cref="InputBindingProfileSO"/> に定義されたバインディング情報を読み取り、
    /// <see cref="FacialController"/> と <see cref="InputSystemAdapter"/> を結線する MonoBehaviour。
    /// OnEnable 時に InputActionAsset をインスタンス化して ActionMap を有効化し、
    /// バインディング一覧を登録する。OnDisable 時には登録を解除し、インスタンス化した
    /// アセットを破棄する。
    /// </summary>
    [AddComponentMenu("FacialControl/Facial Input Binder")]
    public class FacialInputBinder : MonoBehaviour
    {
        [Tooltip("バインディング対象の FacialController")]
        [SerializeField]
        private FacialController _facialController;

        [Tooltip("バインディング定義を持つ InputBindingProfileSO")]
        [SerializeField]
        private InputBindingProfileSO _bindingProfile;

        private InputSystemAdapter _adapter;
        private InputActionAsset _runtimeActionAsset;
        private InputActionMap _runtimeActionMap;

        private void OnEnable()
        {
            if (_bindingProfile == null)
            {
                Debug.LogWarning(
                    "FacialInputBinder: _bindingProfile が未設定のためバインディング登録を行いません。");
                return;
            }

            var sourceAsset = _bindingProfile.ActionAsset;
            if (sourceAsset == null)
            {
                Debug.LogWarning(
                    "FacialInputBinder: _bindingProfile.ActionAsset が未設定のためバインディング登録を行いません。");
                return;
            }

            _runtimeActionAsset = Instantiate(sourceAsset);
            _runtimeActionMap = _runtimeActionAsset.FindActionMap(_bindingProfile.ActionMapName);
            if (_runtimeActionMap == null)
            {
                Debug.LogWarning(
                    $"FacialInputBinder: ActionMap '{_bindingProfile.ActionMapName}' が InputActionAsset に存在しません。");
                return;
            }

            _runtimeActionMap.Enable();
            _adapter = new InputSystemAdapter(_facialController);

            IReadOnlyList<DomainInputBinding> bindings = _bindingProfile.GetBindings();
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];

                Expression? expression = ResolveExpression(binding.ExpressionId);
                if (!expression.HasValue)
                {
                    Debug.LogWarning(
                        $"FacialInputBinder: ExpressionId '{binding.ExpressionId}' が FacialController のプロファイルに存在しないためバインディングをスキップします。");
                    continue;
                }

                var action = _runtimeActionMap.FindAction(binding.ActionName);
                if (action == null)
                {
                    Debug.LogWarning(
                        $"FacialInputBinder: Action '{binding.ActionName}' が ActionMap '{_bindingProfile.ActionMapName}' に見つかりません。");
                    continue;
                }

                _adapter.BindExpression(action, expression.Value);
            }
        }

        private void OnDisable()
        {
            if (_adapter != null)
            {
                _adapter.UnbindAll();
                _adapter.Dispose();
                _adapter = null;
            }

            if (_runtimeActionMap != null)
            {
                _runtimeActionMap.Disable();
                _runtimeActionMap = null;
            }

            if (_runtimeActionAsset != null)
            {
                Destroy(_runtimeActionAsset);
                _runtimeActionAsset = null;
            }
        }

        private Expression? ResolveExpression(string expressionId)
        {
            if (_facialController == null)
            {
                return null;
            }

            var profile = _facialController.CurrentProfile;
            if (!profile.HasValue)
            {
                return null;
            }

            return profile.Value.FindExpressionById(expressionId);
        }
    }
}
