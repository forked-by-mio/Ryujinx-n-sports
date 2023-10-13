using System;

namespace Ryujinx.Horizon.Sdk.Fs
{
    [Flags]
    public enum NxFileAttributes : byte
    {
        None = 0,
        Directory = 1,
        Archive = 2,
    }
}