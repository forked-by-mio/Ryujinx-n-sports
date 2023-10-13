using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using System;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    abstract partial class ContentStorageImplBase : IContentStorage
    {
        public delegate void MakeContentPathFunction(out string path, ContentId contentId, string rootPath);
        public delegate void MakePlaceHolderPathFunction(out string path, PlaceHolderId placeHolderId, string rootPath);

        protected string RootPath { get; set; }
        protected MakeContentPathFunction MakeContentPathFunc { get; set; }
        protected bool Disabled { get; set; }

        protected Result EnsureEnabled()
        {
            if (Disabled)
            {
                return NcmResult.InvalidContentStorage;
            }

            return Result.Success;
        }

        public abstract Result GeneratePlaceHolderId(out PlaceHolderId placeHolderId);
        public abstract Result CreatePlaceHolder(PlaceHolderId placeHolderId, ContentId contentId, long fileSize);
        public abstract Result DeletePlaceHolder(PlaceHolderId placeHolderId);
        public abstract Result HasPlaceHolder(out bool hasPlaceHolder, PlaceHolderId placeHolderId);
        public abstract Result WritePlaceHolder(PlaceHolderId placeHolderId, long offset, ReadOnlySpan<byte> buffer);
        public abstract Result Register(PlaceHolderId placeHolderId, ContentId contentId);
        public abstract Result Delete(ContentId contentId);
        public abstract Result Has(out bool hasContent, ContentId contentId);
        public abstract Result GetPath(out Path path, ContentId contentId);
        public abstract Result GetPlaceHolderPath(out Path path, PlaceHolderId placeHolderId);
        public abstract Result CleanupAllPlaceHolder();
        public abstract Result ListPlaceHolder(out int count, Span<PlaceHolderId> placeHolderIds);
        public abstract Result GetContentCount(out int count);
        public abstract Result ListContentId(out int count, Span<ContentId> contentIds, int startOffset);
        public abstract Result GetSizeFromContentId(out long size, ContentId contentId);
        public abstract Result DisableForcibly();
        public abstract Result RevertToPlaceHolder(PlaceHolderId placeHolderId, ContentId oldContentId, ContentId newContentId);
        public abstract Result SetPlaceHolderSize(PlaceHolderId placeHolderId, long size);
        public abstract Result ReadContentIdFile(Span<byte> buffer, ContentId contentId, long offset);
        public abstract Result GetRightsIdFromPlaceHolderId(out RightsId rightsId, PlaceHolderId placeHolderId, ContentAttributes attr);
        public abstract Result GetRightsIdFromContentId(out RightsId rightsId, ContentId contentId, ContentAttributes attr);
        public abstract Result WriteContentForDebug(ContentId contentId, long offset, ReadOnlySpan<byte> buffer);
        public abstract Result GetFreeSpaceSize(out long size);
        public abstract Result GetTotalSpaceSize(out long size);
        public abstract Result FlushPlaceHolder();
        public abstract Result GetSizeFromPlaceHolderId(out long size, PlaceHolderId placeHolderId);
        public abstract Result RepairInvalidFileAttribute();
        public abstract Result GetRightsIdFromPlaceHolderIdWithCache(out RightsId rightsId, PlaceHolderId placeHolderId, ContentId cacheContentId, ContentAttributes attr);
        public abstract Result RegisterPath(ContentId contentId, in Path path);
        public abstract Result ClearRegisteredPath();
        public abstract Result GetProgramId(out ProgramId programId, ContentId contentId, ContentAttributes attr);
    }
}