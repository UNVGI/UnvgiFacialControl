namespace Hidano.FacialControl.Adapters.ScriptableObject.Serializable
{
    /// <summary>
    /// 表情の駆動方式。Inspector で表情行のレイアウトを切替える判別に用いる。
    /// </summary>
    public enum ExpressionKind
    {
        /// <summary>AnimationClip に基づくスナップショット表情（既定）。</summary>
        AnimationClip = 0,

        /// <summary>Vector2 入力で連続的に駆動するアナログ表情（目線等）。</summary>
        EyeLook = 1,
    }
}
