namespace Ryujinx.Horizon.Sdk.Fs
{
    public enum SaveDataSpaceId : byte
    {
        System = 0,
        User = 1,
        SdSystem = 2,
        Temporary = 3,
        SdUser = 4,
        ProperSystem = 100,
        SafeMode = 101,
    }
}