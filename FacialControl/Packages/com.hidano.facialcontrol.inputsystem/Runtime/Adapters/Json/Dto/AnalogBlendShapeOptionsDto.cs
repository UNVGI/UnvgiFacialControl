namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// 予約 ID <c>analog-blendshape</c> の入力源固有 options（preview.1 では空、Req 3.8）。
    /// </summary>
    /// <remarks>
    /// 将来フィールドを追加しても JSON 後方互換のために派生型を維持する。
    /// </remarks>
    [System.Serializable]
    public sealed class AnalogBlendShapeOptionsDto : InputSourceOptionsDto
    {
    }
}
