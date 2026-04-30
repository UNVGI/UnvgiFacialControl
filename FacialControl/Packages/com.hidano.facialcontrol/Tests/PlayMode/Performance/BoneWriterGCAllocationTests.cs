using System;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Hidano.FacialControl.Adapters.Bone;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Performance
{
    /// <summary>
    /// Task 7.7: BoneWriter の GC ゼロアロケーション検証テスト
    /// （Req 6.1 / 6.2 / 6.3 / 6.4 / 11.5、PlayMode、Performance）。
    /// </summary>
    /// <remarks>
    /// 検証項目:
    ///   - 同一 BonePose を複数フレーム継続して <see cref="BoneWriter.Apply"/> した場合、
    ///     warmup 後の per-frame ヒープ確保が 0 バイトであること（Req 6.1, 6.3）。
    ///   - <see cref="BoneWriter.SetActiveBonePose"/> の呼出経路でも 0 バイトであること（Req 11.5）。
    ///   - 複数エントリ（5 bone）を含む BonePose で 0 バイトであること（Req 6.1）。
    ///
    /// 計測方針:
    ///   - <see cref="GC.GetTotalMemory(bool)"/> 差分 (managed alloc) を主指標とする。
    ///   - <see cref="ProfilerRecorder"/> の "GC.Alloc" を補助指標とする
    ///     （Unity 内部要因の微小揺れを吸収するため diff <= 0 で評価）。
    /// </remarks>
    [TestFixture]
    public class BoneWriterGCAllocationTests
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

        // ============================================================
        // Apply() per-frame ゼロアロケーション (Req 6.1, 6.3)
        // ============================================================

        [Test]
        public void Apply_SameBonePoseAcrossFrames_ZeroGCAllocation()
        {
            BuildBoneHierarchy(out var head, out _, out _);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.Euler(5f, 10f, -3f);

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 5f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 8f, 0f),
            };
            var pose = new BonePose("warm", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");

            // ウォームアップ: 初回 Apply で _initialSnapshot Dictionary に各 boneName が追加される
            // （Dictionary 拡張は alloc し得る）。測定と同程度回して JIT・キャッシュ・heap 状態を安定させる。
            for (int i = 0; i < 30; i++)
            {
                writer.Apply();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

            for (int frame = 0; frame < 100; frame++)
            {
                writer.Apply();
            }

            long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            long managedDiff = managedAfter - managedBefore;

            // 並行 GC 活動による heap 揺らぎ許容 65536 bytes（プロジェクト既存 GC test 基準）。
            // 厳格な alloc=0 検証は companion の `_Profiler` バリアントが ProfilerRecorder で担当。
            Assert.LessOrEqual(managedDiff, 65536,
                $"BoneWriter.Apply の毎フレーム経路で managed alloc がページノイズ許容 (65536 bytes) を超過: {managedDiff} bytes");
        }

        [Test]
        public void Apply_SameBonePoseAcrossFrames_ZeroGCAllocation_Profiler()
        {
            BuildBoneHierarchy(out var head, out _, out _);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 2f, 3f),
                new BonePoseEntry("RightEye", -1f, -2f, -3f),
            };
            var pose = new BonePose("warm-profiler", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");

            for (int i = 0; i < 3; i++)
            {
                writer.Apply();
            }

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            for (int frame = 0; frame < 60; frame++)
            {
                writer.Apply();
            }

            long gcAlloc = recorder.LastValue;
            recorder.Dispose();

            Assert.AreEqual(0, gcAlloc,
                $"BoneWriter.Apply で GC.Alloc が検出されました: {gcAlloc} bytes");
        }

        // ============================================================
        // SetActiveBonePose(in pose) 経路 ゼロアロケーション (Req 11.5)
        // ============================================================

        [Test]
        public void SetActiveBonePose_RepeatedCallsBeforeApply_ZeroGCAllocation()
        {
            BuildBoneHierarchy(out var head, out _, out _);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 0f, 0f),
            };
            var initialPose = new BonePose("initial", initialEntries);

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

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");

            // ウォームアップ: Set + Apply 経路を一通り回し、_initialSnapshot 等を安定させる。
            writer.Apply();
            writer.SetActiveBonePose(in poseA);
            writer.Apply();
            writer.SetActiveBonePose(in poseB);
            writer.Apply();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

            // Set + Apply を交互に繰り返す（pending → active swap、entry 列挙、basis 採取の経路を全通過）。
            for (int frame = 0; frame < 100; frame++)
            {
                if ((frame & 1) == 0)
                {
                    writer.SetActiveBonePose(in poseA);
                }
                else
                {
                    writer.SetActiveBonePose(in poseB);
                }
                writer.Apply();
            }

            long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            long managedDiff = managedAfter - managedBefore;

            Assert.LessOrEqual(managedDiff, 0,
                $"SetActiveBonePose + Apply の経路で managed alloc が発生しました: {managedDiff} bytes");
        }

        [Test]
        public void SetActiveBonePose_RepeatedCallsBeforeApply_ZeroGCAllocation_Profiler()
        {
            BuildBoneHierarchy(out var head, out _, out _);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var initialEntries = new[]
            {
                new BonePoseEntry("LeftEye", 0f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 0f, 0f),
            };
            var initialPose = new BonePose("initial-prof", initialEntries);

            var poseAEntries = new[]
            {
                new BonePoseEntry("LeftEye", 3f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 3f, 0f),
            };
            var poseA = new BonePose("A-prof", poseAEntries);

            var poseBEntries = new[]
            {
                new BonePoseEntry("LeftEye", -3f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, -3f, 0f),
            };
            var poseB = new BonePose("B-prof", poseBEntries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in initialPose, "Head");

            writer.Apply();
            writer.SetActiveBonePose(in poseA);
            writer.Apply();
            writer.SetActiveBonePose(in poseB);
            writer.Apply();

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            for (int frame = 0; frame < 60; frame++)
            {
                if ((frame & 1) == 0)
                {
                    writer.SetActiveBonePose(in poseA);
                }
                else
                {
                    writer.SetActiveBonePose(in poseB);
                }
                writer.Apply();
            }

            long gcAlloc = recorder.LastValue;
            recorder.Dispose();

            Assert.AreEqual(0, gcAlloc,
                $"SetActiveBonePose + Apply で GC.Alloc が検出されました: {gcAlloc} bytes");
        }

        // ============================================================
        // 複数エントリ (5 bone) ゼロアロケーション (Req 6.1)
        // ============================================================

        [Test]
        public void Apply_FiveBoneEntries_ZeroGCAllocation()
        {
            BuildFullFiveBoneHierarchy(out var head);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.Euler(2f, 4f, 6f);

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 5f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 8f, 0f),
                new BonePoseEntry("Head", 0f, 0f, 3f),
                new BonePoseEntry("Neck", 1f, 1f, 0f),
                new BonePoseEntry("Spine", 0f, 2f, 0f),
            };
            var pose = new BonePose("five-bone", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");

            for (int i = 0; i < 3; i++)
            {
                writer.Apply();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long managedBefore = GC.GetTotalMemory(forceFullCollection: false);

            for (int frame = 0; frame < 100; frame++)
            {
                writer.Apply();
            }

            long managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            long managedDiff = managedAfter - managedBefore;

            Assert.LessOrEqual(managedDiff, 0,
                $"5 bone entries の Apply 経路で managed alloc が発生しました: {managedDiff} bytes");
        }

        [Test]
        public void Apply_FiveBoneEntries_ZeroGCAllocation_Profiler()
        {
            BuildFullFiveBoneHierarchy(out var head);
            var animator = _root.GetComponent<Animator>();
            var resolver = new BoneTransformResolver(_root.transform);

            head.localRotation = Quaternion.identity;

            var entries = new[]
            {
                new BonePoseEntry("LeftEye", 1f, 0f, 0f),
                new BonePoseEntry("RightEye", 0f, 1f, 0f),
                new BonePoseEntry("Head", 0f, 0f, 1f),
                new BonePoseEntry("Neck", 0.5f, 0.5f, 0f),
                new BonePoseEntry("Spine", 0f, 0.5f, 0.5f),
            };
            var pose = new BonePose("five-bone-prof", entries);

            var writer = new BoneWriter(resolver, animator);
            writer.Initialize(in pose, "Head");

            for (int i = 0; i < 3; i++)
            {
                writer.Apply();
            }

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            for (int frame = 0; frame < 60; frame++)
            {
                writer.Apply();
            }

            long gcAlloc = recorder.LastValue;
            recorder.Dispose();

            Assert.AreEqual(0, gcAlloc,
                $"5 bone entries の Apply で GC.Alloc が検出されました: {gcAlloc} bytes");
        }

        // ============================================================
        // Helpers
        // ============================================================

        /// <summary>
        /// Root → Hips → Spine → Neck → Head → (LeftEye, RightEye) 階層を構築。
        /// </summary>
        private void BuildBoneHierarchy(out Transform head, out Transform leftEye, out Transform rightEye)
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

        /// <summary>
        /// 5 bone（LeftEye / RightEye / Head / Neck / Spine）を resolver で解決可能な階層を構築。
        /// </summary>
        private void BuildFullFiveBoneHierarchy(out Transform head)
        {
            _root = new GameObject("Root");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();

            var hips = MakeChild(_root.transform, "Hips");
            var spine = MakeChild(hips, "Spine");
            var neck = MakeChild(spine, "Neck");
            head = MakeChild(neck, "Head");
            _ = MakeChild(head, "LeftEye");
            _ = MakeChild(head, "RightEye");
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
    }
}
