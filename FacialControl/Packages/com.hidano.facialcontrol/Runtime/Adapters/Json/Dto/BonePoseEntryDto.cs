using UnityEngine;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// <c>bonePoses[].entries[]</c> の 1 エントリを表す JSON 直接 DTO。
    /// JsonUtility 互換のため <see cref="System.SerializableAttribute"/> を付与する。
    /// </summary>
    [System.Serializable]
    public sealed class BonePoseEntryDto
    {
        /// <summary>対象ボーン名。多バイト文字を含む任意の文字列を受理する。</summary>
        public string boneName;

        /// <summary>X/Y/Z 軸オイラー角（度、Z-X-Y Tait-Bryan 順で解釈される）。</summary>
        public Vector3 eulerXYZ;
    }
}
