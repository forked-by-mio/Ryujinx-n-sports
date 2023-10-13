using System;

namespace Ryujinx.Horizon.Sdk.Fs
{
    [Flags]
    public enum SaveDataFlags
    {
        None = 0,
        KeepAfterResettingSystemSaveData = 1,
        KeepAfterRefurbishment = 2,
        KeepAfterResettingSystemSaveDataWithoutUserSaveData = 4,
        NeedsSecureDelete = 8,
        Restore = 0x10,
    }
}