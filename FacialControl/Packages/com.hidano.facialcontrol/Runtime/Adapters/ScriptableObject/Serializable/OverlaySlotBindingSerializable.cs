using System;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// <see cref="Hidano.FacialControl.Domain.Models.OverlaySlotBinding"/> の Unity Serializable 投影。
    /// SO Inspector で編集される。<see cref="expressionId"/> が空文字なら明示 suppress。
    /// </summary>
    [Serializable]
    public sealed class OverlaySlotBindingSerializable
    {
        [Tooltip("slot 識別子（例: blink）。")]
        public string slot;

        [Tooltip("発火させる overlay Expression の ID。空文字で明示 suppress（active 表情で当該 slot を発火させない）。")]
        public string expressionId;
    }
}
