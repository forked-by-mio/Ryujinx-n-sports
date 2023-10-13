using Ryujinx.Horizon.Common;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Kvdb
{
    ref struct ArchiveReader
    {
        private ReadOnlySpan<byte> _data;
        private int _offset;

        public ArchiveReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        public readonly Result Peek<T>(out T destination) where T : unmanaged
        {
            destination = new();
            return Peek(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref destination, 1)));
        }

        public Result Read<T>(out T destination) where T : unmanaged
        {
            destination = new();
            return Read(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref destination, 1)));
        }

        public Result Read(Span<byte> destination)
        {
            Result result = Peek(destination);

            if (result.IsFailure)
            {
                return result;
            }

            _offset += destination.Length;

            return Result.Success;
        }

        public readonly Result Peek(Span<byte> destination)
        {
            if ((uint)(_offset + destination.Length) > (uint)_data.Length)
            {
                return KvdbResult.InvalidKeyValue;
            }

            if ((uint)_offset >= (uint)(_offset + destination.Length))
            {
                return KvdbResult.InvalidKeyValue;
            }

            _data.Slice(_offset, destination.Length).CopyTo(destination);

            return Result.Success;
        }

        public Result ReadEntryCount(out uint entryCount)
        {
            entryCount = 0;

            DebugUtil.Assert(_offset == 0);

            Result result = Read(out ArchiveHeader header);

            if (result.IsFailure)
            {
                return result;
            }

            result = header.Validate();

            if (result.IsFailure)
            {
                return result;
            }

            entryCount = header.EntryCount;

            return Result.Success;
        }

        public readonly Result GetEntrySize(out uint keySize, out uint valueSize)
        {
            keySize = 0;
            valueSize = 0;

            DebugUtil.Assert(_offset != 0);

            Result result = Peek(out ArchiveEntryHeader header);

            if (result.IsFailure)
            {
                return result;
            }

            result = header.Validate();

            if (result.IsFailure)
            {
                return result;
            }

            keySize = header.KeySize;
            valueSize = header.ValueSize;

            return Result.Success;
        }

        public Result ReadEntry<TKey>(out TKey key, Span<byte> value) where TKey : unmanaged
        {
            key = default;

            DebugUtil.Assert(_offset != 0);

            Result result = Read(out ArchiveEntryHeader header);

            if (result.IsFailure)
            {
                return result;
            }

            result = header.Validate();

            if (result.IsFailure)
            {
                return result;
            }

            DebugUtil.Assert(Unsafe.SizeOf<TKey>() == header.KeySize);
            DebugUtil.Assert(value.Length == header.ValueSize);

            Read(out key).AbortOnFailure();
            Read(value).AbortOnFailure();

            return Result.Success;
        }
    }
}