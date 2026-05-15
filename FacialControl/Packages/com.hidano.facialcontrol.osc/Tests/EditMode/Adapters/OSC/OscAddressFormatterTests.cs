using System;
using Hidano.FacialControl.Adapters.OSC;
using NUnit.Framework;

namespace Hidano.FacialControl.Tests.EditMode.Adapters
{
    [TestFixture]
    public class OscAddressFormatterTests
    {
        [Test]
        public void TryFormatBlendShapeAddress_VRChatNameWithMultibyteAndSymbols_WritesCompleteAddress()
        {
            const string blendShapeName = "\u7b11\u9854_\u53e3.\u3042";
            Span<char> destination = stackalloc char[64];

            bool formatted = OscAddressFormatter.TryFormatBlendShapeAddress(
                AddressPresetKind.VRChat,
                blendShapeName.AsSpan(),
                destination,
                out int written);

            Assert.IsTrue(formatted);
            Assert.AreEqual("/avatar/parameters/" + blendShapeName, ToString(destination.Slice(0, written)));
        }

        [Test]
        public void TryFormatBlendShapeAddress_DestinationTooShort_ReturnsFalseAndRequiredLength()
        {
            const string blendShapeName = "jawOpen";
            Span<char> destination = stackalloc char[4];

            bool formatted = OscAddressFormatter.TryFormatBlendShapeAddress(
                AddressPresetKind.VRChat,
                blendShapeName.AsSpan(),
                destination,
                out int written);

            Assert.IsFalse(formatted);
            Assert.AreEqual(OscAddressFormatter.GetBlendShapeAddressLength(
                AddressPresetKind.VRChat,
                blendShapeName.AsSpan()), written);
        }

        [Test]
        public void TryFormatGazeAddress_VRChatExpressionIdWithSymbols_WritesAxisAddress()
        {
            const string expressionId = "\u8996\u7dda.left_01";
            Span<char> destination = stackalloc char[64];

            bool formatted = OscAddressFormatter.TryFormatGazeAddress(
                AddressPresetKind.VRChat,
                expressionId.AsSpan(),
                OscAddressFormatter.VRChatGazeXAxis,
                destination,
                out int written);

            Assert.IsTrue(formatted);
            Assert.AreEqual("/avatar/parameters/" + expressionId + "X", ToString(destination.Slice(0, written)));
        }

        [Test]
        public void FormatBlendShapeAddress_VRChatName_ReturnsCompleteAddressString()
        {
            Assert.AreEqual(
                "/avatar/parameters/eyeBlinkLeft",
                OscAddressFormatter.FormatBlendShapeAddress(AddressPresetKind.VRChat, "eyeBlinkLeft"));
        }

        private static string ToString(ReadOnlySpan<char> value)
        {
            return new string(value.ToArray());
        }
    }
}
