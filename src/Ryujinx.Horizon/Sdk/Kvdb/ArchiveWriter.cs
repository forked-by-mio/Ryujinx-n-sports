using Ryujinx.Horizon.Common;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Kvdb
{
    ref struct ArchiveWriter
    {
        private Span<byte> _data;
        private int _offset;

        public ArchiveWriter(Span<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        public Result Write<T>(ref T value) where T : unmanaged
        {
            return Write(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateReadOnlySpan(ref value, 1)));
        }

        public Result Write(ReadOnlySpan<byte> source)
        {
            if ((uint)(_offset + source.Length) > (uint)_data.Length)
            {
                return KvdbResult.InvalidKeyValue;
            }

            if ((uint)_offset >= (uint)(_offset + source.Length))
            {
                return KvdbResult.InvalidKeyValue;
            }

            source.CopyTo(_data.Slice(_offset, source.Length));

            _offset += source.Length;

            return Result.Success;
        }

        public void WriteHeader(uint entryCount)
        {
            DebugUtil.Assert(_offset == 0);

            ArchiveHeader header = ArchiveHeader.Create(entryCount);
            Write(ref header).AbortOnFailure();
        }

        public void WriteEntry<TKey>(TKey key, ReadOnlySpan<byte> value) where TKey : unmanaged
        {
            DebugUtil.Assert(_offset != 0);

            ArchiveEntryHeader header = ArchiveEntryHeader.Create((uint)Unsafe.SizeOf<TKey>(), (uint)value.Length);
            Write(ref header).AbortOnFailure();
            Write(ref key).AbortOnFailure();
            Write(value).AbortOnFailure();
        }
    }
}