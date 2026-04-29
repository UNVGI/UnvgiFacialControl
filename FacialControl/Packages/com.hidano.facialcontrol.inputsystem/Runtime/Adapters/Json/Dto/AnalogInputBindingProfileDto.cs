using System.Collections.Generic;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    /// <summary>
    /// <see cref="Hidano.FacialControl.Domain.Models.AnalogInputBindingProfile"/> の JSON 永続化 DTO（Req 6.3, 6.4, 6.7）。
    /// </summary>
    /// <remarks>
    /// preview 中は <see cref="version"/> を文字列保持のみで分岐に使用しない（Req 9.6、preview.2 以降に
    /// マイグレーションパスを設ける）。
    /// </remarks>
    [System.Serializable]
    public sealed class AnalogInputBindingProfileDto
    {
        /// <summary>プロファイルバージョン文字列（preview 中は分岐なし）。</summary>
        public string version;

        /// <summary>バインディングエントリ一覧。null / 空双方が空プロファイル扱い。</summary>
        public List<AnalogBindingEntryDto> bindings;
    }
}
