using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    [TestFixture]
    public class InputSourceIdTests
    {
        [TestCase("osc")]
        [TestCase("lipsync")]
        [TestCase("controller-expr")]
        [TestCase("keyboard-expr")]
        [TestCase("input")]
        public void TryParse_ReservedId_ReturnsTrueAndIsReservedIsTrue(string reserved)
        {
            var parsed = InputSourceId.TryParse(reserved, out var id);

            Assert.IsTrue(parsed);
            Assert.AreEqual(reserved, id.Value);
            Assert.IsTrue(id.IsReserved);
            Assert.IsFalse(id.IsThirdPartyExtension);
        }

        [TestCase("x-mycompany-arm-sensor")]
        [TestCase("x-test")]
        [TestCase("x-")]
        public void TryParse_ThirdPartyPrefix_ReturnsTrueAndIsReservedIsFalse(string extension)
        {
            var parsed = InputSourceId.TryParse(extension, out var id);

            Assert.IsTrue(parsed);
            Assert.AreEqual(extension, id.Value);
            Assert.IsFalse(id.IsReserved);
            Assert.IsTrue(id.IsThirdPartyExtension);
        }

        [Test]
        public void TryParse_LegacyIdentifier_IsRejected()
        {
            var parsed = InputSourceId.TryParse("legacy", out var id);

            Assert.IsFalse(parsed);
            Assert.AreEqual(default(InputSourceId), id);
        }

        [Test]
        public void TryParse_Null_IsRejected()
        {
            var parsed = InputSourceId.TryParse(null, out var id);

            Assert.IsFalse(parsed);
            Assert.AreEqual(default(InputSourceId), id);
        }

        [Test]
        public void TryParse_Empty_IsRejected()
        {
            var parsed = InputSourceId.TryParse(string.Empty, out var id);

            Assert.IsFalse(parsed);
            Assert.AreEqual(default(InputSourceId), id);
        }

        [TestCase("bad id")]
        [TestCase("bad/id")]
        [TestCase("bad:id")]
        [TestCase("日本語")]
        [TestCase("has\tTab")]
        [TestCase("has\nNewline")]
        public void TryParse_InvalidCharacters_IsRejected(string invalid)
        {
            var parsed = InputSourceId.TryParse(invalid, out var id);

            Assert.IsFalse(parsed);
            Assert.AreEqual(default(InputSourceId), id);
        }

        [Test]
        public void TryParse_ExceedsSixtyFourCharacters_IsRejected()
        {
            var tooLong = new string('a', 65);

            var parsed = InputSourceId.TryParse(tooLong, out var id);

            Assert.IsFalse(parsed);
            Assert.AreEqual(default(InputSourceId), id);
        }

        [Test]
        public void TryParse_ExactlySixtyFourCharacters_IsAccepted()
        {
            var boundary = new string('a', 64);

            var parsed = InputSourceId.TryParse(boundary, out var id);

            Assert.IsTrue(parsed);
            Assert.AreEqual(boundary, id.Value);
        }

        [TestCase("_under_score")]
        [TestCase("dot.separated")]
        [TestCase("dash-separated")]
        [TestCase("Alpha123")]
        [TestCase("a")]
        public void TryParse_AllowedAsciiCharacters_IsAccepted(string candidate)
        {
            var parsed = InputSourceId.TryParse(candidate, out var id);

            Assert.IsTrue(parsed);
            Assert.AreEqual(candidate, id.Value);
        }

        [Test]
        public void TryParse_OscReservedId_YieldsIsReservedTrue()
        {
            Assert.IsTrue(InputSourceId.TryParse("osc", out var id));
            Assert.IsTrue(id.IsReserved);
        }

        [Test]
        public void IsReservedId_StaticHelper_ReturnsExpectedResults()
        {
            Assert.IsTrue(InputSourceId.IsReservedId("osc"));
            Assert.IsTrue(InputSourceId.IsReservedId("lipsync"));
            Assert.IsTrue(InputSourceId.IsReservedId("controller-expr"));
            Assert.IsTrue(InputSourceId.IsReservedId("keyboard-expr"));
            Assert.IsTrue(InputSourceId.IsReservedId("input"));

            Assert.IsFalse(InputSourceId.IsReservedId("legacy"));
            Assert.IsFalse(InputSourceId.IsReservedId("x-custom"));
            Assert.IsFalse(InputSourceId.IsReservedId("unknown"));
            Assert.IsFalse(InputSourceId.IsReservedId(null));
            Assert.IsFalse(InputSourceId.IsReservedId(string.Empty));
        }

        [Test]
        public void Equality_SameValue_AreEqual()
        {
            Assert.IsTrue(InputSourceId.TryParse("osc", out var a));
            Assert.IsTrue(InputSourceId.TryParse("osc", out var b));

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentValue_AreNotEqual()
        {
            Assert.IsTrue(InputSourceId.TryParse("osc", out var a));
            Assert.IsTrue(InputSourceId.TryParse("lipsync", out var b));

            Assert.AreNotEqual(a, b);
            Assert.IsFalse(a == b);
            Assert.IsTrue(a != b);
        }

        [Test]
        public void ToString_ReturnsUnderlyingValue()
        {
            Assert.IsTrue(InputSourceId.TryParse("controller-expr", out var id));

            Assert.AreEqual("controller-expr", id.ToString());
        }

        [Test]
        public void Default_ValueIsNullAndIsReservedIsFalse()
        {
            var defaultId = default(InputSourceId);

            Assert.IsNull(defaultId.Value);
            Assert.IsFalse(defaultId.IsReserved);
            Assert.IsFalse(defaultId.IsThirdPartyExtension);
        }
    }
}
