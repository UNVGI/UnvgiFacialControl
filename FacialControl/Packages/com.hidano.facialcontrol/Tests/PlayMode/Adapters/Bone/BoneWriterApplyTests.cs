using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Bone
{
    /// <summary>
    /// BoneWriter の Apply 順序・basis 採取テスト（Red、PlayMode、Task 7.1）。
    ///
    /// PlayMode 配置の理由: 実 <see cref="UnityEngine.Animator"/> + <see cref="UnityEngine.Transform"/>
    /// 階層 + LateUpdate 順序が必要。
    ///
    /// 検証項目:
    ///   - <c>Apply()</c> 開始時に basis bone の <c>localRotation</c> を 1 回だけ採取し、
    ///     entries ループ前にキャッシュすること（Req 5.5、決定的順序）。
    ///   - active BonePose が空 / Entries が空のとき、いかなる Transform にも触れないこと
    ///     （Req 5.4, 10.1）。
    ///   - 解決失敗 boneName のエントリは <see cref="Debug.LogWarning"/> + skip し、
    ///     他のエントリ適用に影響しないこと（Req 2.4）。
    ///   - basis bone 解決失敗時は Warning + そのフレームの bone 適用を全 skip
    ///     （world 軸フォールバックしない、Req 4.6）。
    ///   - Animator が Update で bone を書いた後の値（body tilt 込み）に対して BoneWriter が乗り、
    ///     結果が basis 相対であること（Req 4.2, 4.5, 5.2, 5.3）。
    ///
    /// _Requirements: 2.4, 4.2, 4.5, 4.6, 5.2, 5.3, 5.4, 5.5, 10.1
    /// </summary>
    [TestFixture]
    public class BoneWriterApplyTests
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
        // basis 採取: Apply 開始時に 1 回だけ basis localRotation を読む（Req 5.5）
        // ================================================================

        [Test]
        public void Apply_WithRotatedBasis_ComposesEachEntryAsBasisTimesEulerOffset()
        {
            // basis bone (Head) を回転させた状態で entries を書込む。
            // 各 entry の最終 localRotation は basis * Euler(entry) と一致すること
            // （Req 4.2 / 4.4 / 5.5、Hamilton 積、basis 相対）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.Euler(15f, 20f, -10f);

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 5f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 8f, 0f),
            };
            var pose = new BonePose("rotated-basis", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");
            writer.Apply();

            // 期待値: head.localRotation × Quaternion.Euler(entry)
            var expectedLeft = head.localRotation * Quaternion.Euler(5f, 0f, 0f);
            var expectedRight = head.localRotation * Quaternion.Euler(0f, 8f, 0f);

            AssertQuaternionApprox(expectedLeft, leftEye.localRotation, 1e-4f, "LeftEye = basis * Euler(5,0,0)");
            AssertQuaternionApprox(expectedRight, rightEye.localRotation, 1e-4f, "RightEye = basis * Euler(0,8,0)");
        }

        [Test]
        public void Apply_WithMultipleEntriesAndZeroEuler_AllEntriesMatchBasisRotation()
        {
            // 「basis を Apply 開始時に 1 回だけ採取し全 entries で同じ値を使う」
            // ことの構造的保証: entry の Euler を全て (0,0,0) にすると、
            // 最終 target localRotation はちょうど basis localRotation に一致する。
            // 複数 entries が同じ basis snapshot を参照していれば、両 target が
            // 同じ basis rotation を持つはず（Req 5.5）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.Euler(0f, 30f, 0f);

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 0f, 0f),
            };
            var pose = new BonePose("identity-relative", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");
            writer.Apply();

            AssertQuaternionApprox(head.localRotation, leftEye.localRotation, 1e-4f,
                "LeftEye は basis snapshot と同値（entry euler = 0）");
            AssertQuaternionApprox(head.localRotation, rightEye.localRotation, 1e-4f,
                "RightEye は basis snapshot と同値（entry euler = 0）");
        }

        // ================================================================
        // 空 BonePose: いかなる Transform にも触れない（Req 5.4, 10.1）
        // ================================================================

        [Test]
        public void Apply_WithEmptyEntries_DoesNotModifyAnyTransform()
        {
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            // 観測しやすい局所姿勢を eyes に仕込む
            var preLeft = Quaternion.Euler(11f, 22f, 33f);
            var preRight = Quaternion.Euler(-5f, 6f, -7f);
            leftEye.localRotation = preLeft;
            rightEye.localRotation = preRight;

            // 空 BonePose で Initialize → Apply は no-op であること
            var emptyPose = new BonePose("empty", Array.Empty<BonePoseEntry>());
            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in emptyPose, "Head");

            writer.Apply();

            AssertQuaternionApprox(preLeft, leftEye.localRotation, 1e-5f, "LeftEye 不変（空 BonePose）");
            AssertQuaternionApprox(preRight, rightEye.localRotation, 1e-5f, "RightEye 不変（空 BonePose）");
        }

        [Test]
        public void Apply_WithEmptyEntries_DoesNotModifyBasisBoneEither()
        {
            // basis bone 自身も空 BonePose では書換えられないこと（Req 5.4）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var preHead = Quaternion.Euler(7f, 8f, 9f);
            head.localRotation = preHead;

            var emptyPose = new BonePose("empty", Array.Empty<BonePoseEntry>());
            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in emptyPose, "Head");

            writer.Apply();

            AssertQuaternionApprox(preHead, head.localRotation, 1e-5f, "basis (Head) も不変");
        }

        // ================================================================
        // 解決失敗 boneName: Warning + skip、他エントリ正常適用（Req 2.4）
        // ================================================================

        [Test]
        public void Apply_WithUnresolvedBoneName_SkipsWithWarningAndStillAppliesOthers()
        {
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;
            var preRight = Quaternion.Euler(2f, 3f, 4f);
            rightEye.localRotation = preRight;

            // 1 件目: 解決不能。2 件目: 正常解決。3 件目: 観測用に未参照のまま。
            var entries = new[]
            {
                new BonePoseEntry("NonExistentBone", 10f, 0f, 0f),
                new BonePoseEntry("LeftEye", 5f, 0f, 0f),
            };
            var pose = new BonePose("partial", entries);

            // BoneTransformResolver が dedupe 込みで一度だけ Warning を出す（Req 2.4）。
            LogAssert.Expect(LogType.Warning, new Regex("NonExistentBone"));

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");
            writer.Apply();

            // 解決可能な entry (LeftEye) は適用される
            var expectedLeft = head.localRotation * Quaternion.Euler(5f, 0f, 0f);
            AssertQuaternionApprox(expectedLeft, leftEye.localRotation, 1e-4f,
                "解決可能な LeftEye は他エントリ失敗の影響を受けず適用される");

            // entries に含まれない RightEye は不変
            AssertQuaternionApprox(preRight, rightEye.localRotation, 1e-5f,
                "entries に含まれない RightEye は不変");
        }

        // ================================================================
        // basis bone 解決失敗: Warning + そのフレームの bone 適用を全 skip（Req 4.6）
        // ================================================================

        [Test]
        public void Apply_WithUnresolvedBasisBone_SkipsAllEntriesWithoutWorldFallback()
        {
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            // 観測しやすい局所姿勢を eyes に仕込む
            var preLeft = Quaternion.Euler(3f, 4f, 5f);
            var preRight = Quaternion.Euler(-2f, -1f, 0.5f);
            leftEye.localRotation = preLeft;
            rightEye.localRotation = preRight;

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 10f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 10f, 0f),
            };
            var pose = new BonePose("with-bad-basis", entries);

            // basis 解決失敗時の Warning（resolver と BoneWriter の双方から発生し得るが、
            // dedupe + 警告メッセージの揺れを許容するため ignoreFailingMessages を使う）。
            LogAssert.ignoreFailingMessages = true;

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "NonExistentBasis");
            writer.Apply();

            LogAssert.ignoreFailingMessages = false;

            // basis 解決失敗時は world 軸フォールバックしない → entries は書かれず初期姿勢のまま
            AssertQuaternionApprox(preLeft, leftEye.localRotation, 1e-5f,
                "LeftEye 不変（basis 解決失敗 → world 軸フォールバックしない）");
            AssertQuaternionApprox(preRight, rightEye.localRotation, 1e-5f,
                "RightEye 不変（basis 解決失敗 → world 軸フォールバックしない）");
        }

        [Test]
        public void Apply_WithEmptyBasisBoneName_SkipsAllEntriesWithoutWorldFallback()
        {
            // basisBoneName が空文字（HumanoidBoneAutoAssigner が non-Humanoid に対して
            // empty を返すケース）でも、world 軸フォールバックしないこと（Req 4.6）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var preLeft = Quaternion.Euler(1f, 2f, 3f);
            leftEye.localRotation = preLeft;

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 10f, 0f, 0f),
            };
            var pose = new BonePose("empty-basis", entries);

            LogAssert.ignoreFailingMessages = true;

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, string.Empty);
            writer.Apply();

            LogAssert.ignoreFailingMessages = false;

            AssertQuaternionApprox(preLeft, leftEye.localRotation, 1e-5f,
                "LeftEye 不変（basis 名 empty → 全 skip）");
        }

        // ================================================================
        // Animator → BlendShape → BoneWriter の順序性（Req 4.2, 4.5, 5.2, 5.3）
        //
        // 真の Animator + AnimationClip 駆動はテスト側で AnimatorController を組むのが
        // PlayMode 上で煩雑なため、ここでは「フレーム頭の Animator 駆動済み状態」を
        // 手動で basis bone に書込むことで再現する（Req 5.3 LateUpdate 末尾呼出の前提を満たす）。
        // ================================================================

        [Test]
        public void Apply_WithBodyTiltBasis_AppliesEntriesRelativeToBasisNotWorld()
        {
            // body tilt（roll +25deg）込みの basis localRotation に対して、
            // 顔相対 Euler (gaze yaw +15deg) が乗る。
            // Req 4.5「body tilt が gaze に漏れない」= basis 相対で正しく合成される。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            // Animator が bone を書いたあとの状態を擬似（body tilt 込みの basis localRotation）
            head.localRotation = Quaternion.Euler(0f, 0f, 25f); // roll +25deg

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 15f, 0f), // gaze yaw +15deg (relative)
            };
            var pose = new BonePose("gaze", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");
            writer.Apply();

            // 期待値: tilt 込み basis に対して relative euler が乗る（basis * offset）
            var expected = head.localRotation * Quaternion.Euler(0f, 15f, 0f);
            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                "LeftEye は body tilt 込み basis * gaze relative");

            // tilt なし (identity basis) と比較して結果が異なる（= basis 相対の証拠）
            var withoutTilt = Quaternion.identity * Quaternion.Euler(0f, 15f, 0f);
            AssertQuaternionNotApprox(withoutTilt, leftEye.localRotation, 1e-3f,
                "tilt なし basis (identity) と tilt あり basis では結果が異なる（basis 相対）");
        }

        [Test]
        public void Apply_BasisLocalRotationChangeBetweenFrames_ResultUpdatesWithCurrentBasis()
        {
            // basis localRotation はフレームごとに採取され直す（Animator が毎フレーム書き換える前提）。
            // フレーム間で basis を変更した場合、次の Apply は新しい basis に基づく結果になる
            // （Req 5.2: 毎フレーム書込、Req 5.5: Apply 開始時に basis 採取）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 10f, 0f),
            };
            var pose = new BonePose("frame-test", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");

            // フレーム 1: basis = identity
            head.localRotation = Quaternion.identity;
            writer.Apply();
            var resultFrame1 = leftEye.localRotation;

            // フレーム 2: basis = roll +30deg
            head.localRotation = Quaternion.Euler(0f, 0f, 30f);
            writer.Apply();
            var resultFrame2 = leftEye.localRotation;

            var expectedFrame1 = Quaternion.identity * Quaternion.Euler(0f, 10f, 0f);
            var expectedFrame2 = Quaternion.Euler(0f, 0f, 30f) * Quaternion.Euler(0f, 10f, 0f);

            AssertQuaternionApprox(expectedFrame1, resultFrame1, 1e-4f, "フレーム 1 結果");
            AssertQuaternionApprox(expectedFrame2, resultFrame2, 1e-4f, "フレーム 2 結果");
            AssertQuaternionNotApprox(resultFrame1, resultFrame2, 1e-3f,
                "basis が変わればフレーム間で結果も変わる（毎フレーム basis 採取の証拠）");
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// Root → Hips → Spine → Neck → Head → (LeftEye, RightEye) 階層を構築。
        /// Animator コンポーネントは Root に追加（Avatar 未設定 = Generic）。
        /// </summary>
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
        /// |dot| ≈ 1 を判定する。
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
