
namespace Ryujinx.Horizon.Sdk.Fs
{
    public enum BisPartitionId
    {
        BootPartition1Root = 0,
        BootPartition2Root = 10,
        UserDataRoot = 20,
        BootConfigAndPackage2Part1 = 21,
        BootConfigAndPackage2Part2 = 22,
        BootConfigAndPackage2Part3 = 23,
        BootConfigAndPackage2Part4 = 24,
        BootConfigAndPackage2Part5 = 25,
        BootConfigAndPackage2Part6 = 26,
        CalibrationBinary = 27,
        CalibrationFile = 28,
        SafeMode = 29,
        User = 30,
        System = 31,
        SystemProperEncryption = 32,
        SystemProperPartition = 33,
        SignedSystemPartitionOnSafeMode = 34,
        DeviceTreeBlob = 35,
        System0 = 36,
    }
}