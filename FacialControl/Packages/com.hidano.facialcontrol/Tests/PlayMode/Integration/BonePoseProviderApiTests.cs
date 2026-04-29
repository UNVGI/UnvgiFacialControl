using System;
using System.Collections;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Integration
{
    /// <summary>
    /// Task 10.2: IBonePoseProvider 経由の analog-input-binding 互換性検証（PlayMode）。
    ///
    /// 本テストは <c>analog-input-binding</c> spec が消費する API surface
    /// （<see cref="IBonePoseProvider"/> / <see cref="IBonePoseSource"/>）の
    /// 安定性と契約を保証する。
    ///
    /// 検証項目:
    ///   - 任意の MonoBehaviour（テスト用 Fake Provider）から
    ///     <see cref="FacialController.SetActiveBonePose"/> を呼び、次フレームから
    ///     <c>Apply()</c> 結果が変わること（Req 11.2, 11.3, 11.4）。
    ///   - 入力源を仮定しない汎用 API であること（Fake Provider が
    ///     <see cref="IBonePoseProvider"/> のみに依存して動作可能であることを
    ///     型レベルで確認）（Req 11.4）。
    ///   - hot path（Set/Apply）で GC alloc が 0 バイトであること（Req 11.5）。
    ///
    /// _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5
    /// </summary>
    [TestFixture]
    public class BonePoseProviderApiTests
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
        // 任意の MonoBehaviour（Fake Provider）から SetActiveBonePose を呼べる
        // → 次フレームの Apply で反映される（Req 11.2, 11.3, 11.4）
        // ================================================================

        [UnityTest]
        public IEnumerator FakeMonoBehaviourProvider_SetActiveBonePose_AppliedFromNextFrame()
        {
            BuildFacialControllerHierarchy(out var head, out var leftEye, out _);
            var controller = _root.GetComponent<FacialController>();

            head.localRotation = Quaternion.identity;
            leftEye.localRotation = Quaternion.identity;

            controller.InitializeWithProfile(CreateEmptyBonePoseProfile());

            // analog-input-binding を模した Fake Provider をアタッチ。
            // 具象 FacialController に直接依存せず、IBonePoseProvider のみで結線する。
            var fake = _root.AddComponent<FakeAnalogBindingProvider>();
            fake.Bind(controller);

            // Fake から pose を投入（同一フレーム内）
            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 33f, 0f),
            };
            var pose = new BonePose("fake-staged", entries);
            fake.PushPose(in pose);

            // 1 フレーム進めて FacialController.LateUpdate → BoneWriter.Apply を回す
            yield return null;

            var expected = Quaternion.identity * Quaternion.Euler(0f, 33f, 0f);
            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                "Fake MonoBehaviour Provider が IBonePoseProvider 経由で投入した pose は次フレームの Apply で leftEye に反映される");
        }

        [UnityTest]
        public IEnumerator FakeMonoBehaviourProvider_SameFrameSet_NotReflectedUntilNextFrame()
        {
            // Set 直後（同一フレーム内、まだ LateUpdate が回っていない時点）の
            // 観測値が「以前のフレーム」の Apply 結果のままであること。
            // next-frame セマンティクス（Req 11.2）の API surface 検証。
            BuildFacialControllerHierarchy(out var head, out var leftEye, out _);
            var controller = _root.GetComponent<FacialController>();

            head.localRotation = Quaternion.identity;

            // 初期 BonePose を適用済みにしておく
            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 5f, 0f),
            };
            var initialProfile = CreateProfileWithBonePose("initial", initialEntries);
            controller.InitializeWithProfile(initialProfile);

            // 1 フレーム進めて initialPose を Apply 反映
            yield return null;

            var afterInitialLeft = leftEye.localRotation;

            // ここで Fake Provider が新しい pose を投入する（同一フレーム内、Apply 未呼出）
            var fake = _root.AddComponent<FakeAnalogBindingProvider>();
            fake.Bind(controller);

            var stagedEntries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 50f, 0f),
            };
            var stagedPose = new BonePose("staged", stagedEntries);
            fake.PushPose(in stagedPose);

            // この時点（次フレームの LateUpdate 前）では leftEye は依然として initial の値
            AssertQuaternionApprox(afterInitialLeft, leftEye.localRotation, 1e-6f,
                "SetActiveBonePose を呼んでも、その同一フレームの観測時点では Transform は変化しない（next-frame セマンティクス）");

            // 次フレームで反映される
            yield return null;

            var expected = Quaternion.identity * Quaternion.Euler(0f, 50f, 0f);
            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                "次フレームの LateUpdate で staged pose が Apply に反映される");
        }

        // ================================================================
        // 入力源を仮定しない汎用 API: IBonePoseProvider のみへの依存で動く
        // （Fake Provider のシグネチャレビュー、Req 11.4）
        // ================================================================

        [Test]
        public void FakeProvider_DependsOnlyOnIBonePoseProvider_NotConcreteFacialController()
        {
            // FakeAnalogBindingProvider.Bind の引数型・保持フィールド型が
            // IBonePoseProvider である（具象 BoneWriter / FacialController に依存しない）
            // ことを reflection で検証する。これにより analog-input-binding spec が
            // 任意の Provider 実装に対して動作可能であることを構造的に保証する。
            var fakeType = typeof(FakeAnalogBindingProvider);

            var bindMethod = fakeType.GetMethod(
                "Bind",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(bindMethod, "FakeAnalogBindingProvider.Bind が存在する");

            var parameters = bindMethod.GetParameters();
            Assert.AreEqual(1, parameters.Length, "Bind は 1 引数を取る");
            Assert.AreEqual(typeof(IBonePoseProvider), parameters[0].ParameterType,
                "Bind の引数型は IBonePoseProvider（具象 FacialController / BoneWriter に依存しない）");

            var providerField = fakeType.GetField(
                "_provider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(providerField, "FakeAnalogBindingProvider._provider が存在する");
            Assert.AreEqual(typeof(IBonePoseProvider), providerField.FieldType,
                "_provider フィールド型は IBonePoseProvider（入力源非依存の API surface）");
        }

        [Test]
        public void FacialController_AssignableToIBonePoseProvider_ForExternalConsumers()
        {
            // analog-input-binding 等の外部 spec が FacialController を
            // IBonePoseProvider として参照できることの構造的保証（Req 11.1, 11.3）。
            BuildFacialControllerHierarchy(out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();

            IBonePoseProvider provider = controller;
            Assert.IsNotNull(provider, "FacialController は IBonePoseProvider として代入可能");

            IBonePoseSource source = controller;
            Assert.IsNotNull(source, "FacialController は IBonePoseSource として代入可能");
        }

        // ================================================================
        // hot path（Set/Apply）で GC alloc 0 バイト（Req 11.5、Task 7.7 と
        // 重複だが API 経路 = FacialController 経由で再確認）
        // ================================================================

        [UnityTest]
        public IEnumerator HotPath_SetAndApplyViaController_ZeroGCAllocation()
        {
            BuildFacialControllerHierarchy(out var head, out _, out _);
            var controller = _root.GetComponent<FacialController>();

            head.localRotation = Quaternion.identity;

            controller.InitializeWithProfile(CreateEmptyBonePoseProfile());

            var poseAEntries = new[]
            {
                new BonePoseEntry("LeftEye", 5f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 5f, 0f),
            };
            var poseA = new BonePose("A", poseAEntries);

            var poseBEntries = new[]
            {
                new BonePoseEntry("LeftEye", -5f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, -5f, 0f),
            };
            var poseB = new BonePose("B", poseBEntries);

            // ウォームアップ: pending → active swap、_initialSnapshot 等を安定化
            controller.SetActiveBonePose(in poseA);
            yield return null;
            controller.SetActiveBonePose(in poseB);
            yield return null;
            controller.SetActiveBonePose(in poseA);
            yield return null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

            // FacialController 経由で Set を 100 回行い、それぞれ
            // GetActiveBonePose 経由の Read 経路も叩く（Req 11.1 / 11.5）。
            for (int i = 0; i < 100; i++)
            {
                if ((i & 1) == 0)
                {
                    controller.SetActiveBonePose(in poseA);
                }
                else
                {
                    controller.SetActiveBonePose(in poseB);
                }
                _ = controller.GetActiveBonePose();
            }

            long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            long managedDiff = managedAfter - managedBefore;

            Assert.LessOrEqual(managedDiff, 0,
                $"FacialController.SetActiveBonePose / GetActiveBonePose 経路で managed alloc が発生しました: {managedDiff} bytes");
        }

        [UnityTest]
        public IEnumerator HotPath_SetAndApplyViaController_ZeroGCAllocation_Profiler()
        {
            BuildFacialControllerHierarchy(out var head, out _, out _);
            var controller = _root.GetComponent<FacialController>();

            head.localRotation = Quaternion.identity;

            controller.InitializeWithProfile(CreateEmptyBonePoseProfile());

            var poseAEntries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 1f, 0f),
            };
            var poseA = new BonePose("A-prof", poseAEntries);

            var poseBEntries = new[]
            {
                new BonePoseEntry("LeftEye", -1f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, -1f, 0f),
            };
            var poseB = new BonePose("B-prof", poseBEntries);

            // ウォームアップ
            controller.SetActiveBonePose(in poseA);
            yield return null;
            controller.SetActiveBonePose(in poseB);
            yield return null;
            controller.SetActiveBonePose(in poseA);
            yield return null;

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            for (int i = 0; i < 60; i++)
            {
                if ((i & 1) == 0)
                {
                    controller.SetActiveBonePose(in poseA);
                }
                else
                {
                    controller.SetActiveBonePose(in poseB);
                }
                _ = controller.GetActiveBonePose();
            }

            long gcAlloc = recorder.LastValue;
            recorder.Dispose();

            Assert.AreEqual(0, gcAlloc,
                $"FacialController 経由の Set/Get で GC.Alloc が検出されました: {gcAlloc} bytes");
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// FacialController + Animator + SkinnedMeshRenderer + Hips/Spine/Neck/Head/LeftEye/RightEye
        /// の Transform 階層を構築。
        /// </summary>
        private void BuildFacialControllerHierarchy(
            out Transform head,
            out Transform leftEye,
            out Transform rightEye)
        {
            _root = new GameObject("BonePoseProviderApiRoot");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();
            _root.AddComponent<FacialController>();

            var meshObj = new GameObject("Mesh");
            meshObj.transform.SetParent(_root.transform, worldPositionStays: false);
            meshObj.AddComponent<SkinnedMeshRenderer>();

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

        private static FacialProfile CreateEmptyBonePoseProfile()
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            return new FacialProfile("1.0.0", layers, null, null, null, Array.Empty<BonePose>());
        }

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

        private static void AssertQuaternionApprox(Quaternion expected, Quaternion actual, float tol, string label)
        {
            float dot = expected.x * actual.x + expected.y * actual.y +
                        expected.z * actual.z + expected.w * actual.w;
            float diff = Mathf.Abs(Mathf.Abs(dot) - 1f);
            Assert.That(diff, Is.LessThan(tol),
                $"{label}: 期待値 {expected} と実測値 {actual} が一致しません（|dot|-1={diff}）");
        }

        // ================================================================
        // Fake Provider: analog-input-binding の消費パターンを模した MonoBehaviour
        //
        // 重要: このクラスは IBonePoseProvider のみに依存し、具象
        // FacialController / BoneWriter には依存しない。これにより
        // 「入力源を仮定しない汎用 API である」ことを構造的に保証する（Req 11.4）。
        // ================================================================

        private sealed class FakeAnalogBindingProvider : MonoBehaviour
        {
            private IBonePoseProvider _provider;

            public void Bind(IBonePoseProvider provider)
            {
                _provider = provider;
            }

            public void PushPose(in BonePose pose)
            {
                _provider?.SetActiveBonePose(in pose);
            }
        }
    }
}
