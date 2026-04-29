using System;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// FacialProfile への BonePoses optional 追加に対する後方互換テスト（Red）。
    ///
    /// 検証範囲:
    ///   - 既存コンストラクタ呼出（bonePoses 引数なし）が warning なく通り、
    ///     <see cref="FacialProfile.BonePoses"/> が空配列（Length == 0）を返す
    ///   - BonePoses を渡したオーバーロードで防御的コピーが行われる
    ///     （外部配列を後から書き換えても内部値が変わらない）
    ///   - null / 空配列の BonePoses が空として扱われる
    ///   - 既存 FacialProfileTests と同等の他フィールド構造的保証（無修正担保）
    ///
    /// _Requirements: 1.4, 1.5, 10.1, 10.3
    /// _Boundary: Domain.Models.FacialProfile
    /// _Depends: 1.4 (BonePose)
    /// </summary>
    [TestFixture]
    public class FacialProfileBonePosesBackwardCompatTests
    {
        // --- ヘルパー ---

        private static LayerDefinition[] CreateDefaultLayers()
        {
            return new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
                new LayerDefinition("lipsync", 1, ExclusionMode.Blend),
                new LayerDefinition("eye", 2, ExclusionMode.LastWins)
            };
        }

        private static Expression CreateExpression(
            string id = "expr-1",
            string name = "smile",
            string layer = "emotion")
        {
            return new Expression(id, name, layer);
        }

        private static BonePose CreateBonePose(string id, string boneName)
        {
            return new BonePose(id, new[]
            {
                new BonePoseEntry(boneName, 0f, 0f, 0f)
            });
        }

        // --- 既存コンストラクタ（bonePoses 引数なし）の後方互換 ---

        [Test]
        public void Constructor_WithoutBonePoses_BonePosesIsEmpty()
        {
            var profile = new FacialProfile("1.0", CreateDefaultLayers(), new[] { CreateExpression() });

            Assert.AreEqual(0, profile.BonePoses.Length);
        }

        [Test]
        public void Constructor_MinimalArgs_BonePosesIsEmpty()
        {
            var profile = new FacialProfile("1.0");

            Assert.AreEqual(0, profile.BonePoses.Length);
        }

        [Test]
        public void Constructor_WithRendererPaths_BonePosesIsEmpty()
        {
            var paths = new[] { "Armature/Body", "Armature/Face" };

            var profile = new FacialProfile("1.0", CreateDefaultLayers(), null, paths);

            Assert.AreEqual(0, profile.BonePoses.Length);
        }

        [Test]
        public void Constructor_WithLayerInputSources_BonePosesIsEmpty()
        {
            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                Array.Empty<InputSourceDeclaration[]>());

            Assert.AreEqual(0, profile.BonePoses.Length);
        }

        [Test]
        public void BonePoses_DefaultValue_IsReadOnlyMemoryOfBonePose()
        {
            var profile = new FacialProfile("1.0");

            ReadOnlyMemory<BonePose> bonePoses = profile.BonePoses;

            Assert.AreEqual(0, bonePoses.Length);
        }

        // --- 新オーバーロード: BonePoses を受け取る ---

        [Test]
        public void Constructor_WithBonePoses_StoresAllBonePoses()
        {
            var bonePoses = new[]
            {
                CreateBonePose("look-front", "LeftEye"),
                CreateBonePose("look-right", "RightEye"),
            };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                null,
                bonePoses);

            Assert.AreEqual(2, profile.BonePoses.Length);
        }

        [Test]
        public void Constructor_NullBonePoses_TreatedAsEmpty()
        {
            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                null,
                null);

            Assert.AreEqual(0, profile.BonePoses.Length);
        }

        [Test]
        public void Constructor_EmptyBonePoses_TreatedAsEmpty()
        {
            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                null,
                Array.Empty<BonePose>());

            Assert.AreEqual(0, profile.BonePoses.Length);
        }

        // --- 防御的コピー（不変性の保証） ---

        [Test]
        public void BonePoses_IsDefensiveCopy_OriginalArrayModificationDoesNotAffect()
        {
            var original = CreateBonePose("look-front", "LeftEye");
            var bonePoses = new[] { original };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                null,
                bonePoses);

            // 外部配列を書換える
            bonePoses[0] = CreateBonePose("modified", "Head");

            // FacialProfile 内部は元の値を保持していること
            Assert.AreEqual(1, profile.BonePoses.Length);
            Assert.AreEqual("look-front", profile.BonePoses.Span[0].Id);
        }

        [Test]
        public void BonePoses_IsDefensiveCopy_OriginalArrayLengthChangeDoesNotAffect()
        {
            var bonePoses = new[]
            {
                CreateBonePose("look-front", "LeftEye"),
                CreateBonePose("look-right", "RightEye"),
            };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                null,
                bonePoses);

            // 外部配列のエントリを書換えても内部長は固定
            bonePoses[0] = CreateBonePose("hijacked", "Head");

            Assert.AreEqual(2, profile.BonePoses.Length);
            Assert.AreEqual("look-front", profile.BonePoses.Span[0].Id);
            Assert.AreEqual("look-right", profile.BonePoses.Span[1].Id);
        }

        // --- 既存フィールドの非破壊性（Req 10.1 / 10.3 の構造的保証） ---

        [Test]
        public void Constructor_WithBonePoses_PreservesExistingLayers()
        {
            var bonePoses = new[] { CreateBonePose("look-front", "LeftEye") };
            var layers = CreateDefaultLayers();

            var profile = new FacialProfile(
                "1.0",
                layers,
                null,
                null,
                null,
                bonePoses);

            Assert.AreEqual(3, profile.Layers.Length);
            Assert.AreEqual("emotion", profile.Layers.Span[0].Name);
        }

        [Test]
        public void Constructor_WithBonePoses_PreservesExistingExpressions()
        {
            var bonePoses = new[] { CreateBonePose("look-front", "LeftEye") };
            var expressions = new[] { CreateExpression("id-1", "smile", "emotion") };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                expressions,
                null,
                null,
                bonePoses);

            Assert.AreEqual(1, profile.Expressions.Length);
            Assert.AreEqual("id-1", profile.Expressions.Span[0].Id);
        }

        [Test]
        public void Constructor_WithBonePoses_PreservesRendererPaths()
        {
            var bonePoses = new[] { CreateBonePose("look-front", "LeftEye") };
            var paths = new[] { "Armature/Body", "Armature/Face" };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                paths,
                null,
                bonePoses);

            Assert.AreEqual(2, profile.RendererPaths.Length);
            Assert.AreEqual("Armature/Body", profile.RendererPaths.Span[0]);
        }

        [Test]
        public void Constructor_WithBonePoses_PreservesLayerInputSources()
        {
            var bonePoses = new[] { CreateBonePose("look-front", "LeftEye") };
            var layerInputSources = new[]
            {
                Array.Empty<InputSourceDeclaration>(),
                Array.Empty<InputSourceDeclaration>(),
                Array.Empty<InputSourceDeclaration>(),
            };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                layerInputSources,
                bonePoses);

            Assert.AreEqual(3, profile.LayerInputSources.Length);
        }

        [Test]
        public void Constructor_WithBonePoses_PreservesSchemaVersion()
        {
            var bonePoses = new[] { CreateBonePose("look-front", "LeftEye") };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                null,
                bonePoses);

            Assert.AreEqual("1.0", profile.SchemaVersion);
        }

        // --- 多バイト boneName を含む BonePoses（Req 10.3 の round-trip 担保） ---

        [Test]
        public void Constructor_WithMultiByteBonePose_StoresAndPreserves()
        {
            var bonePoses = new[]
            {
                new BonePose("視線_前方", new[]
                {
                    new BonePoseEntry("頭", 0f, 0f, 0f),
                    new BonePoseEntry("左目", 0f, 0f, 0f),
                }),
            };

            var profile = new FacialProfile(
                "1.0",
                CreateDefaultLayers(),
                null,
                null,
                null,
                bonePoses);

            Assert.AreEqual(1, profile.BonePoses.Length);
            Assert.AreEqual("視線_前方", profile.BonePoses.Span[0].Id);
            Assert.AreEqual(2, profile.BonePoses.Span[0].Entries.Length);
            Assert.AreEqual("頭", profile.BonePoses.Span[0].Entries.Span[0].BoneName);
        }
    }
}
