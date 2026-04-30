using System.IO;
using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Samples
{
    /// <summary>
    /// <c>AnalogBindingDemo</c> サンプル一式の smoke テスト (新統合 SO 形式)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Samples~ は Unity のコンパイル対象外のため、Test Runner から直接 Scene を
    /// ロードする経路は存在しない。代わりに以下を整合性として検証する:
    /// </para>
    /// <list type="bullet">
    ///   <item>Samples~/AnalogBindingDemo/ に必須ファイル (新形式) が揃っていること</item>
    ///   <item>StreamingAssets/.../analog_bindings.json が <see cref="AnalogInputBindingJsonLoader.Load"/> で
    ///         パースでき、想定の binding 件数 (right_stick × 4 + arkit_jaw_open × 1) を持つこと</item>
    ///   <item>JSON が round-trip (Load → Save → Load) で安定すること</item>
    ///   <item>dev mirror (Assets/Samples) と Samples~ の <c>*.cs</c> / <c>*.json</c> が drift していないこと</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class AnalogBindingDemoSmokeTests
    {
        private const string SamplesCanonicalDir =
            "Packages/com.hidano.facialcontrol.inputsystem/Samples~/AnalogBindingDemo";

        private const string SamplesMirrorDir =
            "Assets/Samples/FacialControl InputSystem/0.1.0-preview.1/Analog Binding Demo";

        private const string AnalogBindingsRelativePath =
            "StreamingAssets/FacialControl/AnalogBindingDemoCharacter/analog_bindings.json";

        private const string ProfileRelativePath =
            "StreamingAssets/FacialControl/AnalogBindingDemoCharacter/profile.json";

        private static readonly string[] s_requiredFiles = new[]
        {
            "AnalogBindingDemo.unity",
            "AnalogBindingDemoCharacter.asset",
            "AnalogBindingDemoHUD.cs",
            "README.md",
            AnalogBindingsRelativePath,
            ProfileRelativePath
        };

        [Test]
        public void SamplesCanonical_HasAllRequiredFiles()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            foreach (var name in s_requiredFiles)
            {
                var path = Path.Combine(projectRoot, SamplesCanonicalDir, name);
                Assert.IsTrue(File.Exists(path),
                    $"Samples~/AnalogBindingDemo/{name} が存在しません: {path}");
            }
        }

        [Test]
        public void AnalogBindingDemoJson_LoadsExpectedBindings()
        {
            var json = LoadCanonicalAnalogJson();
            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual("1.0.0", profile.Version, "version は 1.0.0 を期待");
            Assert.AreEqual(5, profile.Bindings.Length,
                "right_stick 4 binding (LeftEye/RightEye の Y/X) + arkit_jaw_open 1 binding を期待");

            var bindings = profile.Bindings.Span;
            int boneposeCount = 0;
            int blendshapeCount = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].TargetKind == AnalogBindingTargetKind.BonePose) boneposeCount++;
                if (bindings[i].TargetKind == AnalogBindingTargetKind.BlendShape) blendshapeCount++;
            }
            Assert.AreEqual(4, boneposeCount, "BonePose binding は 4 件");
            Assert.AreEqual(1, blendshapeCount, "BlendShape binding は 1 件");
        }

        [Test]
        public void AnalogBindingDemoJson_RoundTripsStably()
        {
            var json = LoadCanonicalAnalogJson();
            var first = AnalogInputBindingJsonLoader.Load(json);
            var resaved = AnalogInputBindingJsonLoader.Save(first);
            var second = AnalogInputBindingJsonLoader.Load(resaved);

            Assert.AreEqual(first.Version, second.Version);
            Assert.AreEqual(first.Bindings.Length, second.Bindings.Length,
                "round-trip 後も binding 件数が保たれること");

            var aSpan = first.Bindings.Span;
            var bSpan = second.Bindings.Span;
            for (int i = 0; i < aSpan.Length; i++)
            {
                Assert.AreEqual(aSpan[i].SourceId, bSpan[i].SourceId);
                Assert.AreEqual(aSpan[i].SourceAxis, bSpan[i].SourceAxis);
                Assert.AreEqual(aSpan[i].TargetKind, bSpan[i].TargetKind);
                Assert.AreEqual(aSpan[i].TargetIdentifier, bSpan[i].TargetIdentifier);
                Assert.AreEqual(aSpan[i].TargetAxis, bSpan[i].TargetAxis);
                Assert.AreEqual(aSpan[i].Mapping.Scale, bSpan[i].Mapping.Scale, 1e-6f);
                Assert.AreEqual(aSpan[i].Mapping.Min, bSpan[i].Mapping.Min, 1e-6f);
                Assert.AreEqual(aSpan[i].Mapping.Max, bSpan[i].Mapping.Max, 1e-6f);
            }
        }

        [Test]
        public void DevMirror_FilesMatchCanonical()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            // .cs と .json はランタイム挙動に直結するため drift があると Package Manager 経由
            // で import したユーザーと dev の挙動が乖離する。同期されていることをアサート。
            string[] driftCriticalFiles =
            {
                "AnalogBindingDemoHUD.cs",
                AnalogBindingsRelativePath,
                ProfileRelativePath
            };

            foreach (var name in driftCriticalFiles)
            {
                var canonical = Path.Combine(projectRoot, SamplesCanonicalDir, name);
                var mirror = Path.Combine(projectRoot, SamplesMirrorDir, name);

                Assert.IsTrue(File.Exists(canonical), $"canonical 不在: {canonical}");
                Assert.IsTrue(File.Exists(mirror), $"dev mirror 不在: {mirror}");

                string canonicalText = File.ReadAllText(canonical);
                string mirrorText = File.ReadAllText(mirror);
                Assert.AreEqual(NormalizeLineEndings(canonicalText), NormalizeLineEndings(mirrorText),
                    $"{name} が canonical (Samples~) と dev mirror (Assets/Samples) で drift しています。" +
                    "両方を同期させてください (CLAUDE.md の Samples 二重管理ルール)。");
            }
        }

        private static string LoadCanonicalAnalogJson()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            var path = Path.Combine(projectRoot, SamplesCanonicalDir, AnalogBindingsRelativePath);
            Assert.IsTrue(File.Exists(path), $"canonical JSON 不在: {path}");
            return File.ReadAllText(path);
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
