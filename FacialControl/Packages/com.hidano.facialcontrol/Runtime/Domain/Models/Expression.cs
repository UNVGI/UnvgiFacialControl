using System;

namespace Hidano.FacialControl.Domain.Models
{
    /// <summary>
    /// 表情定義。AnimationClip 由来の <see cref="ExpressionSnapshot"/> を <see cref="SnapshotId"/> で参照する不変オブジェクト。
    /// <para>
    /// Phase 3.1 (inspector-and-data-model-redesign) で派生 5 値の independent field
    /// (<c>TransitionDuration / TransitionCurve / BlendShapeValues / LayerSlots</c>) を撤去し、
    /// <see cref="OverrideMask"/> + <see cref="SnapshotId"/> 経由の参照型へ移行する Domain 値型。
    /// 旧 field は Phase 3.2〜3.6 の連鎖破壊リファクタが完了するまでの bridge 期間中、
    /// 互換目的で保持されるが、Domain の primary identity は (Id, Name, Layer, OverrideMask, SnapshotId) の 5 値である。
    /// </para>
    /// </summary>
    public readonly struct Expression
    {
        /// <summary>
        /// 一意識別子（GUID）
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 表情名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 所属レイヤー名
        /// </summary>
        public string Layer { get; }

        /// <summary>
        /// 他レイヤーへのオーバーライド対象を示すビットフラグ（Req 3.1, 3.4）。
        /// </summary>
        public LayerOverrideMask OverrideMask { get; }

        /// <summary>
        /// AnimationClip 由来の <see cref="ExpressionSnapshot"/> を識別する snapshot id（Req 1.5）。
        /// 通常は <see cref="Id"/> と同値で運用するが、別 snapshot を参照することも許される。
        /// </summary>
        public string SnapshotId { get; }

        // ===== Bridge fields (Phase 3.2〜3.6 で物理削除予定) =====

        /// <summary>
        /// [Bridge] 遷移時間（0〜1 秒、範囲外は自動クランプ）。
        /// Phase 3.6 で <see cref="ExpressionSnapshot.TransitionDuration"/> 経路へ移行後に撤去予定。
        /// </summary>
        public float TransitionDuration { get; }

        /// <summary>
        /// [Bridge] 遷移カーブ設定。Phase 3.6 で <see cref="TransitionCurvePreset"/> 経路へ移行後に撤去予定。
        /// </summary>
        public TransitionCurve TransitionCurve { get; }

        /// <summary>
        /// [Bridge] BlendShape 値の配列。Phase 3.6 で <see cref="ExpressionSnapshot.BlendShapes"/> 経路へ移行後に撤去予定。
        /// </summary>
        public ReadOnlyMemory<BlendShapeMapping> BlendShapeValues { get; }

        /// <summary>
        /// [Bridge] 他レイヤーへのオーバーライドスロット配列。Phase 3.2 で <see cref="OverrideMask"/> 経路へ移行後に撤去予定。
        /// </summary>
        public ReadOnlyMemory<LayerSlot> LayerSlots { get; }

        /// <summary>
        /// SnapshotId 参照型コンストラクタ（Phase 3.1 新 API）。
        /// Bridge field（TransitionDuration / TransitionCurve / BlendShapeValues / LayerSlots）は default 値で初期化される。
        /// </summary>
        /// <param name="id">一意識別子（空文字不可）</param>
        /// <param name="name">表情名（空文字不可）</param>
        /// <param name="layer">所属レイヤー名（空文字不可）</param>
        /// <param name="overrideMask">オーバーライド対象レイヤーのビットフラグ</param>
        /// <param name="snapshotId">AnimationClip 由来 snapshot の識別子（空文字不可）</param>
        public Expression(
            string id,
            string name,
            string layer,
            LayerOverrideMask overrideMask,
            string snapshotId)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID を空にすることはできません。", nameof(id));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("名前を空にすることはできません。", nameof(name));
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));
            if (string.IsNullOrWhiteSpace(layer))
                throw new ArgumentException("レイヤー名を空にすることはできません。", nameof(layer));
            if (snapshotId == null)
                throw new ArgumentNullException(nameof(snapshotId));
            if (string.IsNullOrWhiteSpace(snapshotId))
                throw new ArgumentException("SnapshotId を空にすることはできません。", nameof(snapshotId));

            Id = id;
            Name = name;
            Layer = layer;
            OverrideMask = overrideMask;
            SnapshotId = snapshotId;

            TransitionDuration = 0.25f;
            TransitionCurve = default;
            BlendShapeValues = Array.Empty<BlendShapeMapping>();
            LayerSlots = Array.Empty<LayerSlot>();
        }

        /// <summary>
        /// [Bridge ctor] Phase 3.2〜3.6 で撤去予定の旧 API。
        /// 新規コードは SnapshotId 受けの ctor を使用すること。
        /// 配列パラメータは防御的コピーされる。
        /// </summary>
        /// <param name="id">一意識別子（空文字不可）</param>
        /// <param name="name">表情名（空文字不可）</param>
        /// <param name="layer">所属レイヤー名（空文字不可）</param>
        /// <param name="transitionDuration">遷移時間（0〜1 秒、範囲外は自動クランプ）</param>
        /// <param name="transitionCurve">遷移カーブ設定</param>
        /// <param name="blendShapeValues">BlendShape 値の配列。null の場合は空配列</param>
        /// <param name="layerSlots">他レイヤーへのオーバーライドスロット配列。null の場合は空配列</param>
        public Expression(
            string id,
            string name,
            string layer,
            float transitionDuration = 0.25f,
            TransitionCurve transitionCurve = default,
            BlendShapeMapping[] blendShapeValues = null,
            LayerSlot[] layerSlots = null)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID を空にすることはできません。", nameof(id));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("名前を空にすることはできません。", nameof(name));
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));
            if (string.IsNullOrWhiteSpace(layer))
                throw new ArgumentException("レイヤー名を空にすることはできません。", nameof(layer));

            Id = id;
            Name = name;
            Layer = layer;
            OverrideMask = LayerOverrideMask.None;
            SnapshotId = id;
            TransitionDuration = Math.Clamp(transitionDuration, 0f, 1f);
            TransitionCurve = transitionCurve;

            if (blendShapeValues != null)
            {
                var bsCopy = new BlendShapeMapping[blendShapeValues.Length];
                Array.Copy(blendShapeValues, bsCopy, blendShapeValues.Length);
                BlendShapeValues = bsCopy;
            }
            else
            {
                BlendShapeValues = Array.Empty<BlendShapeMapping>();
            }

            if (layerSlots != null)
            {
                var slotCopy = new LayerSlot[layerSlots.Length];
                Array.Copy(layerSlots, slotCopy, layerSlots.Length);
                LayerSlots = slotCopy;
            }
            else
            {
                LayerSlots = Array.Empty<LayerSlot>();
            }
        }

        /// <summary>
        /// 表情の人間可読表現を返す（Phase 3.1 Refactor: <c>{Id}:{Name}@{Layer}</c> 形式に統一）。
        /// </summary>
        public override string ToString()
        {
            return $"{Id}:{Name}@{Layer}";
        }
    }
}
