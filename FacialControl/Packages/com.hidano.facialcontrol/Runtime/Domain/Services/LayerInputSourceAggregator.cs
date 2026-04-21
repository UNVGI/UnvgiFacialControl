using System;

namespace Hidano.FacialControl.Domain.Services
{
    /// <summary>
    /// レイヤー毎に複数の <see cref="Hidano.FacialControl.Domain.Interfaces.IInputSource"/> 出力を
    /// 加重和で集約し、最後に <c>[0, 1]</c> へクランプする Domain サービス (D-3 2 段パイプラインの前段)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本クラスは毎フレーム 1 回 <see cref="Aggregate"/> が呼ばれ、内部で
    /// <see cref="LayerInputSourceWeightBuffer.SwapIfDirty"/> を行ったあと、
    /// 各 (layer, source) について <c>source.Tick(deltaTime)</c> +
    /// <c>source.TryWriteValues(scratch)</c> を呼び出し、
    /// <c>output[k] = clamp01(Σ wᵢ · values_i[k])</c> を per-layer float バッファに書込む
    /// (Req 2.2 / 2.3 / 6.3)。
    /// </para>
    /// <para>
    /// 出力は <see cref="LayerBlender.LayerInput"/> の <see cref="Span{T}"/> として受け取る。
    /// 各スロットには毎フレーム <c>new LayerBlender.LayerInput(priority, weight, pooledBuffer)</c>
    /// が代入されるが、<c>pooledBuffer</c> はコンストラクタで一度だけ確保した per-layer
    /// <c>float[]</c> を使い回すため GC アロケーションは発生しない (Design §Output semantics)。
    /// </para>
    /// <para>
    /// 本タスク時点での責務: per-layer 加重和 + 最終クランプ (5.1)、
    /// 長さ不一致時の overlap-only 処理 + 無効ソース (TryWriteValues=false) のゼロ寄与 (5.2、Req 1.3 / 1.4)、
    /// 空レイヤー検出 + per-layer per-session 1 回 warning (5.3、Req 2.4)、
    /// および既存 <see cref="LayerBlender.Blend(System.ReadOnlySpan{LayerBlender.LayerInput}, System.Span{float})"/>
    /// との 2 段パイプライン接続 (5.4、Req 2.6 / 2.7 / 6.4 / 7.1)。
    /// </para>
    /// <para>
    /// overlap-only 契約: 各 <c>source.TryWriteValues(scratch)</c> の直前に scratch を
    /// <see cref="Span{T}.Clear"/> でゼロ初期化し、source が BlendShape 個数未満しか書込まない場合も
    /// 未書込インデックスはゼロ寄与として加算されるようにする (前フレームの値が leak しない)。
    /// 無効ソース (戻り値 false) は scratch が全ゼロのまま加算ループを <c>continue</c> でスキップし、
    /// 例外を出さず寄与 0 として扱う (Req 1.4)。
    /// </para>
    /// <para>
    /// 空レイヤー契約 (Req 2.4): 当該レイヤーに登録された source が 0 本、または全 source が
    /// <see cref="Hidano.FacialControl.Domain.Interfaces.IInputSource.TryWriteValues"/> で
    /// false を返した場合、そのレイヤーの出力はゼロに保たれ、<see cref="UnityEngine.Debug.LogWarning(object)"/>
    /// がそのレイヤーについてセッション (= Aggregator インスタンス) あたり 1 回だけ発火する。
    /// weight=0 の valid source は「空レイヤー」とはみなさない (IsValid が基準、Req 2.4 / design §Edge Cases)。
    /// 一度 warning を出したレイヤーは、以後 valid に戻って再び空になっても warning は再発しない。
    /// </para>
    /// <para>
    /// 2 段パイプライン (5.4、Req 2.6 / 2.7): intra-layer の加重和 + クランプ (source weight による合成)
    /// と inter-layer の <see cref="LayerBlender.LayerInput.Weight"/> による優先度ブレンドは独立に適用される。
    /// Aggregator は source weight を per-layer float バッファに畳み込んだうえで、呼出側から渡された
    /// inter-layer weight をそのまま <see cref="LayerBlender.LayerInput"/> の <c>weight</c> に載せる
    /// (乗算しない、Req 2.7 / D-4)。<see cref="AggregateAndBlend"/> を使えば per-layer バッファから
    /// 最終 BlendShape 配列までの経路を 1 呼出しで回せる。
    /// </para>
    /// <para>
    /// 診断 snapshot API (5.5) / verbose log rate-limit (5.6) は別タスクで積み増す。
    /// </para>
    /// </remarks>
    public sealed class LayerInputSourceAggregator
    {
        private readonly LayerInputSourceRegistry _registry;
        private readonly LayerInputSourceWeightBuffer _weightBuffer;
        private readonly int _blendShapeCount;
        private readonly float[][] _perLayerOutput;
        private readonly bool[] _emptyLayerWarned;
        private readonly LayerBlender.LayerInput[] _layerInputScratch;

