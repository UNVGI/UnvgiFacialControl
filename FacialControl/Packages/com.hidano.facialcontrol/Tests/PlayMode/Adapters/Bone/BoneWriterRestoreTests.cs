using System;
using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Bone
{
    /// <summary>
    /// RestoreInitialRotations 遅延スナップショットテスト（Red、PlayMode、Task 7.5）。
    ///
    /// PlayMode 配置の理由: 実 <see cref="UnityEngine.Transform"/> への書込みと復元の検証が必要。
    ///
    /// 検証項目（MAJOR-1 反映 / 遅延スナップショット方式）:
    ///   - <see cref="BoneWriter.Initialize"/> を空 BonePose で呼び、その後
    ///     <see cref="BoneWriter.SetActiveBonePose"/> で BonePose を流して
    ///     <see cref="BoneWriter.Apply"/> した場合、各エントリの対象 Transform に
    ///     「初回書込み直前」の <c>localRotation</c> が <c>_initialSnapshot[boneName]</c>
    ///     に記録されること（観測は <see cref="BoneWriter.RestoreInitialRotations"/> 経由）。
    ///   - <see cref="BoneWriter.RestoreInitialRotations"/> を呼ぶと、スナップショットを巡回して
    ///     全対象 Transform が初期姿勢に戻ること。
    ///   - 一度も書込みされなかった Transform は <c>_initialSnapshot</c> に登録されず、
    ///     復元時にも触らないこと。
    ///   - 同一 boneName への複数回適用後でも、最初の書込み直前の値で復元されること。
    ///
    /// _Requirements: 5.4, 10.1, 10.3
    /// </summary>
    [TestFixture]
    public class BoneWriterRestoreTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        // ================================================================
        // 遅延スナップショット: Initialize を空 BonePose で呼び、SetActiveBonePose + Apply で
        // 初回書込み直前の localRotation が記録される。Restore で復元される（MAJOR-1 反映）。
        // ================================================================

        [Test]
        public void RestoreInitialRotations_AfterEmptyInitializeAndSetThenApply_RestoresPreApplyLocalRotation()
        {
            // 典型ケース: Initialize は空 BonePose（preview.1 の analog-input-binding が後から
            // SetActiveBonePose で BonePose を流す典型シナリオ）。
            BuildEyesUnderHead(out var head, out var leftEye, out _);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            // basis は identity に固定（snapshot ロジックが basis 依存しないことの確認用）
            head.localRotation = Quaternion.identity;

            // 観測しやすい初期姿勢を eye に仕込む（これが snapshot の対象値）
            var preApplyLeft = Quaternion.Euler(11f, 22f, 33f);
            leftEye.localRotation = preApplyLeft;

            // Initialize は空 BonePose（snapshot は Apply 内で遅延採取される設計）
            var emptyPose = new BonePose("empty", Array.Empty<BonePoseEntry>());
            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in emptyPose, "Head");

            // 後から SetActiveBonePose で実 pose を流して Apply
            var newEntries = new[]
            {
                new BonePoseEntry("LeftEye", 30f, 0f, 0f),
            };
            var newPose = new BonePose("staged", newEntries);
            writer.SetActiveBonePose(in newPose);
            writer.Apply();

            // Apply 後は basis 相対の値で書換えられているはず
            var afterApplyLeft = leftEye.localRotation;
            AssertQuaternionNotApprox(preApplyLeft, afterApplyLeft, 1e-3f,
                "Apply 後の LeftEye は preApply 値と異なる（書換え発生の前提確認）");

            // RestoreInitialRotations で初回書込み直前の値に戻る
            writer.RestoreInitialRotations();

            AssertQuaternionApprox(preApplyLeft, leftEye.localRotation, 1e-5f,
                "Restore 後の LeftEye は初回 Apply 直前の localRotation に戻る");
        }

        // ================================================================
        // 全対象 Transform を巡回復元（Req 5.4, 10.1, 10.3）
        // ================================================================

        [Test]
        public void RestoreInitialRotations_RestoresAllAppliedBones()
        {
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var preLeft = Quaternion.Euler(7f, 0f, 0f);
            var preRight = Quaternion.Euler(0f, 9f, 0f);
            leftEye.localRotation = preLeft;
            rightEye.localRotation = preRight;

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 30f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 30f, 0f),
            };
            var pose = new BonePose("two-eyes", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");
            writer.Apply();

            writer.RestoreInitialRotations();

            AssertQuaternionApprox(preLeft, leftEye.localRotation, 1e-5f,
                "Restore は LeftEye を初回書込み直前の値に戻す");
            AssertQuaternionApprox(preRight, rightEye.localRotation, 1e-5f,
                "Restore は RightEye を初回書込み直前の値に戻す");
        }

        // ================================================================
        // 一度も書込みされなかった Transform は snapshot 非登録、復元で触らない
        // ================================================================

        [Test]
        public void RestoreInitialRotations_DoesNotTouchUntouchedTransforms()
        {
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var preLeft = Quaternion.Euler(2f, 0f, 0f);
            leftEye.localRotation = preLeft;

            // RightEye は書込み対象外（pose に含まれない）。Apply 後・Restore 後を通じて
            // 連続して書換えられる任意の値を観測値として固定する。
            var fixedRight = Quaternion.Euler(45f, 12f, -7f);
            rightEye.localRotation = fixedRight;

            // 1 entry のみ（LeftEye）。RightEye は entries に含まれない
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 20f, 0f, 0f),
            };
            var pose = new BonePose("only-left", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");
            writer.Apply();

            // RightEye は書込まれていない（snapshot 対象外）はず
            AssertQuaternionApprox(fixedRight, rightEye.localRotation, 1e-5f,
                "Apply は RightEye を書換えない（entries 非対象）");

            // テストを観測しやすくするため Restore 直前に RightEye を任意値に動かす。
            // Restore は RightEye を **触らない**（snapshot に登録されていないため）。
            var probedRight = Quaternion.Euler(80f, -33f, 11f);
            rightEye.localRotation = probedRight;

            writer.RestoreInitialRotations();

            // LeftEye は preApply 値に戻る
            AssertQuaternionApprox(preLeft, leftEye.localRotation, 1e-5f,
                "Restore は LeftEye を初回書込み直前の値に戻す");

            // RightEye は Restore 直前に書換えた probedRight のまま
            // （snapshot に登録されていないため Restore で触らない）
            AssertQuaternionApprox(probedRight, rightEye.localRotation, 1e-5f,
                "Restore は entries に含まれない RightEye を触らない（snapshot 非登録）");
        }

        // ================================================================
        // 複数回適用後でも、最初の書込み直前の値で復元される（snapshot は first-write-only）
        // ================================================================

        [Test]
        public void RestoreInitialRotations_AfterMultipleApplies_RestoresFirstSnapshotValue()
        {
            // 同一 boneName への複数回 Apply 後、Restore は **最初の** Apply 直前の値に戻すこと。
            // snapshot は first-write-only（既に key がある場合は上書きしない）の構造的保証。
            BuildEyesUnderHead(out var head, out var leftEye, out _);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            // 最初の Apply 直前の値（snapshot に記録されるべき）
            var firstPreApplyLeft = Quaternion.Euler(11f, 22f, 33f);
            leftEye.localRotation = firstPreApplyLeft;

            // フレーム 1 の pose
            var entriesA = new[]
            {
                new BonePoseEntry("LeftEye", 10f, 0f, 0f),
            };
            var poseA = new BonePose("A", entriesA);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in poseA, "Head");
            writer.Apply();

            // フレーム 2: 別 pose を Set + Apply（pendingPose の処理経路を通すための差替）
            var entriesB = new[]
            {
                new BonePoseEntry("LeftEye", 50f, 25f, 0f),
            };
            var poseB = new BonePose("B", entriesB);
            writer.SetActiveBonePose(in poseB);
            writer.Apply();

            // フレーム 3: さらに別の値で Apply
            var entriesC = new[]
            {
                new BonePoseEntry("LeftEye", -20f, 0f, 60f),
            };
            var poseC = new BonePose("C", entriesC);
            writer.SetActiveBonePose(in poseC);
            writer.Apply();

            // Restore は **最初の** Apply 直前の値に戻る（B / C 直前の値ではない）
            writer.RestoreInitialRotations();

            AssertQuaternionApprox(firstPreApplyLeft, leftEye.localRotation, 1e-5f,
                "Restore は first-write-only スナップショット値（最初の Apply 直前）に戻す");
        }

        // ================================================================
        // Apply 未実行時は snapshot 空、Restore は no-op（一切 Transform に触らない）
        // ================================================================

        [Test]
        public void RestoreInitialRotations_WithoutAnyApply_DoesNotModifyAnyTransform()
        {
            // Initialize 後 Apply を一度も呼ばずに Restore を呼んだ場合、
            // snapshot は空 → 何もしない（no-op、Req 5.4 / 10.1 / 10.3 と整合）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var preLeft = Quaternion.Euler(11f, 22f, 33f);
            var preRight = Quaternion.Euler(-5f, 6f, -7f);
            leftEye.localRotation = preLeft;
            rightEye.localRotation = preRight;

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 30f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 30f, 0f),
            };
            var pose = new BonePose("not-applied-yet", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");

            // Apply を呼ばずに直接 Restore
            writer.RestoreInitialRotations();

            AssertQuaternionApprox(preLeft, leftEye.localRotation, 1e-5f,
                "Apply 未実行 → snapshot 空 → Restore は LeftEye に触らない");
            AssertQuaternionApprox(preRight, rightEye.localRotation, 1e-5f,
                "Apply 未実行 → snapshot 空 → Restore は RightEye に触らない");
        }

        // ================================================================
        // Helpers (BoneWriterApplyTests / BoneWriterProviderTests と同型)
        // ================================================================

        private void BuildEyesUnderHead(out Transform head, out Transform leftEye, out Transform rightEye)
        {
            _root = new GameObject("Root");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();

            var hips = MakeChild(_root.transform, "Hips");
            var spine = MakeChild(hips, "Spine");
            var neck = MakeChild(spine, "Neck");
            head = MakeChild(neck, "Head");
            leftEye = MakeChild(head, "LeftEye");
            rightEye = MakeChild(head, "RightEye");
        }

        private static Transform MakeChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>
        /// 同一回転表現 ±q（4 成分の符号反転）も等価とみなす近似比較。
        /// </summary>
        private static void AssertQuaternionApprox(Quaternion expected, Quaternion actual, float tol, string label)
        {
            float dot = expected.x * actual.x + expected.y * actual.y +
                        expected.z * actual.z + expected.w * actual.w;
            float diff = Mathf.Abs(Mathf.Abs(dot) - 1f);
            Assert.That(diff, Is.LessThan(tol),
                $"{label}: 期待値 {expected} と実測値 {actual} が一致しません（|dot|-1={diff}）");
        }

        /// <summary>
        /// 2 つの quaternion が異なる回転を表すこと（|dot| が 1 から十分離れていること）を判定する。
        /// </summary>
        private static void AssertQuaternionNotApprox(Quaternion expected, Quaternion actual, float tol, string label)
        {
            float dot = expected.x * actual.x + expected.y * actual.y +
                        expected.z * actual.z + expected.w * actual.w;
            float diff = Mathf.Abs(Mathf.Abs(dot) - 1f);
            Assert.That(diff, Is.GreaterThan(tol),
                $"{label}: 期待値 {expected} と実測値 {actual} が同一回転とみなされました（|dot|-1={diff}）");
        }
    }
}
