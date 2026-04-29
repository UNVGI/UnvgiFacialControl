using System;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Models
{
    /// <summary>
    /// <see cref="AnalogMappingFunction"/> 値型の構築・不変性・<c>Identity</c> 既定値テスト
    /// （tasks.md 1.2 / Req 2.1, 2.5, 2.7）。
    /// </summary>
    [TestFixture]
    public class AnalogMappingFunctionTests
    {
        [Test]
        public void Identity_DefaultValues_MatchSpec()
        {
            // Req 2.7: dead-zone=0, scale=1, offset=0, curve=Linear, invert=false, min=0, max=1
            var fn = AnalogMappingFunction.Identity;

            Assert.AreEqual(0f, fn.DeadZone, "Identity.DeadZone は 0");
            Assert.AreEqual(1f, fn.Scale, "Identity.Scale は 1");
            Assert.AreEqual(0f, fn.Offset, "Identity.Offset は 0");
            Assert.AreEqual(TransitionCurveType.Linear, fn.Curve.Type, "Identity.Curve は Linear");
            Assert.IsFalse(fn.Invert, "Identity.Invert は false");
            Assert.AreEqual(0f, fn.Min, "Identity.Min は 0");
            Assert.AreEqual(1f, fn.Max, "Identity.Max は 1");
        }

        [Test]
        public void Constructor_AllParameters_StoredAsIs()
        {
            var curve = new TransitionCurve(TransitionCurveType.EaseIn);

            var fn = new AnalogMappingFunction(
                deadZone: 0.15f,
                scale: 2.5f,
                offset: -0.25f,
                curve: curve,
                invert: true,
                min: -1f,
                max: 1f);

            Assert.AreEqual(0.15f, fn.DeadZone);
            Assert.AreEqual(2.5f, fn.Scale);
            Assert.AreEqual(-0.25f, fn.Offset);
            Assert.AreEqual(TransitionCurveType.EaseIn, fn.Curve.Type);
            Assert.IsTrue(fn.Invert);
            Assert.AreEqual(-1f, fn.Min);
            Assert.AreEqual(1f, fn.Max);
        }

        [Test]
        public void Constructor_MinGreaterThanMax_ThrowsArgumentException()
        {
            // Req 2.5
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new AnalogMappingFunction(
                    deadZone: 0f,
                    scale: 1f,
                    offset: 0f,
                    curve: TransitionCurve.Linear,
                    invert: false,
                    min: 1f,
                    max: 0f);
            });
        }

        [Test]
        public void Constructor_MinSlightlyGreaterThanMax_ThrowsArgumentException()
        {
            // 境界: min がわずかに max を上回るケースも reject されるべき（Req 2.5）
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new AnalogMappingFunction(
                    deadZone: 0f,
                    scale: 1f,
                    offset: 0f,
                    curve: TransitionCurve.Linear,
                    invert: false,
                    min: 0.5000001f,
                    max: 0.5f);
            });
        }

        [Test]
        public void Constructor_MinEqualToMax_DoesNotThrow()
        {
            // 境界: min == max は固定値クランプとして許容（Req 2.5 は ">" のみ）
            Assert.DoesNotThrow(() =>
            {
                _ = new AnalogMappingFunction(
                    deadZone: 0f,
                    scale: 1f,
                    offset: 0f,
                    curve: TransitionCurve.Linear,
                    invert: false,
                    min: 0.5f,
                    max: 0.5f);
            });
        }

        [Test]
        public void Constructor_NegativeMinAndMax_DoesNotThrowWhenOrdered()
        {
            // 負域だけの範囲 (例: -10 〜 -5) は ordered なら許容
            Assert.DoesNotThrow(() =>
            {
                _ = new AnalogMappingFunction(
                    deadZone: 0f,
                    scale: 1f,
                    offset: 0f,
                    curve: TransitionCurve.Linear,
                    invert: false,
                    min: -10f,
                    max: -5f);
            });
        }

        [Test]
        public void Identity_IsImmutable_AcrossAccesses()
        {
            // readonly struct のためコピー渡しは値が変化しない
            var first = AnalogMappingFunction.Identity;
            var second = AnalogMappingFunction.Identity;

            Assert.AreEqual(first.DeadZone, second.DeadZone);
            Assert.AreEqual(first.Scale, second.Scale);
            Assert.AreEqual(first.Offset, second.Offset);
            Assert.AreEqual(first.Curve.Type, second.Curve.Type);
            Assert.AreEqual(first.Invert, second.Invert);
            Assert.AreEqual(first.Min, second.Min);
            Assert.AreEqual(first.Max, second.Max);
        }

        [Test]
        public void Constructor_CustomCurveWithKeys_StoredOnFunction()
        {
            // Custom カーブを mapping 関数に保持できることを確認（Req 2.2 / R-1）
            var keys = new[]
            {
                new CurveKeyFrame(0f, 0f, 1f, 1f),
                new CurveKeyFrame(1f, 1f, 1f, 1f)
            };
            var curve = new TransitionCurve(TransitionCurveType.Custom, keys);

            var fn = new AnalogMappingFunction(
                deadZone: 0f, scale: 1f, offset: 0f,
                curve: curve, invert: false, min: 0f, max: 1f);

            Assert.AreEqual(TransitionCurveType.Custom, fn.Curve.Type);
            Assert.AreEqual(2, fn.Curve.Keys.Length);
        }
    }
}
