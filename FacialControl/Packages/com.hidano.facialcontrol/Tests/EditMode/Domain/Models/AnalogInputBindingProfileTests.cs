using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Models
{
    /// <summary>
    /// <see cref="AnalogInputBindingProfile"/> 値型のコントラクトテスト
    /// （tasks.md 1.3 / Req 6.3, 6.7）。
    /// </summary>
    [TestFixture]
    public class AnalogInputBindingProfileTests
    {
        [Test]
        public void Constructor_EmptyBindings_ConstructsValidProfile()
        {
            // Req 6.7: bindings 0 件で構築可能であること
            var profile = new AnalogInputBindingProfile(
                version: "1.0.0",
                bindings: new AnalogBindingEntry[0]);

            Assert.AreEqual("1.0.0", profile.Version);
            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Constructor_NullBindings_TreatedAsEmpty()
        {
            // null 引数は空集合扱い（呼出側の null 防御）
            var profile = new AnalogInputBindingProfile(
                version: "1.0.0",
                bindings: null);

            Assert.AreEqual("1.0.0", profile.Version);
            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Constructor_NullVersion_TreatedAsEmptyString()
        {
            var profile = new AnalogInputBindingProfile(
                version: null,
                bindings: new AnalogBindingEntry[0]);

            Assert.AreEqual(string.Empty, profile.Version);
        }

        [Test]
        public void Constructor_NonEmptyBindings_StoredInOrder()
        {
            var entries = new[]
            {
                BuildEntry("controller-expr", 0, AnalogBindingTargetKind.BonePose, "LeftEye", AnalogTargetAxis.X),
                BuildEntry("controller-expr", 1, AnalogBindingTargetKind.BonePose, "LeftEye", AnalogTargetAxis.Y),
                BuildEntry("analog-blendshape", 0, AnalogBindingTargetKind.BlendShape, "jawOpen", AnalogTargetAxis.X)
            };

            var profile = new AnalogInputBindingProfile("1.0.0", entries);

            Assert.AreEqual(3, profile.Bindings.Length);
            var span = profile.Bindings.Span;
            Assert.AreEqual("controller-expr", span[0].SourceId);
            Assert.AreEqual(0, span[0].SourceAxis);
            Assert.AreEqual(AnalogTargetAxis.X, span[0].TargetAxis);
            Assert.AreEqual(1, span[1].SourceAxis);
            Assert.AreEqual(AnalogBindingTargetKind.BlendShape, span[2].TargetKind);
            Assert.AreEqual("jawOpen", span[2].TargetIdentifier);
        }

        [Test]
        public void Constructor_DefensiveCopy_ExternalArrayMutationDoesNotAffectProfile()
        {
            // Req 6.3 / 防御的コピー: 外部配列を後から書換えても profile 内部値が不変
            var external = new[]
            {
                BuildEntry("controller-expr", 0, AnalogBindingTargetKind.BlendShape, "jawOpen", AnalogTargetAxis.X)
            };

            var profile = new AnalogInputBindingProfile("1.0.0", external);

            // 外部配列を別エントリに上書き
            external[0] = BuildEntry("analog-bonepose", 2, AnalogBindingTargetKind.BonePose, "Head", AnalogTargetAxis.Z);

            Assert.AreEqual(1, profile.Bindings.Length);
            var snapshot = profile.Bindings.Span[0];
            Assert.AreEqual("controller-expr", snapshot.SourceId, "防御的コピーされた binding は外部書換えの影響を受けない");
            Assert.AreEqual(0, snapshot.SourceAxis);
            Assert.AreEqual(AnalogBindingTargetKind.BlendShape, snapshot.TargetKind);
            Assert.AreEqual("jawOpen", snapshot.TargetIdentifier);
            Assert.AreEqual(AnalogTargetAxis.X, snapshot.TargetAxis);
        }

        [Test]
        public void Bindings_DefaultStruct_IsEmpty()
        {
            // default(struct) を扱った場合に NRE せず空コレクションとして読めること
            var profile = default(AnalogInputBindingProfile);

            Assert.AreEqual(0, profile.Bindings.Length);
            Assert.AreEqual(string.Empty, profile.Version);
        }

        private static AnalogBindingEntry BuildEntry(
            string sourceId,
            int sourceAxis,
            AnalogBindingTargetKind kind,
            string targetIdentifier,
            AnalogTargetAxis axis)
        {
            return new AnalogBindingEntry(
                sourceId: sourceId,
                sourceAxis: sourceAxis,
                targetKind: kind,
                targetIdentifier: targetIdentifier,
                targetAxis: axis);
        }
    }
}
