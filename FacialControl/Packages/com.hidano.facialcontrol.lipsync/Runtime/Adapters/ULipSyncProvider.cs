using System;
using System.Collections;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using UnityEngine;

namespace Hidano.FacialControl.LipSync.Adapters
{
    public sealed class ULipSyncProvider : ILipSyncProvider, ILipSyncContributeMaskProvider, IDisposable
    {
        // 本家 uLipSyncBlendShape.cs:27 と同値。SmoothDamp の追従時間定数 (秒)。
        public const float DefaultSmoothness = 0.05f;

        private readonly IULipSyncEventSource _eventSource;
        private readonly float[] _accum;
        private readonly BitArray _contributeMask;
        private readonly string[] _blendShapeNames;
        private readonly string[] _phonemeKeys;
        private readonly int[] _phonemeIndices;
        private readonly float[][] _snapshotWeights;
        private readonly int _phonemeCount;

        private readonly ITimeProvider _timeProvider;
        private readonly float _smoothness;

        // volume 系 (uLipSyncBlendShape.UpdateVolume と等価)。
        private float _targetVolume;
        private float _smoothedVolume;
        private float _volumeVelocity;

        // phoneme weight 系 (uLipSyncBlendShape.UpdateVowels と等価)。
        // OnLipSyncUpdate で target を更新し、GetLipSyncValues で SmoothDamp + sum=1 正規化する。
        private readonly float[] _phonemeTargetRatios;
        private readonly float[] _phonemeSmoothedWeights;
        private readonly float[] _phonemeVelocities;

        // ITimeProvider.UnscaledTimeSeconds の差分から dt を算出する。
        // 初期値 -1 は「初回呼び出しなので dt=0 で再起動」のセンチネル。
        private double _lastTimeSeconds;

        private bool _zeroOutputRequested;
        private bool _isDisposed;

        public ULipSyncProvider(
            IULipSyncEventSource eventSource,
            IReadOnlyList<PhonemeSnapshot> snapshots,
            int blendShapeCount,
            float smoothness = DefaultSmoothness,
            ITimeProvider timeProvider = null)
        {
            if (eventSource == null)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (blendShapeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blendShapeCount));
            }

