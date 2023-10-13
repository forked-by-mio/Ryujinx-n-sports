using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Horizon;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS
{
    class HorizonFsClient : IFsClient
    {
        private readonly Horizon _system;
        private readonly LibHac.Fs.FileSystemClient _fsClient;
        private readonly ConcurrentDictionary<string, LocalStorage> _mountedStorages;

        public HorizonFsClient(Horizon system)
        {
            _system = system;
            _fsClient = _system.LibHacHorizonManager.FsClient.Fs;
            _mountedStorages = new();
        }

        public Result CleanDirectoryRecursively(string path)
        {
            return _fsClient.CleanDirectoryRecursively(path.ToU8Span()).ToHorizonResult();
        }

        public void CloseDirectory(DirectoryHandle handle)
        {
            _fsClient.CloseDirectory((LibHac.Fs.DirectoryHandle)handle.Value);
        }

        public void CloseFile(FileHandle handle)
        {
            _fsClient.CloseFile((LibHac.Fs.FileHandle)handle.Value);
        }

        public Result CommitSaveData(string mountName)
        {
            return _fsClient.CommitSaveData(mountName.ToU8Span()).ToHorizonResult();
        }

        public Result ConvertToFsCommonPath(Span<byte> commonPathBuffer, string path)
        {
            return _fsClient.ConvertToFsCommonPath(new U8SpanMutable(commonPathBuffer), path.ToU8Span()).ToHorizonResult();
        }

        public Result CreateFile(string path, long size)
        {
            return _fsClient.CreateFile(path.ToU8Span(), size).ToHorizonResult();
        }

        public Result CreateSystemSaveData(SaveDataSpaceId spaceId, ulong saveDataId, ulong ownerId, ulong size, ulong journalSize, SaveDataFlags flags)
        {
            return _fsClient.CreateSystemSaveData((LibHac.Fs.SaveDataSpaceId)spaceId, saveDataId, ownerId, (long)size, (long)journalSize, (LibHac.Fs.SaveDataFlags)flags).ToHorizonResult();
        }

        public Result DeleteFile(string path)
        {
            return _fsClient.DeleteFile(path.ToU8Span()).ToHorizonResult();
        }

        public Result DeleteSaveData(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return _fsClient.DeleteSaveData((LibHac.Fs.SaveDataSpaceId)spaceId, saveDataId).ToHorizonResult();
        }

        public void DisableAutoSaveDataCreation()
        {
            _fsClient.DisableAutoSaveDataCreation();
        }

        public Result EnsureDirectory(string path)
        {
            try
            {
                _fsClient.EnsureDirectoryExists(path);
            }
            catch (HorizonResultException ex)
            {
                return ex.ResultValue.ToHorizonResult();
            }

            return Result.Success;
        }

        public Result EnsureParentDirectory(string path)
        {
            return EnsureDirectory(PathTools.GetParentDirectory(path));
        }

        public Result FlushFile(FileHandle handle)
        {
            return _fsClient.FlushFile((LibHac.Fs.FileHandle)handle.Value).ToHorizonResult();
        }

        public Result GetEntryType(out DirectoryEntryType type, string path)
        {
            var result = _fsClient.GetEntryType(out var entryType, path.ToU8Span());
            type = (DirectoryEntryType)entryType;

            return result.ToHorizonResult();
        }

        public Result GetFileSize(out long size, FileHandle handle)
        {
            return _fsClient.GetFileSize(out size, (LibHac.Fs.FileHandle)handle.Value).ToHorizonResult();
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            return _fsClient.GetFreeSpaceSize(out freeSpace, path.ToU8Span()).ToHorizonResult();
        }

        public Result GetGameCardHandle(out GameCardHandle outHandle)
        {
            var result = _fsClient.GetGameCardHandle(out var gcHandle);
            outHandle = new(gcHandle);

            return result.ToHorizonResult();
        }

        public Result GetProgramId(out Ryujinx.Horizon.Sdk.Ncm.ProgramId programId, ReadOnlySpan<byte> path, ContentAttributes attr)
        {
            // TODO: Needs Libhac implementation.

            throw new NotImplementedException();
        }

        public Result GetRightsId(out FsRightsId rightsId, out byte keyGeneration, ReadOnlySpan<byte> path, ContentAttributes attr)
        {
            // TODO: Pass ContentAttributes too when supported on LibHac.

            var result = _fsClient.GetRightsId(out var fsRightsId, out keyGeneration, new U8Span(path));
            rightsId = Unsafe.As<LibHac.Fs.RightsId, FsRightsId>(ref fsRightsId);

            return result.ToHorizonResult();
        }

        public Result GetSaveDataFlags(out SaveDataFlags flags, ulong saveDataId)
        {
            var result = _fsClient.GetSaveDataFlags(out var saveDataFlags, saveDataId);
            flags = (SaveDataFlags)saveDataFlags;

            return result.ToHorizonResult();
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            return _fsClient.GetTotalSpaceSize(out totalSpace, path.ToU8Span()).ToHorizonResult();
        }

        public Result HasDirectory(out bool exists, string path)
        {
            try
            {
                exists = _fsClient.DirectoryExists(path);
            }
            catch (HorizonResultException ex)
            {
                exists = false;

                return ex.ResultValue.ToHorizonResult();
            }

            return Result.Success;
        }

        public Result HasFile(out bool exists, string path)
        {
            try
            {
                exists = _fsClient.FileExists(path);
            }
            catch (HorizonResultException ex)
            {
                exists = false;

                return ex.ResultValue.ToHorizonResult();
            }

            return Result.Success;
        }

        public bool IsSignedSystemPartitionOnSdCardValid(string systemRootPath)
        {
            return _fsClient.IsValidSignedSystemPartitionOnSdCard(systemRootPath.ToU8Span());
        }

        public Result MountBis(string mountName, BisPartitionId partitionId)
        {
            return _fsClient.MountBis(mountName.ToU8Span(), (LibHac.Fs.BisPartitionId)partitionId).ToHorizonResult();
        }

        public Result MountContentStorage(string mountName, ContentStorageId storageId)
        {
            return _fsClient.MountContentStorage(mountName.ToU8Span(), (LibHac.Fs.ContentStorageId)storageId).ToHorizonResult();
        }

        public Result MountGameCardPartition(string mountName, GameCardHandle handle, GameCardPartition partitionId)
        {
            return _fsClient.MountGameCardPartition(mountName.ToU8Span(), (uint)handle.Value, (LibHac.Fs.GameCardPartition)partitionId).ToHorizonResult();
        }

        public Result MountSystemData(string mountName, ulong dataId)
        {
            string contentPath = _system.ContentManager.GetInstalledContentPath(dataId, StorageId.BuiltInSystem, NcaContentType.PublicData);
            string installPath = VirtualFileSystem.SwitchPathToSystemPath(contentPath);

            if (!string.IsNullOrWhiteSpace(installPath))
            {
                string ncaPath = installPath;

                if (File.Exists(ncaPath))
                {
                    LocalStorage ncaStorage = null;

                    try
                    {
                        ncaStorage = new LocalStorage(ncaPath, FileAccess.Read, FileMode.Open);

                        Nca nca = new(_system.KeySet, ncaStorage);

                        using var ncaFileSystem = nca.OpenFileSystem(NcaSectionType.Data, _system.FsIntegrityCheckLevel);
                        using var ncaFsRef = new UniqueRef<IFileSystem>(ncaFileSystem);

                        Result result = _fsClient.Register(mountName.ToU8Span(), ref ncaFsRef.Ref).ToHorizonResult();
                        if (result.IsFailure)
                        {
                            ncaStorage.Dispose();
                        }
                        else
                        {
                            _mountedStorages.TryAdd(mountName, ncaStorage);
                        }

                        return result;
                    }
                    catch (HorizonResultException ex)
                    {
                        ncaStorage?.Dispose();

                        return ex.ResultValue.ToHorizonResult();
                    }
                }
            }

            // TODO: Return correct result here, this is likely wrong.

            return LibHac.Fs.ResultFs.TargetNotFound.Handle().ToHorizonResult();
        }

        public Result MountSystemSaveData(string mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return _fsClient.MountSystemSaveData(mountName.ToU8Span(), (LibHac.Fs.SaveDataSpaceId)spaceId, saveDataId).ToHorizonResult();
        }

        public Result OpenDirectory(out DirectoryHandle handle, string path, Ryujinx.Horizon.Sdk.Fs.OpenDirectoryMode mode)
        {
            var result = _fsClient.OpenDirectory(out var directoryHandle, path.ToU8Span(), (LibHac.Fs.Fsa.OpenDirectoryMode)mode);
            handle = new(directoryHandle);

            return result.ToHorizonResult();
        }

        public Result OpenFile(out FileHandle handle, string path, OpenMode openMode)
        {
            var result = _fsClient.OpenFile(out var fileHandle, path.ToU8Span(), (LibHac.Fs.OpenMode)openMode);
            handle = new(fileHandle);

            return result.ToHorizonResult();
        }

        public Result QueryMountSystemDataCacheSize(out long size, ulong dataId)
        {
            // TODO.

            size = 0;

            return Result.Success;
        }

        public Result ReadDirectory(out long entriesRead, Span<DirectoryEntry> entryBuffer, DirectoryHandle handle)
        {
            return _fsClient.ReadDirectory(
                out entriesRead,
                MemoryMarshal.Cast<DirectoryEntry, LibHac.Fs.DirectoryEntry>(entryBuffer),
                (LibHac.Fs.DirectoryHandle)handle.Value).ToHorizonResult();
        }

        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination)
        {
            return _fsClient.ReadFile((LibHac.Fs.FileHandle)handle.Value, offset, destination).ToHorizonResult();
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            return _fsClient.RenameFile(oldPath.ToU8Span(), newPath.ToU8Span()).ToHorizonResult();
        }

        public Result SetConcatenationFileAttribute(string path)
        {
            return _fsClient.SetConcatenationFileAttribute(path.ToU8Span()).ToHorizonResult();
        }

        public Result SetFileSize(FileHandle handle, long size)
        {
            return _fsClient.SetFileSize((LibHac.Fs.FileHandle)handle.Value, size).ToHorizonResult();
        }

        public Result SetSaveDataFlags(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataFlags flags)
        {
            return _fsClient.SetSaveDataFlags(saveDataId, (LibHac.Fs.SaveDataSpaceId)spaceId, (LibHac.Fs.SaveDataFlags)flags).ToHorizonResult();
        }

        public void Unmount(string mountName)
        {
            if (_mountedStorages.TryRemove(mountName, out LocalStorage ncaStorage))
            {
                ncaStorage.Dispose();
            }

            _fsClient.Unmount(mountName.ToU8Span());
        }

        public Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source, WriteOption option)
        {
            return _fsClient.WriteFile((LibHac.Fs.FileHandle)handle.Value, offset, source, new((LibHac.Fs.WriteOptionFlag)option.Flags)).ToHorizonResult();
        }
    }
}
