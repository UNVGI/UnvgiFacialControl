using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Domain.Services
{
    /// <summary>
    /// レイヤー × ソース ID → <see cref="IInputSource"/> の対応表と、
    /// Aggregator が <see cref="IInputSource.TryWriteValues"/> の書込先として借りる
    /// scratch バッファの事前確保プールを管理する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// プロファイルロード時に 1 回だけ <c>(layerIdx, sourceIdx, IInputSource)</c> の
    /// bindings から構築され、<c>layerCount × maxSourcesPerLayer × blendShapeCount</c>
    /// サイズの 1 本の <c>float[]</c> を確保する (D-14)。各 (layer, source) には
    /// <see cref="Memory{T}"/> slice を通して連続・非重複・固定アドレスの区間を配布する。
    /// </para>
    /// <para>
    /// <see cref="MaxSourcesPerLayer"/> は bindings に現れる最大 sourceIdx + 1 から導出される。
    /// 空 bindings の場合は 0 となり scratch は確保されない。
    /// </para>
    /// <para>
    /// Invariants:
    /// <list type="bullet">
    ///   <item><see cref="LayerCount"/> / <see cref="MaxSourcesPerLayer"/> / <see cref="BlendShapeCount"/>
    ///   は初期化後不変（ランタイム追加 API は別タスク 3.6 で追加）。</item>
    ///   <item><see cref="GetScratchBuffer"/> は同 (layer, source) に対して常に同一メモリ領域を返す。</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class LayerInputSourceRegistry : IDisposable
    {
        /// <summary>レイヤー本数。<see cref="FacialProfile.Layers"/> の長さと一致する。</summary>
        public int LayerCount { get; }

        /// <summary>1 レイヤー当たりの最大入力源スロット数。bindings の最大 sourceIdx + 1。</summary>
        public int MaxSourcesPerLayer { get; }

        /// <summary>1 入力源が書込む BlendShape の個数。scratch slice の長さに等しい。</summary>
        public int BlendShapeCount { get; }

        private readonly IInputSource[] _sources;       // 2D を flat で [l * Max + s] に格納
        private readonly int[] _sourceCounts;           // 各レイヤーの登録済みスロット数
        private float[] _scratchBuffer;                 // 連続 1 本の scratch プール
        private bool _disposed;

        /// <summary>
        /// レジストリを構築し、scratch バッファを確保する。
        /// </summary>
        /// <param name="profile">表情プロファイル。<see cref="LayerCount"/> の決定に用いる。</param>
        /// <param name="blendShapeCount">1 入力源が書込む BlendShape の個数。0 以上。</param>
        /// <param name="bindings">
        /// <c>(layerIdx, sourceIdx, source)</c> のタプル列。
        /// 範囲外の layerIdx / 負の sourceIdx は無視される。
        /// 同一 (layerIdx, sourceIdx) への複数登録は後勝ち。
        /// </param>
        public LayerInputSourceRegistry(
            FacialProfile profile,
            int blendShapeCount,
            IReadOnlyList<(int layerIdx, int sourceIdx, IInputSource source)> bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            if (blendShapeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blendShapeCount), blendShapeCount,
                    "blendShapeCount は 0 以上を指定してください。");
            }

            LayerCount = profile.Layers.Length;
            BlendShapeCount = blendShapeCount;

            int maxSources = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.layerIdx < 0 || b.layerIdx >= LayerCount)
                {
                    continue;
                }
                if (b.sourceIdx < 0)
                {
                    continue;
                }
                int needed = b.sourceIdx + 1;
                if (needed > maxSources)
                {
                    maxSources = needed;
                }
            }
            MaxSourcesPerLayer = maxSources;

            int slotCount = LayerCount * MaxSourcesPerLayer;
            _sources = slotCount == 0 ? Array.Empty<IInputSource>() : new IInputSource[slotCount];
            _sourceCounts = LayerCount == 0 ? Array.Empty<int>() : new int[LayerCount];

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if ((uint)b.layerIdx >= (uint)LayerCount)
                {
                    continue;
                }
                if ((uint)b.sourceIdx >= (uint)MaxSourcesPerLayer)
                {
                    continue;
                }

                int flat = (b.layerIdx * MaxSourcesPerLayer) + b.sourceIdx;
                _sources[flat] = b.source;
                int registered = b.sourceIdx + 1;
                if (registered > _sourceCounts[b.layerIdx])
                {
                    _sourceCounts[b.layerIdx] = registered;
                }
            }

            int scratchSize = LayerCount * MaxSourcesPerLayer * BlendShapeCount;
            _scratchBuffer = scratchSize == 0 ? Array.Empty<float>() : new float[scratchSize];
        }

        /// <summary>
        /// 指定レイヤーに登録済みの入力源スロット数を返す。<br/>
        /// スロットは 0-origin で詰めて埋められる想定ではなく、最大 sourceIdx + 1 を返す。
        /// </summary>
        public int GetSourceCountForLayer(int layerIdx)
        {
            if ((uint)layerIdx >= (uint)LayerCount)
            {
                return 0;
            }
            return _sourceCounts[layerIdx];
        }

        /// <summary>
        /// 指定 (layerIdx, sourceIdx) の入力源を返す。未登録または範囲外は <c>null</c>。
        /// </summary>
        public IInputSource GetSource(int layerIdx, int sourceIdx)
        {
            if ((uint)layerIdx >= (uint)LayerCount)
            {
                return null;
            }
            if ((uint)sourceIdx >= (uint)MaxSourcesPerLayer)
            {
                return null;
            }
            int flat = (layerIdx * MaxSourcesPerLayer) + sourceIdx;
            return _sources[flat];
        }

        /// <summary>
        /// 指定 (layerIdx, sourceIdx) に割り当てられた scratch バッファ slice を返す。
        /// 長さは <see cref="BlendShapeCount"/> と等しく、同 (layer, source) に対しては常に
        /// 同一メモリ領域を返す (Aggregator の per-frame ゼロクリア + 書込が安定して効くため)。
        /// </summary>
        /// <remarks>
        /// 範囲外インデックス、および <see cref="Dispose"/> 後の呼出しは
        /// <see cref="Memory{T}.Empty"/> を返す。
        /// </remarks>
        public Memory<float> GetScratchBuffer(int layerIdx, int sourceIdx)
        {
            if (_disposed || _scratchBuffer == null || _scratchBuffer.Length == 0)
            {
                return Memory<float>.Empty;
            }
            if ((uint)layerIdx >= (uint)LayerCount)
            {
                return Memory<float>.Empty;
            }
            if ((uint)sourceIdx >= (uint)MaxSourcesPerLayer)
            {
                return Memory<float>.Empty;
            }

            int offset = ((layerIdx * MaxSourcesPerLayer) + sourceIdx) * BlendShapeCount;
            return new Memory<float>(_scratchBuffer, offset, BlendShapeCount);
        }

        /// <summary>
        /// 内部バッファを解放する。重複呼出しは無視される。
        /// Dispose 後は <see cref="GetScratchBuffer"/> が <see cref="Memory{T}.Empty"/> を返す。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _scratchBuffer = null;
            _disposed = true;
        }
    }
}
