namespace Ryujinx.Horizon.Sdk.Ncm
{
    enum StorageId : byte
    {
        None = 0,
        Host = 1,
        GameCard = 2,
        BuiltInSystem = 3,
        BuiltInUser = 4,
        SdCard = 5,
        Any = 6,
    }

    static class StorageIdExtensions
    {
        public static bool IsUniqueStorage(this StorageId storageId)
        {
            return storageId != StorageId.None && storageId != StorageId.Any;
        }

        public static bool IsInstallableStorage(this StorageId storageId)
        {
            return storageId == StorageId.BuiltInSystem || storageId == StorageId.BuiltInUser || storageId == StorageId.SdCard || storageId == StorageId.Any;
        }
    }
}