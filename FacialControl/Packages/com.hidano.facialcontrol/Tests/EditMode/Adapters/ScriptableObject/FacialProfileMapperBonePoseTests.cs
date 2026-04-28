using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests
{
    /// <summary>
    /// tasks.md 5.3: <see cref="FacialProfileMapper"/> の BonePose round-trip 契約テスト（Red、EditMode）。
    ///
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///     <item>
    ///         Domain <see cref="BonePose"/> → <see cref="BonePoseSerializable"/> →
    ///         Domain <see cref="BonePose"/> の名前 / Euler 値が同値であることをアサート（Req 8.1, 8.2, 8.5）
    ///     </item>
    ///     <item>
    ///         SO → JSON 経由 → Domain → SO の経路でも値が保たれること
    ///     </item>
    ///     <item>
    ///         <c>_bonePoses</c> 空配列の SO が Domain で空 BonePoses に変換されること（Req 10.1）
    ///     </item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 設計意図: 5.4（Green）で追加される変換 API を reflection で discover し、
    /// 未実装時は Assert で fail させて Red 状態を保証する。
    /// 期待 API（5.4 で実装）:
    /// <list type="bullet">
    ///     <item>
    ///         <c>static BonePoseSerializable[] FacialProfileMapper.ToSerializableBonePoses(
    ///         ReadOnlyMemory&lt;BonePose&gt; domain)</c>
    ///     </item>
    ///     <item>
    ///         <c>static BonePose[] FacialProfileMapper.ToDomainBonePoses(
    ///         BonePoseSerializable[] serializable)</c>
    ///     </item>
    ///     <item>
    ///         <c>UpdateSO(so, profile)</c> が <c>profile.BonePoses</c> を
    ///         <c>so.BonePoses</c>(<c>_bonePoses</c>) に同期する（既存 UpdateSO の延長）
    ///     </item>
    /// </list>
    /// </para>
    ///
    /// _Requirements: 8.1, 8.2, 8.5, 10.1
    /// _Boundary: Adapters.ScriptableObject.FacialProfileMapper
    /// _Depends: 4.4 (SystemTextJsonParser bonePoses 拡張), 5.2 (FacialProfileSO._bonePoses)
    /// </summary>
    [TestFixture]
    public class FacialProfileMapperBonePoseTests
    {
        private const string ToSerializableMethodName = "ToSerializableBonePoses";
        private const string ToDomainMethodName = "ToDomainBonePoses";

        // --- Fake 実装（既存 FacialProfileMapperTests と同型） ---

        private class FakeProfileRepository : IProfileRepository
        {
            public FacialProfile? LoadResult { get; set; }
            public string LastSavedPath { get; private set; }
            public FacialProfile? LastSavedProfile { get; private set; }

            public FacialProfile LoadProfile(string path)
            {
                if (LoadResult.HasValue)
                    return LoadResult.Value;
                return new FacialProfile("1.0");
            }

            public void SaveProfile(string path, FacialProfile profile)
            {
                LastSavedPath = path;
                LastSavedProfile = profile;
            }
        }

        // --- ヘルパー ---

        private FakeProfileRepository _repository;
        private FacialProfileMapper _mapper;

        private static FacialProfileSO CreateSO(string jsonFilePath = "profiles/test.json")
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            so.JsonFilePath = jsonFilePath;
            return so;
        }

        [SetUp]
        public void SetUp()
        {
            _repository = new FakeProfileRepository();
            _mapper = new FacialProfileMapper(_repository);
        }

        // ================================================================
        // Reflection helpers — 5.4 が追加する static API を解決
        // ================================================================

        /// <summary>
        /// <c>BonePoseSerializable[] FacialProfileMapper.ToSerializableBonePoses(ReadOnlyMemory&lt;BonePose&gt;)</c>
        /// を reflection で解決し、未実装なら Assert.Fail。
        /// </summary>
        private static BonePoseSerializable[] InvokeToSerializableBonePoses(ReadOnlyMemory<BonePose> domain)
        {
            var method = typeof(FacialProfileMapper).GetMethod(
                ToSerializableMethodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(ReadOnlyMemory<BonePose>) },
                modifiers: null);

            if (method == null)
            {
                Assert.Fail(
                    $"static {nameof(FacialProfileMapper)}.{ToSerializableMethodName}(ReadOnlyMemory<BonePose>) が未実装です。" +
                    " tasks.md 5.4 で追加してください。");
            }

            return (BonePoseSerializable[])method.Invoke(null, new object[] { domain });
        }

        /// <summary>
        /// <c>BonePose[] FacialProfileMapper.ToDomainBonePoses(BonePoseSerializable[])</c>
        /// を reflection で解決し、未実装なら Assert.Fail。
        /// </summary>
        private static BonePose[] InvokeToDomainBonePoses(BonePoseSerializable[] serializable)
        {
            var method = typeof(FacialProfileMapper).GetMethod(
                ToDomainMethodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(BonePoseSerializable[]) },
                modifiers: null);

            if (method == null)
            {
                Assert.Fail(
                    $"static {nameof(FacialProfileMapper)}.{ToDomainMethodName}(BonePoseSerializable[]) が未実装です。" +
                    " tasks.md 5.4 で追加してください。");
            }

            return (BonePose[])method.Invoke(null, new object[] { serializable });
        }

        private static BonePose CreateDomainPose(string id, params (string boneName, float x, float y, float z)[] entries)
        {
            var domainEntries = entries
                .Select(e => new BonePoseEntry(e.boneName, e.x, e.y, e.z))
                .ToArray();
            return new BonePose(id, domainEntries);
        }

        private static void AssertEntryEquals(
            BonePoseEntry expected,
            BonePoseEntry actual,
            string contextMessage)
        {
            Assert.AreEqual(expected.BoneName, actual.BoneName, $"{contextMessage}: BoneName 不一致");
            Assert.AreEqual(expected.EulerX, actual.EulerX, $"{contextMessage}: EulerX 不一致");
            Assert.AreEqual(expected.EulerY, actual.EulerY, $"{contextMessage}: EulerY 不一致");
            Assert.AreEqual(expected.EulerZ, actual.EulerZ, $"{contextMessage}: EulerZ 不一致");
        }

        // ================================================================
        // (A) Domain → Serializable → Domain の round-trip
        //     Req 8.1, 8.2, 8.5
        // ================================================================

        [Test]
        public void ToSerializable_PreservesIdAndEntries()
        {
            var domain = new BonePose[]
            {
                CreateDomainPose("look-up", ("Head", 1f, 2f, 3f)),
                CreateDomainPose("look-down", ("Head", -5f, 0f, 0f), ("Neck", 0f, -1.5f, 0f)),
            };

            var serializable = InvokeToSerializableBonePoses(domain);

            Assert.IsNotNull(serializable, "ToSerializableBonePoses が null を返してはいけません。");
            Assert.AreEqual(2, serializable.Length, "BonePose 配列の長さが保たれるべき。");

            Assert.AreEqual("look-up", serializable[0].id);
            Assert.AreEqual(1, serializable[0].entries.Length);
            Assert.AreEqual("Head", serializable[0].entries[0].boneName);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), serializable[0].entries[0].eulerXYZ);

            Assert.AreEqual("look-down", serializable[1].id);
            Assert.AreEqual(2, serializable[1].entries.Length);
            Assert.AreEqual("Head", serializable[1].entries[0].boneName);
            Assert.AreEqual(new Vector3(-5f, 0f, 0f), serializable[1].entries[0].eulerXYZ);
            Assert.AreEqual("Neck", serializable[1].entries[1].boneName);
            Assert.AreEqual(new Vector3(0f, -1.5f, 0f), serializable[1].entries[1].eulerXYZ);
        }

        [Test]
        public void ToDomain_PreservesIdAndEntries()
        {
            var serializable = new[]
            {
                new BonePoseSerializable
                {
                    id = "from-serializable",
                    entries = new[]
                    {
                        new BonePoseEntrySerializable { boneName = "LeftEye",  eulerXYZ = new Vector3(0f, 12.5f, 0f) },
                        new BonePoseEntrySerializable { boneName = "RightEye", eulerXYZ = new Vector3(0f, -7.5f, 0f) },
                    },
                },
            };

            var domain = InvokeToDomainBonePoses(serializable);

            Assert.IsNotNull(domain);
            Assert.AreEqual(1, domain.Length);
            Assert.AreEqual("from-serializable", domain[0].Id);

            var entries = domain[0].Entries.Span;
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("LeftEye", entries[0].BoneName);
            Assert.AreEqual(12.5f, entries[0].EulerY);
            Assert.AreEqual("RightEye", entries[1].BoneName);
            Assert.AreEqual(-7.5f, entries[1].EulerY);
        }

        [Test]
        public void RoundTrip_DomainToSerializableToDomain_PreservesNamesAndEulerValues()
        {
            // Req 8.1, 8.2, 8.5: Domain → Serializable → Domain の同値保証
            var original = new[]
            {
                CreateDomainPose("gaze-right", ("LeftEye", 0f, 15f, 0f), ("RightEye", 0f, 12.5f, 0f)),
                CreateDomainPose("head-tilt",  ("Head", 5.5f, -10.25f, 0.125f)),
                CreateDomainPose("",           ("Neck", 0.001f, -0.001f, 0f)),
            };

            var serializable = InvokeToSerializableBonePoses(original);
            var roundTripped = InvokeToDomainBonePoses(serializable);

            Assert.AreEqual(original.Length, roundTripped.Length, "round-trip で配列長が変わってはいけません。");

            for (int i = 0; i < original.Length; i++)
            {
                Assert.AreEqual(original[i].Id, roundTripped[i].Id, $"[{i}] Id 不一致");

                var origEntries = original[i].Entries.Span;
                var rtEntries = roundTripped[i].Entries.Span;
                Assert.AreEqual(origEntries.Length, rtEntries.Length, $"[{i}] entries.Length 不一致");

                for (int j = 0; j < origEntries.Length; j++)
                {
                    AssertEntryEquals(origEntries[j], rtEntries[j], $"[{i}][{j}]");
                }
            }
        }

        [Test]
        public void RoundTrip_DomainToSerializableToDomain_PreservesMultiByteBoneNames()
        {
            // Req 2.2 / 8.5: 多バイト boneName が round-trip で壊れない
            var original = new[]
            {
                CreateDomainPose("multibyte",
                    ("頭", 1f, 2f, 3f),
                    ("左目_あ", 0f, 4.5f, 0f),
                    ("右目★", -1f, 0f, 0f)),
            };

            var serializable = InvokeToSerializableBonePoses(original);
            var roundTripped = InvokeToDomainBonePoses(serializable);

            var entries = roundTripped[0].Entries.Span;
            Assert.AreEqual("頭", entries[0].BoneName);
            Assert.AreEqual("左目_あ", entries[1].BoneName);
            Assert.AreEqual("右目★", entries[2].BoneName);
        }

        [Test]
        public void RoundTrip_DomainToSerializableToDomain_PreservesFloatPrecision()
        {
            // Req 8.5: float 精度が round-trip で保たれる
            var values = new[] { -179.99f, -90f, -0.001f, 0f, 0.001f, 90f, 179.99f };
            foreach (var v in values)
            {
                var original = new[]
                {
                    CreateDomainPose("precision", ("Head", v, v, v)),
                };

                var serializable = InvokeToSerializableBonePoses(original);
                var roundTripped = InvokeToDomainBonePoses(serializable);

                var e = roundTripped[0].Entries.Span[0];
                Assert.AreEqual(v, e.EulerX, $"EulerX mismatch for {v}");
                Assert.AreEqual(v, e.EulerY, $"EulerY mismatch for {v}");
                Assert.AreEqual(v, e.EulerZ, $"EulerZ mismatch for {v}");
            }
        }

        // ================================================================
        // (B) 空配列 / null の取扱い
        //     Req 10.1
        // ================================================================

        [Test]
        public void ToSerializable_EmptyDomain_ReturnsEmptyArray()
        {
            // Req 10.1: 空配列 round-trip
            var serializable = InvokeToSerializableBonePoses(ReadOnlyMemory<BonePose>.Empty);

            Assert.IsNotNull(serializable, "空 Domain でも null ではなく空配列を返すべき。");
            Assert.AreEqual(0, serializable.Length);
        }

        [Test]
        public void ToDomain_EmptySerializable_ReturnsEmptyArray()
        {
            // Req 10.1: 空 SO → 空 Domain
            var domain = InvokeToDomainBonePoses(Array.Empty<BonePoseSerializable>());

            Assert.IsNotNull(domain, "空 SO でも null ではなく空配列を返すべき。");
            Assert.AreEqual(0, domain.Length);
        }

        [Test]
        public void ToDomain_NullSerializable_ReturnsEmptyArray()
        {
            // Req 10.1: null 入力でも空配列扱い（後方互換 / 防御的読み取り）
            var domain = InvokeToDomainBonePoses(null);

            Assert.IsNotNull(domain, "null 入力でも null を返してはならず空配列であるべき。");
            Assert.AreEqual(0, domain.Length);
        }

        [Test]
        public void EmptyBonePosesSO_ConvertsToEmptyDomainBonePoses()
        {
            // Req 10.1 主要観測条件: `_bonePoses` 空配列の SO が Domain で空 BonePoses になる
            var so = CreateSO();
            try
            {
                Assert.IsNotNull(so.BonePoses, "5.2 で _bonePoses は既定で空配列で初期化されるはず。");
                Assert.AreEqual(0, so.BonePoses.Length, "既定生成 SO の BonePoses は空配列。");

                var domain = InvokeToDomainBonePoses(so.BonePoses);

                Assert.IsNotNull(domain);
                Assert.AreEqual(0, domain.Length, "空 SO は空 Domain BonePoses に変換されるべき（Req 10.1）。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ================================================================
        // (C) UpdateSO 経由での Domain → SO 同期
        //     5.4 Green で UpdateSO に BonePoses 同期が追加される想定
        // ================================================================

        [Test]
        public void UpdateSO_ProfileWithBonePoses_SyncsBonePosesToSO()
        {
            // 5.4 で UpdateSO(so, profile) が profile.BonePoses → so._bonePoses を同期する。
            var so = CreateSO();
            try
            {
                var bonePoses = new[]
                {
                    CreateDomainPose("test-pose", ("Head", 10f, 20f, 30f)),
                };
                var profile = new FacialProfile("1.0", bonePoses: bonePoses);

                _mapper.UpdateSO(so, profile);

                Assert.IsNotNull(so.BonePoses, "UpdateSO 後 SO.BonePoses は null であってはならない。");
                Assert.AreEqual(
                    1,
                    so.BonePoses.Length,
                    "UpdateSO は profile.BonePoses を SO._bonePoses に同期するべき（5.4 で追加）。");
                Assert.AreEqual("test-pose", so.BonePoses[0].id);
                Assert.IsNotNull(so.BonePoses[0].entries);
                Assert.AreEqual(1, so.BonePoses[0].entries.Length);
                Assert.AreEqual("Head", so.BonePoses[0].entries[0].boneName);
                Assert.AreEqual(new Vector3(10f, 20f, 30f), so.BonePoses[0].entries[0].eulerXYZ);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void UpdateSO_ProfileWithEmptyBonePoses_SetsEmptyBonePosesArray()
        {
            // Req 10.1: UpdateSO は空 profile.BonePoses でも空配列を SO に書き込む
            var so = CreateSO();
            try
            {
                var profile = new FacialProfile("1.0");

                _mapper.UpdateSO(so, profile);

                Assert.IsNotNull(so.BonePoses, "空 profile でも SO.BonePoses は null であってはならない。");
                Assert.AreEqual(0, so.BonePoses.Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ================================================================
        // (D) SO → JSON 経由 → Domain → SO の round-trip
        //     主要観測条件: 値が保たれること
        // ================================================================

        [Test]
        public void RoundTrip_SOToJsonToDomainToSO_PreservesValues()
        {
            // SO の _bonePoses → BonePoseDto (JSON 経路) → BonePose → SO._bonePoses
            // の経路で名前 / Euler 値が保たれる。
            var so = CreateSO();
            try
            {
                so.BonePoses = new[]
                {
                    new BonePoseSerializable
                    {
                        id = "json-trip",
                        entries = new[]
                        {
                            new BonePoseEntrySerializable { boneName = "Head",     eulerXYZ = new Vector3(5.5f, -10.25f, 0.125f) },
                            new BonePoseEntrySerializable { boneName = "LeftEye",  eulerXYZ = new Vector3(0f, 12.5f, 0f) },
                            new BonePoseEntrySerializable { boneName = "右目★",    eulerXYZ = new Vector3(-1f, 0f, 0.5f) },
                        },
                    },
                };

                // SO → Domain
                var domain = InvokeToDomainBonePoses(so.BonePoses);
                Assert.AreEqual(1, domain.Length);

                // Domain → JSON (BonePoseDto 経由) → Domain
                BonePoseDto dto = BonePoseDto.FromDomain(domain[0]);
                string json = JsonUtility.ToJson(dto);
                BonePoseDto parsedDto = JsonUtility.FromJson<BonePoseDto>(json);
                BonePose afterJson = parsedDto.ToDomain();

                // Domain → SO
                var serializable = InvokeToSerializableBonePoses(new BonePose[] { afterJson });
                Assert.AreEqual(1, serializable.Length);

                // SO 値が原始 SO 値と同値
                Assert.AreEqual(so.BonePoses[0].id, serializable[0].id);
                Assert.AreEqual(so.BonePoses[0].entries.Length, serializable[0].entries.Length);
                for (int i = 0; i < so.BonePoses[0].entries.Length; i++)
                {
                    Assert.AreEqual(
                        so.BonePoses[0].entries[i].boneName,
                        serializable[0].entries[i].boneName,
                        $"[{i}] boneName 不一致");
                    Assert.AreEqual(
                        so.BonePoses[0].entries[i].eulerXYZ,
                        serializable[0].entries[i].eulerXYZ,
                        $"[{i}] eulerXYZ 不一致");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void RoundTrip_DomainToSOToJsonToDomain_PreservesValues()
        {
            // Domain → SO (Serializable[]) → JSON → Domain の経路でも保たれること。
            // UpdateSO で SO に書き、SO の BonePoses[0] を BonePoseDto 互換 JSON にして
            // ふたたび Domain に戻す経路を検証する。
            var so = CreateSO();
            try
            {
                var bonePoses = new[]
                {
                    CreateDomainPose("look-up",
                        ("Head", 5.5f, -10.25f, 0.125f),
                        ("LeftEye", 0f, 12.5f, 0f)),
                };
                var profile = new FacialProfile("1.0", bonePoses: bonePoses);

                _mapper.UpdateSO(so, profile);

                Assert.AreEqual(1, so.BonePoses.Length);
                var serial = so.BonePoses[0];

                // Serializable[] → BonePoseDto 互換 → JSON → Domain
                var dto = new BonePoseDto
                {
                    id = serial.id,
                    entries = new System.Collections.Generic.List<BonePoseEntryDto>(),
                };
                foreach (var e in serial.entries)
                {
                    dto.entries.Add(new BonePoseEntryDto { boneName = e.boneName, eulerXYZ = e.eulerXYZ });
                }
                string json = JsonUtility.ToJson(dto);
                BonePose afterJson = JsonUtility.FromJson<BonePoseDto>(json).ToDomain();

                Assert.AreEqual("look-up", afterJson.Id);
                var entries = afterJson.Entries.Span;
                Assert.AreEqual(2, entries.Length);

                AssertEntryEquals(bonePoses[0].Entries.Span[0], entries[0], "[0]");
                AssertEntryEquals(bonePoses[0].Entries.Span[1], entries[1], "[1]");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }
    }
}
