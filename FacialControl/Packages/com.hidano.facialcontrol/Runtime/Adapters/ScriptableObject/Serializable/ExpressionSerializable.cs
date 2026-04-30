using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// Expression 定義 (旧 JSON: expressions[])。
    /// <see cref="Hidano.FacialControl.Domain.Models.Expression"/> の Unity Serializable 投影。
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

        [Tooltip("遷移時間 (秒)。0〜1 範囲外は自動クランプ。")]
        [Range(0f, 1f)]
        public float transitionDuration = 0.25f;

        [Tooltip("遷移カーブ。プリセットまたは Custom。")]
        public TransitionCurveSerializable transitionCurve = new TransitionCurveSerializable();

        [Tooltip("BlendShape 値の配列。")]
        public List<BlendShapeMappingSerializable> blendShapeValues = new List<BlendShapeMappingSerializable>();

        [Tooltip("他レイヤーへのオーバーライド (オプション)。例: emotion 遷移時に eye レイヤーへも書き込みたい場合。")]
        public List<LayerSlotSerializable> layerSlots = new List<LayerSlotSerializable>();
    }
}
