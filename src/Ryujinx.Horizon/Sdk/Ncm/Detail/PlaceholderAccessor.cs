using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using System;
using System.Globalization;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class PlaceHolderAccessor
    {
        private const string BasePlaceHolderDirectory = "/placehld";
        private const string PlaceHolderExtension = ".nca";

        private const int PlaceHolderExtensionLength = 4;
        private const int PlaceHolderFileNameLengthWithoutExtension = 2 * 16; // PlaceHolderId has 16 bytes, times 2 because each byte takes 2 characters in hex.
        private const int PlaceHolderFileNameLength = PlaceHolderFileNameLengthWithoutExtension + PlaceHolderExtensionLength;

        private struct CacheEntry
        {
            public PlaceHolderId PlaceHolderId;
            public FileHandle FileHandle;
            public ulong Counter;
        }

        private readonly IFsClient _fs;
        private CacheEntry[] _caches;
        private string _rootPath;
        private ulong _curCounter;
        private readonly object _cacheLock;
        private ContentStorageImplBase.MakePlaceHolderPathFunction _makePlaceHolderPathFunc;
        private bool _delayFlush;

        public PlaceHolderAccessor(IFsClient fs)
        {
            _fs = fs;
            _caches = new CacheEntry[2];
            _cacheLock = new();
        }

        public void Initialize(string rootPath, ContentStorageImplBase.MakePlaceHolderPathFunction placeHolderPathFunction, bool delayFlush)
        {
            _rootPath = rootPath;
            _makePlaceHolderPathFunc = placeHolderPathFunction;
            _delayFlush = delayFlush;
        }

        private static void MakeBasePlaceHolderDirectoryPath(out string path, string rootPath)
        {
            path = $"{rootPath}{BasePlaceHolderDirectory}";
        }

        private static void MakePlaceHolderFilePath(out string path, PlaceHolderId placeHolderId, ContentStorageImplBase.MakePlaceHolderPathFunction func, string rootPath)
        {
            MakeBasePlaceHolderDirectoryPath(out string basePath, rootPath);
            func(out path, placeHolderId, basePath);
        }

        public int GetHierarchicalDirectoryDepth()
        {
            return Sdk.Ncm.Detail.MakePath.GetHierarchicalPlaceHolderDirectoryDepth(_makePlaceHolderPathFunc);
        }

        private Result Open(out FileHandle handle, PlaceHolderId placeHolderId)
        {
            if (LoadFromCache(out handle, placeHolderId))
            {
                return Result.Success;
            }

            MakePath(out string placeHolderPath, placeHolderId);

            return _fs.OpenFile(out handle, placeHolderPath, OpenMode.Write);
        }

        private bool LoadFromCache(out FileHandle handle, PlaceHolderId placeHolderId)
        {
            lock (_cacheLock)
            {
                int entryIndex = FindInCache(placeHolderId);
                if (entryIndex < 0)
                {
                    handle = default;
                    return false;
                }

                ref CacheEntry entry = ref _caches[entryIndex];

                entry.PlaceHolderId = default;
                handle = entry.FileHandle;
            }

            return true;
        }

        private void StoreToCache(PlaceHolderId placeHolderId, FileHandle handle)
        {
            lock (_cacheLock)
            {
                ref CacheEntry entry = ref GetFreeEntry();

                entry.PlaceHolderId = placeHolderId;
                entry.FileHandle = handle;
                entry.Counter = _curCounter++;
            }
        }

        private void Invalidate(int entryIndex)
        {
            if (entryIndex >= 0)
            {
                ref CacheEntry entry = ref _caches[entryIndex];

                _fs.FlushFile(entry.FileHandle);
                _fs.CloseFile(entry.FileHandle);
                entry.PlaceHolderId = default;
            }
        }

        private int FindInCache(PlaceHolderId placeHolderId)
        {
            if (placeHolderId.Id != 0)
            {
                for (int i = 0; i < _caches.Length; i++)
                {
                    if (placeHolderId.Id == _caches[i].PlaceHolderId.Id)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private ref CacheEntry GetFreeEntry()
        {
            // First we try to find a free entry.
            for (int i = 0; i < _caches.Length; i++)
            {
                if (_caches[i].PlaceHolderId.Id == 0)
                {
                    return ref _caches[i];
                }
            }

            // If none is found, we return the least used one.
            int candidateIndex = 0;

            for (int i = 1; i < _caches.Length; i++)
            {
                if (_caches[candidateIndex].Counter < _caches[i].Counter)
                {
                    candidateIndex = i;
                }
            }

            return ref _caches[candidateIndex];
        }

        public void MakePath(out string path, PlaceHolderId placeHolderId)
        {
            MakePlaceHolderFilePath(out path, placeHolderId, _makePlaceHolderPathFunc, _rootPath);
        }

        public static void MakeBaseDirectoryPath(out string path, string rootPath)
        {
            MakeBasePlaceHolderDirectoryPath(out path, rootPath);
        }

        public Result EnsurePlaceHolderDirectory(PlaceHolderId placeHolderId)
        {
            MakePath(out string path, placeHolderId);
            return _fs.EnsureParentDirectory(path);
        }

        public static Result GetPlaceHolderIdFromFileName(out PlaceHolderId placeHolderId, string name)
        {
            placeHolderId = default;

            if (name.Length < PlaceHolderFileNameLength)
            {
                return NcmResult.InvalidPlaceHolderFile;
            }

            if (name[PlaceHolderFileNameLengthWithoutExtension..] != PlaceHolderExtension)
            {
                return NcmResult.InvalidPlaceHolderFile;
            }

            if (!UInt128.TryParse(name[..PlaceHolderFileNameLengthWithoutExtension], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out UInt128 id))
            {
                return NcmResult.InvalidPlaceHolderFile;
            }

            placeHolderId = new(id);

            return Result.Success;
        }

        public void GetPath(out string placeHolderPath, PlaceHolderId placeHolderId)
        {
            lock (_cacheLock)
            {
                Invalidate(FindInCache(placeHolderId));
            }

            MakePath(out placeHolderPath, placeHolderId);
        }

        public Result CreatePlaceHolderFile(PlaceHolderId placeHolderId, long fileSize)
        {
            Result result = EnsurePlaceHolderDirectory(placeHolderId);

            if (result.IsFailure)
            {
                return result;
            }

            GetPath(out string placeHolderPath, placeHolderId);

            result = _fs.CreateFile(placeHolderPath, fileSize); // TODO: BigFile option?

            if (result == FsResult.PathAlreadyExists)
            {
                return NcmResult.PlaceHolderAlreadyExists;
            }

            return result;
        }

        public Result DeletePlaceHolderFile(PlaceHolderId placeHolderId)
        {
            GetPath(out string placeHolderPath, placeHolderId);

            Result result = _fs.DeleteFile(placeHolderPath);

            if (result == FsResult.PathNotFound)
            {
                return NcmResult.PlaceHolderNotFound;
            }

            return result;
        }

        public Result WritePlaceHolderFile(PlaceHolderId placeHolderId, long offset, ReadOnlySpan<byte> buffer)
        {
            Result result = Open(out FileHandle handle, placeHolderId);

            if (result == FsResult.PathNotFound)
            {
                return NcmResult.PlaceHolderNotFound;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            try
            {
                return _fs.WriteFile(handle, offset, buffer, _delayFlush ? WriteOption.Flush : WriteOption.None);
            }
            finally
            {
                StoreToCache(placeHolderId, handle);
            }
        }

        public Result SetPlaceHolderFileSize(PlaceHolderId placeHolderId, long fileSize)
        {
            Result result = Open(out FileHandle handle, placeHolderId);

            if (result == FsResult.PathNotFound)
            {
                return NcmResult.PlaceHolderNotFound;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            try
            {
                return _fs.SetFileSize(handle, fileSize);
            }
            finally
            {
                _fs.CloseFile(handle);
            }
        }

        public Result TryGetPlaceHolderFileSize(out bool found, out long fileSize, PlaceHolderId placeHolderId)
        {
            found = LoadFromCache(out FileHandle handle, placeHolderId);

            if (found)
            {
                StoreToCache(placeHolderId, handle);

                Result result = _fs.GetFileSize(out fileSize, handle);

                if (result.IsFailure)
                {
                    found = false;
                    return result;
                }
            }
            else
            {
                fileSize = 0;
            }

            return Result.Success;
        }

        public void InvalidateAll()
        {
            for (int i = 0; i < _caches.Length; i++)
            {
                if (_caches[i].PlaceHolderId.Id != 0)
                {
                    Invalidate(i);
                }
            }
        }
    }
}