using System;

namespace Ryujinx.Horizon.Sdk.Fs
{
    [Flags]
    public enum OpenDirectoryMode
    {
        Directory = 1,
        File = 2,
        NoFileSize = int.MinValue,
        All = 3,
    }
}