using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.FileSystem;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using Hidano.FacialControl.InputSystem.Editor.AutoExport;
using DomainExpression = Hidano.FacialControl.Domain.Models.Expression;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Editor.AutoExport
{
    /// <summary>
    /// Task 6: <see cref="FacialCharacterSOAutoExporter.ExportToStreamingAssets"/> の動作検証。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検証観点:
    /// <list type="bullet">
    ///   <item><see cref="FacialCharacterSO"/> 保存時に <c>profile.json</c> が StreamingAssets 配下に出力される</item>
    ///   <item>同 SO で <c>analog_bindings.json</c> も併せて出力される</item>
    ///   <item>抽象基底のみの具象 SO (<see cref="FacialCharacterSO"/> 以外) では analog_bindings.json は出力されない</item>
    ///   <item>ラウンドトリップ: 出力 JSON を <see cref="FileProfileRepository"/> で読み戻すと SO データと一致する</item>
    ///   <item>SO 名が空のときは警告ログのみで no-op</item>
    /// </list>
    /// </para>
    /// <para>
    /// テスト用ファイルは <see cref="Application.streamingAssetsPath"/> 配下の専用フォルダに書き出され、
    /// <see cref="TearDown"/> で必ず削除する (dev プロジェクトの version control に diff を残さない)。
    /// </para>
    /// </remarks>
    [TestFixture]
    public class FacialCharacterSOAutoExporterTests
    {
        /// <summary>
        /// 抽象 <see cref="FacialCharacterProfileSO"/> のみを継承するテスト用具象 SO。
        /// アナログバインディングは持たないため、analog_bindings.json は出力されない想定。
        /// </summary>
        private sealed class TestCoreOnlyCharacterSO : FacialCharacterProfileSO { }

        private string _tempAssetName;
        private string _tempCharacterFolder;

        [SetUp]
        public void SetUp()
        {
            // 衝突回避のため毎回 GUID で一意化。先頭にプレフィックスを付けて手動清掃を可能にする。
            _tempAssetName = "AutoExporterTest_" + Guid.NewGuid().ToString("N");
            _tempCharacterFolder = Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                FacialCharacterProfileSO.StreamingAssetsRootFolder,
                _tempAssetName);
        }

        [TearDown]
        public void TearDown()
        {
            // テスト出力フォルダを丸ごと削除し、dev プロジェクトに残骸を残さない。
            try
            {
                if (!string.IsNullOrEmpty(_tempCharacterFolder) && Directory.Exists(_tempCharacterFolder))
                {
                    Directory.Delete(_tempCharacterFolder, recursive: true);
                }
                // .meta が AssetDatabase 経由で生成されている場合は併せて削除。
                string metaPath = _tempCharacterFolder + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"FacialCharacterSOAutoExporterTests: TearDown でテストフォルダ削除に失敗しました: {ex.Message}");
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private FacialCharacterSO CreateInputCharacterSO()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            so.name = _tempAssetName;
            so.SchemaVersion = "1.0";

            so.Layers.Add(new LayerDefinitionSerializable
            {
                name = "emotion",
                priority = 0,
                exclusionMode = ExclusionMode.LastWins,
            });
            so.Expressions.Add(new ExpressionSerializable
            {
                id = "smile",
                name = "Smile",
                layer = "emotion",
                transitionDuration = 0.25f,
            });
            so.RendererPaths.Add("Body");

            so.AnalogBindings.Add(new AnalogBindingEntrySerializable
            {
                sourceId = "x-right-stick",
                sourceAxis = 0,
                targetKind = AnalogBindingTargetKind.BlendShape,
                targetIdentifier = "Mouth_A",
                targetAxis = AnalogTargetAxis.X,
                mapping = new AnalogMappingFunctionSerializable
                {
                    deadZone = 0.1f,
                    scale = 1.0f,
                    offset = 0.0f,
                    invert = false,
                    min = 0.0f,
                    max = 1.0f,
                },
            });

            return so;
        }

        // ============================================================
        // profile.json: 出力されること
        // ============================================================

        [Test]
        public void ExportToStreamingAssets_FacialCharacterSO_WritesProfileJson()
        {
            var so = CreateInputCharacterSO();
            try
            {
                bool result = FacialCharacterSOAutoExporter.ExportToStreamingAssets(so);

                Assert.IsTrue(result, "ExportToStreamingAssets は true を返すべきです (少なくとも 1 ファイルを書き出す)。");

                string profilePath = FacialCharacterProfileSO.GetStreamingAssetsProfilePath(_tempAssetName);
                Assert.IsTrue(File.Exists(profilePath),
                    $"profile.json が規約パスに出力されるべきです: {profilePath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ============================================================
        // analog_bindings.json: FacialCharacterSO で出力されること
        // ============================================================

        [Test]
        public void ExportToStreamingAssets_FacialCharacterSO_WritesAnalogBindingsJson()
        {
            var so = CreateInputCharacterSO();
            try
            {
                FacialCharacterSOAutoExporter.ExportToStreamingAssets(so);

                string analogPath = FacialCharacterSOAutoExporter
                    .GetStreamingAssetsAnalogBindingsPath(_tempAssetName);
                Assert.IsTrue(File.Exists(analogPath),
                    $"analog_bindings.json が規約パスに出力されるべきです: {analogPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ============================================================
        // 抽象基底のみ: analog_bindings.json は出力されない
        // ============================================================

        [Test]
        public void ExportToStreamingAssets_CoreOnlySO_DoesNotWriteAnalogBindings()
        {
            var so = ScriptableObject.CreateInstance<TestCoreOnlyCharacterSO>();
            try
            {
                so.name = _tempAssetName;
                so.SchemaVersion = "1.0";
                so.Layers.Add(new LayerDefinitionSerializable
                {
                    name = "emotion",
                    priority = 0,
                    exclusionMode = ExclusionMode.LastWins,
                });

                bool result = FacialCharacterSOAutoExporter.ExportToStreamingAssets(so);
                Assert.IsTrue(result, "profile.json だけでも書き出せれば true を返すべきです。");

                string profilePath = FacialCharacterProfileSO.GetStreamingAssetsProfilePath(_tempAssetName);
                Assert.IsTrue(File.Exists(profilePath), "profile.json は出力されるべきです。");

                string analogPath = FacialCharacterSOAutoExporter
                    .GetStreamingAssetsAnalogBindingsPath(_tempAssetName);
                Assert.IsFalse(File.Exists(analogPath),
                    "FacialCharacterSO 派生でない SO に対して analog_bindings.json は出力されてはなりません。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ============================================================
        // ラウンドトリップ: 出力 JSON を読み戻し、元の SO データと一致
        // ============================================================

        [Test]
        public void ExportedProfileJson_RoundTripsThroughFileProfileRepository()
        {
            var so = CreateInputCharacterSO();
            try
            {
                FacialCharacterSOAutoExporter.ExportToStreamingAssets(so);

                string profilePath = FacialCharacterProfileSO.GetStreamingAssetsProfilePath(_tempAssetName);
                Assert.IsTrue(File.Exists(profilePath), "profile.json が事前に出力されている必要があります。");

                var repo = new FileProfileRepository(new SystemTextJsonParser());
                FacialProfile loaded = repo.LoadProfile(profilePath);
                FacialProfile expected = so.BuildFallbackProfile();

                Assert.AreEqual(expected.SchemaVersion, loaded.SchemaVersion,
                    "schemaVersion がラウンドトリップで一致するべきです。");
                Assert.AreEqual(expected.Layers.Length, loaded.Layers.Length,
                    "Layer 数がラウンドトリップで一致するべきです。");
                Assert.AreEqual(expected.Expressions.Length, loaded.Expressions.Length,
                    "Expression 数がラウンドトリップで一致するべきです。");
                Assert.AreEqual(expected.RendererPaths.Length, loaded.RendererPaths.Length,
                    "RendererPaths 数がラウンドトリップで一致するべきです。");

                if (expected.Expressions.Length > 0 && loaded.Expressions.Length > 0)
                {
                    DomainExpression expectedFirst = expected.Expressions.Span[0];
                    DomainExpression loadedFirst = loaded.Expressions.Span[0];
                    Assert.AreEqual(expectedFirst.Id, loadedFirst.Id,
                        "先頭 Expression の Id がラウンドトリップで一致するべきです。");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ============================================================
        // ラウンドトリップ: analog_bindings.json
        // ============================================================

        [Test]
        public void ExportedAnalogBindingsJson_RoundTripsThroughLoader()
        {
            var so = CreateInputCharacterSO();
            try
            {
                FacialCharacterSOAutoExporter.ExportToStreamingAssets(so);

                string analogPath = FacialCharacterSOAutoExporter
                    .GetStreamingAssetsAnalogBindingsPath(_tempAssetName);
                Assert.IsTrue(File.Exists(analogPath), "analog_bindings.json が事前に出力されている必要があります。");

                string json = File.ReadAllText(analogPath, System.Text.Encoding.UTF8);
                AnalogInputBindingProfile loaded = AnalogInputBindingJsonLoader.Load(json);

                Assert.AreEqual(1, loaded.Bindings.Length,
                    "ラウンドトリップしたアナログバインディング件数は元の SO データと一致するべきです。");

                AnalogBindingEntry first = loaded.Bindings.Span[0];
                Assert.AreEqual("x-right-stick", first.SourceId);
                Assert.AreEqual(0, first.SourceAxis);
                Assert.AreEqual(AnalogBindingTargetKind.BlendShape, first.TargetKind);
                Assert.AreEqual("Mouth_A", first.TargetIdentifier);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ============================================================
        // SO 名が空: 警告のみで no-op
        // ============================================================

        [Test]
        public void ExportToStreamingAssets_EmptyAssetName_LogsWarningAndReturnsFalse()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            try
            {
                so.name = string.Empty;

                LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex("SO 名が空のため"));

                bool result = FacialCharacterSOAutoExporter.ExportToStreamingAssets(so);

                Assert.IsFalse(result, "SO 名が空のときは false を返すべきです。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ============================================================
        // null 入力: no-op
        // ============================================================

        [Test]
        public void ExportToStreamingAssets_NullSO_ReturnsFalseSilently()
        {
            bool result = FacialCharacterSOAutoExporter.ExportToStreamingAssets(null);
            Assert.IsFalse(result, "null 入力に対しては false を返すべきです (例外を投げない)。");
        }
    }
}