            if (smoothness < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(smoothness));
            }

            _eventSource = eventSource;
            _smoothness = smoothness;
            _timeProvider = timeProvider ?? DefaultTimeProvider.Instance;

            _accum = new float[blendShapeCount];
            _contributeMask = new BitArray(blendShapeCount, false);
            _blendShapeNames = new string[blendShapeCount];
            for (int i = 0; i < _blendShapeNames.Length; i++)
            {
                _blendShapeNames[i] = string.Empty;
            }

            int snapshotCount = snapshots.Count;
            _phonemeKeys = snapshotCount == 0 ? Array.Empty<string>() : new string[snapshotCount];
            _phonemeIndices = snapshotCount == 0 ? Array.Empty<int>() : new int[snapshotCount];
            _snapshotWeights = snapshotCount == 0 ? Array.Empty<float[]>() : new float[snapshotCount][];

            int validCount = 0;
            for (int i = 0; i < snapshotCount; i++)
            {
                PhonemeSnapshot snapshot = snapshots[i];
                if (string.IsNullOrEmpty(snapshot.PhonemeId))
                {
                    _snapshotWeights[i] = Array.Empty<float>();
                    continue;
                }

                _snapshotWeights[i] = CopyWeights(snapshot.Weights, blendShapeCount);
                MarkContributeIndexes(_snapshotWeights[i], _contributeMask);
                _phonemeKeys[validCount] = snapshot.PhonemeId;
                _phonemeIndices[validCount] = i;
                validCount++;
            }

            _phonemeCount = validCount;

            _phonemeTargetRatios = validCount == 0 ? Array.Empty<float>() : new float[validCount];
            _phonemeSmoothedWeights = validCount == 0 ? Array.Empty<float>() : new float[validCount];
            _phonemeVelocities = validCount == 0 ? Array.Empty<float>() : new float[validCount];

            _lastTimeSeconds = -1.0;

            _eventSource.OnLipSyncUpdate += OnLipSyncUpdate;
        }

        public ReadOnlySpan<string> BlendShapeNames => _blendShapeNames;

        public BitArray ContributeMask => _contributeMask;

        public void GetLipSyncValues(Span<float> output)
        {
            if (_zeroOutputRequested)
            {
                output.Clear();
                ResetSmoothingState();
                _zeroOutputRequested = false;
                return;
            }

            // dt は ITimeProvider 経由で算出する。初回 (= _lastTimeSeconds < 0) は dt=0 で立ち上げる。
            double now = _timeProvider.UnscaledTimeSeconds;
            float dt = _lastTimeSeconds < 0.0 ? 0f : (float)(now - _lastTimeSeconds);
            if (dt < 0f) dt = 0f;
            _lastTimeSeconds = now;

            // (a) volume の SmoothDamp。本家 uLipSyncBlendShape.cs:117 と等価。
            _smoothedVolume = Mathf.SmoothDamp(
                _smoothedVolume, _targetVolume, ref _volumeVelocity,
                _smoothness, Mathf.Infinity, dt);

            // (b) 各 phoneme weight の SmoothDamp。本家 uLipSyncBlendShape.cs:139-142 と等価。
            float sum = 0f;
            for (int i = 0; i < _phonemeCount; i++)
            {
                _phonemeSmoothedWeights[i] = Mathf.SmoothDamp(
                    _phonemeSmoothedWeights[i], _phonemeTargetRatios[i],
                    ref _phonemeVelocities[i], _smoothness, Mathf.Infinity, dt);
                sum += _phonemeSmoothedWeights[i];
            }

            // (c) 母音 sum=1 正規化。本家 uLipSyncBlendShape.cs:145-148 と等価。
            if (sum > 0f)
            {
                float inv = 1f / sum;
                for (int i = 0; i < _phonemeCount; i++)
                {
                    _phonemeSmoothedWeights[i] *= inv;
                }
            }

            // (d) _accum 組み立て: factor_i = smoothedWeight_i * smoothedVolume。
            // 本家 uLipSyncBlendShape.cs:173 (bs.weight * bs.maxWeight * volume) と等価な合成。
            Array.Clear(_accum, 0, _accum.Length);
            for (int i = 0; i < _phonemeCount; i++)
            {
                float factor = _phonemeSmoothedWeights[i] * _smoothedVolume;
                if (factor == 0f) continue;
                float[] weights = _snapshotWeights[_phonemeIndices[i]];
                for (int k = 0; k < weights.Length; k++)
                {
                    _accum[k] += factor * weights[k];
                }
            }

            // (e) output コピー。
            int copyLength = output.Length < _accum.Length ? output.Length : _accum.Length;
            for (int i = 0; i < copyLength; i++)
            {
                output[i] = _accum[i];
            }
        }

        public void RequestZeroOutputForNextFrame()
        {
            _zeroOutputRequested = true;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _eventSource.OnLipSyncUpdate -= OnLipSyncUpdate;
            _isDisposed = true;
        }

        private void OnLipSyncUpdate(uLipSync.LipSyncInfo info)
        {
            if (_isDisposed)
            {
                return;
            }

            // target のみ更新する。実際の SmoothDamp は GetLipSyncValues 側で
            // フレーム同期した dt を使って適用される。
            _targetVolume = info.volume;

            if (info.phonemeRatios == null)
            {
                for (int i = 0; i < _phonemeCount; i++)
                {
                    _phonemeTargetRatios[i] = 0f;
                }
                return;
            }

            for (int i = 0; i < _phonemeCount; i++)
            {
                _phonemeTargetRatios[i] = info.phonemeRatios.TryGetValue(_phonemeKeys[i], out float ratio)
                    ? ratio
                    : 0f;
            }
        }

        // device swap / Dispose 直前等で前回状態を完全に消すための共通リセット。
        // velocity も含めて 0 に戻すことで、再開時の SmoothDamp 立ち上がりを初回相当にする。
        private void ResetSmoothingState()
        {
            _targetVolume = 0f;
            _smoothedVolume = 0f;
            _volumeVelocity = 0f;
            for (int i = 0; i < _phonemeCount; i++)
            {
                _phonemeTargetRatios[i] = 0f;
                _phonemeSmoothedWeights[i] = 0f;
                _phonemeVelocities[i] = 0f;
            }
            Array.Clear(_accum, 0, _accum.Length);
            _lastTimeSeconds = -1.0;
        }

        private static float[] CopyWeights(float[] source, int blendShapeCount)
        {
            if (blendShapeCount == 0)
            {
                return Array.Empty<float>();
            }

            var weights = new float[blendShapeCount];
            if (source == null)
            {
                return weights;
            }

            int copyLength = source.Length < blendShapeCount ? source.Length : blendShapeCount;
            for (int i = 0; i < copyLength; i++)
            {
                weights[i] = source[i];
            }

            return weights;
        }

        // ContributeMask に取り込む閾値。これ未満の |weights[i]| は「触らない BlendShape」と扱う。
        // Animation Recording で誤って小さな値 (= 0.0001 程度) が混入したスナップショットが
        // ContributeMask を膨らませて、表情レイヤー側の BlendShape を不必要に上書きするのを防ぐ。
        // <see cref="LipSyncInputSource.SilenceThreshold"/> と同値で整合させる。
        private const float ContributeThreshold = 1e-4f;

        private static void MarkContributeIndexes(float[] weights, BitArray mask)
        {
            int count = weights.Length < mask.Length ? weights.Length : mask.Length;
            for (int i = 0; i < count; i++)
            {
                float w = weights[i];
                if (w < 0f) w = -w;
                if (w > ContributeThreshold)
                {
                    mask[i] = true;
                }
            }
        }

        // ITimeProvider が注入されなかった場合のフォールバック。
        // LayerInputSourceAggregator.DefaultTimeProvider と同じパターンで、
        // 単独で使えるようにする。
        private sealed class DefaultTimeProvider : ITimeProvider
        {
            public static readonly DefaultTimeProvider Instance = new DefaultTimeProvider();
            private DefaultTimeProvider() { }
            public double UnscaledTimeSeconds => UnityEngine.Time.unscaledTimeAsDouble;
        }
    }
}
