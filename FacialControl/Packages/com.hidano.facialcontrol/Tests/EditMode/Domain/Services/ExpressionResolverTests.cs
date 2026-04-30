using System;
using System.Collections.Generic;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Services
{
    /// <summary>
    /// <see cref="ExpressionResolver"/> の SnapshotId → BlendShape / Bone 値解決テスト
    /// （tasks.md 3.4 / Req 3.2, 9.3, 11.1, 11.4）。
    /// </summary>
    [TestFixture]
    public class ExpressionResolverTests
    {
        private const float Tolerance = 1e-6f;

        private static ExpressionSnapshot BuildSampleSnapshot(string id)
        {
            var blendShapes = new[]
            {
                new BlendShapeSnapshot("Body", "Smile", 0.5f),
                new BlendShapeSnapshot("Body", "BrowDown", 0.25f),
                new BlendShapeSnapshot("Body", "EyeWide", 0.75f),
            };
            var bones = new[]
            {
                new BoneSnapshot("Armature/Head", 0.1f, 0.2f, 0.3f, 1f, 2f, 3f, 1f, 1f, 1f),
                new BoneSnapshot("Armature/Jaw", 0f, 0f, 0f, 0f, 0f, -10f, 1f, 1f, 1f),
            };
            return new ExpressionSnapshot(
                id,
                transitionDuration: 0.25f,
                transitionCurvePreset: TransitionCurvePreset.Linear,
                blendShapes: blendShapes,
                bones: bones,
                rendererPaths: new[] { "Body" });
        }

        [Test]
        public void TryResolve_KnownId_FillsOutputs()
        {
            const string id = "smile";
            var snapshot = BuildSampleSnapshot(id);
            var dict = new Dictionary<string, ExpressionSnapshot>(StringComparer.Ordinal)
            {
                { id, snapshot },
            };
            var resolver = new ExpressionResolver(dict);

            Span<float> blendShapeOutput = stackalloc float[8];
            Span<BoneSnapshot> boneOutput = stackalloc BoneSnapshot[4];

            bool result = resolver.TryResolve(id, blendShapeOutput, boneOutput);

            Assert.IsTrue(result, "登録済み SnapshotId は true を返す");
            Assert.AreEqual(0.5f, blendShapeOutput[0], Tolerance);
            Assert.AreEqual(0.25f, blendShapeOutput[1], Tolerance);
            Assert.AreEqual(0.75f, blendShapeOutput[2], Tolerance);
            Assert.AreEqual("Armature/Head", boneOutput[0].BonePath);
            Assert.AreEqual(2f, boneOutput[0].EulerY, Tolerance);
            Assert.AreEqual("Armature/Jaw", boneOutput[1].BonePath);
            Assert.AreEqual(-10f, boneOutput[1].EulerZ, Tolerance);
        }

        [Test]
        public void TryResolve_UnknownId_ReturnsFalse()
        {
            var snapshot = BuildSampleSnapshot("smile");
            var dict = new Dictionary<string, ExpressionSnapshot>(StringComparer.Ordinal)
            {
                { "smile", snapshot },
            };
            var resolver = new ExpressionResolver(dict);

            Span<float> blendShapeOutput = stackalloc float[8];
            Span<BoneSnapshot> boneOutput = stackalloc BoneSnapshot[4];

            bool result = resolver.TryResolve("unknown-id", blendShapeOutput, boneOutput);

            Assert.IsFalse(result, "未登録 SnapshotId は false を返す");
        }

        [Test]
        public void TryResolve_OutputBufferTooSmall_ReturnsFalse()
        {
            const string id = "smile";
            var snapshot = BuildSampleSnapshot(id);
            var dict = new Dictionary<string, ExpressionSnapshot>(StringComparer.Ordinal)
            {
                { id, snapshot },
            };
            var resolver = new ExpressionResolver(dict);

            // BlendShape 出力バッファが必要な 3 件に対して 2 件しかない
            Span<float> tooSmallBlendShapeOutput = stackalloc float[2];
            Span<BoneSnapshot> boneOutput = stackalloc BoneSnapshot[4];

            bool blendShapeResult = resolver.TryResolve(id, tooSmallBlendShapeOutput, boneOutput);
            Assert.IsFalse(blendShapeResult, "BlendShape バッファ不足は false を返す");

            // Bone 出力バッファが必要な 2 件に対して 1 件しかない
            Span<float> blendShapeOutput = stackalloc float[8];
            Span<BoneSnapshot> tooSmallBoneOutput = stackalloc BoneSnapshot[1];

            bool boneResult = resolver.TryResolve(id, blendShapeOutput, tooSmallBoneOutput);
            Assert.IsFalse(boneResult, "Bone バッファ不足は false を返す");
        }
    }
}
