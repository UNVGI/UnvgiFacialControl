using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.FileSystem;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Integration
{
    /// <summary>
    /// Task 10.1: JSON → SO → Domain → BoneWriter → Transform の E2E 統合テスト（PlayMode）。
    ///
    /// 全経路（JSON parse → FacialProfileSO への serializable コピー → SO から Domain への
    /// 復元 → FacialController.InitializeWithProfile → LateUpdate での BoneWriter.Apply →
    /// 対象 Transform.localRotation 書込み）が値を破壊せずに通り、最終結果が basis 相対
    /// （basis * Quaternion.Euler(eulerXYZ)）になっていることを検証する。
    ///
    /// _Requirements: 1.1, 4.2, 4.5, 5.2, 5.3, 7.2, 8.1, 8.2, 8.5, 10.3
    /// </summary>
    [TestFixture]
    public class BonePoseEndToEndTests
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
        // 全経路 JSON → SO → Domain → BoneWriter → Transform
        // ================================================================

        [UnityTest]
        public IEnumerator FullPath_JsonRoundTripsThroughSoAndAppliesToTransform()
        {
            // 1) JSON 文字列に bonePoses ブロックを含める
            const string json = @"{
                ""schemaVersion"": ""1.0"",
                ""layers"": [
                    {""name"": ""emotion"", ""priority"": 0, ""exclusionMode"": ""lastWins"", ""inputSources"": [
                        {""id"": ""controller-expr"", ""weight"": 1.0}
                    ]}
                ],
                ""expressions"": [],
                ""bonePoses"": [
                    {
                        ""id"": ""look-right"",
                        ""entries"": [
                            {""boneName"": ""LeftEye"", ""eulerXYZ"": {""x"": 0.0, ""y"": 25.0, ""z"": 0.0}},
                            {""boneName"": ""RightEye"", ""eulerXYZ"": {""x"": 0.0, ""y"": 25.0, ""z"": 0.0}}
                        ]
                    }
                ]
            }";

            // 2) JSON → Domain (Adapters/Json)
            var parser = new SystemTextJsonParser();
            FacialProfile parsed = parser.ParseProfile(json);

            // 3) Domain → SO (FacialProfileMapper.UpdateSO で Serializable に展開)
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            try
            {
                var mapper = new FacialProfileMapper(new FileProfileRepository(parser));
                mapper.UpdateSO(so, parsed);

                // SO 上の serializable BonePose 配列が round-trip しているか確認
                Assert.AreEqual(1, so.BonePoses.Length, "SO に BonePose が 1 件転写される");
                Assert.AreEqual("look-right", so.BonePoses[0].id, "Id が SO に転写される");
                Assert.AreEqual(2, so.BonePoses[0].entries.Length, "エントリ 2 件");

                // 4) SO → Domain (再度 Domain BonePose に戻す)
                var soBonePoses = FacialProfileMapper.ToDomainBonePoses(so.BonePoses);
                var rebuilt = new FacialProfile(
                    parsed.SchemaVersion,
                    parsed.Layers.ToArray(),
                    parsed.Expressions.ToArray(),
                    parsed.RendererPaths.ToArray(),
                    null,
                    soBonePoses);

                // 5) ヒエラルキー構築 + FacialController.InitializeWithProfile
                BuildHierarchy(out var head, out var leftEye, out var rightEye);
                var controller = _root.GetComponent<FacialController>();

                head.localRotation = Quaternion.identity;
                leftEye.localRotation = Quaternion.identity;
                rightEye.localRotation = Quaternion.identity;

                controller.InitializeWithProfile(rebuilt);

                // 6) 1 フレーム進めて LateUpdate を回す → BoneWriter.Apply が走る
                yield return null;

                // 7) Transform 書込みが basis * Quaternion.Euler(eulerXYZ) と一致
                //    （basis = identity なので Quaternion.Euler(0,25,0) が直接の期待値）
                var expected = Quaternion.identity * Quaternion.Euler(0f, 25f, 0f);
                AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                    "JSON → SO → Domain → BoneWriter → Transform の全経路で LeftEye が basis 相対の合成結果になる");
                AssertQuaternionApprox(expected, rightEye.localRotation, 1e-4f,
                    "JSON → SO → Domain → BoneWriter → Transform の全経路で RightEye が basis 相対の合成結果になる");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ================================================================
        // body tilt 込みでも basis 相対のため tilt が gaze に漏れない（Req 4.5）
        // ================================================================

        [UnityTest]
        public IEnumerator FullPath_WithBodyTilt_GazeRemainsBasisRelativeNoLeak()
        {
            const string json = @"{
                ""schemaVersion"": ""1.0"",
                ""layers"": [
                    {""name"": ""emotion"", ""priority"": 0, ""exclusionMode"": ""lastWins"", ""inputSources"": [
                        {""id"": ""controller-expr"", ""weight"": 1.0}
                    ]}
                ],
                ""expressions"": [],
                ""bonePoses"": [
                    {
                        ""id"": ""gaze-yaw"",
                        ""entries"": [
                            {""boneName"": ""LeftEye"", ""eulerXYZ"": {""x"": 0.0, ""y"": 15.0, ""z"": 0.0}}
                        ]
                    }
                ]
            }";

            var parser = new SystemTextJsonParser();
            FacialProfile parsed = parser.ParseProfile(json);

            var so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            try
            {
                var mapper = new FacialProfileMapper(new FileProfileRepository(parser));
                mapper.UpdateSO(so, parsed);

                var soBonePoses = FacialProfileMapper.ToDomainBonePoses(so.BonePoses);
                var rebuilt = new FacialProfile(
                    parsed.SchemaVersion,
                    parsed.Layers.ToArray(),
                    parsed.Expressions.ToArray(),
                    parsed.RendererPaths.ToArray(),
                    null,
                    soBonePoses);

                BuildHierarchy(out var head, out var leftEye, out _);
                var controller = _root.GetComponent<FacialController>();

                // body tilt: basis (Head) を roll +25deg に傾ける
                var tilt = Quaternion.Euler(0f, 0f, 25f);
                head.localRotation = tilt;
                leftEye.localRotation = Quaternion.identity;

                controller.InitializeWithProfile(rebuilt);

                yield return null;

                // 期待値: basis (tilt) * 相対 Euler。tilt が gaze に乗算されている
                // ことが「basis 相対」の証拠（Req 4.5）。
                var expected = tilt * Quaternion.Euler(0f, 15f, 0f);
                AssertQuaternionApprox(expected, leftEye.localRotation, 1e-4f,
                    "body tilt 込みでも BoneWriter は basis 相対で合成し、tilt が gaze に漏れない（Req 4.5）");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ================================================================
        // 多バイト boneName が JSON → SO → Domain で値破壊なく通る（Req 7.2 round-trip）
        // ================================================================

        [UnityTest]
        public IEnumerator FullPath_MultiByteBoneName_RoundTripsWithoutCorruption()
        {
            // boneName に多バイト文字（"頭" / "左目"）を含める。
            const string json = @"{
                ""schemaVersion"": ""1.0"",
                ""layers"": [
                    {""name"": ""emotion"", ""priority"": 0, ""exclusionMode"": ""lastWins"", ""inputSources"": [
                        {""id"": ""controller-expr"", ""weight"": 1.0}
                    ]}
                ],
                ""expressions"": [],
                ""bonePoses"": [
                    {
                        ""id"": ""multibyte"",
                        ""entries"": [
                            {""boneName"": ""左目"", ""eulerXYZ"": {""x"": 1.5, ""y"": 2.5, ""z"": 3.5}}
                        ]
                    }
                ]
            }";

            var parser = new SystemTextJsonParser();
            FacialProfile parsed = parser.ParseProfile(json);

            // 1 階層目: JSON → Domain
            Assert.AreEqual(1, parsed.BonePoses.Length, "BonePose 1 件");
            var pose = parsed.BonePoses.Span[0];
            Assert.AreEqual("multibyte", pose.Id);
            Assert.AreEqual(1, pose.Entries.Length);
            var entry = pose.Entries.Span[0];
            Assert.AreEqual("左目", entry.BoneName, "多バイト boneName が JSON → Domain で破壊されない");
            Assert.That(entry.EulerX, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(entry.EulerY, Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(entry.EulerZ, Is.EqualTo(3.5f).Within(1e-5f));

            // 2 階層目: Domain → SO → Domain（FacialProfileMapper 経由の round-trip）
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            try
            {
                var mapper = new FacialProfileMapper(new FileProfileRepository(parser));
                mapper.UpdateSO(so, parsed);

                Assert.AreEqual("左目", so.BonePoses[0].entries[0].boneName,
                    "多バイト boneName が Domain → SO で破壊されない");

                var soBonePoses = FacialProfileMapper.ToDomainBonePoses(so.BonePoses);
                Assert.AreEqual(1, soBonePoses.Length);
                var roundtrippedEntry = soBonePoses[0].Entries.Span[0];
                Assert.AreEqual("左目", roundtrippedEntry.BoneName,
                    "多バイト boneName が SO → Domain で破壊されない");
                Assert.That(roundtrippedEntry.EulerX, Is.EqualTo(1.5f).Within(1e-5f));
                Assert.That(roundtrippedEntry.EulerY, Is.EqualTo(2.5f).Within(1e-5f));
                Assert.That(roundtrippedEntry.EulerZ, Is.EqualTo(3.5f).Within(1e-5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }

            yield break;
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// FacialController + Animator + SkinnedMeshRenderer + Hips/Spine/Neck/Head/LeftEye/RightEye の
        /// Transform 階層を構築。Head が basis bone のデフォルト名と一致する。
        /// </summary>
        private void BuildHierarchy(out Transform head, out Transform leftEye, out Transform rightEye)
        {
            _root = new GameObject("BonePoseEndToEndRoot");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();
            _root.AddComponent<FacialController>();

            // SkinnedMeshRenderer 子（既存パイプライン互換のため）
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
    }
}
