using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Playables;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Adapters.Playable
{
    /// <summary>
    /// PlayableGraph のルートノードとして機能する ScriptPlayable。
    /// 複数の LayerPlayable からの出力をレイヤー優先度に基づいてブレンドして最終出力を生成する。
    /// <para>
    /// <see cref="Hidano.FacialControl.Domain.Models.LayerOverrideMask"/> による他レイヤー抑制は
    /// Animator ベース経路（<c>LayerUseCase</c>）で適用される。Playable/Timeline 経路（本クラス）は
    /// 将来対応で、現状 OverrideMask を解釈しない。
    /// </para>
    /// </summary>
    public class FacialControlMixer : PlayableBehaviour, IDisposable
    {
        private int _blendShapeCount;
        private string[] _blendShapeNames;
        private FacialCharacterProfileSO _characterProfile;
        private LayerInputSourceAggregator _aggregator;

        // 最終出力バッファ
        private NativeArray<float> _outputWeights;

        // 登録レイヤー情報
        private readonly List<LayerEntry> _layers = new List<LayerEntry>();

        // ComputeOutput 用の事前確保バッファ
        private LayerBlender.LayerInput[] _layerInputBuffer;
        private float[] _outputBuffer;
        private float[][] _layerValueBuffers;
        private int[] _aggregationPriorities;
        private float[] _aggregationLayerWeights;
        private float[] _baseExpressionValues;

        private bool _disposed;

        private void EnsureOutputBuffer()
        {
            if (_outputBuffer == null || _outputBuffer.Length < _blendShapeCount)
            {
                _outputBuffer = new float[_blendShapeCount];
            }
        }

        private void InitializeOutputBuffer()
        {
            Array.Copy(_baseExpressionValues, _outputBuffer, _blendShapeCount);
        }

        private void CopyOutputBufferToNativeArray()
        {
            for (int i = 0; i < _blendShapeCount; i++)
            {
                _outputWeights[i] = _outputBuffer[i];
            }
        }

        /// <summary>
        /// 最終出力 BlendShape ウェイト配列（読み取り専用）。
        /// </summary>
        public NativeArray<float> OutputWeights => _outputWeights;

        /// <summary>
        /// BlendShape 数。
        /// </summary>
        public int BlendShapeCount => _blendShapeCount;

        /// <summary>
        /// 登録済みレイヤー数。
        /// </summary>
        public int LayerCount => _layers.Count;

        /// <summary>
        /// BlendShape 名のリスト。
        /// </summary>
        public ReadOnlySpan<string> BlendShapeNames => _blendShapeNames;

        /// <summary>
        /// FacialControlMixer を生成し、PlayableGraph に追加する。
        /// </summary>
        /// <param name="graph">PlayableGraph</param>
        /// <param name="blendShapeNames">全 BlendShape 名のリスト</param>
        /// <returns>生成された ScriptPlayable</returns>
        public static ScriptPlayable<FacialControlMixer> Create(
            PlayableGraph graph,
            string[] blendShapeNames)
        {
            var playable = ScriptPlayable<FacialControlMixer>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Initialize(blendShapeNames, null, null, null, null);
            return playable;
        }

        public static ScriptPlayable<FacialControlMixer> Create(
            PlayableGraph graph,
            string[] blendShapeNames,
            FacialCharacterProfileSO characterProfile)
        {
            var playable = ScriptPlayable<FacialControlMixer>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Initialize(blendShapeNames, characterProfile, null, null, null);
            return playable;
        }

        public static ScriptPlayable<FacialControlMixer> Create(
            PlayableGraph graph,
            string[] blendShapeNames,
            LayerInputSourceAggregator aggregator,
            int[] priorities,
            float[] layerWeights)
        {
            var playable = ScriptPlayable<FacialControlMixer>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Initialize(blendShapeNames, null, aggregator, priorities, layerWeights);
            return playable;
        }

        public static ScriptPlayable<FacialControlMixer> Create(
            PlayableGraph graph,
            string[] blendShapeNames,
            LayerInputSourceAggregator aggregator)
        {
            var playable = ScriptPlayable<FacialControlMixer>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Initialize(blendShapeNames, null, aggregator, priorities: null, layerWeights: null);
            return playable;
        }

        public static ScriptPlayable<FacialControlMixer> Create(
            PlayableGraph graph,
            string[] blendShapeNames,
            FacialCharacterProfileSO characterProfile,
            LayerInputSourceAggregator aggregator,
            int[] priorities,
            float[] layerWeights)
        {
            var playable = ScriptPlayable<FacialControlMixer>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Initialize(blendShapeNames, characterProfile, aggregator, priorities, layerWeights);
            return playable;
        }

        public static ScriptPlayable<FacialControlMixer> Create(
            PlayableGraph graph,
            string[] blendShapeNames,
            FacialCharacterProfileSO characterProfile,
            LayerInputSourceAggregator aggregator)
        {
            var playable = ScriptPlayable<FacialControlMixer>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Initialize(blendShapeNames, characterProfile, aggregator, priorities: null, layerWeights: null);
            return playable;
        }

        private void Initialize(
            string[] blendShapeNames,
            FacialCharacterProfileSO characterProfile,
            LayerInputSourceAggregator aggregator,
            int[] priorities,
            float[] layerWeights)
        {
            _blendShapeNames = blendShapeNames ?? Array.Empty<string>();
            _blendShapeCount = _blendShapeNames.Length;
            _characterProfile = characterProfile;
            _aggregator = aggregator;
            _aggregationPriorities = CopyOrEmpty(priorities);
            _aggregationLayerWeights = CopyOrEmpty(layerWeights);
            BuildBaseExpressionValues();

            if (_blendShapeCount > 0)
            {
                _outputWeights = new NativeArray<float>(_blendShapeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
            else
            {
                _outputWeights = new NativeArray<float>(0, Allocator.Persistent);
            }
        }

        /// <summary>
        /// レイヤーを登録する。
        /// </summary>
        /// <param name="layerName">レイヤー名</param>
        /// <param name="priority">優先度（値が大きいほど優先）</param>
        /// <param name="weight">レイヤーウェイト（0〜1）</param>
        /// <param name="layerPlayable">対応する LayerPlayable</param>
        public void RegisterLayer(string layerName, int priority, float weight, ScriptPlayable<LayerPlayable> layerPlayable)
        {
            _layers.Add(new LayerEntry(layerName, priority, weight, layerPlayable));
        }

        /// <summary>
        /// レイヤーウェイトを設定する。
        /// </summary>
        /// <param name="layerName">レイヤー名</param>
        /// <param name="weight">新しいウェイト（0〜1）</param>
        public void SetLayerWeight(string layerName, float weight)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].Name == layerName)
                {
                    _layers[i] = new LayerEntry(
                        _layers[i].Name,
                        _layers[i].Priority,
                        weight,
                        _layers[i].Playable);
                    if (_aggregationLayerWeights != null && i < _aggregationLayerWeights.Length)
                    {
                        _aggregationLayerWeights[i] = weight;
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// PlayableGraph のフレーム準備コールバック。
        /// 毎フレーム全レイヤーの遷移を進め、最終出力を計算する。
        /// </summary>
        public override void PrepareFrame(UnityEngine.Playables.Playable playable, FrameData info)
        {
            float deltaTime = info.deltaTime;

            // 全レイヤーの遷移を進める
            for (int i = 0; i < _layers.Count; i++)
            {
                var behaviour = _layers[i].Playable.GetBehaviour();
                behaviour.UpdateTransition(deltaTime);
            }

            // レイヤー出力をブレンドして最終出力を計算
            ComputeOutput(deltaTime);
        }

        /// <summary>
        /// 全レイヤーの出力をブレンドして最終出力を計算する。
        /// </summary>
        public void ComputeOutput()
        {
            ComputeOutput(0f);
        }

        public void ComputeOutput(float deltaTime)
        {
            if (_blendShapeCount == 0)
            {
                return;
            }

            EnsureOutputBuffer();
            InitializeOutputBuffer();

            if (_aggregator != null)
            {
                EnsureAggregationMetadata();
                _aggregator.AggregateAndBlend(
                    deltaTime,
                    _aggregationPriorities,
                    _aggregationLayerWeights,
                    _outputBuffer);
                CopyOutputBufferToNativeArray();
                return;
            }

            if (_layers.Count == 0)
            {
                CopyOutputBufferToNativeArray();
                return;
            }

            // 事前確保バッファのサイズが合わなければ再確保
            EnsureBuffers();

            for (int i = 0; i < _layers.Count; i++)
            {
                var entry = _layers[i];
                var layerBehaviour = entry.Playable.GetBehaviour();
                var layerOutput = layerBehaviour.OutputWeights;

                // NativeArray から事前確保 float[] にコピー
                var values = _layerValueBuffers[i];
                int copyLen = Math.Min(_blendShapeCount, layerOutput.Length);
                for (int j = 0; j < copyLen; j++)
                {
                    values[j] = layerOutput[j];
                }
                // 残りをゼロクリア
                for (int j = copyLen; j < _blendShapeCount; j++)
                {
                    values[j] = 0f;
                }

                _layerInputBuffer[i] = new LayerBlender.LayerInput(
                    entry.Priority,
                    entry.Weight,
                    values);
            }

            // 出力バッファをゼロクリア
            InitializeOutputBuffer();

            // Domain サービスでブレンド計算
            LayerBlender.Blend(_layerInputBuffer, _outputBuffer);

            // 結果を NativeArray にコピー
            for (int i = 0; i < _blendShapeCount; i++)
            {
                _outputWeights[i] = _outputBuffer[i];
            }
        }

        /// <summary>
        /// ComputeOutput 用の内部バッファを確保・再確保する。
        /// レイヤー数や BlendShape 数が変わった場合のみ再確保する。
        /// </summary>
        private void EnsureBuffers()
        {
            int layerCount = _layers.Count;

            EnsureOutputBuffer();

            if (_layerInputBuffer == null || _layerInputBuffer.Length < layerCount)
            {
                _layerInputBuffer = new LayerBlender.LayerInput[layerCount];
            }

            if (_layerValueBuffers == null || _layerValueBuffers.Length < layerCount)
            {
                var newBuffers = new float[layerCount][];
                if (_layerValueBuffers != null)
                {
                    Array.Copy(_layerValueBuffers, newBuffers, _layerValueBuffers.Length);
                }
                _layerValueBuffers = newBuffers;
            }

            for (int i = 0; i < layerCount; i++)
            {
                if (_layerValueBuffers[i] == null || _layerValueBuffers[i].Length < _blendShapeCount)
                {
                    _layerValueBuffers[i] = new float[_blendShapeCount];
                }
            }
        }

        /// <summary>
        /// Aggregator 直接経路に渡すレイヤーメタデータを確保する。
        /// </summary>
        private void EnsureAggregationMetadata()
        {
            int layerCount = _aggregator != null ? _aggregator.LayerCount : _layers.Count;
            if (layerCount == 0)
            {
                return;
            }

            if (_aggregationPriorities == null || _aggregationPriorities.Length < layerCount)
            {
                _aggregationPriorities = new int[layerCount];
                for (int i = 0; i < layerCount; i++)
                {
                    _aggregationPriorities[i] = i < _layers.Count ? _layers[i].Priority : i;
                }
            }

            if (_aggregationLayerWeights == null || _aggregationLayerWeights.Length < layerCount)
            {
                _aggregationLayerWeights = new float[layerCount];
                for (int i = 0; i < layerCount; i++)
                {
                    _aggregationLayerWeights[i] = i < _layers.Count ? _layers[i].Weight : 1f;
                }
            }
        }

        private void BuildBaseExpressionValues()
        {
            _baseExpressionValues = new float[_blendShapeCount];
            if (_characterProfile == null || _blendShapeCount == 0)
            {
                return;
            }

            var baseExpression = _characterProfile.BaseExpression;
            if (baseExpression == null || baseExpression.animationClip == null)
            {
                return;
            }

            var snapshot = baseExpression.EnsureCachedSnapshot();
            var blendShapes = snapshot.blendShapes;
            if (blendShapes == null || blendShapes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < blendShapes.Count; i++)
            {
                var item = blendShapes[i];
                if (item == null || string.IsNullOrEmpty(item.name))
                {
                    continue;
                }

                int index = FindBlendShapeIndex(item.name);
                if (index >= 0)
                {
                    _baseExpressionValues[index] = Clamp01(item.value);
                }
            }
        }

        private int FindBlendShapeIndex(string name)
        {
            for (int i = 0; i < _blendShapeNames.Length; i++)
            {
                if (_blendShapeNames[i] == name)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int[] CopyOrEmpty(int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<int>();
            }

            var copy = new int[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static float[] CopyOrEmpty(float[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<float>();
            }

            var copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        /// <summary>
        /// NativeArray リソースを解放する。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_outputWeights.IsCreated)
            {
                _outputWeights.Dispose();
            }

            _layers.Clear();
            _disposed = true;
        }

        public override void OnPlayableDestroy(UnityEngine.Playables.Playable playable)
        {
            Dispose();
        }

        /// <summary>
        /// レイヤー登録情報。
        /// </summary>
        private struct LayerEntry
        {
            public string Name;
            public int Priority;
            public float Weight;
            public ScriptPlayable<LayerPlayable> Playable;

            public LayerEntry(string name, int priority, float weight, ScriptPlayable<LayerPlayable> playable)
            {
                Name = name;
                Priority = priority;
                Weight = weight;
                Playable = playable;
            }
        }
    }
}
