
using Ryujinx.Common.Memory;
using Ryujinx.Horizon.Common;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Kvdb
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct ArchiveHeader
    {
        private static readonly byte[] _expectedMagic = "IMKV"u8.ToArray();

        public Array4<byte> Magic;
        public uint Pad;
        public uint EntryCount;

        public Result Validate()
        {
            if (!Magic.AsSpan().SequenceEqual(_expectedMagic))
            {
                return KvdbResult.InvalidKeyValue;
            }

            return Result.Success;
        }

        public static ArchiveHeader Create(uint entryCount)
        {
            ArchiveHeader header = new()
            {
                Pad = 0,
                EntryCount = entryCount,
            };

            _expectedMagic.AsSpan().CopyTo(header.Magic.AsSpan());

            return header;
        }
    }
}