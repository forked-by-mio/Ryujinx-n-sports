using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Collections.Generic;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    partial class IntegratedContentStorageImpl : IContentStorage
    {
        private readonly struct ListType
        {
            public readonly IContentStorage Storage;
            public readonly byte Id;

            public ListType(IContentStorage storage, byte id)
            {
                Storage = storage;
                Id = id;
            }
        }

        private readonly List<ListType> _list;
        private bool _disabled;

        public IntegratedContentStorageImpl()
        {
            _list = new();
        }

        public void Add(IContentStorage storage, byte id)
        {
            _list.Add(new(storage, id));
        }

        private Result EnsureEnabled()
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

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(1)]
        public Result CreatePlaceHolder(PlaceHolderId placeHolderId, ContentId contentId, long fileSize)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(2)]
        public Result DeletePlaceHolder(PlaceHolderId placeHolderId)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(3)]
        public Result HasPlaceHolder(out bool hasPlaceHolder, PlaceHolderId placeHolderId)
        {
            hasPlaceHolder = false;

            return Result.Success;
        }

        [CmifCommand(4)]
        public Result WritePlaceHolder(PlaceHolderId placeHolderId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(5)]
        public Result Register(PlaceHolderId placeHolderId, ContentId contentId)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(6)]
        public Result Delete(ContentId contentId)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(7)]
        public Result Has(out bool hasContent, ContentId contentId)
        {
            hasContent = false;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                foreach (var data in _list)
                {
                    result = data.Storage.Has(out hasContent, contentId);

                    if (!hasContent && result.IsSuccess)
                    {
                        result = NcmResult.ContentNotFound;
                    }

                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(8)]
        public Result GetPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ContentId contentId)
        {
            path = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.Storage.Has(out bool hasContent, contentId);

                    if (!hasContent && result.IsSuccess)
                    {
                        result = NcmResult.ContentNotFound;
                    }

                    if (hasContent)
                    {
                        return data.Storage.GetPath(out path, contentId);
                    }
                }

                return result;
            }
        }

        [CmifCommand(9)]
        public Result GetPlaceHolderPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, PlaceHolderId placeHolderId)
        {
            path = default;

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(10)]
        public Result CleanupAllPlaceHolder()
        {
            return Result.Success;
        }

        [CmifCommand(11)]
        public Result ListPlaceHolder(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<PlaceHolderId> placeHolderIds)
        {
            count = 0;

            return Result.Success;
        }

        [CmifCommand(12)]
        public Result GetContentCount(out int count)
        {
            count = 0;

            return Result.Success;
        }

        [CmifCommand(13)]
        public Result ListContentId(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentId> contentIds, int startOffset)
        {
            count = 0;

            return Result.Success;
        }

        [CmifCommand(14)]
        public Result GetSizeFromContentId(out long size, ContentId contentId)
        {
            size = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.Storage.Has(out bool hasContent, contentId);

                    if (!hasContent && result.IsSuccess)
                    {
                        result = NcmResult.ContentNotFound;
                    }

                    if (hasContent)
                    {
                        return data.Storage.GetSizeFromContentId(out size, contentId);
                    }
                }

                return result;
            }
        }

        [CmifCommand(15)]
        public Result DisableForcibly()
        {
            lock (_list)
            {
                _disabled = true;

                return Result.Success;
            }
        }

        [CmifCommand(16)]
        public Result RevertToPlaceHolder(PlaceHolderId placeHolderId, ContentId oldContentId, ContentId newContentId)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(17)]
        public Result SetPlaceHolderSize(PlaceHolderId placeHolderId, long size)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(18)]
        public Result ReadContentIdFile([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> buffer, ContentId contentId, long offset)
        {
            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.Storage.Has(out bool hasContent, contentId);

                    if (!hasContent && result.IsSuccess)
                    {
                        result = NcmResult.ContentNotFound;
                    }

                    if (hasContent)
                    {
                        return data.Storage.ReadContentIdFile(buffer, contentId, offset);
                    }
                }

                return result;
            }
        }

        [CmifCommand(19)]
        public Result GetRightsIdFromPlaceHolderId(out RightsId rightsId, PlaceHolderId placeHolderId, ContentAttributes attr)
        {
            rightsId = default;

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(20)]
        public Result GetRightsIdFromContentId(out RightsId rightsId, ContentId contentId, ContentAttributes attr)
        {
            rightsId = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.Storage.Has(out bool hasContent, contentId);

                    if (!hasContent && result.IsSuccess)
                    {
                        result = NcmResult.ContentNotFound;
                    }

                    if (hasContent)
                    {
                        return data.Storage.GetRightsIdFromContentId(out rightsId, contentId, attr);
                    }
                }

                return result;
            }
        }

        [CmifCommand(21)]
        public Result WriteContentForDebug(ContentId contentId, long offset, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> buffer)
        {
            return NcmResult.InvalidOperation;
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

            long totalSize = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                foreach (var data in _list)
                {
                    result = data.Storage.GetTotalSpaceSize(out long currentSize);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    totalSize += currentSize;
                }
            }

            size = totalSize;

            return Result.Success;
        }

        [CmifCommand(24)]
        public Result FlushPlaceHolder()
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(25)]
        public Result GetSizeFromPlaceHolderId(out long size, PlaceHolderId placeHolderId)
        {
            size = 0;

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(26)]
        public Result RepairInvalidFileAttribute()
        {
            return Result.Success;
        }

        [CmifCommand(27)]
        public Result GetRightsIdFromPlaceHolderIdWithCache(out RightsId rightsId, PlaceHolderId placeHolderId, ContentId cacheContentId, ContentAttributes attr)
        {
            rightsId = default;

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(28)]
        public Result RegisterPath(ContentId contentId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path)
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(29)]
        public Result ClearRegisteredPath()
        {
            return NcmResult.InvalidOperation;
        }

        [CmifCommand(30)]
        public Result GetProgramId(out ProgramId programId, ContentId contentId, ContentAttributes attr)
        {
            programId = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.Storage.Has(out bool hasContent, contentId);

                    if (!hasContent && result.IsSuccess)
                    {
                        result = NcmResult.ContentNotFound;
                    }

                    if (hasContent)
                    {
                        return data.Storage.GetProgramId(out programId, contentId, attr);
                    }
                }

                return result;
            }
        }
    }
}