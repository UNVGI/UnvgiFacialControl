using System;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Application.UseCases
{
    /// <summary>
    /// レイヤーの補間更新と最終 BlendShape 出力の計算を管理するユースケース。
    /// 内部では <see cref="LayerInputSourceAggregator"/> / <see cref="LayerInputSourceWeightBuffer"/> /
    /// <see cref="LayerInputSourceRegistry"/> に委譲し、per-layer Expression 遷移を
    /// <see cref="IInputSource"/> アダプタとして供給する (8.1)。公開 API シグネチャは非破壊に維持する
    /// (Req 7.1)。
    /// </summary>
    public class LayerUseCase : IDisposable
    {
        private FacialProfile _profile;
        private readonly ExpressionUseCase _expressionUseCase;
        private string[] _blendShapeNames;
        private readonly Dictionary<string, float> _layerWeights;

        private LayerInputSourceRegistry _registry;
        private LayerInputSourceWeightBuffer _weightBuffer;
        private LayerInputSourceAggregator _aggregator;
        private LayerExpressionSource[] _layerSources;
        private int[] _layerPriorities;
        private float[] _layerInterWeights;
        private LayerBlender.LayerInput[] _layerInputScratch;
        private LayerBlender.LayerInput[] _filteredLayerInputs;
        private float[] _finalOutput;
        private bool _disposed;

        /// <summary>
        /// LayerUseCase を生成する。
        /// </summary>
        /// <param name="profile">対象の表情設定プロファイル</param>
        /// <param name="expressionUseCase">Expression 管理ユースケース</param>
        /// <param name="blendShapeNames">BlendShape 名の配列</param>
        public LayerUseCase(FacialProfile profile, ExpressionUseCase expressionUseCase, string[] blendShapeNames)
        {
            if (blendShapeNames == null)
                throw new ArgumentNullException(nameof(blendShapeNames));

            _profile = profile;
            _expressionUseCase = expressionUseCase;
            _blendShapeNames = blendShapeNames;
            _layerWeights = new Dictionary<string, float>();

            BuildAggregatorPipeline();
        }

        /// <summary>
        /// レイヤーウェイトを設定する。値は 0〜1 にクランプされる。
        /// </summary>
        /// <param name="layer">レイヤー名</param>
        /// <param name="weight">ウェイト値（0〜1）</param>
        public void SetLayerWeight(string layer, float weight)
        {
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));

            float clamped = Clamp01(weight);
            _layerWeights[layer] = clamped;

            if (_layerInterWeights == null)
                return;

            var layerSpan = _profile.Layers.Span;
            for (int i = 0; i < layerSpan.Length; i++)
            {
                if (layerSpan[i].Name == layer)
                {
                    _layerInterWeights[i] = clamped;
                    break;
                }
            }
        }

        /// <summary>
        /// 全レイヤーの補間を deltaTime 分だけ進行させる。
        /// アクティブな Expression の変更を検出し、遷移割込を処理したうえで、
        /// Aggregator 経由で per-layer 加重和 + LayerBlender による優先度ブレンドを行う。
        /// </summary>
        /// <param name="deltaTime">経過時間（秒）</param>
        public void UpdateWeights(float deltaTime)
        {
            int bsCount = _blendShapeNames.Length;
            if (bsCount == 0 || _aggregator == null)
                return;

            var activeExpressions = _expressionUseCase.GetActiveExpressions();
            var expressionsByLayer = GroupByLayer(activeExpressions);

            var layerSpan = _profile.Layers.Span;
            for (int l = 0; l < layerSpan.Length; l++)
            {
                string layerName = layerSpan[l].Name;
                var exclusionMode = layerSpan[l].ExclusionMode;

                if (expressionsByLayer.TryGetValue(layerName, out var layerExpressions) &&
                    layerExpressions.Count > 0)
                {
                    _layerSources[l].UpdateExpressions(layerExpressions, exclusionMode, _blendShapeNames);
                }
            }

            _aggregator.Aggregate(
                deltaTime,
                _layerPriorities,
                _layerInterWeights,
                _layerInputScratch);

            int activeCount = 0;
            for (int l = 0; l < layerSpan.Length; l++)
            {
                if (_layerSources[l].HasBeenActive)
                {
                    _filteredLayerInputs[activeCount++] = _layerInputScratch[l];
                }
            }

            Array.Clear(_finalOutput, 0, _finalOutput.Length);
            if (activeCount > 0)
            {
                LayerBlender.Blend(
                    new ReadOnlySpan<LayerBlender.LayerInput>(_filteredLayerInputs, 0, activeCount),
                    new Span<float>(_finalOutput));
            }
        }

        /// <summary>
        /// 全レイヤーのブレンド結果を計算し、最終出力 BlendShape 値を返す。
        /// 返されるのは防御的コピーである。
        /// </summary>
        /// <returns>BlendShape ウェイト配列</returns>
        public float[] GetBlendedOutput()
        {
            int bsCount = _blendShapeNames.Length;
            var output = new float[bsCount];
            if (bsCount > 0 && _finalOutput != null)
            {
                int copyLen = Math.Min(bsCount, _finalOutput.Length);
                Array.Copy(_finalOutput, output, copyLen);
            }
            return output;
        }

        /// <summary>
        /// プロファイルを切り替え、遷移状態をリセットする。
        /// </summary>
        /// <param name="profile">新しいプロファイル</param>
        /// <param name="blendShapeNames">新しい BlendShape 名リスト</param>
        public void SetProfile(FacialProfile profile, string[] blendShapeNames)
        {
            _profile = profile;
            _blendShapeNames = blendShapeNames ?? throw new ArgumentNullException(nameof(blendShapeNames));
            _layerWeights.Clear();
            BuildAggregatorPipeline();
        }

        /// <summary>
        /// 内部の Registry / WeightBuffer が保持する NativeArray を解放する。
        /// 呼出後に <see cref="UpdateWeights"/> / <see cref="SetProfile"/> を呼ぶと
        /// 再構築は行われず、<see cref="GetBlendedOutput"/> は直近の出力コピーを返し続ける。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            DisposePipeline();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~LayerUseCase()
        {
            // Finalizer による保険: NativeArray.Dispose は明示 Dispose を想定しているが、
            // 既存テストが LayerUseCase を Dispose しないため、漏れた NativeArray を回収する。
            DisposePipeline();
        }

        private void DisposePipeline()
        {
            _registry?.Dispose();
            _weightBuffer?.Dispose();
            _registry = null;
            _weightBuffer = null;
            _aggregator = null;
        }

        private void BuildAggregatorPipeline()
        {
            DisposePipeline();

            int bsCount = _blendShapeNames.Length;
            _finalOutput = new float[bsCount];

            int layerCount = _profile.Layers.Length;
            _layerPriorities = layerCount == 0 ? Array.Empty<int>() : new int[layerCount];
            _layerInterWeights = layerCount == 0 ? Array.Empty<float>() : new float[layerCount];
            _layerSources = layerCount == 0 ? Array.Empty<LayerExpressionSource>() : new LayerExpressionSource[layerCount];
            _layerInputScratch = layerCount == 0 ? Array.Empty<LayerBlender.LayerInput>() : new LayerBlender.LayerInput[layerCount];
            _filteredLayerInputs = layerCount == 0 ? Array.Empty<LayerBlender.LayerInput>() : new LayerBlender.LayerInput[layerCount];

            var bindings = new List<(int layerIdx, int sourceIdx, IInputSource source)>(layerCount);
            var layerSpan = _profile.Layers.Span;
            for (int l = 0; l < layerCount; l++)
            {
                _layerPriorities[l] = layerSpan[l].Priority;
                _layerInterWeights[l] = 1f;
                var src = new LayerExpressionSource(bsCount);
                _layerSources[l] = src;
                bindings.Add((l, 0, src));
            }

            _registry = new LayerInputSourceRegistry(_profile, bsCount, bindings);
            int maxSources = _registry.MaxSourcesPerLayer > 0 ? _registry.MaxSourcesPerLayer : 1;
            _weightBuffer = new LayerInputSourceWeightBuffer(layerCount, maxSources);
            for (int l = 0; l < layerCount; l++)
            {
                _weightBuffer.SetWeight(l, 0, 1f);
            }
            _aggregator = new LayerInputSourceAggregator(_registry, _weightBuffer, bsCount);
        }

        private Dictionary<string, List<Expression>> GroupByLayer(List<Expression> expressions)
        {
            var grouped = new Dictionary<string, List<Expression>>();

            for (int i = 0; i < expressions.Count; i++)
            {
                string effectiveLayer = _profile.GetEffectiveLayer(expressions[i]);

                if (!grouped.TryGetValue(effectiveLayer, out var list))
                {
                    list = new List<Expression>();
                    grouped[effectiveLayer] = list;
                }
                list.Add(expressions[i]);
            }

            return grouped;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        /// <summary>
        /// 1 レイヤー分の Expression ベース遷移を <see cref="IInputSource"/> として
        /// <see cref="LayerInputSourceAggregator"/> へ供給する内部アダプタ。
        /// 旧 <c>LayerUseCase.LayerTransitionState</c> が保持していた
        /// snapshot / target / current / elapsed / curve / previousActiveIds を内包し、
        /// <see cref="Tick"/> で <see cref="TransitionCalculator.ComputeBlendWeight"/> +
        /// <see cref="ExclusionResolver.ResolveLastWins"/> による補間を進める。
        /// </summary>
        private sealed class LayerExpressionSource : IInputSource
        {
            public string Id => "input";
            public InputSourceType Type => InputSourceType.ExpressionTrigger;
            public int BlendShapeCount { get; }

            public bool HasBeenActive { get; private set; }

            private readonly float[] _snapshotValues;
            private readonly float[] _targetValues;
            private readonly float[] _currentValues;
            private readonly List<string> _previousActiveIds;
            private float _elapsedTime;
            private float _duration;
            private TransitionCurve _curve;
            private bool _isComplete;

            public LayerExpressionSource(int blendShapeCount)
            {
                BlendShapeCount = blendShapeCount;
                _snapshotValues = new float[blendShapeCount];
                _targetValues = new float[blendShapeCount];
                _currentValues = new float[blendShapeCount];
                _previousActiveIds = new List<string>();
                _elapsedTime = 0f;
                _duration = 0f;
                _curve = TransitionCurve.Linear;
                _isComplete = true;
                HasBeenActive = false;
            }

            public void UpdateExpressions(
                List<Expression> currentExpressions,
                ExclusionMode exclusionMode,
                string[] blendShapeNames)
            {
                bool changed = DetectExpressionChange(currentExpressions);
                if (changed)
                {
                    Array.Copy(_currentValues, _snapshotValues, _currentValues.Length);
                    ComputeTargetValues(currentExpressions, exclusionMode, blendShapeNames);

                    var lastExpr = currentExpressions[currentExpressions.Count - 1];
                    _duration = lastExpr.TransitionDuration;
                    _curve = lastExpr.TransitionCurve;
                    _elapsedTime = 0f;
                    _isComplete = false;

                    UpdateActiveIds(currentExpressions);
                }

                HasBeenActive = true;
            }

            public void Tick(float deltaTime)
            {
                if (_isComplete)
                    return;

                _elapsedTime += deltaTime;
                float weight = TransitionCalculator.ComputeBlendWeight(_curve, _elapsedTime, _duration);
                ExclusionResolver.ResolveLastWins(_snapshotValues, _targetValues, weight, _currentValues);

                if (_elapsedTime >= _duration)
                {
                    _isComplete = true;
                }
            }

            public bool TryWriteValues(Span<float> output)
            {
                int len = output.Length < _currentValues.Length ? output.Length : _currentValues.Length;
                for (int i = 0; i < len; i++)
                {
                    output[i] = _currentValues[i];
                }
                return true;
            }

            private bool DetectExpressionChange(List<Expression> currentExpressions)
            {
                if (_previousActiveIds.Count != currentExpressions.Count)
                    return true;

                for (int i = 0; i < currentExpressions.Count; i++)
                {
                    if (_previousActiveIds[i] != currentExpressions[i].Id)
                        return true;
                }

                return false;
            }

            private void UpdateActiveIds(List<Expression> expressions)
            {
                _previousActiveIds.Clear();
                for (int i = 0; i < expressions.Count; i++)
                {
                    _previousActiveIds.Add(expressions[i].Id);
                }
            }

            private void ComputeTargetValues(
                List<Expression> expressions,
                ExclusionMode exclusionMode,
                string[] blendShapeNames)
            {
                Array.Clear(_targetValues, 0, _targetValues.Length);

                if (exclusionMode == ExclusionMode.LastWins)
                {
                    var lastExpr = expressions[expressions.Count - 1];
                    MapBlendShapeValues(lastExpr, _targetValues, blendShapeNames);
                }
                else
                {
                    for (int e = 0; e < expressions.Count; e++)
                    {
                        MapBlendShapeValuesAdditive(expressions[e], _targetValues, blendShapeNames);
                    }
                }
            }

            private static void MapBlendShapeValues(Expression expression, float[] target, string[] blendShapeNames)
            {
                var bsSpan = expression.BlendShapeValues.Span;
                for (int v = 0; v < bsSpan.Length; v++)
                {
                    int idx = FindBlendShapeIndex(bsSpan[v].Name, blendShapeNames);
                    if (idx >= 0)
                    {
                        target[idx] = bsSpan[v].Value;
                    }
                }
            }

            private static void MapBlendShapeValuesAdditive(Expression expression, float[] target, string[] blendShapeNames)
            {
                var bsSpan = expression.BlendShapeValues.Span;
                for (int v = 0; v < bsSpan.Length; v++)
                {
                    int idx = FindBlendShapeIndex(bsSpan[v].Name, blendShapeNames);
                    if (idx >= 0)
                    {
                        float sum = target[idx] + bsSpan[v].Value;
                        if (sum < 0f) sum = 0f;
                        else if (sum > 1f) sum = 1f;
                        target[idx] = sum;
                    }
                }
            }

            private static int FindBlendShapeIndex(string name, string[] blendShapeNames)
            {
                for (int i = 0; i < blendShapeNames.Length; i++)
                {
                    if (blendShapeNames[i] == name)
                        return i;
                }
                return -1;
            }
        }
    }
}
