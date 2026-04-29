using System;
using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Bone
{
    /// <summary>
    /// SetActiveBonePose / GetActiveBonePose の next-frame セマンティクステスト（Red、PlayMode、Task 7.3）。
    ///
    /// PlayMode 配置の理由: フレーム同期（Apply 呼出を「フレーム」の単位として扱う）が必要。
    ///
    /// 検証項目:
    ///   - <c>SetActiveBonePose(in newPose)</c> 呼出後、まだ <c>Apply</c> が走っていない
    ///     「同一フレーム」の観測（Get / Transform 状態）には反映されないこと（Req 11.2）。
    ///   - 次の <c>Apply</c>（= 次フレームの apply step）から反映されること（Req 11.2）。
    ///   - <c>GetActiveBonePose()</c> が現在 active な pose を返すこと（Req 5.6, 11.1）。
    ///   - 入力源を仮定しない: <see cref="IBonePoseProvider"/> インターフェース経由で
    ///     任意のスクリプト / 任意の MonoBehaviour から呼出可能であること（Req 11.4）。
    ///
    /// _Requirements: 5.6, 11.1, 11.2, 11.3, 11.4
    /// </summary>
    [TestFixture]
    public class BoneWriterProviderTests
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
        // GetActiveBonePose: Initialize 直後の active pose を返す（Req 5.6, 11.1）
        // ================================================================

        [Test]
        public void GetActiveBonePose_AfterInitialize_ReturnsInitialPose()
        {
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 2f, 3f),
            };
            var initialPose = new BonePose("initial", initialEntries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");

            var got = writer.GetActiveBonePose();

            Assert.That(got.Id, Is.EqualTo("initial"),
                "Initialize 直後の Get は initialPose の Id を返す");
            Assert.That(got.Entries.Length, Is.EqualTo(1),
                "Initialize 直後の Get は initialPose の Entries を返す");
            Assert.That(got.Entries.Span[0].BoneName, Is.EqualTo("LeftEye"));
            Assert.That(got.Entries.Span[0].EulerX, Is.EqualTo(1f));
            Assert.That(got.Entries.Span[0].EulerY, Is.EqualTo(2f));
            Assert.That(got.Entries.Span[0].EulerZ, Is.EqualTo(3f));
        }

        [Test]
        public void GetActiveBonePose_AfterInitializeWithEmpty_ReturnsEmptyPose()
        {
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var emptyPose = new BonePose("empty", Array.Empty<BonePoseEntry>());

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in emptyPose, "Head");

            var got = writer.GetActiveBonePose();

            Assert.That(got.Entries.Length, Is.EqualTo(0),
                "空 BonePose で Initialize した直後の Get は空 Entries を返す");
        }

        // ================================================================
        // 同一フレーム内: Set 直後の Apply 未呼出時点では反映されない（Req 11.2）
        // ================================================================

        [Test]
        public void SetActiveBonePose_BeforeNextApply_DoesNotMutateTransformsImmediately()
        {
            // Initialize 後 Apply で初期姿勢を一度反映、その後 Set だけ呼んでも
            // Apply を再呼出しない限り Transform は変化しないこと（next-frame セマンティクス）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 5f, 0f, 0f),
            };
            var initialPose = new BonePose("initial", initialEntries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");
            writer.Apply();

            // 初期 Apply 適用後の状態を捕捉
            var afterFirstApplyLeft = leftEye.localRotation;
            var afterFirstApplyRight = rightEye.localRotation;

            // 別 pose を Set（pending 化のみ、Transform は触らない）
            var newEntries = new[]
            {
                new BonePoseEntry("LeftEye", 30f, 40f, 0f),
                new BonePoseEntry("RightEye", 0f, 25f, 0f),
            };
            var newPose = new BonePose("staged", newEntries);
            writer.SetActiveBonePose(in newPose);

            // Apply を呼ばない限り Transform は変化しないはず
            AssertQuaternionApprox(afterFirstApplyLeft, leftEye.localRotation, 1e-6f,
                "Set のみで Apply 未呼出 → LeftEye.localRotation は変化しない");
            AssertQuaternionApprox(afterFirstApplyRight, rightEye.localRotation, 1e-6f,
                "Set のみで Apply 未呼出 → RightEye.localRotation は変化しない");
        }

        [Test]
        public void GetActiveBonePose_AfterSetWithoutApply_StillReturnsPreviousActivePose()
        {
            // Set した直後（Apply 未呼出）の Get は **以前の** active pose を返す。
            // pending と active の分離が正しく行われていることの構造的保証（Req 11.2）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 2f, 3f),
            };
            var initialPose = new BonePose("initial", initialEntries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");

            var newEntries = new[]
            {
                new BonePoseEntry("RightEye", 9f, 8f, 7f),
            };
            var newPose = new BonePose("pending", newEntries);
            writer.SetActiveBonePose(in newPose);

            var got = writer.GetActiveBonePose();

            Assert.That(got.Id, Is.EqualTo("initial"),
                "Set 後 Apply 未呼出 → Get は依然として initial の Id を返す（pending は active 化されていない）");
            Assert.That(got.Entries.Length, Is.EqualTo(1));
            Assert.That(got.Entries.Span[0].BoneName, Is.EqualTo("LeftEye"),
                "Set 後 Apply 未呼出 → Get の Entries は initial のまま");
        }

        // ================================================================
        // 次フレーム: Set 後の Apply で反映される（Req 11.2）
        // ================================================================

        [Test]
        public void SetActiveBonePose_NextApply_AppliesNewPose()
        {
            // Set → Apply 一連で、Apply 開始時に pending → active swap が走り、
            // 新しい pose が Transform に反映されること。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var initialPose = new BonePose("initial", Array.Empty<BonePoseEntry>());
            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");

            // 「以前のフレーム」の Apply
            writer.Apply();

            // Set + 次の Apply で newPose が active になる
            var newEntries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 12f, 0f),
            };
            var newPose = new BonePose("next-frame", newEntries);
            writer.SetActiveBonePose(in newPose);

            writer.Apply();

            var expectedLeft = head.localRotation * Quaternion.Euler(0f, 12f, 0f);
            AssertQuaternionApprox(expectedLeft, leftEye.localRotation, 1e-4f,
                "次フレームの Apply で newPose が反映され、LeftEye = basis * Euler(0,12,0)");
        }

        [Test]
        public void GetActiveBonePose_AfterSetAndApply_ReturnsNewPose()
        {
            // Set → Apply 後の Get は新しい pose を返す（pending → active swap が完了）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 2f, 3f),
            };
            var initialPose = new BonePose("initial", initialEntries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");

            var newEntries = new[]
            {
                new BonePoseEntry("RightEye", 9f, 8f, 7f),
            };
            var newPose = new BonePose("activated", newEntries);
            writer.SetActiveBonePose(in newPose);
            writer.Apply();

            var got = writer.GetActiveBonePose();

            Assert.That(got.Id, Is.EqualTo("activated"),
                "Set + Apply 後の Get は newPose の Id を返す");
            Assert.That(got.Entries.Length, Is.EqualTo(1));
            Assert.That(got.Entries.Span[0].BoneName, Is.EqualTo("RightEye"),
                "Set + Apply 後の Get は newPose の Entries を返す");
        }

        // ================================================================
        // 入力源を仮定しない: IBonePoseProvider 経由で任意のスクリプトから呼出可能（Req 11.3, 11.4）
        // ================================================================

        [Test]
        public void SetActiveBonePose_ViaIBonePoseProvider_DoesNotAssumeSpecificCaller()
        {
            // 任意のスクリプトが IBonePoseProvider 経由で BonePose を設定できる。
            // 具象 BoneWriter 型ではなくインターフェースに依存する設計の structural assertion（Req 11.3, 11.4）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var emptyPose = new BonePose(string.Empty, Array.Empty<BonePoseEntry>());
            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in emptyPose, "Head");
            writer.Apply();

            // 任意の caller を装う: ここではテストフィクスチャ自体が caller として
            // IBonePoseProvider にのみ依存して呼び出す（input source 非依存）。
            IBonePoseProvider provider = writer;

            var newEntries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 7f, 0f),
            };
            var newPose = new BonePose("via-provider", newEntries);
            provider.SetActiveBonePose(in newPose);

            writer.Apply();

            head.localRotation = Quaternion.identity;
            // ※ Apply は basis 採取済みのため、上書きは next Apply に効く。
            // ここでは Set → Apply で反映が観測できるかのみを検証する。
            var expected = Quaternion.identity * Quaternion.Euler(0f, 7f, 0f);
            // 実 head.localRotation = identity でこの Apply の結果を再採取するため、再度 Apply
            writer.Apply();

            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                "IBonePoseProvider 経由で Set した pose が Apply 後に LeftEye に反映される");
        }

        [Test]
        public void GetActiveBonePose_ViaIBonePoseSource_ReturnsActivePose()
        {
            // 任意の reader が IBonePoseSource 経由で active pose を取得できる（Req 5.6, 11.1）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 2f, 3f),
            };
            var initialPose = new BonePose("readable", initialEntries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");

            IBonePoseSource source = writer;

            var got = source.GetActiveBonePose();

            Assert.That(got.Id, Is.EqualTo("readable"),
                "IBonePoseSource 経由でも active pose の Id が取得できる");
            Assert.That(got.Entries.Length, Is.EqualTo(1));
        }

        // ================================================================
        // 連続呼出時の latest-wins セマンティクス: 最後に Set した pose が next Apply で active になる
        // ================================================================

        [Test]
        public void SetActiveBonePose_CalledMultipleTimesBeforeApply_LatestWins()
        {
            // 同一フレーム内で複数回 Set した場合、最後の Set が次 Apply で active になる
            // （pending は単純な「最新値保持」、queue ではない）。
            BuildEyesUnderHead(out var head, out var leftEye, out var rightEye);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var initialPose = new BonePose("initial", Array.Empty<BonePoseEntry>());
            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");
            writer.Apply();

            var poseA = new BonePose("A", new[] { new BonePoseEntry("LeftEye", 0f, 5f, 0f) });
            var poseB = new BonePose("B", new[] { new BonePoseEntry("LeftEye", 0f, 25f, 0f) });

            writer.SetActiveBonePose(in poseA);
            writer.SetActiveBonePose(in poseB);
            writer.Apply();

            var got = writer.GetActiveBonePose();
            Assert.That(got.Id, Is.EqualTo("B"),
                "連続 Set 後の Apply では最後に Set した pose B が active");

            var expected = head.localRotation * Quaternion.Euler(0f, 25f, 0f);
            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                "Apply 後の LeftEye は最後に Set した pose B の Euler を反映");
        }

        // ================================================================
        // Helpers (BoneWriterApplyTests と同型)
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

        private static void AssertQuaternionApprox(Quaternion expected, Quaternion actual, float tol, string label)
        {
            float dot = expected.x * actual.x + expected.y * actual.y +
                        expected.z * actual.z + expected.w * actual.w;
            float diff = Mathf.Abs(Mathf.Abs(dot) - 1f);
            Assert.That(diff, Is.LessThan(tol),
                $"{label}: 期待値 {expected} と実測値 {actual} が一致しません（|dot|-1={diff}）");
        }
    }
}
