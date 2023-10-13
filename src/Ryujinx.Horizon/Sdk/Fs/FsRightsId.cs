using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public readonly struct FsRightsId
    {
        public readonly UInt128 Id;

        public static int Length => Unsafe.SizeOf<UInt128>();

        public FsRightsId(UInt128 id, byte keyGeneration)
        {
            Id = id;
        }
    }
}