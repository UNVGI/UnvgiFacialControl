using System;
using System.Threading;
using NUnit.Framework;
using Hidano.FacialControl.Adapters.OSC;

namespace Hidano.FacialControl.Tests.EditMode.Adapters
{
    [TestFixture]
    public class OscDoubleBufferTests
    {
        [Test]
        public void Constructor_ValidSize_CreatesBuffer()
        {
            using var buffer = new OscDoubleBuffer(16);

            Assert.AreEqual(16, buffer.Size);
            Assert.AreEqual(16, buffer.GetReadBuffer().Length);
        }

        [Test]
        public void Constructor_ZeroSize_CreatesEmptyBuffer()
        {
            using var buffer = new OscDoubleBuffer(0);

            Assert.AreEqual(0, buffer.Size);
            Assert.AreEqual(0, buffer.GetReadBuffer().Length);
        }

        [Test]
        public void Constructor_NegativeSize_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var buffer = new OscDoubleBuffer(-1);
                buffer.Dispose();
            });
        }

        [Test]
        public void Write_ValidIndexAndValue_StoresValueAfterSwap()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.5f);
            buffer.Swap();

            Assert.AreEqual(0.5f, buffer.GetReadBuffer()[0], 0.0001f);
        }

        [Test]
        public void Write_MultipleValues_StoresAllAfterSwap()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.1f);
            buffer.Write(1, 0.2f);
            buffer.Write(2, 0.3f);
            buffer.Write(3, 0.4f);
            buffer.Swap();

            var readBuffer = buffer.GetReadBuffer();
            Assert.AreEqual(0.1f, readBuffer[0], 0.0001f);
            Assert.AreEqual(0.2f, readBuffer[1], 0.0001f);
            Assert.AreEqual(0.3f, readBuffer[2], 0.0001f);
            Assert.AreEqual(0.4f, readBuffer[3], 0.0001f);
        }

        [Test]
        public void Write_OverwritesSameIndex_KeepsLatestValue()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.1f);
            buffer.Write(0, 0.9f);
            buffer.Swap();

            Assert.AreEqual(0.9f, buffer.GetReadBuffer()[0], 0.0001f);
        }

        [Test]
        public void Write_IndexOutOfRange_DropsWrite()
        {
            using var buffer = new OscDoubleBuffer(4);
            int before = buffer.WriteTick;

            Assert.DoesNotThrow(() => buffer.Write(4, 0.5f));
            Assert.DoesNotThrow(() => buffer.Write(-1, 0.5f));
            buffer.Swap();

            Assert.AreEqual(before, buffer.WriteTick);
            Assert.AreEqual(0f, buffer.GetReadBuffer()[0]);
        }

        [Test]
        public void Swap_WrittenValuesNotVisibleBeforeSwap()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.5f);

            Assert.AreEqual(0f, buffer.GetReadBuffer()[0]);
        }

        [Test]
        public void Swap_ClearsWriteBuffer()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.5f);
            buffer.Swap();
            buffer.Swap();

            Assert.AreEqual(0f, buffer.GetReadBuffer()[0]);
        }

        [Test]
        public void Write_FromDifferentThread_DoesNotCorruptReadBuffer()
        {
            using var buffer = new OscDoubleBuffer(4);

            var writeThread = new Thread(() =>
            {
                buffer.Write(0, 0.5f);
                buffer.Write(1, 0.75f);
            });
            writeThread.Start();
            writeThread.Join();

            Assert.AreEqual(0f, buffer.GetReadBuffer()[0]);

            buffer.Swap();
            var readBuffer = buffer.GetReadBuffer();
            Assert.AreEqual(0.5f, readBuffer[0], 0.0001f);
            Assert.AreEqual(0.75f, readBuffer[1], 0.0001f);
        }

        [Test]
        public void Resize_LargerSize_AllocatesNewSize()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Resize(8);

            Assert.AreEqual(8, buffer.Size);
            Assert.AreEqual(8, buffer.GetReadBuffer().Length);
        }

        [Test]
        public void Resize_SmallerSize_AllocatesNewSize()
        {
            using var buffer = new OscDoubleBuffer(8);

            buffer.Resize(4);

            Assert.AreEqual(4, buffer.Size);
            Assert.AreEqual(4, buffer.GetReadBuffer().Length);
        }

        [Test]
        public void Resize_SameSize_DoesNothing()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.5f);
            buffer.Swap();
            buffer.Resize(4);

            Assert.AreEqual(0.5f, buffer.GetReadBuffer()[0], 0.0001f);
        }

        [Test]
        public void Resize_PreservesOverlappingData()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.5f);
            buffer.Swap();
            buffer.Resize(8);

            var readBuffer = buffer.GetReadBuffer();
            Assert.AreEqual(0.5f, readBuffer[0], 0.0001f);
            Assert.AreEqual(0f, readBuffer[4]);
        }

        [Test]
        public void Resize_NegativeSize_ThrowsArgumentOutOfRangeException()
        {
            using var buffer = new OscDoubleBuffer(4);

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Resize(-1));
        }

        [Test]
        public void Resize_WritesWorkAfterResize()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Resize(8);
            buffer.Write(7, 0.9f);
            buffer.Swap();

            Assert.AreEqual(0.9f, buffer.GetReadBuffer()[7], 0.0001f);
        }

        [Test]
        public void Resize_ConcurrentWrites_NoNativeCrash()
        {
            using var buffer = new OscDoubleBuffer(8);
            Exception exception = null;
            int[] sizes = { 4, 16, 2, 32, 8 };
            const int iterations = 1000;

            var writeThread = new Thread(() =>
            {
                try
                {
                    for (int frame = 0; frame < iterations; frame++)
                    {
                        for (int i = 0; i < 32; i++)
                        {
                            buffer.Write(i, frame + i);
                        }
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            var resizeThread = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        buffer.Resize(sizes[i % sizes.Length]);
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            writeThread.Start();
            resizeThread.Start();
            writeThread.Join();
            resizeThread.Join();

            Assert.IsNull(exception, $"Concurrent Resize/Write threw: {exception}");
        }

        [Test]
        public void Write_IndexOutsideResizedBuffer_DropsWrite()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.5f);
            buffer.Swap();
            buffer.Resize(1);
            int before = buffer.WriteTick;

            Assert.DoesNotThrow(() => buffer.Write(3, 0.9f));
            buffer.Swap();

            Assert.AreEqual(before, buffer.WriteTick);
            Assert.AreEqual(0f, buffer.GetReadBuffer()[0]);
        }

        [Test]
        public void WriteTick_InitialValue_IsZero()
        {
            using var buffer = new OscDoubleBuffer(4);

            Assert.AreEqual(0, buffer.WriteTick);
        }

        [Test]
        public void Write_IncrementsWriteTick()
        {
            using var buffer = new OscDoubleBuffer(4);

            int before = buffer.WriteTick;
            buffer.Write(0, 0.5f);

            Assert.AreEqual(before + 1, buffer.WriteTick);
        }

        [Test]
        public void Write_IndexOutOfRange_DoesNotIncrementWriteTick()
        {
            using var buffer = new OscDoubleBuffer(4);

            int before = buffer.WriteTick;
            buffer.Write(4, 0.5f);

            Assert.AreEqual(before, buffer.WriteTick);
        }

        [Test]
        public void Swap_DoesNotIncrementWriteTick()
        {
            using var buffer = new OscDoubleBuffer(4);

            buffer.Write(0, 0.5f);
            int afterWrite = buffer.WriteTick;

            buffer.Swap();

            Assert.AreEqual(afterWrite, buffer.WriteTick);
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var buffer = new OscDoubleBuffer(4);
            buffer.Dispose();

            Assert.DoesNotThrow(() => buffer.Dispose());
        }
    }
}
