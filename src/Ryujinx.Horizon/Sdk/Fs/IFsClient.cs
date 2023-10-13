using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using System;

namespace Ryujinx.Horizon.Sdk.Fs
{
    public interface IFsClient
    {
        Result CleanDirectoryRecursively(string path);
        void CloseDirectory(DirectoryHandle handle);
        void CloseFile(FileHandle handle);
        Result CommitSaveData(string mountName);
        Result ConvertToFsCommonPath(Span<byte> commonPathBuffer, string path);
        Result CreateFile(string path, long size);
        Result CreateSystemSaveData(SaveDataSpaceId spaceId, ulong saveDataId, ulong ownerId, ulong size, ulong journalSize, SaveDataFlags flags);

        Result DeleteFile(string path);
        Result DeleteSaveData(SaveDataSpaceId spaceId, ulong saveDataId);
        void DisableAutoSaveDataCreation();

        Result EnsureDirectory(string path);
        Result EnsureParentDirectory(string path);

        Result FlushFile(FileHandle handle);

        Result GetEntryType(out DirectoryEntryType type, string path);
        Result GetFileSize(out long size, FileHandle handle);
        Result GetFreeSpaceSize(out long freeSpace, string path);
        Result GetGameCardHandle(out GameCardHandle outHandle);
        Result GetProgramId(out ProgramId programId, ReadOnlySpan<byte> path, ContentAttributes attr);
        Result GetRightsId(out FsRightsId rightsId, out byte keyGeneration, ReadOnlySpan<byte> path, ContentAttributes attr);
        Result GetSaveDataFlags(out SaveDataFlags flags, ulong saveDataId);
        Result GetTotalSpaceSize(out long totalSpace, string path);

        Result HasDirectory(out bool exists, string path);
        Result HasFile(out bool exists, string path);

        bool IsSignedSystemPartitionOnSdCardValid(string systemRootPath);

        Result MountBis(string mountName, BisPartitionId partitionId);
        Result MountContentStorage(string mountName, ContentStorageId storageId);
        Result MountGameCardPartition(string mountName, GameCardHandle handle, GameCardPartition partitionId);
        Result MountSystemData(string mountName, ulong dataId);
        Result MountSystemSaveData(string mountName, SaveDataSpaceId spaceId, ulong saveDataId);

        Result OpenDirectory(out DirectoryHandle handle, string path, OpenDirectoryMode mode);
        Result OpenFile(out FileHandle handle, string path, OpenMode openMode);

        Result QueryMountSystemDataCacheSize(out long size, ulong dataId);

        Result ReadDirectory(out long entriesRead, Span<DirectoryEntry> entryBuffer, DirectoryHandle handle);
        Result ReadFile(FileHandle handle, long offset, Span<byte> destination);

        Result RenameFile(string oldPath, string newPath);

        Result SetConcatenationFileAttribute(string path);
        Result SetFileSize(FileHandle handle, long size);
        Result SetSaveDataFlags(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataFlags flags);

        void Unmount(string mountName);

        Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source, WriteOption option);
    }
}
