using System.Collections.Generic;

namespace Hidano.FacialControl.Adapters.Json.Dto
{
    [System.Obsolete("schema v1.0 DTO. Use BoneSnapshotDto for v2.0. Physical deletion in Phase 3.6.")]
    [System.Serializable]
    public sealed class BonePoseDto
    {
        public string id;
        public System.Collections.Generic.List<BonePoseEntryDto> entries;
    }
}