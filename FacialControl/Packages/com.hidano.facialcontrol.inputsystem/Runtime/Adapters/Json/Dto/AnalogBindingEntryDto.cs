namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// <see cref="Hidano.FacialControl.Domain.Models.AnalogBindingEntry"/> の JSON 永続化 DTO（Req 6.3）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="targetKind"/> は文字列で永続化（"blendshape" / "bonepose"、大小無視）。
    /// <see cref="targetAxis"/> は文字列で永続化（"X" / "Y" / "Z"、BlendShape ターゲット時は無視）。
    /// 不正値はローダ側で <c>Debug.LogWarning</c> + skip 扱い（Req 6.5）。
    /// </para>
    /// </remarks>
    [System.Serializable]
    public sealed class AnalogBindingEntryDto
    {
        /// <summary>入力源 ID 文字列。</summary>
        public string sourceId;

        /// <summary>入力源軸番号（&gt;= 0、scalar=0 / Vector2 で X=0,Y=1）。</summary>
        public int sourceAxis;

        /// <summary>ターゲット種別文字列（"blendshape" / "bonepose"、大小無視）。</summary>
        public string targetKind;

        /// <summary>ターゲット識別子（BlendShape 名 または bone 名）。</summary>
        public string targetIdentifier;

        /// <summary>BonePose ターゲットの Euler 軸文字列（"X"/"Y"/"Z"、大小無視）。BlendShape では無視。</summary>
        public string targetAxis;
    }
}
