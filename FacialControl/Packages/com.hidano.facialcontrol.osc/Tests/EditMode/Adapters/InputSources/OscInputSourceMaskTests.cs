using System;
using System.Collections;
using System.Reflection;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Tests.Shared;
using NUnit.Framework;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.InputSources
{
    [TestFixture]
    public class OscInputSourceMaskTests
    {
        [Test]
        public void ContributeMask_MappingCountDiffersFromMeshCount_ReturnsMeshIndexMask()
        {
            using var buffer = new OscDoubleBuffer(2);
            var contributeMask = new BitArray(4, false)
            {
                [0] = true,
                [3] = true
            };

            var source = CreateMeshMappedSource(
                buffer,
                skipMask: new BitArray(4, false),
                contributeMask: contributeMask,
                mappingIndexToMeshIndex: new[] { 3, 0 });

            Assert.That(source.ContributeMask.Length, Is.EqualTo(4));
            Assert.That(source.ContributeMask[0], Is.True);
            Assert.That(source.ContributeMask[1], Is.False);
            Assert.That(source.ContributeMask[2], Is.False);
            Assert.That(source.ContributeMask[3], Is.True);
        }

        [Test]
        public void TryWriteValues_MappingOrderDiffersFromMeshOrder_WritesToCorrectMeshIndex()
        {
            using var buffer = new OscDoubleBuffer(2);
            var source = CreateMeshMappedSource(
                buffer,
                skipMask: new BitArray(4, false),
                contributeMask: new BitArray(new[] { true, false, false, true }),
                mappingIndexToMeshIndex: new[] { 3, 0 });

            buffer.Write(0, 0.75f);
            buffer.Write(1, 0.25f);
            buffer.Swap();

            var output = new[] { -1f, -1f, -1f, -1f };
            bool wrote = source.TryWriteValues(output);

            Assert.That(wrote, Is.True);
            Assert.That(output[0], Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(output[1], Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(output[2], Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(output[3], Is.EqualTo(0.75f).Within(1e-6f));
        }

        [Test]
        public void TryWriteValues_UnmappedMappingIndex_LeavesMeshOutputUntouched()
        {
            using var buffer = new OscDoubleBuffer(3);
            var source = CreateMeshMappedSource(
                buffer,
                skipMask: new BitArray(4, false),
                contributeMask: new BitArray(new[] { false, true, false, true }),
                mappingIndexToMeshIndex: new[] { 3, -1, 1 });

            buffer.Write(0, 0.9f);
            buffer.Write(1, 0.5f);
            buffer.Write(2, 0.1f);
            buffer.Swap();

            var output = new[] { -1f, -1f, -1f, -1f };
            bool wrote = source.TryWriteValues(output);

            Assert.That(wrote, Is.True);
            Assert.That(output[0], Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(output[1], Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(output[2], Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(output[3], Is.EqualTo(0.9f).Within(1e-6f));
        }

        private static OscInputSource CreateMeshMappedSource(
            OscDoubleBuffer buffer,
            BitArray skipMask,
            BitArray contributeMask,
            int[] mappingIndexToMeshIndex)
        {
            ConstructorInfo constructor = typeof(OscInputSource).GetConstructor(new[]
            {
                typeof(OscDoubleBuffer),
                typeof(float),
                typeof(Hidano.FacialControl.Domain.Interfaces.ITimeProvider),
                typeof(FailSafeMode),
                typeof(BitArray),
                typeof(BitArray),
                typeof(int[])
            });

            Assert.That(
                constructor,
                Is.Not.Null,
                "OscInputSource must expose a mesh-index constructor that accepts mappingIndexToMeshIndex.");

            return (OscInputSource)constructor.Invoke(new object[]
            {
                buffer,
                0f,
                new ManualTimeProvider(),
                FailSafeMode.HoldLastValue,
                skipMask,
                contributeMask,
                mappingIndexToMeshIndex
            });
        }
    }
}
