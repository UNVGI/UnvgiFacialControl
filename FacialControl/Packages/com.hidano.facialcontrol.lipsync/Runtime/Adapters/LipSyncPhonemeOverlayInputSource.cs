using System;
using System.Collections;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.LipSync.Adapters
{
    public sealed class LipSyncPhonemeOverlayInputSource : ValueProviderInputSourceBase
    {
        public const string SlugPrefix = "lipsync-overlay";

        // provider が出す合成値（uLipSync 委譲後の volume × phoneme weight × snapshot）の総和が
        // この閾値を下回るフレームはレイヤー合成に乗せない。無音時に下位レイヤーを上書きしない契約。
        public const float SilenceThreshold = 1e-4f;

        private readonly string _phonemeId;
        private readonly ULipSyncProvider _provider;
        private readonly BitArray _contributeMask;
        private readonly float[] _scratch;
        private readonly bool _phonemeRegistered;

        public LipSyncPhonemeOverlayInputSource(
            InputSourceId id,
            string phonemeId,
            ULipSyncProvider provider,
            int blendShapeCount)
            : base(id, blendShapeCount)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _phonemeId = phonemeId;
            _provider = provider;
            _scratch = new float[blendShapeCount];
            _phonemeRegistered = provider.TryGetPhonemeIndex(phonemeId, out _);
            _contributeMask = _phonemeRegistered
                ? ResolveContributeMask(provider, phonemeId, blendShapeCount)
                : new BitArray(blendShapeCount, false);
        }

        public override BitArray ContributeMask => _contributeMask;

        public override bool TryWriteValues(Span<float> output)
        {
            if (!_phonemeRegistered)
            {
                return false;
            }

            Span<float> scratch = _scratch;
            scratch.Clear();
            if (!_provider.TryComposePhonemeWeights(_phonemeId, scratch))
            {
                return false;
            }

            float sum = 0f;
            for (int i = 0; i < scratch.Length; i++)
            {
                sum += scratch[i];
            }

            if (sum < SilenceThreshold)
            {
                return false;
            }

            int copyLength = output.Length < scratch.Length ? output.Length : scratch.Length;
            for (int i = 0; i < copyLength; i++)
            {
                output[i] = scratch[i];
            }

            return true;
        }

        private static BitArray ResolveContributeMask(
            ULipSyncProvider provider,
            string phonemeId,
            int blendShapeCount)
        {
            BitArray mask = provider.GetPhonemeContributeMask(phonemeId);
            if (mask == null)
            {
                return new BitArray(blendShapeCount, false);
            }

            if (mask.Length != blendShapeCount)
            {
                throw new ArgumentException(
                    "provider phoneme ContributeMask.Length must match blendShapeCount.",
                    nameof(provider));
            }

            return mask;
        }
    }
}
