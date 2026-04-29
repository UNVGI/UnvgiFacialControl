using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Playable
{
    /// <summary>
    /// FacialController と BoneWriter 統合テスト（Red、PlayMode、Task 8.1）。
    ///
    /// PlayMode 配置の理由: 実 <see cref="UnityEngine.Animator"/> + LateUpdate 同期 +
    /// 実フレーム必要。
    ///
    /// 検証項目:
    ///   - 同一フレーム内で <c>Animator → BlendShape 書込 → BoneWriter.Apply</c> の順で
    ///     実行されること（Req 5.3, 10.3）。最終的に bone Transform が basis 相対の
    ///     合成結果になっていることで順序を間接観測する。
    ///   - 既存 <see cref="Hidano.FacialControl.Adapters.Playable.FacialControlMixer"/> 出力
    ///     （BlendShape weight, layer slots）が BoneWriter 統合後も無変更であること（Req 10.3）。
    ///   - <c>OnDisable</c> で BoneWriter の Restore + Dispose が呼ばれ、書込中だった
    ///     Transform が初期姿勢に戻ること。
    ///   - <see cref="IBonePoseProvider"/> / <see cref="IBonePoseSource"/> が
    ///     <see cref="FacialController"/> 経由で外部から呼出可能であること（Req 11.1, 11.3）。
    ///
    /// _Requirements: 5.3, 10.3, 11.1, 11.2, 11.3
    /// </summary>
    [TestFixture]
    public class FacialControllerBoneIntegrationTests
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
        // FacialController が IBonePoseProvider / IBonePoseSource を実装する
        // ================================================================

        [Test]
        public void FacialController_ImplementsIBonePoseProvider()
        {
            BuildFacialControllerHierarchy(out _, out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();

            Assert.IsTrue(controller is IBonePoseProvider,
                "FacialController は IBonePoseProvider を実装する（Req 11.1, 11.3）。");
        }

        [Test]
        public void FacialController_ImplementsIBonePoseSource()
        {
            BuildFacialControllerHierarchy(out _, out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();

            Assert.IsTrue(controller is IBonePoseSource,
                "FacialController は IBonePoseSource を実装する（Req 5.6, 11.1）。");
        }

        // ================================================================
        // SetActiveBonePose / GetActiveBonePose: 外部から呼出可（Req 11.1, 11.2, 11.3, 11.4）
        // ================================================================

        [UnityTest]
        public IEnumerator SetActiveBonePose_AnyExternalCaller_AppliedFromNextFrame()
        {
            // 任意の MonoBehaviour（テスト用 Fake Provider）から
            // FacialController.SetActiveBonePose(in pose) を呼び、次フレームから
            // Apply() 結果が変わることを検証（Req 11.2, 11.3, 11.4）。
            BuildFacialControllerHierarchy(out var head, out var leftEye, out _, out var profileSO);
            var controller = _root.GetComponent<FacialController>();

            head.localRotation = Quaternion.identity;
            var preApplyLeft = Quaternion.Euler(7f, 8f, 9f);
            leftEye.localRotation = preApplyLeft;

            // 空 BonePoses プロファイルで初期化
            var profile = CreateProfileWithEmptyBonePoses();
            controller.InitializeWithProfile(profile);

            // 次フレーム適用される pose を投入
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 30f, 0f, 0f),
            };
            var pose = new BonePose("staged-via-controller", entries);
            controller.SetActiveBonePose(in pose);

            // 1 フレーム進めて LateUpdate を回す
            yield return null;

            // BoneWriter.Apply が走り leftEye が basis * Euler(30,0,0) に書換えられている
            var expected = Quaternion.identity * Quaternion.Euler(30f, 0f, 0f);
            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                "外部から SetActiveBonePose で投入した pose が次フレームの Apply に反映される");
        }

        [UnityTest]
        public IEnumerator GetActiveBonePose_ReturnsLastSetPose()
        {
            // GetActiveBonePose が直近 SetActiveBonePose 値を返すこと（Req 5.6, 11.1）。
            BuildFacialControllerHierarchy(out _, out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();
            var profile = CreateProfileWithEmptyBonePoses();
            controller.InitializeWithProfile(profile);

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 11f, 22f, 33f),
            };
            var pose = new BonePose("getter-test", entries);
            controller.SetActiveBonePose(in pose);

            // pending → active への swap は Apply 開始時。1 フレーム進めて確実に active 化させる。
            yield return null;

            var got = controller.GetActiveBonePose();
            Assert.AreEqual("getter-test", got.Id, "Id round-trip");
            Assert.AreEqual(1, got.Entries.Length, "Entries 数 round-trip");
            var entry = got.Entries.Span[0];
            Assert.AreEqual("LeftEye", entry.BoneName);
            Assert.That(entry.EulerX, Is.EqualTo(11f).Within(1e-5f));
            Assert.That(entry.EulerY, Is.EqualTo(22f).Within(1e-5f));
            Assert.That(entry.EulerZ, Is.EqualTo(33f).Within(1e-5f));
        }

        // ================================================================
        // Apply 順序: Animator → BlendShape 書込 → BoneWriter.Apply（Req 5.3, 10.3）
        // ================================================================

        [UnityTest]
        public IEnumerator LateUpdate_AppliesBoneWriterAfterBlendShapeWrite()
        {
            // 同一フレーム内で BoneWriter.Apply が LateUpdate 末尾で起動され、
            // 結果が basis 相対で観測できることを検証（Req 5.3, 10.3）。
            BuildFacialControllerHierarchy(out var head, out var leftEye, out _, out _);
            var controller = _root.GetComponent<FacialController>();

            // basis (Head) を tilt させた状態で BoneWriter が basis 相対に
            // 合成することを観測する。
            head.localRotation = Quaternion.Euler(0f, 0f, 25f); // body tilt roll +25deg

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 15f, 0f), // gaze yaw +15deg
            };
            var profile = CreateProfileWithBonePose("frame-order", entries);
            controller.InitializeWithProfile(profile);

            // 1 フレーム進めて LateUpdate を回す
            yield return null;

            // 期待値: tilt 込み basis * relative euler（basis 相対の証拠）
            var expected = head.localRotation * Quaternion.Euler(0f, 15f, 0f);
            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                "LateUpdate 末尾で BoneWriter.Apply が走り、basis 相対の合成結果になる");
        }

        // ================================================================
        // 既存 BlendShape パイプラインの非破壊（Req 10.3）
        // ================================================================

        [UnityTest]
        public IEnumerator LateUpdate_PreservesExistingBlendShapePipeline()
        {
            // BoneWriter 統合後も既存 BlendShape パイプラインが正しく動作する
            // （初期化が成功し、IsInitialized が true）。Req 10.3 の構造的保証。
            BuildFacialControllerHierarchy(out _, out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();
            var profile = CreateProfileWithEmptyBonePoses();

            controller.InitializeWithProfile(profile);

            yield return null;

            Assert.IsTrue(controller.IsInitialized,
                "BoneWriter 統合後も既存 BlendShape パイプラインの Initialize が成功する");
        }

        [UnityTest]
        public IEnumerator LateUpdate_GetActiveExpressionsRemainsFunctional()
        {
            // BoneWriter 統合後も Expression 系 API（GetActiveExpressions / Activate）が
            // 既存どおり動作することを確認（Req 10.3）。
            BuildFacialControllerHierarchy(out _, out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();
            var profile = CreateProfileWithExpressionAndBonePose();
            controller.InitializeWithProfile(profile);

            var expr = profile.Expressions.Span[0];
            controller.Activate(expr);

            yield return null;

            var active = controller.GetActiveExpressions();
            Assert.AreEqual(1, active.Count,
                "BoneWriter 統合後も Activate / GetActiveExpressions が既存どおり動作");
            Assert.AreEqual(expr.Id, active[0].Id);
        }

        // ================================================================
        // OnDisable で Restore + Dispose（Transform を初期姿勢に戻す）
        // ================================================================

        [UnityTest]
        public IEnumerator OnDisable_RestoresPreApplyTransformAndDisposesBoneWriter()
        {
            // OnDisable で BoneWriter.RestoreInitialRotations が呼ばれ、書込中だった
            // Transform が初回書込み直前の値に戻ることを検証。
            BuildFacialControllerHierarchy(out var head, out var leftEye, out _, out _);
            var controller = _root.GetComponent<FacialController>();

            head.localRotation = Quaternion.identity;
            var preApplyLeft = Quaternion.Euler(11f, 22f, 33f);
            leftEye.localRotation = preApplyLeft;

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 45f, 0f, 0f),
            };
            var profile = CreateProfileWithBonePose("restore-test", entries);
            controller.InitializeWithProfile(profile);

            // 1 フレーム進めて Apply を走らせる（leftEye.localRotation が書換えられる）
            yield return null;

            AssertQuaternionNotApprox(preApplyLeft, leftEye.localRotation, 1e-3f,
                "Apply 後は leftEye が書換えられている");

            // OnDisable をトリガー
            controller.enabled = false;

            yield return null;

            // OnDisable 経由で RestoreInitialRotations が呼ばれて初回書込み直前の値に戻る
            AssertQuaternionApprox(preApplyLeft, leftEye.localRotation, 1e-4f,
                "OnDisable で BoneWriter.Restore が呼ばれ、leftEye が初回書込み直前の値に戻る");
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// FacialController + Animator + SkinnedMeshRenderer + Head/LeftEye/RightEye の
        /// Transform 階層を構築。
        /// </summary>
        private void BuildFacialControllerHierarchy(
            out Transform head,
            out Transform leftEye,
            out Transform rightEye,
            out FacialProfileSO profileSO)
        {
            _root = new GameObject("FacialControllerBoneIntegrationRoot");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();

            // FacialController (RequireComponent(Animator) は満たされる)
            _root.AddComponent<FacialController>();

            // SkinnedMeshRenderer 子（既存パイプライン互換のため）
            var meshObj = new GameObject("Mesh");
            meshObj.transform.SetParent(_root.transform, worldPositionStays: false);
            meshObj.AddComponent<SkinnedMeshRenderer>();

            // Bone 階層
            var hips = MakeChild(_root.transform, "Hips");
            var spine = MakeChild(hips, "Spine");
            var neck = MakeChild(spine, "Neck");
            head = MakeChild(neck, "Head");
            leftEye = MakeChild(head, "LeftEye");
            rightEye = MakeChild(head, "RightEye");

            profileSO = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
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
        /// 空 BonePose 配列を持つ最小プロファイル。
        /// </summary>
        private static FacialProfile CreateProfileWithEmptyBonePoses()
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            return new FacialProfile("1.0.0", layers, null, null, null, Array.Empty<BonePose>());
        }

        /// <summary>
        /// 単一 BonePose（id + entries 指定）を持つプロファイル。
        /// </summary>
        private static FacialProfile CreateProfileWithBonePose(string poseId, BonePoseEntry[] entries)
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            var bonePoses = new[]
            {
                new BonePose(poseId, entries)
            };
            return new FacialProfile("1.0.0", layers, null, null, null, bonePoses);
        }

        /// <summary>
        /// Expression と BonePose の両方を持つプロファイル（既存パイプライン非破壊検証用）。
        /// </summary>
        private static FacialProfile CreateProfileWithExpressionAndBonePose()
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            var expressions = new[]
            {
                new Expression("expr-bone-integration", "Smile", "emotion")
            };
            var bonePoses = new[]
            {
                new BonePose("non-regression",
                    new[] { new BonePoseEntry("LeftEye", 0f, 0f, 0f) })
            };
            return new FacialProfile("1.0.0", layers, expressions, null, null, bonePoses);
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
