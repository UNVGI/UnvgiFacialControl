using System;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Models
{
    /// <summary>
    /// <see cref="AnalogBindingEntry"/> 値型および
    /// <see cref="AnalogBindingTargetKind"/> / <see cref="AnalogTargetAxis"/> enum
    /// のコントラクトテスト（tasks.md 1.3 / Req 6.2, 6.3, 6.7）。
    /// </summary>
    [TestFixture]
    public class AnalogBindingEntryTests
    {
        [Test]
        public void TargetKind_EnumValues_StableOrdinals()
        {
            // Req 6.2: BlendShape=0, BonePose=1 を JSON / 永続化のため固定する
            Assert.AreEqual(0, (int)AnalogBindingTargetKind.BlendShape);
            Assert.AreEqual(1, (int)AnalogBindingTargetKind.BonePose);
        }

        [Test]
        public void TargetAxis_EnumValues_StableOrdinals()
        {
            // Req 6.2: X=0, Y=1, Z=2 を JSON / 永続化のため固定する
            Assert.AreEqual(0, (int)AnalogTargetAxis.X);
            Assert.AreEqual(1, (int)AnalogTargetAxis.Y);
            Assert.AreEqual(2, (int)AnalogTargetAxis.Z);
        }

        [Test]
        public void Constructor_ValidParameters_StoredAsIs()
        {
            var mapping = AnalogMappingFunction.Identity;

            var entry = new AnalogBindingEntry(
                sourceId: "controller-expr",
                sourceAxis: 1,
                targetKind: AnalogBindingTargetKind.BonePose,
                targetIdentifier: "LeftEye",
                targetAxis: AnalogTargetAxis.Y,
                mapping: mapping);

            Assert.AreEqual("controller-expr", entry.SourceId);
            Assert.AreEqual(1, entry.SourceAxis);
            Assert.AreEqual(AnalogBindingTargetKind.BonePose, entry.TargetKind);
            Assert.AreEqual("LeftEye", entry.TargetIdentifier);
            Assert.AreEqual(AnalogTargetAxis.Y, entry.TargetAxis);
            Assert.AreEqual(mapping.Scale, entry.Mapping.Scale);
            Assert.AreEqual(mapping.DeadZone, entry.Mapping.DeadZone);
        }

        [Test]
        public void Constructor_BlendShapeTargetWithIdentityAxis_DoesNotThrow()
        {
            // BlendShape ターゲットでは TargetAxis は無視される（X 既定で構築可）
            Assert.DoesNotThrow(() =>
            {
                _ = new AnalogBindingEntry(
                    sourceId: "analog-blendshape",
                    sourceAxis: 0,
                    targetKind: AnalogBindingTargetKind.BlendShape,
                    targetIdentifier: "jawOpen",
                    targetAxis: AnalogTargetAxis.X,
                    mapping: AnalogMappingFunction.Identity);
            });
        }

        [Test]
        public void Constructor_NegativeSourceAxis_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = new AnalogBindingEntry(
                    sourceId: "controller-expr",
                    sourceAxis: -1,
                    targetKind: AnalogBindingTargetKind.BlendShape,
                    targetIdentifier: "jawOpen",
                    targetAxis: AnalogTargetAxis.X,
                    mapping: AnalogMappingFunction.Identity);
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t\n")]
        public void Constructor_NullOrWhitespaceTargetIdentifier_ThrowsArgumentException(string targetId)
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new AnalogBindingEntry(
                    sourceId: "controller-expr",
                    sourceAxis: 0,
                    targetKind: AnalogBindingTargetKind.BlendShape,
                    targetIdentifier: targetId,
                    targetAxis: AnalogTargetAxis.X,
                    mapping: AnalogMappingFunction.Identity);
            });
        }

        [Test]
        public void Constructor_SourceAxisZero_DoesNotThrow()
        {
            // 境界: SourceAxis=0 (scalar 入力 / X 軸) は許容
            Assert.DoesNotThrow(() =>
            {
                _ = new AnalogBindingEntry(
                    sourceId: "analog-blendshape",
                    sourceAxis: 0,
                    targetKind: AnalogBindingTargetKind.BlendShape,
                    targetIdentifier: "jawOpen",
                    targetAxis: AnalogTargetAxis.X,
                    mapping: AnalogMappingFunction.Identity);
            });
        }

        [Test]
        public void Constructor_LargeSourceAxis_DoesNotThrow()
        {
            // ARKit 52ch 想定: 大きい sourceAxis も受理する
            Assert.DoesNotThrow(() =>
            {
                _ = new AnalogBindingEntry(
                    sourceId: "analog-bonepose",
                    sourceAxis: 51,
                    targetKind: AnalogBindingTargetKind.BlendShape,
                    targetIdentifier: "tongueOut",
                    targetAxis: AnalogTargetAxis.X,
                    mapping: AnalogMappingFunction.Identity);
            });
        }
    }
}
