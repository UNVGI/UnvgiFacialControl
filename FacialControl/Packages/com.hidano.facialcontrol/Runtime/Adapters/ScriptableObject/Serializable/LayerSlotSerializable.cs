using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// 他レイヤーへのオーバーライドスロット (旧 JSON: expressions[].layerSlots[])。
    /// <see cref="Hidano.FacialControl.Domain.Models.LayerSlot"/> の Unity Serializable 投影。
    /// </summary>
    [Serializable]
    public sealed class LayerSlotSerializable
    {
        [Tooltip("オーバーライド対象のレイヤー名。")]
        public string layer;

        [Tooltip("当該レイヤーへ書き込む BlendShape 値の配列。")]
        public List<BlendShapeMappingSerializable> blendShapeValues = new List<BlendShapeMappingSerializable>();
    }
}
