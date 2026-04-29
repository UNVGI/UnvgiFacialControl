using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Models
{
    /// <summary>
    /// <see cref="InputSourceId"/> の予約 ID 集合に <c>analog-blendshape</c> /
    /// <c>analog-bonepose</c> が追加されたことを検証する（tasks.md 1.1 / Req 3.8 / Req 6.8）。
    /// </summary>
    [TestFixture]
    public class InputSourceIdAnalogReservationTests
    {
        [TestCase("analog-blendshape")]
        [TestCase("analog-bonepose")]
        public void Parse_AnalogReservedId_Succeeds(string reserved)
        {
            var id = InputSourceId.Parse(reserved);

            Assert.AreEqual(reserved, id.Value);
            Assert.IsTrue(id.IsReserved);
            Assert.IsFalse(id.IsThirdPartyExtension);
        }

        [TestCase("analog-blendshape")]
        [TestCase("analog-bonepose")]
        public void TryParse_AnalogReservedId_ReturnsTrueAndIsReservedIsTrue(string reserved)
        {
            var parsed = InputSourceId.TryParse(reserved, out var id);

            Assert.IsTrue(parsed);
            Assert.AreEqual(reserved, id.Value);
            Assert.IsTrue(id.IsReserved);
        }

        [TestCase("analog-blendshape")]
        [TestCase("analog-bonepose")]
        public void IsReservedId_AnalogReservedId_ReturnsTrue(string reserved)
        {
            Assert.IsTrue(InputSourceId.IsReservedId(reserved));
        }

        [TestCase("osc")]
        [TestCase("lipsync")]
        [TestCase("controller-expr")]
        [TestCase("keyboard-expr")]
        [TestCase("input")]
        public void IsReservedId_ExistingReservedIds_StillReturnTrue(string reserved)
        {
            Assert.IsTrue(InputSourceId.IsReservedId(reserved),
                "既存の予約 ID は本タスクの追記後も保持されているべき");
        }

        [TestCase("x-mycompany-analog-source")]
        [TestCase("x-arkit-perfect-sync")]
        [TestCase("x-")]
        public void TryParse_ThirdPartyPrefix_StillAcceptedAfterAnalogReservation(string extension)
        {
            // Req 6.8: 3rd-party の x- prefix は予約 ID 追記後も rejected されない
            var parsed = InputSourceId.TryParse(extension, out var id);

            Assert.IsTrue(parsed);
            Assert.AreEqual(extension, id.Value);
            Assert.IsFalse(id.IsReserved);
            Assert.IsTrue(id.IsThirdPartyExtension);
        }

        [Test]
        public void IsReservedId_AnalogBlendShape_DistinctFromBonePose()
        {
            // 予約 ID が独立して登録され、片方だけが定義漏れしていないことを確認
            Assert.IsTrue(InputSourceId.IsReservedId("analog-blendshape"));
            Assert.IsTrue(InputSourceId.IsReservedId("analog-bonepose"));
            Assert.IsFalse(InputSourceId.IsReservedId("analog"));
            Assert.IsFalse(InputSourceId.IsReservedId("analog-other"));
        }
    }
}
