using System;
using Hidano.FacialControl.Adapters.Json.Dto;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// <see cref="Hidano.FacialControl.Domain.Models.OverlaySlotBinding"/> の Unity Serializable 投影。
    /// SO Inspector で編集される。suppress / cachedSnapshot で
    /// default fallback / 明示 suppress / 個別 snapshot override を表現する。
    /// </summary>
    [Serializable]
    public sealed class OverlaySlotBindingSerializable
    {
        [Tooltip("slot 識別子（例: blink）。")]
        public string slot;

        [Tooltip("true の場合、この slot の overlay を明示的に抑制します。AnimationClip と cachedSnapshot は無視されます。")]
        public bool suppress;

        [Tooltip("個別 override 用の AnimationClip。Exporter が cachedSnapshot にベイクします。")]
        public AnimationClip animationClip;

        [Tooltip("Exporter がベイクした overlay snapshot。空の場合は default fallback として扱います。")]
        public ExpressionSnapshotDto cachedSnapshot;
    }
}