        /// <summary>
        /// Aggregator を構築する。per-layer 出力用の <c>float[]</c> を各レイヤー分
        /// 1 本ずつ確保する (レイヤー数 × blendShapeCount のメモリコスト)。
        /// </summary>
        /// <param name="registry">レイヤー × ソースの対応表 + scratch プール。非 null。</param>
        /// <param name="weightBuffer">入力源ウェイトのダブルバッファ。非 null。</param>
        /// <param name="blendShapeCount">1 レイヤーの出力 BlendShape 個数。0 以上。</param>
        public LayerInputSourceAggregator(
            LayerInputSourceRegistry registry,
            LayerInputSourceWeightBuffer weightBuffer,
            int blendShapeCount)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (weightBuffer == null)
            {
                throw new ArgumentNullException(nameof(weightBuffer));
            }

            if (blendShapeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blendShapeCount), blendShapeCount,
                    "blendShapeCount は 0 以上を指定してください。");
            }

            _registry = registry;
            _weightBuffer = weightBuffer;
            _blendShapeCount = blendShapeCount;

            int layerCount = registry.LayerCount;
            _perLayerOutput = layerCount == 0 ? Array.Empty<float[]>() : new float[layerCount][];
            for (int l = 0; l < layerCount; l++)
            {
                _perLayerOutput[l] = blendShapeCount == 0 ? Array.Empty<float>() : new float[blendShapeCount];
            }
            _emptyLayerWarned = layerCount == 0 ? Array.Empty<bool>() : new bool[layerCount];
            _layerInputScratch = layerCount == 0
                ? Array.Empty<LayerBlender.LayerInput>()
                : new LayerBlender.LayerInput[layerCount];
        }

        /// <summary>
        /// 1 フレーム分の per-layer 加重和集約を行う。
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過秒数 (>= 0)。各 source の <c>Tick</c> にそのまま渡す。</param>
        /// <param name="outputPerLayer">
        /// 書込先。長さは <c>registry.LayerCount</c> と一致する必要がある。
        /// 各要素は <c>new LayerBlender.LayerInput(priority, weight, pooledBuffer)</c> で上書きされる。
        /// </param>
        /// <remarks>
        /// 主ループ:
        /// <list type="number">
        ///   <item><see cref="LayerInputSourceWeightBuffer.SwapIfDirty"/> を呼び新しい weight を観測可能にする。</item>
        ///   <item>各 layer について per-layer 累積バッファをゼロクリア。</item>
        ///   <item>各 (layer, source) について <c>Tick</c> → scratch ゼロクリア → <c>TryWriteValues</c> を実行。</item>
        ///   <item>true を返した source の scratch を <c>weightBuffer.GetWeight(l, s)</c> 倍して累積バッファに加算。</item>
        ///   <item>累積バッファを <c>[0, 1]</c> にクランプし、<c>outputPerLayer[l]</c> に <see cref="LayerBlender.LayerInput"/> として代入。</item>
        /// </list>
        /// </remarks>
        public void Aggregate(float deltaTime, Span<LayerBlender.LayerInput> outputPerLayer)
        {
            AggregateInternal(deltaTime, priorities: default, layerWeights: default, outputPerLayer);
        }

        /// <summary>
        /// priorities / layerWeights をレイヤー単位で指定する <see cref="Aggregate(float, Span{LayerBlender.LayerInput})"/>
        /// の overload。2 段パイプラインの前段として使い、後段 (inter-layer blend) に渡す
        /// <see cref="LayerBlender.LayerInput.Priority"/> / <see cref="LayerBlender.LayerInput.Weight"/> を
        /// 呼出側が決めたい場合に用いる (Req 2.6 / 2.7 / 5.4)。
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過秒数 (>= 0)。</param>
        /// <param name="priorities">レイヤー毎の優先度。長さは <c>registry.LayerCount</c> 以上。</param>
        /// <param name="layerWeights">レイヤー毎の inter-layer weight (0〜1 想定。<see cref="LayerBlender"/> 側で clamp)。
        /// 長さは <c>registry.LayerCount</c> 以上。source weight とは独立適用される (Req 2.7 / D-4)。</param>
        /// <param name="outputPerLayer">書込先。長さは <c>registry.LayerCount</c> と一致する必要がある。</param>
        public void Aggregate(
            float deltaTime,
            ReadOnlySpan<int> priorities,
            ReadOnlySpan<float> layerWeights,
            Span<LayerBlender.LayerInput> outputPerLayer)
        {
            int layerCount = _registry.LayerCount;
            if (priorities.Length < layerCount)
            {
                throw new ArgumentException(
                    $"priorities.Length ({priorities.Length}) は registry.LayerCount ({layerCount}) 以上である必要があります。",
                    nameof(priorities));
            }
            if (layerWeights.Length < layerCount)
            {
                throw new ArgumentException(
                    $"layerWeights.Length ({layerWeights.Length}) は registry.LayerCount ({layerCount}) 以上である必要があります。",
                    nameof(layerWeights));
            }

            AggregateInternal(deltaTime, priorities, layerWeights, outputPerLayer);
        }

        /// <summary>
        /// 2 段パイプラインを 1 呼出しで回すエントリポイント (5.4)。
        /// per-layer 加重和 + クランプを行った後、事前確保済みの
        /// <see cref="LayerBlender.LayerInput"/> スクラッチ経由で
        /// <see cref="LayerBlender.Blend(ReadOnlySpan{LayerBlender.LayerInput}, Span{float})"/>
        /// を呼び、最終 BlendShape 配列 (<paramref name="finalOutput"/>) を書込む。
        /// </summary>
        /// <remarks>
        /// source weight は intra-layer 段でのみ適用され、inter-layer 段には持ち越さない
        /// (Req 2.7 / D-4)。<paramref name="layerWeights"/> は <see cref="LayerBlender.LayerInput.Weight"/>
        /// としてそのまま載せられ、後段 <see cref="LayerBlender.Blend(ReadOnlySpan{LayerBlender.LayerInput}, Span{float})"/>
        /// が優先度ブレンドを行う。内部スクラッチはコンストラクタで 1 度だけ確保され、本メソッドは
        /// <see cref="LayerBlender"/> のシグネチャを一切変更しない (Req 7.1)。
        /// </remarks>
        public void AggregateAndBlend(
            float deltaTime,
            ReadOnlySpan<int> priorities,
            ReadOnlySpan<float> layerWeights,
            Span<float> finalOutput)
        {
            int layerCount = _registry.LayerCount;
            var scratchSpan = new Span<LayerBlender.LayerInput>(_layerInputScratch, 0, layerCount);

            Aggregate(deltaTime, priorities, layerWeights, scratchSpan);

            LayerBlender.Blend((ReadOnlySpan<LayerBlender.LayerInput>)scratchSpan, finalOutput);
        }

        private void AggregateInternal(
            float deltaTime,
            ReadOnlySpan<int> priorities,
            ReadOnlySpan<float> layerWeights,
            Span<LayerBlender.LayerInput> outputPerLayer)
        {
            _weightBuffer.SwapIfDirty();

            int layerCount = _registry.LayerCount;
            bool useCustomPriorities = priorities.Length >= layerCount;
            bool useCustomWeights = layerWeights.Length >= layerCount;

            for (int l = 0; l < layerCount; l++)
            {
                var layerOutput = _perLayerOutput[l];
                Array.Clear(layerOutput, 0, layerOutput.Length);

                bool hasAnyValidSource = false;
                int sourceCount = _registry.GetSourceCountForLayer(l);
                for (int s = 0; s < sourceCount; s++)
                {
                    var source = _registry.GetSource(l, s);
                    if (source == null)
                    {
                        continue;
                    }

                    source.Tick(deltaTime);

                    var scratchSpan = _registry.GetScratchBuffer(l, s).Span;
                    scratchSpan.Clear();

                    if (!source.TryWriteValues(scratchSpan))
                    {
                        continue;
                    }

                    hasAnyValidSource = true;

                    float w = _weightBuffer.GetWeight(l, s);
                    if (w <= 0f)
                    {
                        continue;
                    }

                    int len = layerOutput.Length < scratchSpan.Length
                        ? layerOutput.Length
                        : scratchSpan.Length;
                    for (int k = 0; k < len; k++)
                    {
                        layerOutput[k] += scratchSpan[k] * w;
                    }
                }

                if (!hasAnyValidSource)
                {
                    WarnEmptyLayerOnce(l);
                }

                for (int k = 0; k < layerOutput.Length; k++)
                {
                    float v = layerOutput[k];
                    if (v < 0f)
                    {
                        layerOutput[k] = 0f;
                    }
                    else if (v > 1f)
                    {
                        layerOutput[k] = 1f;
                    }
                }

                if ((uint)l < (uint)outputPerLayer.Length)
                {
                    int priority = useCustomPriorities ? priorities[l] : l;
                    float layerWeight = useCustomWeights ? layerWeights[l] : 1f;
                    outputPerLayer[l] = new LayerBlender.LayerInput(
                        priority: priority, weight: layerWeight, blendShapeValues: layerOutput);
                }
            }
        }

        private void WarnEmptyLayerOnce(int layerIdx)
        {
            if (_emptyLayerWarned[layerIdx])
            {
                return;
            }
            _emptyLayerWarned[layerIdx] = true;
            UnityEngine.Debug.LogWarning(
                $"[LayerInputSourceAggregator] layer {layerIdx}: no valid input source " +
                "(all sources reported IsValid=false or no source registered). " +
                "Output will be zero. This warning is emitted only once per layer per session.");
        }
    }
}
