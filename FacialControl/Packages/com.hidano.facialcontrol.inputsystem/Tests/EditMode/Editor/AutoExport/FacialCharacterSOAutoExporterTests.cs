using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Sampling;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using Hidano.FacialControl.InputSystem.Editor.AutoExport;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Editor.AutoExport
{
    /// <summary>
    /// Phase 5.3: <see cref="FacialCharacterSOAutoExporter"/> の AnimationClip サンプラ経路 / 進捗 / abort 検証。
    /// </summary>
    /// <remarks>
    /// <para>必須テスト（spec tasks.md 5.3 Red 段階）:</para>
    /// <list type="bullet">
    ///   <item><c>OnWillSaveAssets_ValidSO_WritesV2JsonToStreamingAssets</c></item>
    ///   <item><c>OnWillSaveAssets_SamplerThrows_AbortsSaveAndLogsError</c></item>
    ///   <item><c>OnWillSaveAssets_ProgressBarShown_When_LongerThan200ms</c></item>
    ///   <item><c>OnWillSaveAssets_NonFacialAsset_PassesThrough</c></item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class FacialCharacterSOAutoExporterTests
    {
        private readonly List<string> _streamingAssetsCleanupRoots = new List<string>();
        private readonly List<UnityEngine.Object> _objectsToDestroy = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            // Test seam を必ず元に戻す
            FacialCharacterSOAutoExporter.SamplerOverride = null;
            FacialCharacterSOAutoExporter.ProgressBarPresenterOverride = null;
            FacialCharacterSOAutoExporter.StopwatchProviderOverride = null;
            FacialCharacterSOAutoExporter.AssetLoaderOverride = null;

            for (int i = 0; i < _objectsToDestroy.Count; i++)
            {
                if (_objectsToDestroy[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objectsToDestroy[i]);
                }
            }
            _objectsToDestroy.Clear();

            for (int i = 0; i < _streamingAssetsCleanupRoots.Count; i++)
            {
                try
                {
                    if (Directory.Exists(_streamingAssetsCleanupRoots[i]))
                    {
                        Directory.Delete(_streamingAssetsCleanupRoots[i], recursive: true);
                    }
                }
                catch (IOException) { }
            }
            _streamingAssetsCleanupRoots.Clear();
        }

        // ------------------------------------------------------------------
        // ヘルパー
        // ------------------------------------------------------------------

        private FacialCharacterSO CreateFacialCharacterSOWithBlendShapeClip(string soName, string clipBlendShapeName, float clipValue)
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            so.name = soName;
            _objectsToDestroy.Add(so);

            // Layer 追加
            var layer = new LayerDefinitionSerializable
            {
                name = "emotion",
                priority = 0,
                exclusionMode = ExclusionMode.LastWins,
                inputSources = new List<InputSourceDeclarationSerializable>
                {
                    new InputSourceDeclarationSerializable { id = "controller-expr", weight = 1.0f }
                }
            };
            so.Layers.Add(layer);

            // AnimationClip 作成（時刻 0 で blendShape の値）
            var clip = new AnimationClip { name = "TestClip" };
            _objectsToDestroy.Add(clip);
            var binding = new EditorCurveBinding
            {
                path = "Body",
                propertyName = $"blendShape.{clipBlendShapeName}",
                type = typeof(SkinnedMeshRenderer),
            };
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 0f, clipValue));

            // Expression 追加
            so.Expressions.Add(new ExpressionSerializable
            {
                id = "expr-A",
                name = "Smile",
                layer = "emotion",
                animationClip = clip,
                layerOverrideMask = new List<string> { "emotion" },
            });

            return so;
        }

        private void RegisterStreamingAssetsCleanup(string soName)
        {
            var root = Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                FacialCharacterProfileSO.StreamingAssetsRootFolder,
                soName);
            _streamingAssetsCleanupRoots.Add(root);
        }

        private static string ExpectedProfileJsonPath(string soName)
        {
            return FacialCharacterProfileSO.GetStreamingAssetsProfilePath(soName);
        }

        // ------------------------------------------------------------------
        // 1. ValidSO → schema v2.0 JSON が StreamingAssets に書き出される
        // ------------------------------------------------------------------

        [Test]
        public void OnWillSaveAssets_ValidSO_WritesV2JsonToStreamingAssets()
        {
            string soName = "AutoExporterTestSO_Valid_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var so = CreateFacialCharacterSOWithBlendShapeClip(soName, "Smile", 0.75f);
            RegisterStreamingAssetsCleanup(soName);

            string fakePath = $"Assets/{soName}.asset";
            FacialCharacterSOAutoExporter.AssetLoaderOverride = path => path == fakePath ? so : null;

            var resultPaths = FacialCharacterSOAutoExporter.ProcessAssetSavePaths(new[] { fakePath });

            CollectionAssert.Contains(resultPaths, fakePath, "Valid SO の path は戻り値に維持されるべきです。");

            string profilePath = ExpectedProfileJsonPath(soName);
            Assert.IsTrue(File.Exists(profilePath), $"profile.json が書き出されるはず: {profilePath}");

            string json = File.ReadAllText(profilePath, System.Text.Encoding.UTF8);
            StringAssert.Contains("\"schemaVersion\": \"2.0\"", json, "schemaVersion=2.0 が出力されるべきです。");
            StringAssert.Contains("\"id\": \"expr-A\"", json, "Expression id が JSON に含まれるべきです。");
            StringAssert.Contains("\"name\": \"Smile\"", json, "blendShape 名が snapshot.blendShapes[].name に含まれるべきです。");
            StringAssert.Contains("\"snapshot\":", json, "expressions[].snapshot が出力されるべきです（v2.0）。");

            // cachedSnapshot が ExpressionSerializable に書き戻されていること
            Assert.IsNotNull(so.Expressions[0].cachedSnapshot, "AutoExporter は cachedSnapshot を populate するべきです。");
            Assert.AreEqual(1, so.Expressions[0].cachedSnapshot.blendShapes.Count);
            Assert.AreEqual("Smile", so.Expressions[0].cachedSnapshot.blendShapes[0].name);
            Assert.AreEqual(0.75f, so.Expressions[0].cachedSnapshot.blendShapes[0].value, 1e-5f);
        }

        // ------------------------------------------------------------------
        // 2. Sampler が throw → 当該 SO の path は除外される + LogError
        // ------------------------------------------------------------------

        [Test]
        public void OnWillSaveAssets_SamplerThrows_AbortsSaveAndLogsError()
        {
            string soName = "AutoExporterTestSO_Throw_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var so = CreateFacialCharacterSOWithBlendShapeClip(soName, "Anger", 1.0f);
            RegisterStreamingAssetsCleanup(soName);

            string fakePath = $"Assets/{soName}.asset";
            FacialCharacterSOAutoExporter.AssetLoaderOverride = path => path == fakePath ? so : null;
            FacialCharacterSOAutoExporter.SamplerOverride = new ThrowingSampler();

            // 内部で LogError を発行する想定（Req 9.6）。Regex で functionName 部分文字列を許容。
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*FacialCharacterSOAutoExporter.*"));

            var resultPaths = FacialCharacterSOAutoExporter.ProcessAssetSavePaths(new[] { fakePath });

            Assert.IsFalse(System.Array.Exists(resultPaths, p => p == fakePath),
                "Sampler が throw した SO の path は戻り値から除外されるべきです（Req 9.6）。");
            string profilePath = ExpectedProfileJsonPath(soName);
            Assert.IsFalse(File.Exists(profilePath), "abort された SO の profile.json は書き出されないべきです。");
        }

        // ------------------------------------------------------------------
        // 3. 200ms 超で progress bar が表示され、try/finally で Clear される
        // ------------------------------------------------------------------

        [Test]
        public void OnWillSaveAssets_ProgressBarShown_When_LongerThan200ms()
        {
            string soName = "AutoExporterTestSO_Progress_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var so = CreateFacialCharacterSOWithBlendShapeClip(soName, "Surprise", 0.5f);
            RegisterStreamingAssetsCleanup(soName);

            string fakePath = $"Assets/{soName}.asset";
            FacialCharacterSOAutoExporter.AssetLoaderOverride = path => path == fakePath ? so : null;

            var fakeStopwatch = new FakeStopwatch { ElapsedMilliseconds = 250L };
            FacialCharacterSOAutoExporter.StopwatchProviderOverride = new ConstantStopwatchProvider(fakeStopwatch);

            var fakePresenter = new FakeProgressBarPresenter();
            FacialCharacterSOAutoExporter.ProgressBarPresenterOverride = fakePresenter;

            FacialCharacterSOAutoExporter.ProcessAssetSavePaths(new[] { fakePath });

            Assert.GreaterOrEqual(fakePresenter.ShowCallCount, 1,
                "経過時間が 200ms 超であれば progress bar の Show が呼ばれるべきです（Req 9.5）。");
            Assert.GreaterOrEqual(fakePresenter.ClearCallCount, 1,
                "try/finally で progress bar の Clear が呼ばれるべきです（Req 9.5）。");
        }

        // ------------------------------------------------------------------
        // 4. 非 FacialCharacterProfileSO の path はそのまま素通し
        // ------------------------------------------------------------------

        [Test]
        public void OnWillSaveAssets_NonFacialAsset_PassesThrough()
        {
            string fakePath = "Assets/SomeOtherAsset.asset";
            string nonAssetPath = "ProjectSettings/SomeSettings.asset.txt";

            FacialCharacterSOAutoExporter.AssetLoaderOverride = _ => null;

            var inputPaths = new[] { fakePath, nonAssetPath };
            var resultPaths = FacialCharacterSOAutoExporter.ProcessAssetSavePaths(inputPaths);

            CollectionAssert.AreEquivalent(inputPaths, resultPaths,
                "FacialCharacterProfileSO 由来でない path は順序維持で素通しされるべきです。");
        }

        // ------------------------------------------------------------------
        // Test doubles
        // ------------------------------------------------------------------

        private sealed class ThrowingSampler : IExpressionAnimationClipSampler
        {
            public ExpressionSnapshot SampleSnapshot(string snapshotId, AnimationClip clip)
            {
                throw new InvalidOperationException("intentional sampling failure for test");
            }

            public ClipSummary SampleSummary(AnimationClip clip)
            {
                return new ClipSummary(
                    new List<string>(),
                    new List<string>(),
                    0.25f,
                    TransitionCurvePreset.Linear);
            }
        }

        private sealed class FakeStopwatch : FacialCharacterSOAutoExporter.IElapsedStopwatch
        {
            public long ElapsedMilliseconds { get; set; }
        }

        private sealed class ConstantStopwatchProvider : FacialCharacterSOAutoExporter.IStopwatchProvider
        {
            private readonly FacialCharacterSOAutoExporter.IElapsedStopwatch _stopwatch;

            public ConstantStopwatchProvider(FacialCharacterSOAutoExporter.IElapsedStopwatch stopwatch)
            {
                _stopwatch = stopwatch;
            }

            public FacialCharacterSOAutoExporter.IElapsedStopwatch Start()
            {
                return _stopwatch;
            }
        }

        private sealed class FakeProgressBarPresenter : FacialCharacterSOAutoExporter.IProgressBarPresenter
        {
            public int ShowCallCount;
            public int ClearCallCount;
            public string LastTitle;
            public string LastInfo;
            public float LastProgress;

            public void Show(string title, string info, float progress)
            {
                ShowCallCount++;
                LastTitle = title;
                LastInfo = info;
                LastProgress = progress;
            }

            public void Clear()
            {
                ClearCallCount++;
            }
        }
    }
}
