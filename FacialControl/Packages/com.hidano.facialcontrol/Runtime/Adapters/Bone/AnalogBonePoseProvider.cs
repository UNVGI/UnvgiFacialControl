using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// アナログバインディングを毎フレーム評価し <see cref="BoneSnapshot"/> 列を構築、
    /// <see cref="IBonePoseProvider.SetActiveBoneSnapshots"/> 経由で注入するアダプタ
    /// （Req 4.1〜4.9、tasks.md 4.1）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 構築時に <see cref="AnalogBindingTargetKind.BonePose"/> の binding を抽出し、
    /// ユニークな bone 名ごとに <see cref="BoneSnapshot"/> スロットを 1 度だけ確保する（Req 4.7）。
    /// 毎フレーム <see cref="BuildAndPush"/> で同一スロットの値だけを書換え、
    /// <see cref="ReadOnlyMemory{T}"/> 経由で <see cref="IBonePoseProvider"/> に渡す。
    /// </para>
    /// <para>
    /// 同一 (bone, axis) への複数 binding は post-mapping 値の sum（Req 4.6）。
    /// bindings が 0 件 / 全ソースが無効の場合は空 <see cref="ReadOnlyMemory{T}"/> を発行し、
    /// <see cref="UnityEngine.Debug"/> を呼ばない（<see cref="BoneWriter.Apply"/> は空エントリで no-op）。
    /// </para>
    /// </remarks>
    public sealed class AnalogBonePoseProvider : IDisposable
    {
        /// <summary>
        /// 本アダプタが発行する論理 ID（preview.1 では参照キー未使用、互換目的のみ）。
        /// </summary>
        public const string PoseId = "analog-bonepose";

        private readonly IBonePoseProvider _boneProvider;
        private readonly IReadOnlyDictionary<string, IAnalogInputSource> _sources;
        private readonly ResolvedBinding[] _resolvedBindings;
        private readonly BonePoseSlot[] _slots;
        private readonly BoneSnapshot[] _snapshotBuffer;
        private bool _disposed;

        /// <summary>
        /// <see cref="AnalogBonePoseProvider"/> を構築する。
        /// </summary>
        /// <param name="boneProvider">BoneSnapshot 注入先（<see cref="FacialController"/> 等）。</param>
        /// <param name="sources">sourceId → <see cref="IAnalogInputSource"/> の辞書。</param>
        /// <param name="bonePoseBindings">バインディング集合。<see cref="AnalogBindingTargetKind.BonePose"/> のみ採用。</param>
        public AnalogBonePoseProvider(
            IBonePoseProvider boneProvider,
            IReadOnlyDictionary<string, IAnalogInputSource> sources,
            IReadOnlyList<AnalogBindingEntry> bonePoseBindings)
        {
            _boneProvider = boneProvider ?? throw new ArgumentNullException(nameof(boneProvider));
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            if (bonePoseBindings == null)
            {
                throw new ArgumentNullException(nameof(bonePoseBindings));
            }

            // Step 1: BonePose ターゲットの bindings のみ抽出し、source 解決を済ませる。
            // ユニーク bone ごとに slotIndex を割当てる。
            var boneNameToSlot = new Dictionary<string, int>(StringComparer.Ordinal);
            var resolvedList = new List<ResolvedBinding>(bonePoseBindings.Count);

            for (int i = 0; i < bonePoseBindings.Count; i++)
            {
                var entry = bonePoseBindings[i];
                if (entry.TargetKind != AnalogBindingTargetKind.BonePose)
                {
                    continue;
                }

                if (!_sources.TryGetValue(entry.SourceId, out var source) || source == null)
                {
                    Debug.LogWarning(
                        $"[AnalogBonePoseProvider] source '{entry.SourceId}' not registered " +
                        $"(target='{entry.TargetIdentifier}'). Binding skipped.");
                    continue;
                }

                if (!boneNameToSlot.TryGetValue(entry.TargetIdentifier, out int slot))
                {
                    slot = boneNameToSlot.Count;
                    boneNameToSlot[entry.TargetIdentifier] = slot;
                }

                resolvedList.Add(new ResolvedBinding(
                    source, entry.SourceAxis, slot, entry.TargetAxis));
            }

            _resolvedBindings = resolvedList.Count == 0
                ? Array.Empty<ResolvedBinding>()
                : resolvedList.ToArray();

            // Step 2: ユニーク bone のスロット配列と pre-alloc 済 snapshot buffer を作る。
            int slotCount = boneNameToSlot.Count;
            _slots = slotCount == 0 ? Array.Empty<BonePoseSlot>() : new BonePoseSlot[slotCount];
            foreach (var kv in boneNameToSlot)
            {
                _slots[kv.Value] = new BonePoseSlot(kv.Key);
            }

            _snapshotBuffer = slotCount == 0 ? Array.Empty<BoneSnapshot>() : new BoneSnapshot[slotCount];
            // 初期 snapshot に 0 度 / scale=1 を書込む。BonePath は構築後不変なので毎フレーム書換える必要なし。
            for (int s = 0; s < slotCount; s++)
            {
                _snapshotBuffer[s] = new BoneSnapshot(
                    _slots[s].BoneName,
                    0f, 0f, 0f,
                    0f, 0f, 0f,
                    1f, 1f, 1f);
            }
        }

        /// <summary>
        /// per-frame に呼出され、binding 評価 → <see cref="BoneSnapshot"/> 列構築 →
        /// <see cref="IBonePoseProvider.SetActiveBoneSnapshots"/> を 1 回行う（Req 4.5）。
        /// </summary>
        public void BuildAndPush()
        {
            if (_disposed)
            {
                return;
            }

            int slotCount = _slots.Length;

            // 全スロットの (X, Y, Z) を 0 にリセットする。
            for (int s = 0; s < slotCount; s++)
            {
                _slots[s].EulerX = 0f;
                _slots[s].EulerY = 0f;
                _slots[s].EulerZ = 0f;
            }

            // 各 binding を評価して該当 (slot, axis) に sum で加算する。
            int rbCount = _resolvedBindings.Length;
            for (int i = 0; i < rbCount; i++)
            {
                var rb = _resolvedBindings[i];
                var source = rb.Source;
                if (!source.IsValid)
                {
                    continue;
                }
                if (rb.SourceAxis < 0 || rb.SourceAxis >= source.AxisCount)
                {
                    continue;
                }
                if (!TryReadAxis(source, rb.SourceAxis, out float raw))
                {
                    continue;
                }

                // Phase 3.5: Mapping を撤去（dead-zone / scale / offset / curve / invert / clamp の値変換は
                // Adapters 側 InputProcessor 経路で扱う。Decision 4 / Req 13.3）。生値をそのまま加算する。
                ref var slot = ref _slots[rb.SlotIndex];
                switch (rb.TargetAxis)
                {
                    case AnalogTargetAxis.X:
                        slot.EulerX += raw;
                        break;
                    case AnalogTargetAxis.Y:
                        slot.EulerY += raw;
                        break;
                    case AnalogTargetAxis.Z:
                        slot.EulerZ += raw;
                        break;
                }
            }

            // pre-alloc 済 _snapshotBuffer に値を書込む（BonePath は ctor で確定済、
            // Position / Scale は default で固定）。
            for (int s = 0; s < slotCount; s++)
            {
                var slot = _slots[s];
                _snapshotBuffer[s] = new BoneSnapshot(
                    slot.BoneName,
                    0f, 0f, 0f,
                    slot.EulerX, slot.EulerY, slot.EulerZ,
                    1f, 1f, 1f);
            }

            _boneProvider.SetActiveBoneSnapshots(new ReadOnlyMemory<BoneSnapshot>(_snapshotBuffer, 0, slotCount));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
        }

        private static bool TryReadAxis(IAnalogInputSource source, int axis, out float value)
        {
            if (axis < 0 || axis >= source.AxisCount)
            {
                value = 0f;
                return false;
            }

            if (source.AxisCount == 1)
            {
                return source.TryReadScalar(out value);
            }

            if (source.AxisCount == 2)
            {
                if (source.TryReadVector2(out float x, out float y))
                {
                    value = axis == 0 ? x : y;
                    return true;
                }
                value = 0f;
                return false;
            }

            // N-axis: 軸数分のスタック領域に書込んで 1 軸抽出する。
            Span<float> buf = stackalloc float[source.AxisCount];
            if (source.TryReadAxes(buf))
            {
                value = buf[axis];
                return true;
            }
            value = 0f;
            return false;
        }

        private struct BonePoseSlot
        {
            public string BoneName;
            public float EulerX;
            public float EulerY;
            public float EulerZ;

            public BonePoseSlot(string boneName)
            {
                BoneName = boneName;
                EulerX = 0f;
                EulerY = 0f;
                EulerZ = 0f;
            }
        }

        private readonly struct ResolvedBinding
        {
            public readonly IAnalogInputSource Source;
            public readonly int SourceAxis;
            public readonly int SlotIndex;
            public readonly AnalogTargetAxis TargetAxis;

            public ResolvedBinding(
                IAnalogInputSource source,
                int sourceAxis,
                int slotIndex,
                AnalogTargetAxis targetAxis)
            {
                Source = source;
                SourceAxis = sourceAxis;
                SlotIndex = slotIndex;
                TargetAxis = targetAxis;
            }
        }
    }
}
