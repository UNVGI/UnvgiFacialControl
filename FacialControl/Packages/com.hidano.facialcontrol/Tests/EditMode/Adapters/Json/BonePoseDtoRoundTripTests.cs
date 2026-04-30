using System;
using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Domain.Models;

// schema v1.0 用の Obsolete DTO を意図的にテストするため、CS0618 警告を抑制する。
// 物理削除は Phase 3.6（タスク 3.6 / 3.3）で行われる。
#pragma warning disable 618
namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    /// <summary>
    /// <see cref="BonePoseDto"/> / <see cref="BonePoseEntryDto"/> の round-trip テスト（Red）。
    ///
    /// 検証範囲:
    ///   - サンプル JSON（<c>bonePoses[].id</c>, <c>bonePoses[].entries[].boneName</c>,
    ///     <c>bonePoses[].entries[].eulerXYZ.{x,y,z}</c>）→ DTO → Domain BonePose → DTO
    ///     の名前/値が同値であることをアサート
    ///   - <c>eulerXYZ</c> の degree 値が float round-trip で保持されること（Req 8.5）
    ///   - 多バイト boneName（例: 「頭」「左目_あ」）が壊れずに round-trip すること（Req 2.2）
    ///
    /// _Requirements: 2.2, 7.1, 7.2, 8.5
    /// _Boundary: Adapters.Json.Dto.BonePoseDto, BonePoseEntryDto
    /// _Depends: 1.4 (BonePose / BonePoseEntry)
    /// </summary>
    [TestFixture]
    public class BonePoseDtoRoundTripTests
    {
        // ================================================================
        // BonePoseEntryDto — 単体 round-trip / 既定値 / 多バイト
        // ================================================================

        [Test]
        public void BonePoseEntryDto_FromJson_PopulatesBoneNameAndEulerXYZ()
        {
            var json = "{\"boneName\":\"LeftEye\",\"eulerXYZ\":{\"x\":1.5,\"y\":-2.25,\"z\":0.5}}";

            var dto = JsonUtility.FromJson<BonePoseEntryDto>(json);

            Assert.IsNotNull(dto);
            Assert.AreEqual("LeftEye", dto.boneName);
            Assert.AreEqual(1.5f, dto.eulerXYZ.x);
            Assert.AreEqual(-2.25f, dto.eulerXYZ.y);
            Assert.AreEqual(0.5f, dto.eulerXYZ.z);
        }

        [Test]
        public void BonePoseEntryDto_RoundTrip_PreservesValues()
        {
            var src = new BonePoseEntryDto
            {
                boneName = "RightEye",
                eulerXYZ = new Vector3(15.0f, -7.5f, 30.125f)
            };

            var json = JsonUtility.ToJson(src);
            var dst = JsonUtility.FromJson<BonePoseEntryDto>(json);

            Assert.AreEqual(src.boneName, dst.boneName);
            Assert.AreEqual(src.eulerXYZ.x, dst.eulerXYZ.x);
            Assert.AreEqual(src.eulerXYZ.y, dst.eulerXYZ.y);
            Assert.AreEqual(src.eulerXYZ.z, dst.eulerXYZ.z);
        }

        [Test]
        public void BonePoseEntryDto_RoundTrip_PreservesMultiByteBoneName()
        {
            // Req 2.2: 多バイト boneName（「頭」「左目_あ」など）が壊れないことを保証
            var src = new BonePoseEntryDto
            {
                boneName = "左目_あ",
                eulerXYZ = new Vector3(0.0f, 12.5f, 0.0f)
            };

            var json = JsonUtility.ToJson(src);
            var dst = JsonUtility.FromJson<BonePoseEntryDto>(json);

            Assert.AreEqual("左目_あ", dst.boneName);
            Assert.AreEqual(12.5f, dst.eulerXYZ.y);
        }

        [Test]
        public void BonePoseEntryDto_RoundTrip_PreservesFloatPrecision()
        {
            // Req 8.5: float round-trip で degree 値が保持される
            var values = new[] { -179.99f, -90.0f, -45.5f, -0.001f, 0.0f, 0.001f, 45.5f, 90.0f, 179.99f };

            foreach (var v in values)
            {
                var src = new BonePoseEntryDto
                {
                    boneName = "Head",
                    eulerXYZ = new Vector3(v, v, v)
                };

                var json = JsonUtility.ToJson(src);
                var dst = JsonUtility.FromJson<BonePoseEntryDto>(json);

                Assert.AreEqual(v, dst.eulerXYZ.x, $"x mismatch for {v}");
                Assert.AreEqual(v, dst.eulerXYZ.y, $"y mismatch for {v}");
                Assert.AreEqual(v, dst.eulerXYZ.z, $"z mismatch for {v}");
            }
        }

        // ================================================================
        // BonePoseDto — 単体 round-trip / 空 entries / 多エントリ
        // ================================================================

        [Test]
        public void BonePoseDto_FromJson_PopulatesIdAndEntries()
        {
            var json = "{\"id\":\"default-gaze\",\"entries\":[" +
                       "{\"boneName\":\"LeftEye\",\"eulerXYZ\":{\"x\":0.0,\"y\":0.0,\"z\":0.0}}," +
                       "{\"boneName\":\"RightEye\",\"eulerXYZ\":{\"x\":1.0,\"y\":2.0,\"z\":3.0}}" +
                       "]}";

            var dto = JsonUtility.FromJson<BonePoseDto>(json);

            Assert.IsNotNull(dto);
            Assert.AreEqual("default-gaze", dto.id);
            Assert.IsNotNull(dto.entries);
            Assert.AreEqual(2, dto.entries.Count);
            Assert.AreEqual("LeftEye", dto.entries[0].boneName);
            Assert.AreEqual("RightEye", dto.entries[1].boneName);
            Assert.AreEqual(1.0f, dto.entries[1].eulerXYZ.x);
            Assert.AreEqual(2.0f, dto.entries[1].eulerXYZ.y);
            Assert.AreEqual(3.0f, dto.entries[1].eulerXYZ.z);
        }

        [Test]
        public void BonePoseDto_RoundTrip_PreservesIdAndEntries()
        {
            var src = new BonePoseDto
            {
                id = "look-right",
                entries = new System.Collections.Generic.List<BonePoseEntryDto>
                {
                    new BonePoseEntryDto { boneName = "LeftEye",  eulerXYZ = new Vector3(0f, 15f, 0f) },
                    new BonePoseEntryDto { boneName = "RightEye", eulerXYZ = new Vector3(0f, 12.5f, 0f) },
                }
            };

            var json = JsonUtility.ToJson(src);
            var dst = JsonUtility.FromJson<BonePoseDto>(json);

            Assert.AreEqual(src.id, dst.id);
            Assert.AreEqual(src.entries.Count, dst.entries.Count);
            for (int i = 0; i < src.entries.Count; i++)
            {
                Assert.AreEqual(src.entries[i].boneName, dst.entries[i].boneName);
                Assert.AreEqual(src.entries[i].eulerXYZ, dst.entries[i].eulerXYZ);
            }
        }

        [Test]
        public void BonePoseDto_RoundTrip_PreservesEmptyEntries()
        {
            var src = new BonePoseDto
            {
                id = "empty-pose",
                entries = new System.Collections.Generic.List<BonePoseEntryDto>()
            };

            var json = JsonUtility.ToJson(src);
            var dst = JsonUtility.FromJson<BonePoseDto>(json);

            Assert.AreEqual("empty-pose", dst.id);
            Assert.IsNotNull(dst.entries);
            Assert.AreEqual(0, dst.entries.Count);
        }

        // ================================================================
        // DTO ↔ Domain 変換 round-trip
        // ================================================================

        [Test]
        public void BonePoseDto_ToDomain_PreservesIdAndEntries()
        {
            var dto = new BonePoseDto
            {
                id = "domain-trip",
                entries = new System.Collections.Generic.List<BonePoseEntryDto>
                {
                    new BonePoseEntryDto { boneName = "Head",     eulerXYZ = new Vector3(5f, 10f, 15f) },
                    new BonePoseEntryDto { boneName = "LeftEye",  eulerXYZ = new Vector3(0f, -8.5f, 0f) },
                }
            };

            BonePose domain = dto.ToDomain();

            Assert.AreEqual("domain-trip", domain.Id);
            var entries = domain.Entries.Span;
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("Head", entries[0].BoneName);
            Assert.AreEqual(5f, entries[0].EulerX);
            Assert.AreEqual(10f, entries[0].EulerY);
            Assert.AreEqual(15f, entries[0].EulerZ);
            Assert.AreEqual("LeftEye", entries[1].BoneName);
            Assert.AreEqual(-8.5f, entries[1].EulerY);
        }

        [Test]
        public void BonePoseDto_FromDomain_PreservesIdAndEntries()
        {
            var domain = new BonePose("from-domain", new[]
            {
                new BonePoseEntry("Neck", 1.5f, -2.5f, 3.5f),
                new BonePoseEntry("RightEye", 0f, 7.25f, 0f),
            });

            BonePoseDto dto = BonePoseDto.FromDomain(domain);

            Assert.AreEqual("from-domain", dto.id);
            Assert.IsNotNull(dto.entries);
            Assert.AreEqual(2, dto.entries.Count);
            Assert.AreEqual("Neck", dto.entries[0].boneName);
            Assert.AreEqual(new Vector3(1.5f, -2.5f, 3.5f), dto.entries[0].eulerXYZ);
            Assert.AreEqual("RightEye", dto.entries[1].boneName);
            Assert.AreEqual(new Vector3(0f, 7.25f, 0f), dto.entries[1].eulerXYZ);
        }

        [Test]
        public void BonePoseDto_RoundTrip_JsonToDtoToDomainToDto_PreservesAllValues()
        {
            // Task 4.1 主要観測条件: JSON → DTO → Domain → DTO の全経路で名前/値が同値
            var json = "{" +
                       "\"id\":\"round-trip\"," +
                       "\"entries\":[" +
                       "{\"boneName\":\"Head\",\"eulerXYZ\":{\"x\":5.5,\"y\":-10.25,\"z\":0.125}}," +
                       "{\"boneName\":\"LeftEye\",\"eulerXYZ\":{\"x\":0.0,\"y\":12.5,\"z\":0.0}}" +
                       "]}";

            var firstDto = JsonUtility.FromJson<BonePoseDto>(json);
            BonePose domain = firstDto.ToDomain();
            BonePoseDto secondDto = BonePoseDto.FromDomain(domain);

            Assert.AreEqual(firstDto.id, secondDto.id);
            Assert.AreEqual(firstDto.entries.Count, secondDto.entries.Count);
            for (int i = 0; i < firstDto.entries.Count; i++)
            {
                Assert.AreEqual(firstDto.entries[i].boneName, secondDto.entries[i].boneName);
                Assert.AreEqual(firstDto.entries[i].eulerXYZ, secondDto.entries[i].eulerXYZ);
            }
        }

        [Test]
        public void BonePoseDto_RoundTrip_MultiByteBoneNames_SurviveJsonAndDomainConversion()
        {
            // Req 2.2: 多バイト boneName が JSON / Domain / JSON の全経路で壊れない
            var src = new BonePoseDto
            {
                id = "multibyte-pose",
                entries = new System.Collections.Generic.List<BonePoseEntryDto>
                {
                    new BonePoseEntryDto { boneName = "頭",       eulerXYZ = new Vector3(1f, 2f, 3f) },
                    new BonePoseEntryDto { boneName = "左目_あ",  eulerXYZ = new Vector3(0f, 4.5f, 0f) },
                    new BonePoseEntryDto { boneName = "右目★",   eulerXYZ = new Vector3(-1f, 0f, 0f) },
                }
            };

            var json = JsonUtility.ToJson(src);
            var afterJson = JsonUtility.FromJson<BonePoseDto>(json);
            BonePose domain = afterJson.ToDomain();
            BonePoseDto roundTripped = BonePoseDto.FromDomain(domain);

            Assert.AreEqual("multibyte-pose", roundTripped.id);
            Assert.AreEqual(3, roundTripped.entries.Count);
            Assert.AreEqual("頭", roundTripped.entries[0].boneName);
            Assert.AreEqual("左目_あ", roundTripped.entries[1].boneName);
            Assert.AreEqual("右目★", roundTripped.entries[2].boneName);

            var domainEntries = domain.Entries.Span;
            Assert.AreEqual("頭", domainEntries[0].BoneName);
            Assert.AreEqual("左目_あ", domainEntries[1].BoneName);
            Assert.AreEqual("右目★", domainEntries[2].BoneName);
        }

        [Test]
        public void BonePoseDto_RoundTrip_FloatPrecision_PreservedAcrossDomainConversion()
        {
            // Req 8.5: float round-trip 精度を JSON ↔ DTO ↔ Domain ↔ DTO 経路で保持
            var src = new BonePoseDto
            {
                id = "precision",
                entries = new System.Collections.Generic.List<BonePoseEntryDto>
                {
                    new BonePoseEntryDto { boneName = "Head", eulerXYZ = new Vector3(179.99f, -179.99f, 0.0009765625f) },
                }
            };

            var json = JsonUtility.ToJson(src);
            var afterJson = JsonUtility.FromJson<BonePoseDto>(json);
            BonePose domain = afterJson.ToDomain();
            BonePoseDto roundTripped = BonePoseDto.FromDomain(domain);

            Assert.AreEqual(src.entries[0].eulerXYZ.x, roundTripped.entries[0].eulerXYZ.x);
            Assert.AreEqual(src.entries[0].eulerXYZ.y, roundTripped.entries[0].eulerXYZ.y);
            Assert.AreEqual(src.entries[0].eulerXYZ.z, roundTripped.entries[0].eulerXYZ.z);
        }

        // ================================================================
        // 不正値: Domain ctor のバリデーションが DTO 経由でも有効
        // ================================================================

        [Test]
        public void BonePoseDto_ToDomain_WithEmptyBoneName_ThrowsArgumentException()
        {
            var dto = new BonePoseDto
            {
                id = "invalid",
                entries = new System.Collections.Generic.List<BonePoseEntryDto>
                {
                    new BonePoseEntryDto { boneName = string.Empty, eulerXYZ = Vector3.zero },
                }
            };

            Assert.Throws<ArgumentException>(() => dto.ToDomain());
        }

        [Test]
        public void BonePoseDto_ToDomain_WithDuplicateBoneName_ThrowsArgumentException()
        {
            var dto = new BonePoseDto
            {
                id = "dup",
                entries = new System.Collections.Generic.List<BonePoseEntryDto>
                {
                    new BonePoseEntryDto { boneName = "Head", eulerXYZ = Vector3.zero },
                    new BonePoseEntryDto { boneName = "Head", eulerXYZ = new Vector3(1f, 0f, 0f) },
                }
            };

            Assert.Throws<ArgumentException>(() => dto.ToDomain());
        }
    }
}
