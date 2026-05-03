namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// 表情の駆動方式。Inspector で表情行のレイアウトを切替える判別に用いる。
    /// </summary>
    public enum ExpressionKind
    {
        /// <summary>デジタル操作: ボタン入力で AnimationClip をトリガー。遷移時間で補間する。</summary>
        Digital = 0,

        /// <summary>アナログ操作: 連続値入力でボーン回転や BlendShape を駆動する。</summary>
        Analog = 1,
    }
}
