using System;
using UnityEngine;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// active な BonePose を毎フレーム basis 相対で <see cref="UnityEngine.Transform.localRotation"/> に
    /// 書戻すサービス (Req 5.1〜5.6, 6.1〜6.3, 11.1〜11.5)。
    /// </summary>
    /// <remarks>
    /// preview.1: メインスレッド限定、hot path で alloc しない。
    /// MAJOR-1 反映: <see cref="RestoreInitialRotations"/> は遅延スナップショット方式 (タスク 7.5 / 7.6)。
    /// MINOR-1 反映: <see cref="Initialize"/> で basis をキャッシュ、<see cref="Apply"/> は引数なし (タスク 6.2 で API 形状確定)。
    /// </remarks>
    public sealed class BoneWriter : IBonePoseSource, IBonePoseProvider, IDisposable
    {
        private readonly BoneTransformResolver _resolver;
        private readonly Animator _animator;
        private readonly BonePoseSnapshot _snapshot = new BonePoseSnapshot();

        private BonePose _activePose;
        private BonePose _pendingPose;
        private bool _hasPending;
        private string _basisBoneName;
        private Transform _basisBone;
        private bool _initialized;

        public BoneWriter(BoneTransformResolver resolver, Animator animator)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _animator = animator;
        }

        /// <summary>
        /// 初期 BonePose と basis bone 名で BoneWriter を初期化する。
        /// </summary>
        /// <param name="initialPose">初期 BonePose（空でも可、analog-input-binding が後から流す典型ケース）</param>
        /// <param name="basisBoneName">顔相対軸の基準ボーン名（典型: "Head"）。Initialize 時に basis Transform を解決してキャッシュする</param>
        public void Initialize(in BonePose initialPose, string basisBoneName)
        {
            _activePose = initialPose;
            _basisBoneName = basisBoneName ?? string.Empty;

            if (string.IsNullOrEmpty(_basisBoneName))
            {
                Debug.LogWarning("[BoneWriter] basisBoneName が空のため basis bone を解決できません。Apply は frame ごと skip されます。");
                _basisBone = null;
            }
            else
            {
                _basisBone = _resolver.Resolve(_basisBoneName);
                // 解決失敗時の警告は BoneTransformResolver 側で 1 回だけ出る（dedupe 済み）。
            }

            var entries = initialPose.Entries.Span;
            for (int i = 0; i < entries.Length; i++)
            {
                _ = _resolver.Resolve(entries[i].BoneName);
            }

            _snapshot.EnsureCapacity(entries.Length);
            _initialized = true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// next-frame セマンティクス (Req 11.2): 呼出時点では <c>_pendingPose</c> に格納するのみで、
        /// 次回 <see cref="Apply"/> 開始時に <c>_activePose</c> へ swap する。
        /// 同一フレームで複数回呼ばれた場合は latest-wins（pending は queue ではなく最新値保持）。
        /// メインスレッド限定契約（preview.1）。
        /// </remarks>
        public void SetActiveBonePose(in BonePose pose)
        {
            _pendingPose = pose;
            _hasPending = true;
        }

        /// <inheritdoc />
        public BonePose GetActiveBonePose()
        {
            return _activePose;
        }

        /// <summary>
        /// FacialController.LateUpdate 末尾から呼ばれる毎フレーム適用処理。
        /// </summary>
        /// <remarks>
        /// MINOR-1: 引数なし。Initialize 時にキャッシュした basis Transform の <c>localRotation</c> を 1 回読み、
        /// 各エントリの最終 localRotation を Composer で合成して対象 Transform に書込む。
        /// </remarks>
        public void Apply()
        {
            if (!_initialized)
            {
                return;
            }

            if (_hasPending)
            {
                _activePose = _pendingPose;
                _pendingPose = default;
                _hasPending = false;
            }

            var entries = _activePose.Entries.Span;
            if (entries.Length == 0)
            {
                return;
            }

            if (_basisBone == null)
            {
                // basis 未解決時は world 軸フォールバックしない（Req 4.6）。warning は Initialize で出力済み。
                return;
            }

            var basisRot = _basisBone.localRotation;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var target = _resolver.Resolve(entry.BoneName);
                if (target == null)
                {
                    // bone 名未解決は warning + skip + 続行（Req 2.4）。warning は resolver 側で dedupe 済み。
                    continue;
                }

                BonePoseComposer.Compose(
                    basisRot.x, basisRot.y, basisRot.z, basisRot.w,
                    entry.EulerX, entry.EulerY, entry.EulerZ,
                    out float qx, out float qy, out float qz, out float qw);

                target.localRotation = new Quaternion(qx, qy, qz, qw);
            }
        }

        /// <summary>
        /// 書込中だった bone の <c>localRotation</c> を Initialize 後の最初の書込み直前の値に戻す。
        /// </summary>
        /// <remarks>
        /// MAJOR-1: 遅延スナップショット方式。<see cref="Apply"/> 内で各エントリの初回書込み直前に
        /// <c>_initialSnapshot[boneName] = transform.localRotation</c> を記録し、本メソッドで巡回復元する。
        /// </remarks>
        public void RestoreInitialRotations()
        {
            throw new NotImplementedException("BoneWriter.RestoreInitialRotations はタスク 7.6 で実装する。");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _initialized = false;
            _basisBone = null;
            _pendingPose = default;
            _hasPending = false;
        }
    }
}
