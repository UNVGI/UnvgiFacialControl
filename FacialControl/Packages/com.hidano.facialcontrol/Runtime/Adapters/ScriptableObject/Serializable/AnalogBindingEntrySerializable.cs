using System;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// アナログ入力バインディング 1 件 (analog binding profile: bindings[])。
    /// 入力源軸 → BlendShape または BonePose 軸への 1 対 1 マッピングを定義する。
    /// <see cref="Hidano.FacialControl.Domain.Models.AnalogBindingEntry"/> の Unity Serializable 投影。
    /// </summary>
    /// <remarks>
    /// Phase 3.5 で <c>mapping</c> field を撤去した（Req 6.2, 6.3）。dead-zone / scale / offset / curve /
    /// invert / clamp の値変換は Adapters 側 InputProcessor 経路で扱う（Decision 4 / Req 13.3）。
    /// </remarks>
    [Serializable]
    public sealed class AnalogBindingEntrySerializable
    {
        [Tooltip("入力源 ID。InputSourceId 規約 ([a-zA-Z0-9_.-]{1,64})。例: x-right-stick")]
        public string sourceId;

        [Tooltip("入力源側の軸番号 (0 以上)。scalar=0、Vector2 では X=0 / Y=1。")]
        [Min(0)]
        public int sourceAxis = 0;

        [Tooltip("ターゲット種別。BlendShape 名またはボーン名のいずれを指す宣言か。")]
        public AnalogBindingTargetKind targetKind = AnalogBindingTargetKind.BlendShape;

        [Tooltip("ターゲット識別子。BlendShape ターゲットなら BlendShape 名、BonePose ターゲットならボーン名。")]
        public string targetIdentifier;

        [Tooltip("BonePose ターゲット時の Euler 軸 (X/Y/Z)。BlendShape ターゲット時は無視される。")]
        public AnalogTargetAxis targetAxis = AnalogTargetAxis.X;
    }
}
