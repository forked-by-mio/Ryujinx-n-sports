using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Sf;
using System;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    interface IContentManager : IServiceObject
    {
        Result CreateContentStorage(StorageId storageId);
        Result CreateContentMetaDatabase(StorageId storageId);
        Result VerifyContentStorage(StorageId storageId);
        Result VerifyContentMetaDatabase(StorageId storageId);
        Result OpenContentStorage(out IContentStorage storage, StorageId storageId);
        Result OpenContentMetaDatabase(out IContentMetaDatabase metaDatabase, StorageId storageId);
        Result CloseContentStorageForcibly(StorageId storageId);
        Result CloseContentMetaDatabaseForcibly(StorageId storageId);
        Result CleanupContentMetaDatabase(StorageId storageId);
        Result ActivateContentStorage(StorageId storageId);
        Result InactivateContentStorage(StorageId storageId);
        Result ActivateContentMetaDatabase(StorageId storageId);
        Result InactivateContentMetaDatabase(StorageId storageId);
        Result InvalidateRightsIdCache();
        Result GetMemoryReport(out MemoryReport report);
        Result ActivateFsContentStorage(ContentStorageId contentStorageId);
    }
}