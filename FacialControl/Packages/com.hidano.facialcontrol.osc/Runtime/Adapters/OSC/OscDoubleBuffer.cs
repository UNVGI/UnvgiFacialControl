using System;
using System.Threading;
using Unity.Collections;

namespace Hidano.FacialControl.Adapters.OSC
{
    /// <summary>
    /// OSC receive-side double buffer backed by NativeArray.
    /// </summary>
    public class OscDoubleBuffer : IDisposable
    {
        private NativeArray<float> _bufferA;
        private NativeArray<float> _bufferB;
        private readonly object _resizeLock = new object();
        private int _writeIndex; // 0 = A is write buffer, 1 = B is write buffer
        private int _size;
        private int _writeTick;
        private bool _disposed;

        /// <summary>
        /// Buffer element count.
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// Monotonic counter incremented after successful writes.
        /// </summary>
        public int WriteTick => Volatile.Read(ref _writeTick);

        public OscDoubleBuffer(int size)
        {
            if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be 0 or greater.");
            }

            _size = size;
            _bufferA = new NativeArray<float>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bufferB = new NativeArray<float>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _writeIndex = 0;
        }

        /// <summary>
        /// Writes a value into the current write buffer. Out-of-range writes are dropped.
        /// </summary>
        public void Write(int index, float value)
        {
            lock (_resizeLock)
            {
                if (_disposed || index < 0 || index >= _size)
                {
                    return;
                }

                var writeBuffer = GetWriteBuffer();
                writeBuffer[index] = value;
                Interlocked.Increment(ref _writeTick);
            }
        }

        /// <summary>
        /// Swaps read and write buffers. Called from the main thread at frame boundaries.
        /// </summary>
        public void Swap()
        {
            int oldWriteIndex = _writeIndex;
            int newWriteIndex = 1 - oldWriteIndex;
            Interlocked.Exchange(ref _writeIndex, newWriteIndex);

            var newWriteBuffer = GetWriteBuffer();
            ClearBuffer(newWriteBuffer);
        }

        /// <summary>
        /// Gets the read buffer. Main-thread only.
        /// </summary>
        public NativeArray<float>.ReadOnly GetReadBuffer()
        {
            var readBuffer = _writeIndex == 0 ? _bufferB : _bufferA;
            return readBuffer.AsReadOnly();
        }

        /// <summary>
        /// Resizes both buffers while preserving overlapping values.
        /// </summary>
        public void Resize(int newSize)
        {
            if (newSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newSize), newSize, "Size must be 0 or greater.");
            }

            lock (_resizeLock)
            {
                if (newSize == _size)
                {
                    return;
                }

                var newBufferA = new NativeArray<float>(newSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeArray<float> newBufferB = default;

                try
                {
                    newBufferB = new NativeArray<float>(newSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    CopyBuffer(_bufferA, newBufferA);
                    CopyBuffer(_bufferB, newBufferB);
                }
                catch
                {
                    if (newBufferA.IsCreated)
                    {
                        newBufferA.Dispose();
                    }

                    if (newBufferB.IsCreated)
                    {
                        newBufferB.Dispose();
                    }

                    throw;
                }

                var oldBufferA = _bufferA;
                var oldBufferB = _bufferB;

                _bufferA = newBufferA;
                _bufferB = newBufferB;
                _size = newSize;

                if (oldBufferA.IsCreated)
                {
                    oldBufferA.Dispose();
                }

                if (oldBufferB.IsCreated)
                {
                    oldBufferB.Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (_resizeLock)
            {
                if (_disposed)
                {
                    return;
                }

                if (_bufferA.IsCreated)
                {
                    _bufferA.Dispose();
                }

                if (_bufferB.IsCreated)
                {
                    _bufferB.Dispose();
                }

                _disposed = true;
            }
        }

        private NativeArray<float> GetWriteBuffer()
        {
            return _writeIndex == 0 ? _bufferA : _bufferB;
        }

        private static void ClearBuffer(NativeArray<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = 0f;
            }
        }

        private static void CopyBuffer(NativeArray<float> source, NativeArray<float> destination)
        {
            if (!source.IsCreated || !destination.IsCreated)
            {
                return;
            }

            int count = Math.Min(source.Length, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }
    }
}
