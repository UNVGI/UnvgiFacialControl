namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// <c>expressions[].snapshot.overlays[]</c> および <c>defaultOverlays[]</c> の 1 エントリ DTO。
    /// <para>
    /// <see cref="expressionId"/> が null / 空文字 / 全空白の場合は当該 slot を明示 suppress する。
    /// JsonUtility 互換のため <see cref="System.SerializableAttribute"/> を付与する。
    /// </para>
    /// </summary>
    [System.Serializable]
    public sealed class OverlaySlotBindingDto
    {
        /// <summary>slot 識別子（"blink" 等）。空文字不可。</summary>
        public string slot;

        /// <summary>
        /// 発火させる overlay Expression の ID。null / 空文字で suppress。
        /// </summary>
        public string expressionId;
    }
}
