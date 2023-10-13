using System;
using System.Diagnostics;

namespace Ryujinx.Horizon
{
    class MemoryResource
    {
        private uint _capacity;
        private uint _freeSize;

        public uint AllocatableSize => _capacity;
        public uint AllocatedSize => _capacity - _freeSize;
        public uint TotalFreeSize => _freeSize;

        public MemoryResource(uint capacity)
        {
            _capacity = capacity;
            _freeSize = capacity;
        }

        public T[] Allocate<T>(int count, int elementSize)
        {
            if (!CanAllocate((uint)count * (uint)elementSize))
            {
                return null;
            }

            return new T[count];
        }

        public byte[] Allocate(uint length)
        {
            if (!CanAllocate(length))
            {
                return null;
            }

            try
            {
                return new byte[length];
            }
            catch (OverflowException)
            {
                Deallocate(length);

                return null;
            }
        }

        public T[] Allocate<T>(ReadOnlySpan<T> data)
        {
            if (!CanAllocate((uint)data.Length))
            {
                return null;
            }

            return data.ToArray();
        }

        public void Deallocate<T>(T[] elements, int elementSize)
        {
            Deallocate((uint)elements.Length * (uint)elementSize);
        }

        public void Deallocate(byte[] data)
        {
            Deallocate((uint)data.Length);
        }

        protected virtual bool CanAllocate(uint length)
        {
            if (_freeSize >= length)
            {
                _freeSize -= length;
                Debug.Assert(_freeSize <= _capacity);

                return true;
            }

            return false;
        }

        protected virtual void Deallocate(uint length)
        {
            _freeSize += length;
        }
    }
}