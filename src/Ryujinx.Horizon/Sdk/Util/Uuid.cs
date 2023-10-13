using Ryujinx.Common.Memory;
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Util
{
    readonly struct Uuid
    {
        public readonly UInt128 Data;

        private Uuid(UInt128 data)
        {
            Data = data;
        }

        public static Uuid GenerateUuid()
        {
            return GenerateUuidVersion4();
        }

        private static Uuid GenerateUuidVersion4()
        {
            const ushort Version = 4;
            const byte Reserved = 1;

            Array4<uint> data = new();

            MtRandom.GenerateRandomBytes(MemoryMarshal.Cast<uint, byte>(data.AsSpan()));

            data[1] = (data[1] & ~(0xfu << 28)) | (Version << 28);
            data[2] = (data[1] & ~(0x3u << 6)) | (Reserved << 6);

            EndianSwapUuidIfNeeded(ref data);

            ulong lower = data[0] | ((ulong)data[1] << 32);
            ulong upper = data[2] | ((ulong)data[3] << 32);

            return new(new(upper, lower));
        }

        private static void EndianSwapUuidIfNeeded(ref Array4<uint> data)
        {
            if (BitConverter.IsLittleEndian)
            {
                // UUIDs are in big endian, so do endian swap if needed.

                uint timeLow = data[0];
                ushort timeMid = (ushort)data[1];
                ushort timeHighAndVersion = (ushort)(data[1] >> 16);
                byte clockSeqHighAndReserved = (byte)data[2];
                byte clockSeqLow = (byte)(data[2] >> 8);
                ushort nodeLow = (ushort)(data[2] >> 16);
                uint nodeHigh = data[3];

                ulong node = nodeLow | ((ulong)nodeHigh << 16);

                node = BinaryPrimitives.ReverseEndianness(node) >> 16;

                data[0] = BinaryPrimitives.ReverseEndianness(timeLow);
                data[1] = BinaryPrimitives.ReverseEndianness(timeMid) | ((uint)BinaryPrimitives.ReverseEndianness(timeHighAndVersion) << 16);
                data[2] = clockSeqHighAndReserved | ((uint)clockSeqLow << 8) | (uint)((ushort)node << 16);
                data[3] = (uint)(node >> 16);
            }
        }
    }
}