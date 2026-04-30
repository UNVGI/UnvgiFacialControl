using NUnit.Framework;
using Hidano.FacialControl.Adapters.Processors;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Processors
{
    /// <summary>
    /// 4 種 stateless アナログ <c>InputProcessor</c>（DeadZone / Scale / Offset / Clamp）の
    /// 振る舞いを検証する EditMode テスト（tasks.md 4.1）。
    /// </summary>
    /// <remarks>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item>DeadZone: abs &lt;= min は 0、間は <c>(abs-min)/(max-min)</c> 正規化、サチュレーション側は ±1。</item>
    ///   <item>Scale: <c>value * factor</c>。</item>
    ///   <item>Offset: <c>value + offset</c>。</item>
    ///   <item>Clamp: <c>Mathf.Clamp(value, min, max)</c>。</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class AnalogProcessorTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void DeadZone_Process_BelowMin_ReturnsZero()
        {
            var processor = new AnalogDeadZoneProcessor { min = 0.1f, max = 1f };

            float result = processor.Process(0.05f, control: null);

            Assert.That(result, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void DeadZone_Process_BetweenMinAndMax_ReturnsNormalizedSigned()
        {
            var processor = new AnalogDeadZoneProcessor { min = 0.1f, max = 0.6f };

            float result = processor.Process(-0.5f, control: null);

            Assert.That(result, Is.EqualTo(-0.8f).Within(Tolerance));
        }

        [Test]
        public void DeadZone_Process_AboveMax_ReturnsSignedOne()
        {
            var processor = new AnalogDeadZoneProcessor { min = 0.1f, max = 0.6f };

            float result = processor.Process(0.9f, control: null);

            Assert.That(result, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Scale_Process_PositiveFactor_ReturnsScaledValue()
        {
            var processor = new AnalogScaleProcessor { factor = 2f };

            float result = processor.Process(0.5f, control: null);

            Assert.That(result, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Scale_Process_ZeroInput_ReturnsZero()
        {
            var processor = new AnalogScaleProcessor { factor = 5f };

            float result = processor.Process(0f, control: null);

            Assert.That(result, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Scale_Process_NegativeFactor_InvertsValue()
        {
            var processor = new AnalogScaleProcessor { factor = -1f };

            float result = processor.Process(0.5f, control: null);

            Assert.That(result, Is.EqualTo(-0.5f).Within(Tolerance));
        }

        [Test]
        public void Offset_Process_PositiveOffset_AddsOffset()
        {
            var processor = new AnalogOffsetProcessor { offset = 0.5f };

            float result = processor.Process(0.5f, control: null);

            Assert.That(result, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Offset_Process_NegativeOffset_SubtractsOffset()
        {
            var processor = new AnalogOffsetProcessor { offset = -0.25f };

            float result = processor.Process(0.5f, control: null);

            Assert.That(result, Is.EqualTo(0.25f).Within(Tolerance));
        }

        [Test]
        public void Offset_Process_ZeroOffset_ReturnsValueUnchanged()
        {
            var processor = new AnalogOffsetProcessor { offset = 0f };

            float result = processor.Process(0.5f, control: null);

            Assert.That(result, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Clamp_Process_BelowMin_ReturnsMin()
        {
            var processor = new AnalogClampProcessor { min = 0f, max = 1f };

            float result = processor.Process(-0.5f, control: null);

            Assert.That(result, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Clamp_Process_AboveMax_ReturnsMax()
        {
            var processor = new AnalogClampProcessor { min = 0f, max = 1f };

            float result = processor.Process(1.5f, control: null);

            Assert.That(result, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Clamp_Process_InRange_ReturnsValueUnchanged()
        {
            var processor = new AnalogClampProcessor { min = 0f, max = 1f };

            float result = processor.Process(0.3f, control: null);

            Assert.That(result, Is.EqualTo(0.3f).Within(Tolerance));
        }
    }
}
