using System;
using System.Collections;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hidano.FacialControl.Adapters.InputSources
{
    public sealed class OverlayInputSource : ValueProviderInputSourceBase
    {
        public const string ReservedIdPrefix = "overlay";

        private readonly string _slot;
        private readonly string _emotionLayerName;
        private readonly IActiveExpressionProvider _activeProvider;
        private readonly Dictionary<SlotKey, ResolvedSnapshot> _resolvedBySlot;
        private readonly BitArray _activeMask;
        private readonly BitArray _emptyMask;
        private readonly bool _slotDeclared;
        private bool _hasActiveResolved;
        private bool _logged;

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
                throw new ArgumentException("slot must not be empty.", nameof(slot));
            }
            if (blendShapeNames == null)
            {
                throw new ArgumentNullException(nameof(blendShapeNames));
            }

            _slot = slot;
            _activeProvider = activeProvider;
            _emotionLayerName = string.IsNullOrEmpty(emotionLayerName) ? "emotion" : emotionLayerName;
            _activeMask = new BitArray(blendShapeCount, false);
            _emptyMask = new BitArray(blendShapeCount, false);
            _resolvedBySlot = new Dictionary<SlotKey, ResolvedSnapshot>(
                profile.Expressions.Length + profile.DefaultOverlays.Length,
                EqualityComparer<SlotKey>.Default);

            _slotDeclared = ContainsSlot(profile.Slots.Span, _slot);
            if (!_slotDeclared)
            {
                LogUndeclaredSlotOnce();
                return;
            }

            var nameToIndex = new Dictionary<string, int>(blendShapeNames.Count, StringComparer.Ordinal);
            for (int i = 0; i < blendShapeNames.Count; i++)
            {
                string name = blendShapeNames[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (!nameToIndex.ContainsKey(name))
                {
                    nameToIndex.Add(name, i);
                }
            }

            var exprSpan = profile.Expressions.Span;
            for (int i = 0; i < exprSpan.Length; i++)
            {
                var expression = exprSpan[i];
                var overlaySpan = expression.Overlays.Span;
                for (int j = 0; j < overlaySpan.Length; j++)
                {
                    var binding = overlaySpan[j];
                    if (!string.Equals(binding.Slot, _slot, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (binding.IsDefaultFallback)
                    {
                        continue;
                    }

                    var key = new SlotKey(expression.Id, _slot);
                    if (!_resolvedBySlot.ContainsKey(key))
                    {
                        _resolvedBySlot.Add(
                            key,
                            ResolvedSnapshot.Build(binding, nameToIndex, blendShapeCount));
                    }
                }
            }

            var defaultOverlaySpan = profile.DefaultOverlays.Span;
            for (int i = 0; i < defaultOverlaySpan.Length; i++)
            {
                var binding = defaultOverlaySpan[i];
                if (!string.Equals(binding.Slot, _slot, StringComparison.Ordinal))
                {
                    continue;
                }
                if (binding.IsDefaultFallback)
                {
                    continue;
                }

                var key = new SlotKey(null, _slot);
                if (!_resolvedBySlot.ContainsKey(key))
                {
                    _resolvedBySlot.Add(
                        key,
                        ResolvedSnapshot.Build(binding, nameToIndex, blendShapeCount));
                }
            }
        }

        public override BitArray ContributeMask => _hasActiveResolved ? _activeMask : _emptyMask;

        public override bool TryWriteValues(Span<float> output)
        {
            Profiler.BeginSample("OverlayInputSource.TryWriteValues");
            try
            {
                if (!_slotDeclared || !ResolveSnapshot(out var resolved) || resolved.Suppress || !resolved.HasSnapshot)
                {
                    ClearActiveMask();
                    _hasActiveResolved = false;
                    return false;
                }

                _hasActiveResolved = true;
                CopyMaskFrom(resolved.Mask);

                int copyLen = output.Length < BlendShapeCount ? output.Length : BlendShapeCount;
                for (int i = 0; i < copyLen; i++)
                {
                    output[i] = 0f;
                }

                var indices = resolved.Indices;
                var values = resolved.Values;
                for (int i = 0; i < indices.Length; i++)
                {
                    int index = indices[i];
                    if ((uint)index < (uint)copyLen)
                    {
                        output[index] = values[i];
                    }
                }

                return true;
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        private bool ResolveSnapshot(out ResolvedSnapshot resolved)
        {
            if (_activeProvider != null)
            {
                var active = _activeProvider.TryGetTopActiveExpression(_emotionLayerName);
                if (active.HasValue)
                {
                    var activeKey = new SlotKey(active.Value.Id, _slot);
                    if (_resolvedBySlot.TryGetValue(activeKey, out resolved))
                    {
                        return true;
                    }
                }
            }

            var defaultKey = new SlotKey(null, _slot);
            return _resolvedBySlot.TryGetValue(defaultKey, out resolved);
        }

        private void CopyMaskFrom(BitArray source)
        {
            int length = _activeMask.Length;
            for (int i = 0; i < length; i++)
            {
                _activeMask[i] = i < source.Length && source[i];
            }
        }

        private void ClearActiveMask()
        {
            _activeMask.SetAll(false);
        }

        private void LogUndeclaredSlotOnce()
        {
            if (_logged)
            {
                return;
            }

            _logged = true;
            Debug.LogWarning(
                $"[OverlayInputSource] slot='{_slot}' is not declared in profile.Slots. Overlay input source will stay inactive.");
        }

        private static bool ContainsSlot(ReadOnlySpan<string> slots, string slot)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (string.Equals(slots[i], slot, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal readonly struct SlotKey : IEquatable<SlotKey>
    {
        public readonly string ExpressionId;
        public readonly string Slot;
        private readonly int _hashCode;

        public SlotKey(string expressionId, string slot)
        {
            ExpressionId = expressionId;
            Slot = slot;

            unchecked
            {
                int slotHash = slot != null ? StringComparer.Ordinal.GetHashCode(slot) : 0;
                int expressionHash = expressionId != null ? StringComparer.Ordinal.GetHashCode(expressionId) : 0;
                _hashCode = (slotHash * 397) ^ expressionHash;
            }
        }

        public bool Equals(SlotKey other)
        {
            return string.Equals(Slot, other.Slot, StringComparison.Ordinal)
                && string.Equals(ExpressionId, other.ExpressionId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SlotKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }

    internal readonly struct ResolvedSnapshot
    {
        public readonly bool Suppress;
        public readonly bool HasSnapshot;
        public readonly int[] Indices;
        public readonly float[] Values;
        public readonly BitArray Mask;

        public ResolvedSnapshot(
            bool suppress,
            bool hasSnapshot,
            int[] indices,
            float[] values,
            BitArray mask)
        {
            Suppress = suppress;
            HasSnapshot = hasSnapshot;
            Indices = indices ?? Array.Empty<int>();
            Values = values ?? Array.Empty<float>();
            Mask = mask;
        }

        public static ResolvedSnapshot Build(
            OverlaySlotBinding binding,
            IReadOnlyDictionary<string, int> nameToIndex,
            int blendShapeCount)
        {
            if (binding.Suppress)
            {
                return new ResolvedSnapshot(
                    suppress: true,
                    hasSnapshot: false,
                    indices: Array.Empty<int>(),
                    values: Array.Empty<float>(),
                    mask: new BitArray(blendShapeCount, false));
            }

            if (!binding.Snapshot.HasValue)
            {
                return new ResolvedSnapshot(
                    suppress: false,
                    hasSnapshot: false,
                    indices: Array.Empty<int>(),
                    values: Array.Empty<float>(),
                    mask: new BitArray(blendShapeCount, false));
            }

            return Build(binding.Snapshot.Value, nameToIndex, blendShapeCount);
        }

        private static ResolvedSnapshot Build(
            ExpressionSnapshot snapshot,
            IReadOnlyDictionary<string, int> nameToIndex,
            int blendShapeCount)
        {
            var blendShapes = snapshot.BlendShapes.Span;
            int count = 0;
            for (int i = 0; i < blendShapes.Length; i++)
            {
                string name = blendShapes[i].Name;
                if (!string.IsNullOrEmpty(name) && nameToIndex.ContainsKey(name))
                {
                    count++;
                }
            }

            int[] indices = count == 0 ? Array.Empty<int>() : new int[count];
            float[] values = count == 0 ? Array.Empty<float>() : new float[count];
            var mask = new BitArray(blendShapeCount, false);

            int writeIndex = 0;
            for (int i = 0; i < blendShapes.Length; i++)
            {
                string name = blendShapes[i].Name;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (!nameToIndex.TryGetValue(name, out int index))
                {
                    continue;
                }

                indices[writeIndex] = index;
                values[writeIndex] = blendShapes[i].Value;
                mask[index] = true;
                writeIndex++;
            }

            return new ResolvedSnapshot(
                suppress: false,
                hasSnapshot: true,
                indices: indices,
                values: values,
                mask: mask);
        }
    }
}
