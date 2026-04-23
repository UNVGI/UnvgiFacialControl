using System.Collections.Generic;
using UnityEngine;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Input
{
    /// <summary>
    /// FacialController と同じ GameObject に配置することで、コントローラ・キーボードからの
    /// Expression トリガー入力を表情制御に接続する拡張。
    /// FacialController の初期化時に <see cref="IFacialControllerExtension.ConfigureFactory"/>
    /// が呼ばれ、Controller / Keyboard の予約 id を <see cref="InputSourceFactory"/> に登録する。
    /// </summary>
    [RequireComponent(typeof(FacialController))]
    [AddComponentMenu("FacialControl/Input Facial Extension")]
    public class InputFacialControllerExtension : MonoBehaviour, IFacialControllerExtension
    {
        [Tooltip("Expression トリガー型アダプタの既定排他モード（D-12）")]
        [SerializeField]
        private ExclusionMode _defaultExclusionMode = ExclusionMode.LastWins;

        public ExclusionMode DefaultExclusionMode
        {
            get => _defaultExclusionMode;
            set => _defaultExclusionMode = value;
        }

        public void ConfigureFactory(
            InputSourceFactory factory,
            FacialProfile profile,
            IReadOnlyList<string> blendShapeNames)
        {
            InputRegistration.Register(factory, blendShapeNames, _defaultExclusionMode);
        }
    }
}
