using System;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    [TestFixture]
    public class OverlaySlotBindingTests
    {
        [Test]
        public void Constructor_ValidArgs_KeepsBothFields()
        {
            var binding = new OverlaySlotBinding("blink", "anger_blink");
            Assert.AreEqual("blink", binding.Slot);
            Assert.AreEqual("anger_blink", binding.ExpressionId);
            Assert.IsFalse(binding.IsSuppress);
        }

        [Test]
        public void Constructor_NullExpressionId_MarksSuppress()
        {
            var binding = new OverlaySlotBinding("blink", null);
            Assert.IsTrue(binding.IsSuppress, "ExpressionId が null なら suppress とみなされること");
        }

        [Test]
        public void Constructor_EmptyExpressionId_MarksSuppress()
        {
            var binding = new OverlaySlotBinding("blink", string.Empty);
            Assert.IsTrue(binding.IsSuppress);
        }

        [Test]
        public void Constructor_WhitespaceExpressionId_MarksSuppress()
        {
            var binding = new OverlaySlotBinding("blink", "   ");
            Assert.IsTrue(binding.IsSuppress);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Constructor_InvalidSlot_Throws(string slot)
        {
            Assert.Throws<ArgumentException>(() => new OverlaySlotBinding(slot, "anger_blink"));
        }

        [Test]
        public void Equality_SameSlotAndExpressionId_AreEqual()
        {
            var a = new OverlaySlotBinding("blink", "anger_blink");
            var b = new OverlaySlotBinding("blink", "anger_blink");
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentSlot_NotEqual()
        {
            var a = new OverlaySlotBinding("blink", "x");
            var b = new OverlaySlotBinding("blush", "x");
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equality_SuppressEntries_AreEqualWhenSlotMatches()
        {
            var a = new OverlaySlotBinding("blink", null);
            var b = new OverlaySlotBinding("blink", null);
            Assert.IsTrue(a.Equals(b));
        }
    }

    [TestFixture]
    public class ExpressionOverlaysTests
    {
        [Test]
        public void Overlays_DefaultCtor_IsEmpty()
        {
            var expr = new Expression("smile", "Smile", "emotion");
            Assert.AreEqual(0, expr.Overlays.Length, "overlays 未指定なら空配列");
        }

        [Test]
        public void Overlays_PassedThroughCtor_AreCopiedDefensively()
        {
            var src = new[]
            {
                new OverlaySlotBinding("blink", "anger_blink"),
            };
            var expr = new Expression(
                "anger", "Anger", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: null, overlays: src);

            // 防御的コピー: 元配列を書き換えても影響しない
            src[0] = new OverlaySlotBinding("blink", "TAMPERED");
            Assert.AreEqual("anger_blink", expr.Overlays.Span[0].ExpressionId);
        }

        [Test]
        public void TryGetOverlay_DeclaredSlot_ReturnsTrueAndBinding()
        {
            var expr = new Expression(
                "anger", "Anger", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: null,
                overlays: new[]
                {
                    new OverlaySlotBinding("blink", "anger_blink"),
                    new OverlaySlotBinding("blush", null),
                });

            Assert.IsTrue(expr.TryGetOverlay("blink", out var blink));
            Assert.AreEqual("anger_blink", blink.ExpressionId);
            Assert.IsFalse(blink.IsSuppress);

            Assert.IsTrue(expr.TryGetOverlay("blush", out var blush));
            Assert.IsTrue(blush.IsSuppress, "明示 suppress の slot も TryGetOverlay は true を返す");
        }

        [Test]
        public void TryGetOverlay_UnknownSlot_ReturnsFalse()
        {
            var expr = new Expression(
                "anger", "Anger", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: null,
                overlays: new[] { new OverlaySlotBinding("blink", "anger_blink") });

            Assert.IsFalse(expr.TryGetOverlay("never", out _));
        }

        [TestCase(null)]
        [TestCase("")]
        public void TryGetOverlay_NullOrEmptySlot_ReturnsFalse(string slot)
        {
            var expr = new Expression(
                "anger", "Anger", "emotion",
                Expression.DefaultTransitionDuration, default,
                blendShapeValues: null,
                overlays: new[] { new OverlaySlotBinding("blink", "anger_blink") });

            Assert.IsFalse(expr.TryGetOverlay(slot, out _));
        }
    }

    [TestFixture]
    public class FacialProfileDefaultOverlaysTests
    {
        [Test]
        public void DefaultOverlays_NotPassed_IsEmpty()
        {
            var profile = new FacialProfile("1.0");
            Assert.AreEqual(0, profile.DefaultOverlays.Length);
        }

        [Test]
        public void DefaultOverlays_PassedThroughCtor_AreCopiedDefensively()
        {
            var src = new[] { new OverlaySlotBinding("blink", "default_blink") };
            var profile = new FacialProfile(
                "1.0", layers: null, expressions: null,
                rendererPaths: null, layerInputSources: null,
                defaultOverlays: src);

            src[0] = new OverlaySlotBinding("blink", "TAMPERED");
            Assert.AreEqual("default_blink", profile.DefaultOverlays.Span[0].ExpressionId);
        }

        [Test]
        public void TryGetDefaultOverlay_DeclaredSlot_ReturnsTrue()
        {
            var profile = new FacialProfile(
                "1.0", layers: null, expressions: null,
                rendererPaths: null, layerInputSources: null,
                defaultOverlays: new[]
                {
                    new OverlaySlotBinding("blink", "default_blink"),
                });

            Assert.IsTrue(profile.TryGetDefaultOverlay("blink", out var b));
            Assert.AreEqual("default_blink", b.ExpressionId);
        }

        [Test]
        public void TryGetDefaultOverlay_UnknownSlot_ReturnsFalse()
        {
            var profile = new FacialProfile(
                "1.0", layers: null, expressions: null,
                rendererPaths: null, layerInputSources: null,
                defaultOverlays: new[]
                {
                    new OverlaySlotBinding("blink", "default_blink"),
                });

            Assert.IsFalse(profile.TryGetDefaultOverlay("blush", out _));
        }
    }
}
