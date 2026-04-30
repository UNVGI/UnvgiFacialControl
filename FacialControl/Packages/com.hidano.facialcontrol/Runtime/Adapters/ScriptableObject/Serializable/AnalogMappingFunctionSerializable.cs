using System;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// アナログ入力のマッピング関数 (dead-zone → scale → offset → curve → invert → clamp)。
    /// <see cref="Hidano.FacialControl.Domain.Models.AnalogMappingFunction"/> の Unity Serializable 投影。
    /// </summary>
    [Serializable]
    public sealed class AnalogMappingFunctionSerializable
    {
        [Tooltip("デッドゾーン閾値。|入力| <= dead-zone のとき 0 に丸める。")]
        [Min(0f)]
        public float deadZone = 0f;

        [Tooltip("スケール係数 (gain)。dead-zone 通過後の値に乗算。")]
        public float scale = 1f;

        [Tooltip("オフセット (bias)。スケール後の値に加算。")]
        public float offset = 0f;

        [Tooltip("カーブ。プリセットまたは Custom。")]
        public TransitionCurveSerializable curve = new TransitionCurveSerializable();

        [Tooltip("反転フラグ。true でカーブ評価後に符号を反転。")]
        public bool invert = false;

        [Tooltip("出力下限 (最終 clamp)。")]
        public float min = 0f;

        [Tooltip("出力上限 (最終 clamp)。min <= max を満たすこと。")]
        public float max = 1f;
    }
}
