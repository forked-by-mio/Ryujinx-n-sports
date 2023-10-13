using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class ReadOnlyContentStorageImpl : ContentStorageImplBase
    {
        private readonly IFsClient _fs;

        public ReadOnlyContentStorageImpl(IFsClient fs)
        {
            _fs = fs;
        }

        private static void MakeContentPath(out string path, ContentId contentId, MakeContentPathFunction func, string rootPath)
        {
            func(out path, contentId, rootPath);
        }

        private static void MakeGameCardContentMetaPath(out string path, ContentId contentId, MakeContentPathFunction func, string rootPath)
        {
            func(out string tempPath, contentId, rootPath);

            path = tempPath[..^4] + ".cnmt.nca"; // .nca -> .cnmt.nca
        }

        private Result OpenContentIdFileImpl(out FileHandle handle, ContentId contentId, MakeContentPathFunction func, string rootPath)
        {
            MakeContentPath(out string path, contentId, func, rootPath);

            Result result = _fs.OpenFile(out handle, path, OpenMode.Read);

            if (result == FsResult.PathNotFound)
            {
                MakeGameCardContentMetaPath(out path, contentId, func, rootPath);

                result = _fs.OpenFile(out handle, path, OpenMode.Read);
            }

            return result;
        }

        public Result Initialize(string path, MakeContentPathFunction contentPathFunction)
        {
            Result result = EnsureEnabled();

            if (result.IsFailure)
            {
                return result;
            }

            RootPath = path;
            MakeContentPathFunc = contentPathFunction;

            return Result.Success;
        }

        [CmifCommand(0)]
        public override Result GeneratePlaceHolderId(out PlaceHolderId placeHolderId)
        {
            placeHolderId = default;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(1)]
        public override Result CreatePlaceHolder(PlaceHolderId placeHolderId, ContentId contentId, long fileSize)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(2)]
        public override Result DeletePlaceHolder(PlaceHolderId placeHolderId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(3)]
        public override Result HasPlaceHolder(out bool hasPlaceHolder, PlaceHolderId placeHolderId)
        {
            hasPlaceHolder = false;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(4)]
        public override Result WritePlaceHolder(PlaceHolderId placeHolderId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(5)]
        public override Result Register(PlaceHolderId placeHolderId, ContentId contentId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(6)]
        public override Result Delete(ContentId contentId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
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

            result = _fs.HasFile(out hasContent, contentPath);
            if (result.IsFailure)
            {
                return result;
            }

            if (!hasContent)
            {
                MakeGameCardContentMetaPath(out contentPath, contentId, MakeContentPathFunc, RootPath);

                result = _fs.HasFile(out hasContent, contentPath);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Result.Success;
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

            MakeGameCardContentMetaPath(out string contentPath, contentId, MakeContentPathFunc, RootPath);

            result = _fs.HasFile(out bool hasFile, contentPath);
            if (result.IsFailure)
            {
                return result;
            }

            if (!hasFile)
            {
                MakeContentPath(out contentPath, contentId, MakeContentPathFunc, RootPath);
            }

            result = _fs.ConvertToFsCommonPath(path.AsSpan(), contentPath);
            if (result.IsFailure)
            {
                return result;
            }

            return Result.Success;
        }

        [CmifCommand(9)]
        public override Result GetPlaceHolderPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, PlaceHolderId placeHolderId)
        {
            path = default;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(10)]
        public override Result CleanupAllPlaceHolder()
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(11)]
        public override Result ListPlaceHolder(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<PlaceHolderId> placeHolderIds)
        {
            count = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(12)]
        public override Result GetContentCount(out int count)
        {
            count = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(13)]
        public override Result ListContentId(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentId> contentIds, int startOffset)
        {
            count = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
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

            result = OpenContentIdFileImpl(out FileHandle handle, contentId, MakeContentPathFunc, RootPath);
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

            return Result.Success;
        }

        [CmifCommand(16)]
        public override Result RevertToPlaceHolder(PlaceHolderId placeHolderId, ContentId oldContentId, ContentId newContentId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(17)]
        public override Result SetPlaceHolderSize(PlaceHolderId placeHolderId, long size)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
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

            result = OpenContentIdFileImpl(out FileHandle handle, contentId, MakeContentPathFunc, RootPath);
            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                result = _fs.ReadFile(handle, offset, buffer);
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

        [CmifCommand(19)]
        public override Result GetRightsIdFromPlaceHolderId(out RightsId rightsId, PlaceHolderId placeHolderId, ContentAttributes attr)
        {
            rightsId = default;

            return NcmResult.WriteToReadOnlyContentStorage;
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

            return Result.Success;
        }

        [CmifCommand(21)]
        public override Result WriteContentForDebug(ContentId contentId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(22)]
        public override Result GetFreeSpaceSize(out long size)
        {
            size = 0;

            return Result.Success;
        }

        [CmifCommand(23)]
        public override Result GetTotalSpaceSize(out long size)
        {
            size = 0;

            return Result.Success;
        }

        [CmifCommand(24)]
        public override Result FlushPlaceHolder()
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(25)]
        public override Result GetSizeFromPlaceHolderId(out long size, PlaceHolderId placeHolderId)
        {
            size = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(26)]
        public override Result RepairInvalidFileAttribute()
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(27)]
        public override Result GetRightsIdFromPlaceHolderIdWithCache(out RightsId rightsId, PlaceHolderId placeHolderId, ContentId cacheContentId, ContentAttributes attr)
        {
            rightsId = default;

            return NcmResult.WriteToReadOnlyContentStorage;
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