using System;
using System.Collections;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.InputSources
{
    /// <summary>
    /// Overlay slot ベースのコンテキスト連動 IInputSource (Adapters 層)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 解決ロジック (毎フレーム、per-slot):
    /// <list type="number">
    ///   <item>指定レイヤー (典型的には "emotion") の top active Expression を <see cref="IActiveExpressionProvider"/> から取得。</item>
    ///   <item>active 表情が当該 slot を宣言していれば、その <see cref="OverlaySlotBinding"/> を採用 (suppress 含む)。</item>
    ///   <item>未宣言なら <see cref="FacialProfile.DefaultOverlays"/> から fallback。</item>
    ///   <item>suppress / 未解決時は <see cref="TryWriteValues"/> が false を返し、ContributeMask は空。</item>
    /// </list>
    /// </para>
    /// <para>
    /// ctor 時点で profile 内の全 Expression について「触る BlendShape index 列」「value 列」「mask」を事前計算し、
    /// 毎フレームの解決はキャッシュ参照と output コピーのみで完結する (GC ゼロ寄与)。
    /// </para>
    /// </remarks>
    public sealed class OverlayInputSource : ValueProviderInputSourceBase
    {
        /// <summary>本入力源の予約 ID 接頭辞。実 ID は <c>overlay:&lt;slot&gt;</c> 形式。</summary>
        public const string ReservedIdPrefix = "overlay";

        private readonly string _slot;
        private readonly string _emotionLayerName;
        private readonly FacialProfile _profile;
        private readonly IActiveExpressionProvider _activeProvider;
        private readonly Dictionary<string, ResolvedExpression> _resolvedById;
        private readonly BitArray _activeMask;
        private readonly BitArray _emptyMask;
        private ResolvedExpression _activeResolved;
        private bool _hasActiveResolved;

        /// <summary>
        /// <see cref="OverlayInputSource"/> を構築する。
        /// </summary>
        /// <param name="id">入力源 ID。<c>overlay:&lt;slot&gt;</c> 形式が推奨だが任意の InputSourceId を受け取る。</param>
        /// <param name="slot">slot 識別子（例: "blink"）。</param>
        /// <param name="blendShapeCount">書込む BlendShape 個数。</param>
        /// <param name="blendShapeNames">BlendShape 名配列。</param>
        /// <param name="profile">解決対象の <see cref="FacialProfile"/>。</param>
        /// <param name="activeProvider">指定レイヤーの top active Expression を返す provider。null の場合は default fallback のみ動作。</param>
        /// <param name="emotionLayerName">active 表情を参照するレイヤー名（典型的には "emotion"）。</param>
        public OverlayInputSource(
            InputSourceId id,
            string slot,
            int blendShapeCount,
            IReadOnlyList<string> blendShapeNames,
            FacialProfile profile,
            IActiveExpressionProvider activeProvider,
            string emotionLayerName)
            : base(id, blendShapeCount)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                throw new ArgumentException("slot は空にできません。", nameof(slot));
            }
            if (blendShapeNames == null)
            {
                throw new ArgumentNullException(nameof(blendShapeNames));
            }

            _slot = slot;
            _profile = profile;
            _activeProvider = activeProvider;
            _emotionLayerName = string.IsNullOrEmpty(emotionLayerName) ? "emotion" : emotionLayerName;

            _activeMask = new BitArray(blendShapeCount, false);
            _emptyMask = new BitArray(blendShapeCount, false);

            // BlendShape 名 → index の逆引きマップ。
            var nameToIndex = new Dictionary<string, int>(blendShapeNames.Count, StringComparer.Ordinal);
            for (int i = 0; i < blendShapeNames.Count; i++)
            {
                var n = blendShapeNames[i];
                if (string.IsNullOrEmpty(n)) continue;
                if (!nameToIndex.ContainsKey(n)) nameToIndex[n] = i;
            }

            // 各 Expression を index/value 列 + mask に事前展開する。
            _resolvedById = new Dictionary<string, ResolvedExpression>(StringComparer.Ordinal);
            var exprSpan = profile.Expressions.Span;
            for (int i = 0; i < exprSpan.Length; i++)
            {
                var expr = exprSpan[i];
                _resolvedById[expr.Id] = ResolvedExpression.Build(expr, nameToIndex, blendShapeCount);
            }
        }

        /// <inheritdoc />
        public override BitArray ContributeMask => _hasActiveResolved ? _activeMask : _emptyMask;

        /// <inheritdoc />
        public override bool TryWriteValues(Span<float> output)
        {
            string resolvedId = ResolveOverlayExpressionId();
            if (string.IsNullOrEmpty(resolvedId))
            {
                ClearActiveMask();
                _hasActiveResolved = false;
                return false;
            }

            if (!_resolvedById.TryGetValue(resolvedId, out var resolved))
            {
                Debug.LogWarning(
                    $"[OverlayInputSource] slot='{_slot}' で解決された expressionId '{resolvedId}' が profile に存在しません。skip します。");
                ClearActiveMask();
                _hasActiveResolved = false;
                return false;
            }

            _activeResolved = resolved;
            _hasActiveResolved = true;
            CopyMaskFrom(resolved.Mask);

            int copyLen = output.Length < BlendShapeCount ? output.Length : BlendShapeCount;
            // output 全体をクリアしてから touch 対象 index に値を書く (touch 対象外は 0)。
            for (int i = 0; i < copyLen; i++)
            {
                output[i] = 0f;
            }
            var indices = resolved.Indices;
            var values = resolved.Values;
            for (int k = 0; k < indices.Length; k++)
            {
                int idx = indices[k];
                if ((uint)idx < (uint)copyLen)
                {
                    output[idx] = values[k];
                }
            }
            return true;
        }

        private string ResolveOverlayExpressionId()
        {
            // 1) active 表情の overlays[slot] を最優先
            if (_activeProvider != null)
            {
                var top = _activeProvider.TryGetTopActiveExpression(_emotionLayerName);
                if (top.HasValue && top.Value.TryGetOverlay(_slot, out var explicitBinding))
                {
                    return explicitBinding.IsSuppress ? null : explicitBinding.ExpressionId;
                }
            }

            // 2) profile.defaultOverlays[slot] へ fallback
            if (_profile.TryGetDefaultOverlay(_slot, out var defaultBinding))
            {
                return defaultBinding.IsSuppress ? null : defaultBinding.ExpressionId;
            }

            // 3) どこにも宣言が無ければ no-op
            return null;
        }

        private void CopyMaskFrom(BitArray src)
        {
            int n = _activeMask.Length;
            for (int i = 0; i < n; i++)
            {
                _activeMask[i] = i < src.Length && src[i];
            }
        }

        private void ClearActiveMask()
        {
            _activeMask.SetAll(false);
        }

        /// <summary>
        /// Profile 内の 1 Expression を BlendShape index 列 / value 列 / mask に展開した値型。
        /// 構築後は immutable で、毎フレームの output 書込で参照されるだけ。
        /// </summary>
        private readonly struct ResolvedExpression
        {
            public readonly int[] Indices;
            public readonly float[] Values;
            public readonly BitArray Mask;

            public ResolvedExpression(int[] indices, float[] values, BitArray mask)
            {
                Indices = indices;
                Values = values;
                Mask = mask;
            }

            public static ResolvedExpression Build(
                Expression expr,
                IReadOnlyDictionary<string, int> nameToIndex,
                int blendShapeCount)
            {
                var bsSpan = expr.BlendShapeValues.Span;
                var indexList = new List<int>(bsSpan.Length);
                var valueList = new List<float>(bsSpan.Length);
                var mask = new BitArray(blendShapeCount, false);

                for (int v = 0; v < bsSpan.Length; v++)
                {
                    var name = bsSpan[v].Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!nameToIndex.TryGetValue(name, out int idx)) continue;
                    indexList.Add(idx);
                    valueList.Add(bsSpan[v].Value);
                    mask[idx] = true;
                }

                return new ResolvedExpression(
                    indexList.ToArray(),
                    valueList.ToArray(),
                    mask);
            }
        }
    }
}
