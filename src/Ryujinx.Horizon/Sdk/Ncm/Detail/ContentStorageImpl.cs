using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Util;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class ContentStorageImpl : ContentStorageImplBase
    {
        private const string BaseContentDirectory = "/registered";
        private const int ContentIdStringLength = 2 * 16; // ContentId has 16 bytes, times 2 because each byte takes 2 characters in hex.

        private readonly IFsClient _fs;
        private PlaceHolderAccessor _placeHolderAccessor;
        private ContentId _cachedContentId;
        private FileHandle _cachedFileHandle;
        RightsIdCache _rightsIdCache;

        public ContentStorageImpl(IFsClient fs)
        {
            _fs = fs;
            _placeHolderAccessor = new(fs);
        }

        private static void MakeBaseContentDirectoryPath(out string path, string rootPath)
        {
            path = $"{rootPath}{BaseContentDirectory}";
        }

        private static void MakeContentPath(out string path, ContentId contentId, MakeContentPathFunction func, string rootPath)
        {
            MakeBaseContentDirectoryPath(out string basePath, rootPath);
            func(out path, contentId, basePath);
        }

        private Result EnsureContentDirectory(ContentId contentId, MakeContentPathFunction func, string rootPath)
        {
            MakeContentPath(out string path, contentId, func, rootPath);
            return _fs.EnsureParentDirectory(path);
        }

        private Result DeleteContentFile(ContentId contentId, MakeContentPathFunction func, string rootPath)
        {
            MakeContentPath(out string path, contentId, func, rootPath);

            Result result = _fs.DeleteFile(path);

            if (result == FsResult.PathNotFound)
            {
                return NcmResult.ContentNotFound;
            }

            return result;
        }

        private IEnumerable<DirectoryEntry> TraverseDirectory(string rootPath, int maxLevel)
        {
            if (maxLevel <= 0)
            {
                yield break;
            }

            OpenDirectoryMode openDirMode = OpenDirectoryMode.NoFileSize | OpenDirectoryMode.All;
            DirectoryEntry entry = default;

            _fs.OpenDirectory(out DirectoryHandle dirHandle, rootPath, openDirMode).ThrowOnInvalidResult();

            try
            {
                while (true)
                {
                    _fs.ReadDirectory(out long entriesRead, MemoryMarshal.CreateSpan(ref entry, 1), dirHandle).ThrowOnInvalidResult();

                    if (entriesRead == 0)
                    {
                        break;
                    }

                    string currentPath = $"{rootPath}/{GetUtf8String(entry.Name.AsSpan())}";

                    yield return entry;

                    if (entry.Type == DirectoryEntryType.Directory)
                    {
                        IEnumerable<DirectoryEntry> subPaths = TraverseDirectory(currentPath, maxLevel - 1);

                        foreach (DirectoryEntry subPath in subPaths)
                        {
                            yield return subPath;
                        }
                    }
                }
            }
            finally
            {
                _fs.CloseDirectory(dirHandle);
            }
        }

        private Result TraverseDirectoryForRepair(string rootPath, int maxLevel, Func<string, bool> pathChecker)
        {
            if (maxLevel <= 0)
            {
                return Result.Success;
            }

            OpenDirectoryMode openDirMode = OpenDirectoryMode.NoFileSize | OpenDirectoryMode.All;
            DirectoryEntry entry = default;

            bool retryDirRead= true;
            while (retryDirRead)
            {
                retryDirRead = false;

                Result result = _fs.OpenDirectory(out DirectoryHandle dirHandle, rootPath, openDirMode);
                if (result.IsFailure)
                {
                    return result;
                }

                try
                {
                    while (true)
                    {
                        result = _fs.ReadDirectory(out long entriesRead, MemoryMarshal.CreateSpan(ref entry, 1), dirHandle);
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        if (entriesRead == 0)
                        {
                            break;
                        }

                        string currentPath = $"{rootPath}/{GetUtf8String(entry.Name.AsSpan())}";

                        if (entry.Type == DirectoryEntryType.Directory)
                        {
                            if (pathChecker(currentPath) && _fs.SetConcatenationFileAttribute(currentPath).IsSuccess)
                            {
                                retryDirRead = true;
                                break;
                            }

                            result = TraverseDirectoryForRepair(currentPath, maxLevel - 1, pathChecker);
                            if (result.IsFailure)
                            {
                                return result;
                            }
                        }
                    }
                }
                finally
                {
                    _fs.CloseDirectory(dirHandle);
                }
            }

            return Result.Success;
        }

        private static bool IsContentPath(string path)
        {
            if (!path.EndsWith(".nca"))
            {
                return false;
            }

            string fileName = path;
            int lastSeparatorIndex = path.LastIndexOf('/');
            if (lastSeparatorIndex >= 0)
            {
                fileName = path[(lastSeparatorIndex + 1)..];
            }

            if (fileName.Length != ContentIdStringLength + 4)
            {
                return false;
            }

            for (int i = 0; i < ContentIdStringLength; i++)
            {
                if (!char.IsAsciiHexDigit(fileName[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPlaceHolderPath(string path)
        {
            return IsContentPath(path);
        }

        private static string GetUtf8String(ReadOnlySpan<byte> data)
        {
            int length = data.IndexOf((byte)0);
            if (length >= 0)
            {
                data = data[..length];
            }

            return Encoding.UTF8.GetString(data);
        }

        public static Result InitializeBase(IFsClient fs, string rootPath)
        {
            MakeBaseContentDirectoryPath(out string path, rootPath);
            Result result = fs.EnsureDirectory(path);
            if (result.IsFailure)
            {
                return result;
            }

            PlaceHolderAccessor.MakeBaseDirectoryPath(out path, rootPath);
            return fs.EnsureDirectory(path);
        }

        public static Result VerifyBase(IFsClient fs, string rootPath)
        {
            Result result = fs.HasDirectory(out bool hasDir, rootPath);
            if (result.IsFailure)
            {
                return result;
            }

            if (!hasDir)
            {
                return NcmResult.ContentStorageBaseNotFound;
            }

            MakeBaseContentDirectoryPath(out string path, rootPath);

            result = fs.HasDirectory(out bool hasRegistered, path);
            if (result.IsFailure)
            {
                return result;
            }

            PlaceHolderAccessor.MakeBaseDirectoryPath(out path, rootPath);

            result = fs.HasDirectory(out bool hasPlaceholder, path);
            if (result.IsFailure)
            {
                return result;
            }

            if (!hasRegistered && !hasPlaceholder)
            {
                return NcmResult.ContentStorageBaseNotFound;
            }

            if (!hasRegistered || !hasPlaceholder)
            {
                return NcmResult.InvalidContentStorageBase;
            }

            return Result.Success;
        }

        private void InvalidateFileCache()
        {
            if (_cachedContentId.Id != 0)
            {
                _fs.CloseFile(_cachedFileHandle);
                _cachedContentId = new(0);
            }
        }

        private Result OpenContentIdFile(ContentId contentId)
        {
            if (_cachedContentId == contentId)
            {
                return Result.Success;
            }

            InvalidateFileCache();

            MakeContentPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            Result result = _fs.OpenFile(out _cachedFileHandle, contentPath, OpenMode.Read);

            if (result == FsResult.PathNotFound)
            {
                return NcmResult.ContentNotFound;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            _cachedContentId = contentId;

            return Result.Success;
        }

        public Result Initialize(
            string path,
            MakeContentPathFunction contentPathFunction,
            MakePlaceHolderPathFunction placeHolderPathFunction,
            bool delayFlush,
            RightsIdCache rightsIdCache)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = VerifyBase(_fs, path);
            if (result.IsFailure)
            {
                return result;
            }

            RootPath = path;
            MakeContentPathFunc = contentPathFunction;
            _placeHolderAccessor.Initialize(RootPath, placeHolderPathFunction, delayFlush);
            _rightsIdCache = rightsIdCache;

            return Result.Success;
        }

        [CmifCommand(0)]
        public override Result GeneratePlaceHolderId(out PlaceHolderId placeHolderId)
        {
            placeHolderId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            placeHolderId = new(Uuid.GenerateUuid().Data);

            return Result.Success;
        }

        [CmifCommand(1)]
        public override Result CreatePlaceHolder(PlaceHolderId placeHolderId, ContentId contentId, long fileSize)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = EnsureContentDirectory(contentId, MakeContentPathFunc, RootPath);
            if (result.IsFailure)
            {
                return result;
            }

            return _placeHolderAccessor.CreatePlaceHolderFile(placeHolderId, fileSize);
        }

        [CmifCommand(2)]
        public override Result DeletePlaceHolder(PlaceHolderId placeHolderId)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            return _placeHolderAccessor.DeletePlaceHolderFile(placeHolderId);
        }

        [CmifCommand(3)]
        public override Result HasPlaceHolder(out bool hasPlaceHolder, PlaceHolderId placeHolderId)
        {
            hasPlaceHolder = false;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            _placeHolderAccessor.MakePath(out string placeHolderPath, placeHolderId);

            return _fs.HasFile(out hasPlaceHolder, placeHolderPath);
        }

        [CmifCommand(4)]
        public override Result WritePlaceHolder(PlaceHolderId placeHolderId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            if (offset < 0)
            {
                return NcmResult.InvalidOffset;
            }

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            return _placeHolderAccessor.WritePlaceHolderFile(placeHolderId, offset, buffer);
        }

        [CmifCommand(5)]
        public override Result Register(PlaceHolderId placeHolderId, ContentId contentId)
        {
            InvalidateFileCache();

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            _placeHolderAccessor.GetPath(out string placeHolderPath, placeHolderId);

            MakeContentPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            result = _fs.RenameFile(placeHolderPath, contentPath);

            if (result == FsResult.PathNotFound)
            {
                return NcmResult.PlaceHolderNotFound;
            }
            else if (result == FsResult.PathAlreadyExists)
            {
                return NcmResult.ContentAlreadyExists;
            }

            return result;
        }

        [CmifCommand(6)]
        public override Result Delete(ContentId contentId)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            InvalidateFileCache();

            return DeleteContentFile(contentId, MakeContentPathFunc, RootPath);
        }

        [CmifCommand(7)]
        public override Result Has(out bool hasContent, ContentId contentId)
        {
            hasContent = false;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            MakeContentPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            return _fs.HasFile(out hasContent, contentPath);
        }

        [CmifCommand(8)]
        public override Result GetPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ContentId contentId)
        {
            path = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            MakeContentPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            return _fs.ConvertToFsCommonPath(path.AsSpan(), contentPath);
        }

        [CmifCommand(9)]
        public override Result GetPlaceHolderPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, PlaceHolderId placeHolderId)
        {
            path = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            _placeHolderAccessor.GetPath(out string placeHolderPath, placeHolderId);

            return _fs.ConvertToFsCommonPath(path.AsSpan(), placeHolderPath);
        }

        [CmifCommand(10)]
        public override Result CleanupAllPlaceHolder()
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            _placeHolderAccessor.InvalidateAll();

            PlaceHolderAccessor.MakeBaseDirectoryPath(out string placeHolderDir, RootPath);
            _fs.CleanDirectoryRecursively(placeHolderDir);

            return Result.Success;
        }

        [CmifCommand(11)]
        public override Result ListPlaceHolder(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<PlaceHolderId> placeHolderIds)
        {
            count = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            PlaceHolderAccessor.MakeBaseDirectoryPath(out string placeHolderDir, RootPath);

            int entryCount = 0;
            int maxEntries = placeHolderIds.Length;

            try
            {
                IEnumerable<DirectoryEntry> entries = TraverseDirectory(placeHolderDir, _placeHolderAccessor.GetHierarchicalDirectoryDepth());

                foreach (DirectoryEntry entry in entries)
                {
                    if (entry.Type == DirectoryEntryType.File)
                    {
                        if (entryCount >= maxEntries)
                        {
                            return NcmResult.BufferInsufficient;
                        }

                        result = PlaceHolderAccessor.GetPlaceHolderIdFromFileName(out PlaceHolderId placeHolderId, GetUtf8String(entry.Name.AsSpan()));
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        placeHolderIds[entryCount++] = placeHolderId;
                    }
                }
            }
            catch (InvalidResultException ex)
            {
                return ex.Result;
            }

            count = entryCount;

            return Result.Success;
        }

        [CmifCommand(12)]
        public override Result GetContentCount(out int count)
        {
            count = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            MakeBaseContentDirectoryPath(out string contentDir, RootPath);

            int entryCount = 0;

            try
            {
                IEnumerable<DirectoryEntry> entries = TraverseDirectory(contentDir, MakePath.GetHierarchicalContentDirectoryDepth(MakeContentPathFunc));

                foreach (DirectoryEntry entry in entries)
                {
                    if (entry.Type == DirectoryEntryType.File)
                    {
                        entryCount++;
                    }
                }
            }
            catch (InvalidResultException ex)
            {
                return ex.Result;
            }

            count = entryCount;

            return Result.Success;
        }

        [CmifCommand(13)]
        public override Result ListContentId(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentId> contentIds, int startOffset)
        {
            count = 0;

            if (startOffset < 0)
            {
                return NcmResult.InvalidOffset;
            }

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            MakeBaseContentDirectoryPath(out string contentDir, RootPath);

            int entryCount = 0;

            try
            {
                IEnumerable<DirectoryEntry> entries = TraverseDirectory(contentDir, MakePath.GetHierarchicalContentDirectoryDepth(MakeContentPathFunc));

                foreach (DirectoryEntry entry in entries)
                {
                    if (entryCount >= contentIds.Length)
                    {
                        break;
                    }

                    ReadOnlySpan<byte> nameSpan = entry.Name.AsSpan();
                    nameSpan = nameSpan[..Math.Min(nameSpan.Length, ContentIdStringLength)];

                    if (ContentId.TryParse(GetUtf8String(nameSpan), out ContentId contentId))
                    {
                        if (startOffset > 0)
                        {
                            startOffset--;
                            continue;
                        }

                        contentIds[entryCount++] = contentId;
                    }
                }
            }
            catch (InvalidResultException ex)
            {
                return ex.Result;
            }

            count = entryCount;

            return Result.Success;
        }

        [CmifCommand(14)]
        public override Result GetSizeFromContentId(out long size, ContentId contentId)
        {
            size = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            MakeContentPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            result = _fs.OpenFile(out FileHandle handle, contentPath, OpenMode.Read);
            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                result = _fs.GetFileSize(out size, handle);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            finally
            {
                _fs.CloseFile(handle);
            }

            return Result.Success;
        }

        [CmifCommand(15)]
        public override Result DisableForcibly()
        {
            Disabled = true;
            InvalidateFileCache();
            _placeHolderAccessor.InvalidateAll();

            return Result.Success;
        }

        [CmifCommand(16)]
        public override Result RevertToPlaceHolder(PlaceHolderId placeHolderId, ContentId oldContentId, ContentId newContentId)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            InvalidateFileCache();

            result = EnsureContentDirectory(newContentId, MakeContentPathFunc, RootPath);
            if (result.IsFailure)
            {
                return result;
            }

            result = _placeHolderAccessor.EnsurePlaceHolderDirectory(placeHolderId);
            if (result.IsFailure)
            {
                return result;
            }

            _placeHolderAccessor.GetPath(out string placeHolderPath, placeHolderId);
            MakeContentPath(out string contentPath, oldContentId, MakeContentPathFunc, RootPath);

            result = _fs.RenameFile(contentPath, placeHolderPath);

            if (result == FsResult.PathNotFound)
            {
                return NcmResult.PlaceHolderNotFound;
            }
            else if (result == FsResult.PathAlreadyExists)
            {
                return NcmResult.ContentAlreadyExists;
            }

            return result;
        }

        [CmifCommand(17)]
        public override Result SetPlaceHolderSize(PlaceHolderId placeHolderId, long size)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            return _placeHolderAccessor.SetPlaceHolderFileSize(placeHolderId, size);
        }

        [CmifCommand(18)]
        public override Result ReadContentIdFile([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> buffer, ContentId contentId, long offset)
        {
            if (offset < 0)
            {
                return NcmResult.InvalidOffset;
            }

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            MakeContentPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            result = OpenContentIdFile(contentId);
            if (result.IsFailure)
            {
                return result;
            }

            return _fs.ReadFile(_cachedFileHandle, offset, buffer);
        }

        [CmifCommand(19)]
        public override Result GetRightsIdFromPlaceHolderId(out RightsId rightsId, PlaceHolderId placeHolderId, ContentAttributes attr)
        {
            rightsId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetPlaceHolderPath(out Path path, placeHolderId);
            if (result.IsFailure)
            {
                return result;
            }

            result = _fs.GetRightsId(out FsRightsId fsRightsId, out byte keyGeneration, path.AsSpan(), attr);
            if (result.IsFailure)
            {
                return result;
            }

            rightsId = new(fsRightsId.Id, keyGeneration);

            return Result.Success;
        }

        [CmifCommand(20)]
        public override Result GetRightsIdFromContentId(out RightsId rightsId, ContentId contentId, ContentAttributes attr)
        {
            rightsId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            if (_rightsIdCache.Find(out rightsId, contentId))
            {
                return Result.Success;
            }

            result = GetPath(out Path path, contentId);
            if (result.IsFailure)
            {
                return result;
            }

            result = _fs.GetRightsId(out FsRightsId fsRightsId, out byte keyGeneration, path.AsSpan(), attr);
            if (result.IsFailure)
            {
                return result;
            }

            rightsId = new(fsRightsId.Id, keyGeneration);

            _rightsIdCache.Store(contentId, rightsId);

            return Result.Success;
        }

        [CmifCommand(21)]
        public override Result WriteContentForDebug(ContentId contentId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            if (offset < 0)
            {
                return NcmResult.InvalidOffset;
            }

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            // TODO: Abort unless Spl.IsDevelopment returns true.

            InvalidateFileCache();

            MakeContentPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            result = _fs.OpenFile(out FileHandle handle, contentPath, OpenMode.Write);
            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                return _fs.WriteFile(handle, offset, buffer, WriteOption.Flush);
            }
            finally
            {
                _fs.CloseFile(handle);
            }
        }

        [CmifCommand(22)]
        public override Result GetFreeSpaceSize(out long size)
        {
            return _fs.GetFreeSpaceSize(out size, RootPath);
        }

        [CmifCommand(23)]
        public override Result GetTotalSpaceSize(out long size)
        {
            return _fs.GetTotalSpaceSize(out size, RootPath);
        }

        [CmifCommand(24)]
        public override Result FlushPlaceHolder()
        {
            _placeHolderAccessor.InvalidateAll();

            return Result.Success;
        }

        [CmifCommand(25)]
        public override Result GetSizeFromPlaceHolderId(out long size, PlaceHolderId placeHolderId)
        {
            size = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = _placeHolderAccessor.TryGetPlaceHolderFileSize(out bool found, out size, placeHolderId);
            if (result.IsFailure)
            {
                return result;
            }

            if (found)
            {
                return Result.Success;
            }

            _placeHolderAccessor.GetPath(out string placeHolderPath, placeHolderId);

            result = _fs.OpenFile(out FileHandle handle, placeHolderPath, OpenMode.Read);
            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                result = _fs.GetFileSize(out size, handle);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            finally
            {
                _fs.CloseFile(handle);
            }

            return Result.Success;
        }

        [CmifCommand(26)]
        public override Result RepairInvalidFileAttribute()
        {
            MakeBaseContentDirectoryPath(out string contentPath, RootPath);

            Result result = TraverseDirectoryForRepair(contentPath, MakePath.GetHierarchicalContentDirectoryDepth(MakeContentPathFunc), IsContentPath);
            if (result.IsFailure)
            {
                return result;
            }

            _placeHolderAccessor.InvalidateAll();
            PlaceHolderAccessor.MakeBaseDirectoryPath(out string placeHolderPath, RootPath);

            result = TraverseDirectoryForRepair(placeHolderPath, _placeHolderAccessor.GetHierarchicalDirectoryDepth(), IsPlaceHolderPath);
            if (result.IsFailure)
            {
                return result;
            }

            return Result.Success;
        }

        [CmifCommand(27)]
        public override Result GetRightsIdFromPlaceHolderIdWithCache(out RightsId rightsId, PlaceHolderId placeHolderId, ContentId cacheContentId, ContentAttributes attr)
        {
            rightsId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            if (_rightsIdCache.Find(out rightsId, cacheContentId))
            {
                return Result.Success;
            }

            _placeHolderAccessor.GetPath(out string placeHolderPath, placeHolderId);

            Span<byte> commonPathBuffer = new byte[0x300];

            result = _fs.ConvertToFsCommonPath(commonPathBuffer, placeHolderPath);
            if (result.IsFailure)
            {
                return result;
            }

            result = _fs.GetRightsId(out FsRightsId fsRightsId, out byte keyGeneration, commonPathBuffer, attr);
            if (result.IsFailure)
            {
                return result;
            }

            rightsId = new(fsRightsId.Id, keyGeneration);

            _rightsIdCache.Store(cacheContentId, rightsId);

            return Result.Success;
        }

        [CmifCommand(28)]
        public override Result RegisterPath(ContentId contentId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(29)]
        public override Result ClearRegisteredPath()
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(30)]
        public override Result GetProgramId(out ProgramId programId, ContentId contentId, ContentAttributes attr)
        {
            programId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetPath(out Path path, contentId);
            if (result.IsFailure)
            {
                return result;
            }

            return _fs.GetProgramId(out programId, path.AsSpan(), attr);
        }
    }
}