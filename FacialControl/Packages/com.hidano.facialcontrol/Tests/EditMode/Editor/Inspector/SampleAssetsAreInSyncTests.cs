using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Inspector
{
    [TestFixture]
    public class SampleAssetsAreInSyncTests
    {
        private const string DevStreamingProfilePath =
            "Assets/StreamingAssets/FacialControl/MultiSourceBlendDemoCharacter/profile.json";

        private const string ImportedSampleAssetPath =
            "Assets/Samples/FacialControl InputSystem/0.1.0-preview.2/Multi Source Blend Demo/MultiSourceBlendDemoCharacter.asset";

        private const string PackageSampleProfilePath =
            "Packages/com.hidano.facialcontrol.inputsystem/Samples~/MultiSourceBlendDemo/StreamingAssets/FacialControl/MultiSourceBlendDemoCharacter/profile.json";

        private const string PackageSampleAssetPath =
            "Packages/com.hidano.facialcontrol.inputsystem/Samples~/MultiSourceBlendDemo/MultiSourceBlendDemoCharacter.asset";

        [Test]
        public void ProfileJson_DevStreamingAssetsAndPackageSample_AreByteIdentical()
        {
            string devPath = ResolveProjectPath(DevStreamingProfilePath);
            string packagePath = ResolveProjectPath(PackageSampleProfilePath);
            AssertFileExists(devPath, DevStreamingProfilePath);
            AssertFileExists(packagePath, PackageSampleProfilePath);

            byte[] devBytes = File.ReadAllBytes(devPath);
            byte[] packageBytes = File.ReadAllBytes(packagePath);

            Assert.That(
                devBytes,
                Is.EqualTo(packageBytes),
                "MultiSourceBlendDemo profile.json drift detected between dev StreamingAssets and package Samples~.\n" +
                $"Dev: {DevStreamingProfilePath}\n" +
                $"Samples~: {PackageSampleProfilePath}");
        }

        [Test]
        public void MultiSourceBlendDemoCharacterAsset_DoesNotContainLegacyBlinkOverlayExpression()
        {
            AssertDoesNotContainLegacyBlinkOverlayExpression(ImportedSampleAssetPath, required: true);
            AssertDoesNotContainLegacyBlinkOverlayExpression(PackageSampleAssetPath, required: false);
        }

        [Test]
        public void MultiSourceBlendDemoCharacterAsset_YamlKeySetMatchesPackageSample_WhenPackageAssetExists()
        {
            string importedPath = ResolveProjectPath(ImportedSampleAssetPath);
            string packagePath = ResolveProjectPath(PackageSampleAssetPath);
            AssertFileExists(importedPath, ImportedSampleAssetPath);

            if (!File.Exists(packagePath))
            {
                Assert.Pass("Package Samples~ MultiSourceBlendDemoCharacter.asset does not exist; YAML key-set comparison is not applicable.");
            }

            IReadOnlyCollection<string> importedKeys = ExtractYamlKeySet(File.ReadAllText(importedPath, Encoding.UTF8));
            IReadOnlyCollection<string> packageKeys = ExtractYamlKeySet(File.ReadAllText(packagePath, Encoding.UTF8));

            CollectionAssert.AreEquivalent(
                importedKeys,
                packageKeys,
                "MultiSourceBlendDemoCharacter.asset YAML key set drift detected between imported dev sample and package Samples~.\n" +
                $"Dev sample: {ImportedSampleAssetPath}\n" +
                $"Samples~: {PackageSampleAssetPath}");
        }

        private static void AssertDoesNotContainLegacyBlinkOverlayExpression(string relativePath, bool required)
        {
            string path = ResolveProjectPath(relativePath);
            if (!File.Exists(path))
            {
                if (required)
                {
                    Assert.Fail($"Required sample asset is missing: {relativePath}");
                }

                return;
            }

            string text = NormalizeLineEndings(File.ReadAllText(path, Encoding.UTF8));
            StringAssert.DoesNotContain(
                "\n  - id: blink_overlay\n",
                text,
                $"{relativePath} still contains the legacy blink_overlay Expression entry.");
        }

        private static IReadOnlyCollection<string> ExtractYamlKeySet(string yaml)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            var stack = new List<YamlKeyFrame>();

            using (var reader = new StringReader(NormalizeLineEndings(yaml)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("%", StringComparison.Ordinal) ||
                        trimmed.StartsWith("---", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int indent = CountLeadingSpaces(line);
                    bool sequenceEntry = trimmed.StartsWith("- ", StringComparison.Ordinal);
                    string candidate = sequenceEntry ? trimmed.Substring(2).TrimStart() : trimmed;
                    int colonIndex = candidate.IndexOf(':');
                    if (colonIndex <= 0)
                    {
                        continue;
                    }

                    string key = candidate.Substring(0, colonIndex).Trim();
                    if (!IsYamlKey(key))
                    {
                        continue;
                    }

                    TrimStack(stack, indent, sequenceEntry);

                    string keyPath = stack.Count == 0
                        ? key
                        : string.Join(".", stack.Select(frame => frame.Key).Concat(new[] { key }));
                    keys.Add(keyPath);

                    string value = candidate.Substring(colonIndex + 1).Trim();
                    if (value.Length == 0)
                    {
                        stack.Add(new YamlKeyFrame(indent, key));
                    }
                }
            }

            return keys;
        }

        private static void TrimStack(List<YamlKeyFrame> stack, int indent, bool sequenceEntry)
        {
            while (stack.Count > 0)
            {
                int previousIndent = stack[stack.Count - 1].Indent;
                bool shouldPop = sequenceEntry ? previousIndent > indent : previousIndent >= indent;
                if (!shouldPop)
                {
                    return;
                }

                stack.RemoveAt(stack.Count - 1);
            }
        }

        private static bool IsYamlKey(string key)
        {
            if (key.Length == 0)
            {
                return false;
            }

            return key.IndexOf('"') < 0 &&
                   key.IndexOf('\'') < 0 &&
                   key.IndexOf('{') < 0 &&
                   key.IndexOf('}') < 0;
        }

        private static int CountLeadingSpaces(string line)
        {
            int count = 0;
            while (count < line.Length && line[count] == ' ')
            {
                count++;
            }

            return count;
        }

        private static string ResolveProjectPath(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalized));
        }

        private static void AssertFileExists(string path, string relativePath)
        {
            Assert.That(File.Exists(path), Is.True, $"Required sample file is missing: {relativePath}");
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private readonly struct YamlKeyFrame
        {
            public YamlKeyFrame(int indent, string key)
            {
                Indent = indent;
                Key = key;
            }

            public int Indent { get; }
            public string Key { get; }
        }
    }
}
