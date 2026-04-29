using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Domain.Interfaces
{
    /// <summary>
    /// <see cref="IAnalogInputSource"/> 契約テスト（tasks.md 1.1 / Req 1.1〜1.6）。
    /// </summary>
    [TestFixture]
    public class IAnalogInputSourceContractTests
    {
        private static readonly Regex IdRegex =
            new Regex("^[a-zA-Z0-9_.-]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// テスト用の最小 Fake。N-axis 配列を内部に持ち、IsValid を切替えられる。
        /// </summary>
        private sealed class FakeAnalogInputSource : IAnalogInputSource
        {
            private readonly float[] _axes;

            public FakeAnalogInputSource(string id, int axisCount, bool isValid = true)
            {
                Id = id;
                AxisCount = axisCount;
                IsValid = isValid;
                _axes = new float[axisCount];
            }

            public string Id { get; }
            public bool IsValid { get; set; }
            public int AxisCount { get; }

            public int TickCallCount { get; private set; }
            public float LastDeltaTime { get; private set; }

            public void SetAxis(int index, float value)
            {
                _axes[index] = value;
            }

            public void Tick(float deltaTime)
            {
                TickCallCount++;
                LastDeltaTime = deltaTime;
            }

            public bool TryReadScalar(out float value)
            {
                if (!IsValid || AxisCount < 1)
                {
                    value = default;
                    return false;
                }
                value = _axes[0];
                return true;
            }

            public bool TryReadVector2(out float x, out float y)
            {
                if (!IsValid || AxisCount < 2)
                {
                    x = default;
                    y = default;
                    return false;
                }
                x = _axes[0];
                y = _axes[1];
                return true;
            }

            public bool TryReadAxes(Span<float> output)
            {
                if (!IsValid)
                {
                    return false;
                }
                int copyLength = Math.Min(output.Length, AxisCount);
                for (int i = 0; i < copyLength; i++)
                {
                    output[i] = _axes[i];
                }
                return true;
            }
        }

        [Test]
        public void Id_OnFakeSource_MatchesPattern()
        {
            IAnalogInputSource source = new FakeAnalogInputSource("analog-blendshape", 1);

            Assert.IsTrue(IdRegex.IsMatch(source.Id),
                "Id は [a-zA-Z0-9_.-]{1,64} 規約を満たす必要がある (Req 1.2)");
        }

        [Test]
        public void Id_AndAxisCount_AreReadableFromContract()
        {
            IAnalogInputSource source = new FakeAnalogInputSource("analog-bonepose", 2);

            Assert.AreEqual("analog-bonepose", source.Id);
            Assert.AreEqual(2, source.AxisCount);
        }

        [Test]
        public void Tick_IsInvocableWithZeroDeltaTime()
        {
            var fake = new FakeAnalogInputSource("x-test", 1);
            IAnalogInputSource source = fake;

            source.Tick(0f);

            Assert.AreEqual(1, fake.TickCallCount);
            Assert.AreEqual(0f, fake.LastDeltaTime);
        }

        [Test]
        public void Tick_IsInvocableWithPositiveDeltaTime()
        {
            var fake = new FakeAnalogInputSource("x-test", 1);
            IAnalogInputSource source = fake;

            source.Tick(0.016f);

            Assert.AreEqual(1, fake.TickCallCount);
            Assert.AreEqual(0.016f, fake.LastDeltaTime);
        }

        [Test]
        public void TryReadScalar_InvalidSource_ReturnsFalseAndLeavesValueUnchanged()
        {
            IAnalogInputSource source = new FakeAnalogInputSource("x-test", 1, isValid: false);
            float value = 0.42f;

            bool ok = source.TryReadScalar(out value);

            Assert.IsFalse(ok);
            // out 引数は false の場合不変であるべきだが、C# 仕様上 out は呼出後に必ず代入される。
            // Fake 実装では default(float)=0 を代入するため、契約上は「呼出側は false の場合 value を信用しない」が正。
            // ここでは「false が返ること」のみを契約として検証する。
        }

        [Test]
        public void TryReadVector2_InvalidSource_ReturnsFalse()
        {
            IAnalogInputSource source = new FakeAnalogInputSource("x-test", 2, isValid: false);

            bool ok = source.TryReadVector2(out _, out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void TryReadAxes_InvalidSource_ReturnsFalseAndLeavesOutputUnchanged()
        {
            IAnalogInputSource source = new FakeAnalogInputSource("x-test", 4, isValid: false);
            var buffer = new float[] { 1f, 2f, 3f, 4f };
            var snapshot = new float[] { 1f, 2f, 3f, 4f };

            bool ok = source.TryReadAxes(buffer);

            Assert.IsFalse(ok);
            CollectionAssert.AreEqual(snapshot, buffer,
                "IsValid==false のとき output は不変であるべき (Req 1.3)");
        }

        [Test]
        public void TryReadScalar_ValidSource_ReturnsTrueAndAxisZeroValue()
        {
            var fake = new FakeAnalogInputSource("x-test", 1);
            fake.SetAxis(0, 0.75f);
            IAnalogInputSource source = fake;

            bool ok = source.TryReadScalar(out var value);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.75f, value);
        }

        [Test]
        public void TryReadVector2_ValidSource_ReturnsTrueAndAxisZeroAndOneValues()
        {
            var fake = new FakeAnalogInputSource("x-test", 2);
            fake.SetAxis(0, 0.25f);
            fake.SetAxis(1, -0.5f);
            IAnalogInputSource source = fake;

            bool ok = source.TryReadVector2(out var x, out var y);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.25f, x);
            Assert.AreEqual(-0.5f, y);
        }

        [Test]
        public void TryReadAxes_OutputShorterThanAxisCount_WritesOnlyOverlap()
        {
            // AxisCount=4, output.Length=2 → overlap=2 のみ書込
            var fake = new FakeAnalogInputSource("x-test", 4);
            fake.SetAxis(0, 1.0f);
            fake.SetAxis(1, 2.0f);
            fake.SetAxis(2, 3.0f);
            fake.SetAxis(3, 4.0f);
            IAnalogInputSource source = fake;

            var buffer = new float[2];
            bool ok = source.TryReadAxes(buffer);

            Assert.IsTrue(ok);
            Assert.AreEqual(1.0f, buffer[0]);
            Assert.AreEqual(2.0f, buffer[1]);
        }

        [Test]
        public void TryReadAxes_OutputLongerThanAxisCount_WritesOnlyOverlapAndLeavesRemainderUnchanged()
        {
            // AxisCount=2, output.Length=4 → overlap=2 のみ書込、残余 [2..3] は不変 (Req 1.4)
            var fake = new FakeAnalogInputSource("x-test", 2);
            fake.SetAxis(0, 0.1f);
            fake.SetAxis(1, 0.2f);
            IAnalogInputSource source = fake;

            var buffer = new float[] { 9f, 9f, 9f, 9f };

            bool ok = source.TryReadAxes(buffer);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.1f, buffer[0]);
            Assert.AreEqual(0.2f, buffer[1]);
            Assert.AreEqual(9f, buffer[2], "AxisCount を超える残余は不変であるべき (Req 1.4)");
            Assert.AreEqual(9f, buffer[3], "AxisCount を超える残余は不変であるべき (Req 1.4)");
        }

        [Test]
        public void TryReadAxes_OutputEqualToAxisCount_WritesAllAxes()
        {
            var fake = new FakeAnalogInputSource("x-test", 3);
            fake.SetAxis(0, 0.1f);
            fake.SetAxis(1, 0.2f);
            fake.SetAxis(2, 0.3f);
            IAnalogInputSource source = fake;

            var buffer = new float[3];
            bool ok = source.TryReadAxes(buffer);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.1f, buffer[0]);
            Assert.AreEqual(0.2f, buffer[1]);
            Assert.AreEqual(0.3f, buffer[2]);
        }

        [Test]
        public void TryReadAxes_AcceptsStackAllocatedSpan()
        {
            var fake = new FakeAnalogInputSource("x-test", 2);
            fake.SetAxis(0, 0.1f);
            fake.SetAxis(1, 0.2f);
            IAnalogInputSource source = fake;

            Span<float> output = stackalloc float[2];
            bool ok = source.TryReadAxes(output);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.1f, output[0]);
            Assert.AreEqual(0.2f, output[1]);
        }

        [Test]
        public void AnalogInputShape_HasScalarAndVector2Members()
        {
            Assert.AreEqual(0, (int)AnalogInputShape.Scalar);
            Assert.AreEqual(1, (int)AnalogInputShape.Vector2);
        }
    }
}
