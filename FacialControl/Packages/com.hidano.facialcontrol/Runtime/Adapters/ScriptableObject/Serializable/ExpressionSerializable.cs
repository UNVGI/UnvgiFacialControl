using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// Expression 定義 (旧 JSON: expressions[])。
    /// <see cref="Hidano.FacialControl.Domain.Models.Expression"/> の Unity Serializable 投影。
    /// <para>
    /// Phase 3.2 (inspector-and-data-model-redesign) で旧 <c>layerSlots</c> 配列を撤去し、
    /// レイヤー名配列で永続化される <see cref="layerOverrideMask"/>（Domain
    /// <see cref="Hidano.FacialControl.Domain.Models.LayerOverrideMask"/> の永続化形式）を導入した。
    /// 残る <c>transitionDuration / transitionCurve / blendShapeValues</c> は
    /// Phase 3.6 で snapshot 経路へ移行する bridge field である。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class ExpressionSerializable
    {
        [Tooltip("Expression を識別する一意 ID。GUID 推奨。InputBinding の expressionId と一致させる。")]
        public string id;

        [Tooltip("Inspector / デバッグ表示用の表情名。")]
        public string name;

        [Tooltip("所属レイヤー名 (Layers セクションの name と一致させる)。")]
        public string layer;

        [Tooltip("[Bridge] 遷移時間 (秒)。0〜1 範囲外は自動クランプ。Phase 3.6 で snapshot 経路へ移行予定。")]
        [Range(0f, 1f)]
        public float transitionDuration = 0.25f;

        [Tooltip("[Bridge] 遷移カーブ。Phase 3.6 で snapshot 経路へ移行予定。")]
        public TransitionCurveSerializable transitionCurve = new TransitionCurveSerializable();

        [Tooltip("[Bridge] BlendShape 値の配列。Phase 3.6 で snapshot 経路へ移行予定。")]
        public List<BlendShapeMappingSerializable> blendShapeValues = new List<BlendShapeMappingSerializable>();

        [Tooltip("他レイヤーへのオーバーライド対象を表すレイヤー名配列。Domain の LayerOverrideMask に対応する永続化形式。")]
        public List<string> layerOverrideMask = new List<string>();
    }
}
