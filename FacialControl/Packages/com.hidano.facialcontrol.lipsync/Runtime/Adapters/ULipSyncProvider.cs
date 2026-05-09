using System;
using System.Collections;
using System.Collections.Generic;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;

namespace Hidano.FacialControl.LipSync.Adapters
{
    public sealed class ULipSyncProvider : ILipSyncProvider, ILipSyncContributeMaskProvider, IDisposable
    {
        private readonly IULipSyncEventSource _eventSource;
        private readonly float[] _accum;
        private readonly BitArray _contributeMask;
        private readonly string[] _blendShapeNames;
        private readonly string[] _phonemeKeys;
        private readonly int[] _phonemeIndices;
        private readonly float[][] _snapshotWeights;
        private readonly int _phonemeCount;
        private bool _zeroOutputRequested;
        private bool _isDisposed;

        public ULipSyncProvider(
            IULipSyncEventSource eventSource,
            IReadOnlyList<PhonemeSnapshot> snapshots,
            int blendShapeCount)
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

            _eventSource = eventSource;
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
            _eventSource.OnLipSyncUpdate += OnLipSyncUpdate;
        }

        public ReadOnlySpan<string> BlendShapeNames => _blendShapeNames;

        public BitArray ContributeMask => _contributeMask;

        public void GetLipSyncValues(Span<float> output)
        {
            if (_zeroOutputRequested)
            {
                output.Clear();
                _zeroOutputRequested = false;
                return;
            }

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

            ClearAccum();
            if (info.phonemeRatios == null)
            {
                return;
            }

            float volume = info.volume;
            for (int i = 0; i < _phonemeCount; i++)
            {
                string phonemeKey = _phonemeKeys[i];
                if (!info.phonemeRatios.TryGetValue(phonemeKey, out float ratio))
                {
                    continue;
                }

                float scaledRatio = ratio * volume;
                float[] weights = _snapshotWeights[_phonemeIndices[i]];
                for (int weightIndex = 0; weightIndex < weights.Length; weightIndex++)
                {
                    _accum[weightIndex] += scaledRatio * weights[weightIndex];
                }
            }
        }

        private void ClearAccum()
        {
            for (int i = 0; i < _accum.Length; i++)
            {
                _accum[i] = 0f;
            }
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

        private static void MarkContributeIndexes(float[] weights, BitArray mask)
        {
            int count = weights.Length < mask.Length ? weights.Length : mask.Length;
            for (int i = 0; i < count; i++)
            {
                if (weights[i] != 0f)
                {
                    mask[i] = true;
                }
            }
        }
    }
}
