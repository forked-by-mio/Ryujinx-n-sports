
using Ryujinx.Common.Memory;
using Ryujinx.Horizon.Common;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Kvdb
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct ArchiveEntryHeader
    {
        private static readonly byte[] _expectedMagic = "IMEN"u8.ToArray();

        public Array4<byte> Magic;
        public uint KeySize;
        public uint ValueSize;

        public Result Validate()
        {
            if (!Magic.AsSpan().SequenceEqual(_expectedMagic))
            {
                return KvdbResult.InvalidKeyValue;
            }

            return Result.Success;
        }

        public static ArchiveEntryHeader Create(uint keySize, uint valueSize)
        {
            ArchiveEntryHeader header = new()
            {
                KeySize = keySize,
                ValueSize = valueSize,
            };

            _expectedMagic.AsSpan().CopyTo(header.Magic.AsSpan());

            return header;
        }
    }
}