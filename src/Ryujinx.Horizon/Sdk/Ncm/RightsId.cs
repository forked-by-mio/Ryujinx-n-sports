using Ryujinx.Common.Memory;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    [StructLayout(LayoutKind.Sequential, Size = 0x18)]
    readonly struct RightsId
    {
        public readonly UInt128 Id;
        public readonly byte KeyGeneration;

        private readonly Array7<byte> _padding;

        public static int Length => Unsafe.SizeOf<UInt128>();

        public RightsId(UInt128 id, byte keyGeneration)
        {
            Id = id;
            KeyGeneration = keyGeneration;
            _padding = default;
        }
    }
}