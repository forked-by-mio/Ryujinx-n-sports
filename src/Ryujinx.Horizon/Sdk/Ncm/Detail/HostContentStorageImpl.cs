using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Text;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    partial class HostContentStorageImpl : IContentStorage
    {
        private readonly IFsClient _fs;
        private RegisteredHostContent _registeredContent;
        private bool _disabled;

        public HostContentStorageImpl(IFsClient fs, RegisteredHostContent registeredHostContent)
        {
            _fs = fs;
            _registeredContent = registeredHostContent;
            _disabled = false;
        }

        protected Result EnsureEnabled()
        {
            if (_disabled)
            {
                return NcmResult.InvalidContentStorage;
            }

            return Result.Success;
        }

        [CmifCommand(0)]
        public Result GeneratePlaceHolderId(out PlaceHolderId placeHolderId)
        {
            placeHolderId = default;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(1)]
        public Result CreatePlaceHolder(PlaceHolderId placeHolderId, ContentId contentId, long fileSize)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(2)]
        public Result DeletePlaceHolder(PlaceHolderId placeHolderId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(3)]
        public Result HasPlaceHolder(out bool hasPlaceHolder, PlaceHolderId placeHolderId)
        {
            hasPlaceHolder = false;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(4)]
        public Result WritePlaceHolder(PlaceHolderId placeHolderId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(5)]
        public Result Register(PlaceHolderId placeHolderId, ContentId contentId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(6)]
        public Result Delete(ContentId contentId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(7)]
        public Result Has(out bool hasContent, ContentId contentId)
        {
            hasContent = false;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = _registeredContent.GetPath(out _, contentId);

            if (result == NcmResult.ContentNotFound)
            {
                return Result.Success;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            hasContent = true;

            return Result.Success;
        }

        [CmifCommand(8)]
        public Result GetPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ContentId contentId)
        {
            path = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = _registeredContent.GetPath(out string contentPath, contentId);
            if (result.IsFailure)
            {
                return result;
            }

            path = new(contentPath);

            return Result.Success;
        }

        [CmifCommand(9)]
        public Result GetPlaceHolderPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, PlaceHolderId placeHolderId)
        {
            path = default;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(10)]
        public Result CleanupAllPlaceHolder()
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(11)]
        public Result ListPlaceHolder(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<PlaceHolderId> placeHolderIds)
        {
            count = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(12)]
        public Result GetContentCount(out int count)
        {
            count = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(13)]
        public Result ListContentId(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentId> contentIds, int startOffset)
        {
            count = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(14)]
        public Result GetSizeFromContentId(out long size, ContentId contentId)
        {
            size = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(15)]
        public Result DisableForcibly()
        {
            _disabled = true;

            return Result.Success;
        }

        [CmifCommand(16)]
        public Result RevertToPlaceHolder(PlaceHolderId placeHolderId, ContentId oldContentId, ContentId newContentId)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(17)]
        public Result SetPlaceHolderSize(PlaceHolderId placeHolderId, long size)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(18)]
        public Result ReadContentIdFile([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> buffer, ContentId contentId, long offset)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(19)]
        public Result GetRightsIdFromPlaceHolderId(out RightsId rightsId, PlaceHolderId placeHolderId, ContentAttributes attr)
        {
            rightsId = default;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(20)]
        public Result GetRightsIdFromContentId(out RightsId rightsId, ContentId contentId, ContentAttributes attr)
        {
            rightsId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = _registeredContent.GetPath(out string path, contentId);
            if (result.IsFailure)
            {
                return result;
            }

            int pathSize = Encoding.UTF8.GetByteCount(path);
            Span<byte> pathBuffer = new byte[pathSize + 1];
            Encoding.UTF8.GetBytes(path, pathBuffer);

            result = _fs.GetRightsId(out FsRightsId fsRightsId, out byte keyGeneration, pathBuffer, attr);

            if (result == FsResult.TargetNotFound)
            {
                return Result.Success;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            rightsId = new(fsRightsId.Id, keyGeneration);

            return Result.Success;
        }

        [CmifCommand(21)]
        public Result WriteContentForDebug(ContentId contentId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(22)]
        public Result GetFreeSpaceSize(out long size)
        {
            size = 0;

            return Result.Success;
        }

        [CmifCommand(23)]
        public Result GetTotalSpaceSize(out long size)
        {
            size = 0;

            return Result.Success;
        }

        [CmifCommand(24)]
        public Result FlushPlaceHolder()
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(25)]
        public Result GetSizeFromPlaceHolderId(out long size, PlaceHolderId placeHolderId)
        {
            size = 0;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(26)]
        public Result RepairInvalidFileAttribute()
        {
            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(27)]
        public Result GetRightsIdFromPlaceHolderIdWithCache(out RightsId rightsId, PlaceHolderId placeHolderId, ContentId cacheContentId, ContentAttributes attr)
        {
            rightsId = default;

            return NcmResult.WriteToReadOnlyContentStorage;
        }

        [CmifCommand(28)]
        public Result RegisterPath(ContentId contentId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path)
        {
            // TODO: Abort unless Spl.IsDevelopment returns true.

            return _registeredContent.RegisterPath(contentId, path.ToString());
        }

        [CmifCommand(29)]
        public Result ClearRegisteredPath()
        {
            // TODO: Abort unless Spl.IsDevelopment returns true.

            _registeredContent.ClearPaths();

            return Result.Success;
        }

        [CmifCommand(30)]
        public Result GetProgramId(out ProgramId programId, ContentId contentId, ContentAttributes attr)
        {
            programId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = _registeredContent.GetPath(out string path, contentId);
            if (result.IsFailure)
            {
                return result;
            }

            int lastCharacter = path.Length - 1;
            if (lastCharacter >= 0 && path[lastCharacter] == '/')
            {
                lastCharacter--;
            }

            if (path.Length < 4 || path.Substring(lastCharacter - 4, 4) != ".ncd")
            {
                return NcmResult.InvalidContentMetaDirectory;
            }

            int pathSize = Encoding.UTF8.GetByteCount(path);
            Span<byte> pathBuffer = new byte[pathSize + 1];
            Encoding.UTF8.GetBytes(path, pathBuffer);

            return _fs.GetProgramId(out programId, pathBuffer, attr);
        }
    }
}