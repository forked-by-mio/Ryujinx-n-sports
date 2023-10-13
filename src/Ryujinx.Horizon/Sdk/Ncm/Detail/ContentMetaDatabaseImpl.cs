using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Kvdb;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class ContentMetaDatabaseImpl : ContentMetaDatabaseImplBase
    {
        private readonly IFsClient _fs;

        public ContentMetaDatabaseImpl(IFsClient fs, MemoryKeyValueStore<ContentMetaKey> kvs) : base(kvs)
        {
            _fs = fs;
        }

        public ContentMetaDatabaseImpl(IFsClient fs, MemoryKeyValueStore<ContentMetaKey> kvs, string mountName) : base(kvs, mountName)
        {
            _fs = fs;
        }

        [CmifCommand(0)]
        public override Result Set(ContentMetaKey key, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> value)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            return Kvs.Set(key, value);
        }

        [CmifCommand(1)]
        public override Result Get(out long valueSize, ContentMetaKey key, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> valueBuffer)
        {
            valueSize = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = Kvs.Get(out int size, key, valueBuffer);

            if (result == KvdbResult.KeyNotFound)
            {
                return NcmResult.ContentMetaNotFound;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            valueSize = size;

            return Result.Success;
        }

        [CmifCommand(2)]
        public override Result Remove(ContentMetaKey key)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = Kvs.Remove(key);

            if (result == KvdbResult.KeyNotFound)
            {
                return NcmResult.ContentMetaNotFound;
            }

            return result;
        }

        [CmifCommand(3)]
        public override Result GetContentIdByType(out ContentId contentId, ContentMetaKey key, ContentType type)
        {
            return GetContentIdImpl(out contentId, key, type, null);
        }

        [CmifCommand(4)]
        public override Result ListContentInfo(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentInfo> outInfo, ContentMetaKey key, int startIndex)
        {
            count = 0;

            if (startIndex < 0)
            {
                return NcmResult.InvalidOffset;
            }

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            for (count = 0; count < outInfo.Length && startIndex + count < reader.GetContentCount(); count++)
            {
                outInfo[count] = reader.GetContentInfo(startIndex + count);
            }

            return Result.Success;
        }

        [CmifCommand(5)]
        public override Result List(
            out int totalEntryCount,
            out int matchedEntryCount,
            [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentMetaKey> keys,
            ContentMetaType type,
            ulong applicationTitleId,
            ulong minTitleId,
            ulong maxTitleId,
            ContentInstallType installType)
        {
            totalEntryCount = 0;
            matchedEntryCount = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            int tempTotalEntryCount = 0;
            int tempMatchedEntryCount = 0;

            foreach (var entry in Kvs)
            {
                ContentMetaKey key = entry.Key;

                bool matched = (type == ContentMetaType.Unknown || key.Type == type) &&
                    (minTitleId <= key.TitleId && key.TitleId <= maxTitleId) &&
                    (installType == ContentInstallType.Unknown || key.InstallType == installType);

                if (!matched)
                {
                    continue;
                }

                if (applicationTitleId != 0)
                {
                    result = GetContentMetaValue(out byte[] metaValue, key);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    ContentMetaReader reader = new(metaValue);

                    ulong? entryApplicationTitleId = reader.GetApplicationId(key);

                    if (entryApplicationTitleId.HasValue && entryApplicationTitleId.Value != applicationTitleId)
                    {
                        continue;
                    }
                }

                if (tempMatchedEntryCount < keys.Length)
                {
                    keys[tempMatchedEntryCount++] = key;
                }

                tempTotalEntryCount++;
            }

            totalEntryCount = tempTotalEntryCount;
            matchedEntryCount = tempMatchedEntryCount;

            return Result.Success;
        }

        [CmifCommand(6)]
        public override Result GetLatestContentMetaKey(out ContentMetaKey key, ulong titleId)
        {
            key = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaKey? foundKey = null;

            int entryIndex = Kvs.GetLowerBoundIndex(ContentMetaKey.CreateUnknwonType(titleId, 0));

            for (; entryIndex < Kvs.Count; entryIndex++)
            {
                ref var entry = ref Kvs[entryIndex];

                if (entry.Key.TitleId != titleId)
                {
                    break;
                }

                if (entry.Key.InstallType == ContentInstallType.Full)
                {
                    foundKey = entry.Key;
                }
            }

            if (!foundKey.HasValue)
            {
                return NcmResult.ContentMetaNotFound;
            }

            key = foundKey.Value;

            return Result.Success;
        }

        [CmifCommand(7)]
        public override Result ListApplication(out int totalEntryCount, out int matchedEntryCount, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ApplicationContentMetaKey> keys, ContentMetaType type)
        {
            totalEntryCount = 0;
            matchedEntryCount = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            int tempTotalEntryCount = 0;
            int tempMatchedEntryCount = 0;

            foreach (var entry in Kvs)
            {
                ContentMetaKey key = entry.Key;

                bool matched = type == ContentMetaType.Unknown || key.Type == type;

                if (!matched)
                {
                    continue;
                }

                result = GetContentMetaValue(out byte[] metaValue, key);
                if (result.IsFailure)
                {
                    return result;
                }

                ContentMetaReader reader = new(metaValue);

                ulong? entryApplicationTitleId = reader.GetApplicationId(key);

                if (entryApplicationTitleId.HasValue)
                {
                    if (tempMatchedEntryCount < keys.Length)
                    {
                        keys[tempMatchedEntryCount++] = new(key, entryApplicationTitleId.Value);
                    }

                    tempTotalEntryCount++;
                }
            }

            totalEntryCount = tempTotalEntryCount;
            matchedEntryCount = tempMatchedEntryCount;

            return Result.Success;
        }

        [CmifCommand(8)]
        public override Result Has(out bool hasKey, ContentMetaKey key)
        {
            hasKey = false;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = Kvs.GetValueSize(out _, key);

            if (result == KvdbResult.KeyNotFound)
            {
                return Result.Success;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            hasKey = true;

            return Result.Success;
        }

        [CmifCommand(9)]
        public override Result HasAll(out bool hasAllKeys, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<ContentMetaKey> keys)
        {
            hasAllKeys = false;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            foreach (ContentMetaKey key in keys)
            {
                result = Has(out bool hasKey, key);
                if (result.IsFailure)
                {
                    return result;
                }

                // If we don't have the current key, then we don't have all,
                // we can exit early.
                if (!hasKey)
                {
                    return Result.Success;
                }
            }

            hasAllKeys = true;

            return Result.Success;
        }

        [CmifCommand(10)]
        public override Result GetSize(out long size, ContentMetaKey key)
        {
            size = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetContentMetaSize(out int metaSize, key);
            if (result.IsFailure)
            {
                return result;
            }

            size = metaSize;

            return Result.Success;
        }

        [CmifCommand(11)]
        public override Result GetRequiredSystemVersion(out int version, ContentMetaKey key)
        {
            version = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            if (key.Type != ContentMetaType.Application && key.Type != ContentMetaType.Patch)
            {
                return NcmResult.InvalidContentMetaKey;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            version = key.Type switch
            {
                ContentMetaType.Application => (int)reader.GetExtendedHeader<ApplicationMetaExtendedHeader>().RequiredSystemVersion,
                ContentMetaType.Patch => (int)reader.GetExtendedHeader<PatchMetaExtendedHeader>().RequiredSystemVersion,
                _ => 0,
            };

            return Result.Success;
        }

        [CmifCommand(12)]
        public override Result GetPatchContentMetaId(out ulong patchId, ContentMetaKey key)
        {
            patchId = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            if (key.Type != ContentMetaType.Application && key.Type != ContentMetaType.AddOnContent)
            {
                return NcmResult.InvalidContentMetaKey;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            switch (key.Type)
            {
                case ContentMetaType.Application:
                    patchId = reader.GetExtendedHeader<ApplicationMetaExtendedHeader>().PatchId.Id;
                    break;
                case ContentMetaType.AddOnContent:
                    if (reader.GetExtendedHeaderSize() != Unsafe.SizeOf<AddOnContentMetaExtendedHeader>())
                    {
                        return NcmResult.InvalidAddOnContentMetaExtendedHeader;
                    }

                    patchId = reader.GetExtendedHeader<AddOnContentMetaExtendedHeader>().DataPatchId.Id;
                    break;
                default:
                    DebugUtil.Unreachable();
                    break;
            }

            return Result.Success;
        }

        [CmifCommand(13)]
        public override Result DisableForcibly()
        {
            Disabled = true;

            return Result.Success;
        }

        [CmifCommand(14)]
        public override Result LookupOrphanContent([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<bool> outOrphaned, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<ContentId> contentIds)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            if (outOrphaned.Length < contentIds.Length)
            {
                return NcmResult.BufferInsufficient;
            }

            outOrphaned.Fill(true);

            foreach (var entry in Kvs)
            {
                ContentMetaReader reader = new(entry.Value);

                for (int i = 0; i < reader.GetContentCount(); i++)
                {
                    int foundIndex = contentIds.IndexOf(reader.GetContentInfo(i).ContentId);
                    if (foundIndex >= 0)
                    {
                        outOrphaned[foundIndex] = false;
                    }
                }
            }

            return Result.Success;
        }

        [CmifCommand(15)]
        public override Result Commit()
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = Kvs.Save();
            if (result.IsFailure)
            {
                return result;
            }

            return _fs.CommitSaveData(MountName);
        }

        [CmifCommand(16)]
        public override Result HasContent(out bool hasContent, ContentMetaKey key, ContentId contentId)
        {
            hasContent = false;

            Result result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            for (int i = 0; i < reader.GetContentCount(); i++)
            {
                if (reader.GetContentInfo(i).ContentId == contentId)
                {
                    hasContent = true;
                    break;
                }
            }

            return Result.Success;
        }

        [CmifCommand(17)]
        public override Result ListContentMetaInfo(out int entryCount, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentMetaInfo> outInfo, ContentMetaKey key, int startIndex)
        {
            entryCount = 0;

            if (startIndex < 0)
            {
                return NcmResult.InvalidOffset;
            }

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            int count;
            for (count = 0; count < outInfo.Length && startIndex + count < reader.GetContentMetaCount(); count++)
            {
                outInfo[count] = reader.GetContentMetaInfo(startIndex + count);
            }

            return Result.Success;
        }

        [CmifCommand(18)]
        public override Result GetAttributes(out ContentMetaAttribute attributes, ContentMetaKey key)
        {
            attributes = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            attributes = reader.GetHeader().Attributes;

            return Result.Success;
        }

        [CmifCommand(19)]
        public override Result GetRequiredApplicationVersion(out int version, ContentMetaKey key)
        {
            version = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            switch (key.Type)
            {
                case ContentMetaType.AddOnContent:
                    version = (int)reader.GetExtendedHeader<AddOnContentMetaExtendedHeader>().RequiredApplicationVersion;
                    break;
                case ContentMetaType.Application:
                    version = (int)reader.GetExtendedHeader<ApplicationMetaExtendedHeader>().RequiredApplicationVersion;
                    break;
                default:
                    DebugUtil.Unreachable();
                    break;
            }

            return Result.Success;
        }

        [CmifCommand(20)]
        public override Result GetContentIdByTypeAndIdOffset(out ContentId contentId, ContentMetaKey key, ContentType type, byte idOffset)
        {
            return GetContentIdImpl(out contentId, key, type, idOffset);
        }

        [CmifCommand(21)]
        public override Result GetCount(out uint count)
        {
            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                count = 0;

                return result;
            }

            count = (uint)Kvs.Count;

            return Result.Success;
        }

        [CmifCommand(22)]
        public override Result GetOwnerApplicationId(out Sdk.Ncm.ApplicationId applicationId, ContentMetaKey key)
        {
            applicationId = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            if (key.Type != ContentMetaType.Application &&
                key.Type != ContentMetaType.Patch &&
                key.Type != ContentMetaType.AddOnContent)
            {
                return NcmResult.InvalidContentMetaKey;
            }

            if (key.Type == ContentMetaType.Application)
            {
                applicationId = new(key.TitleId);

                return Result.Success;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            ulong ownerApplicationId = 0;

            switch (key.Type)
            {
                case ContentMetaType.Patch:
                    ownerApplicationId = reader.GetExtendedHeader<PatchMetaExtendedHeader>().ApplicationId;
                    break;
                case ContentMetaType.AddOnContent:
                    ownerApplicationId = reader.GetExtendedHeader<AddOnContentMetaExtendedHeader>().ApplicationId;
                    break;
                default:
                    DebugUtil.Unreachable();
                    break;
            }

            applicationId = new(ownerApplicationId);

            return Result.Success;
        }

        [CmifCommand(23)]
        public override Result GetContentAccessibilities(out byte accessibilities, ContentMetaKey key)
        {
            accessibilities = 0;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            if (key.Type != ContentMetaType.AddOnContent)
            {
                return NcmResult.InvalidContentMetaKey;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            accessibilities = reader.GetExtendedHeader<AddOnContentMetaExtendedHeader>().ContentAccessibilities;

            return Result.Success;
        }

        [CmifCommand(24)]
        public override Result GetContentInfoByType(out ContentInfo contentInfo, ContentMetaKey key, ContentType type)
        {
            return GetContentInfoImpl(out contentInfo, key, type, null);
        }

        [CmifCommand(25)]
        public override Result GetContentInfoByTypeAndIdOffset(out ContentInfo contentInfo, ContentMetaKey key, ContentType type, byte idOffset)
        {
            return GetContentInfoImpl(out contentInfo, key, type, idOffset);
        }

        [CmifCommand(26)]
        public override Result GetPlatform(out ContentMetaPlatform platform, ContentMetaKey key)
        {
            platform = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            platform = reader.GetHeader().Platform;

            return Result.Success;
        }

        private Result GetContentIdImpl(out ContentId contentId, ContentMetaKey key, ContentType type, byte? idOffset)
        {
            Result result = GetContentInfoImpl(out ContentInfo contentInfo, key, type, idOffset);

            contentId = contentInfo.ContentId;

            return result;
        }

        private Result GetContentInfoImpl(out ContentInfo contentInfo, ContentMetaKey key, ContentType type, byte? idOffset)
        {
            contentInfo = default;

            Result result = EnsureEnabled();
            if (result.IsFailure)
            {
                return result;
            }

            int entryIndex = Kvs.GetLowerBoundIndex(key);

            if ((uint)entryIndex >= (uint)Kvs.Count || Kvs[entryIndex].Key.TitleId != key.TitleId)
            {
                return NcmResult.ContentMetaNotFound;
            }

            result = GetContentMetaValue(out byte[] metaValue, key);
            if (result.IsFailure)
            {
                return result;
            }

            ContentMetaReader reader = new(metaValue);

            ContentInfo? info = idOffset.HasValue
                ? reader.GetContentInfo(type, idOffset.Value)
                : reader.GetContentInfo(type);

            if (!info.HasValue)
            {
                return NcmResult.ContentNotFound;
            }

            contentInfo = info.Value;

            return Result.Success;
        }
    }
}