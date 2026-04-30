using System.IO;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests
{
    /// <summary>
    /// 抽象 <see cref="FacialCharacterProfileSO"/> の挙動を検証する。
    /// テスト用の最小具象 SO を介して LoadProfile / BuildFallbackProfile / 規約パス算出をカバーする。
    /// </summary>
    [TestFixture]
    public class FacialCharacterProfileSOTests
    {
        private sealed class TestCharacterSO : FacialCharacterProfileSO { }

        [Test]
        public void GetStreamingAssetsProfilePath_ValidName_ReturnsExpectedPath()
        {
            string path = FacialCharacterProfileSO.GetStreamingAssetsProfilePath("Miku");

            Assert.That(path, Is.Not.Null);
            string expectedTail = Path.Combine(
                FacialCharacterProfileSO.StreamingAssetsRootFolder,
                "Miku",
                FacialCharacterProfileSO.ProfileJsonFileName);
            StringAssert.EndsWith(expectedTail, path);
        }

        [Test]
        public void GetStreamingAssetsProfilePath_EmptyName_ReturnsNull()
        {
            Assert.That(FacialCharacterProfileSO.GetStreamingAssetsProfilePath(null), Is.Null);
            Assert.That(FacialCharacterProfileSO.GetStreamingAssetsProfilePath(""), Is.Null);
            Assert.That(FacialCharacterProfileSO.GetStreamingAssetsProfilePath("   "), Is.Null);
        }

        [Test]
        public void BuildFallbackProfile_FromInspectorData_ReflectsAllFields()
        {
            var so = ScriptableObject.CreateInstance<TestCharacterSO>();
            try
            {
                so.SchemaVersion = "1.0";
                so.Layers.Add(new LayerDefinitionSerializable
                {
                    name = "emotion",
                    priority = 0,
                    exclusionMode = ExclusionMode.LastWins,
                    inputSources = { new InputSourceDeclarationSerializable { id = "controller-expr", weight = 1.0f } },
                });
                so.Expressions.Add(new ExpressionSerializable
                {
                    id = "smile",
                    name = "Smile",
                    layer = "emotion",
                    transitionDuration = 0.25f,
                });
                so.RendererPaths.Add("Body");

                var profile = so.BuildFallbackProfile();

                Assert.That(profile.SchemaVersion, Is.EqualTo("1.0"));
                Assert.That(profile.Layers.Length, Is.EqualTo(1));
                Assert.That(profile.Expressions.Length, Is.EqualTo(1));
                Assert.That(profile.LayerInputSources.Length, Is.EqualTo(1));
                Assert.That(profile.RendererPaths.Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void LoadProfile_NoStreamingAssetsJson_FallsBackToInspectorData()
        {
            var so = ScriptableObject.CreateInstance<TestCharacterSO>();
            try
            {
                // 既知の存在しないアセット名: 衝突回避のため UUID 風の prefix。
                so.name = "NonExistent_" + System.Guid.NewGuid().ToString("N");
                so.Layers.Add(new LayerDefinitionSerializable
                {
                    name = "emotion",
                    priority = 0,
                    exclusionMode = ExclusionMode.LastWins,
                });

                var profile = so.LoadProfile();

                Assert.That(profile.Layers.Length, Is.EqualTo(1));
                Assert.That(profile.Layers.Span[0].Name, Is.EqualTo("emotion"));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void CharacterAssetName_ReflectsScriptableObjectName()
        {
            var so = ScriptableObject.CreateInstance<TestCharacterSO>();
            try
            {
                so.name = "MyCharacter";
                Assert.That(so.CharacterAssetName, Is.EqualTo("MyCharacter"));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }
    }
}
