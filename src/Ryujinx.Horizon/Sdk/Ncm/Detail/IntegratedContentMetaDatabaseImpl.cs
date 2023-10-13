using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Collections.Generic;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    partial class IntegratedContentMetaDatabaseImpl : IContentMetaDatabase
    {
        private readonly struct ListType
        {
            public readonly IContentMetaDatabase MetaDatabase;
            public readonly byte Id;

            public ListType(IContentMetaDatabase metaDatabase, byte id)
            {
                MetaDatabase = metaDatabase;
                Id = id;
            }
        }

        private readonly List<ListType> _list;
        private bool _disabled;

        public IntegratedContentMetaDatabaseImpl()
        {
            _list = new();
            _disabled = false;
        }

        public void Add(IContentMetaDatabase contentMetaDatabase, byte id)
        {
            _list.Add(new(contentMetaDatabase, id));
        }

        private Result EnsureEnabled()
        {
            if (_disabled)
            {
                return NcmResult.InvalidContentMetaDatabase;
            }

            return Result.Success;
        }

        [CmifCommand(0)]
        public Result Set(ContentMetaKey key, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> value)
        {
            // Should throw.

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(1)]
        public Result Get(out long valueSize, ContentMetaKey key, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> valueBuffer)
        {
            valueSize = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.Get(out valueSize, key, valueBuffer);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(2)]
        public Result Remove(ContentMetaKey key)
        {
            // Should throw.

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(3)]
        public Result GetContentIdByType(out ContentId contentId, ContentMetaKey key, ContentType type)
        {
            contentId = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetContentIdByType(out contentId, key, type);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(4)]
        public Result ListContentInfo(out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentInfo> outInfo, ContentMetaKey key, int startIndex)
        {
            count = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.Has(out bool has, key);
                    if (result.IsFailure)
                    {
                        continue;
                    }

                    if (!has)
                    {
                        result = NcmResult.ContentMetaNotFound;
                        continue;
                    }

                    result = data.MetaDatabase.ListContentInfo(out count, outInfo, key, startIndex);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(5)]
        public Result List(
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

            int tempTotalEntryCount = 0;
            int tempMatchedEntryCount = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.List(
                        out int currentTotalEntryCount,
                        out int currentMatchedEntryCount,
                        keys[tempMatchedEntryCount..],
                        type,
                        applicationTitleId,
                        minTitleId,
                        maxTitleId,
                        installType);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    tempTotalEntryCount += currentTotalEntryCount;
                    tempMatchedEntryCount += currentMatchedEntryCount;
                }
            }

            totalEntryCount = tempTotalEntryCount;
            matchedEntryCount = tempMatchedEntryCount;

            return Result.Success;
        }

        [CmifCommand(6)]
        public Result GetLatestContentMetaKey(out ContentMetaKey key, ulong titleId)
        {
            key = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetLatestContentMetaKey(out key, titleId);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(7)]
        public Result ListApplication(out int totalEntryCount, out int matchedEntryCount, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ApplicationContentMetaKey> keys, ContentMetaType type)
        {
            totalEntryCount = 0;
            matchedEntryCount = 0;

            int tempTotalEntryCount = 0;
            int tempMatchedEntryCount = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.ListApplication(
                        out int currentTotalEntryCount,
                        out int currentMatchedEntryCount,
                        keys[tempMatchedEntryCount..],
                        type);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    tempTotalEntryCount += currentTotalEntryCount;
                    tempMatchedEntryCount += currentMatchedEntryCount;
                }
            }

            totalEntryCount = tempTotalEntryCount;
            matchedEntryCount = tempMatchedEntryCount;

            return Result.Success;
        }

        [CmifCommand(8)]
        public Result Has(out bool hasKey, ContentMetaKey key)
        {
            hasKey = false;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.Has(out hasKey, key);

                    if (!hasKey && result.IsSuccess)
                    {
                        result = NcmResult.ContentMetaNotFound;
                    }

                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(9)]
        public Result HasAll(out bool hasAllKeys, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<ContentMetaKey> keys)
        {
            hasAllKeys = false;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                for (int i = 0; i < keys.Length; i++)
                {
                    result = Has(out bool hasKey, keys[i]);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    if (!hasKey)
                    {
                        return Result.Success;
                    }
                }
            }

            hasAllKeys = true;
            return Result.Success;
        }

        [CmifCommand(10)]
        public Result GetSize(out long size, ContentMetaKey key)
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
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetSize(out size, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(11)]
        public Result GetRequiredSystemVersion(out int version, ContentMetaKey key)
        {
            version = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetRequiredSystemVersion(out version, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(12)]
        public Result GetPatchContentMetaId(out ulong patchId, ContentMetaKey key)
        {
            patchId = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetPatchContentMetaId(out patchId, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(13)]
        public Result DisableForcibly()
        {
            lock (_list)
            {
                _disabled = true;
            }

            return Result.Success;
        }

        [CmifCommand(14)]
        public Result LookupOrphanContent([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<bool> outOrphaned, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<ContentId> contentIds)
        {
            // Should throw.

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(15)]
        public Result Commit()
        {
            // Should throw.

            return NcmResult.InvalidOperation;
        }

        [CmifCommand(16)]
        public Result HasContent(out bool hasContent, ContentMetaKey key, ContentId contentId)
        {
            hasContent = false;

            foreach (var data in _list)
            {
                Result result = data.MetaDatabase.Has(out bool hasKey, key);
                if (result.IsFailure)
                {
                    continue;
                }

                if (!hasKey)
                {
                    result = NcmResult.ContentMetaNotFound;
                    continue;
                }

                result = data.MetaDatabase.HasContent(out hasContent, key, contentId);
                if (result.IsSuccess)
                {
                    break;
                }
            }

            return Result.Success;
        }

        [CmifCommand(17)]
        public Result ListContentMetaInfo(out int entryCount, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ContentMetaInfo> outInfo, ContentMetaKey key, int startIndex)
        {
            entryCount = 0;

            foreach (var data in _list)
            {
                Result result = data.MetaDatabase.Has(out bool hasKey, key);
                if (result.IsFailure)
                {
                    continue;
                }

                if (!hasKey)
                {
                    result = NcmResult.ContentMetaNotFound;
                    continue;
                }

                result = data.MetaDatabase.ListContentMetaInfo(out entryCount, outInfo, key, startIndex);
                if (result.IsSuccess)
                {
                    break;
                }
            }

            return Result.Success;
        }

        [CmifCommand(18)]
        public Result GetAttributes(out ContentMetaAttribute attributes, ContentMetaKey key)
        {
            attributes = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetAttributes(out attributes, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(19)]
        public Result GetRequiredApplicationVersion(out int version, ContentMetaKey key)
        {
            version = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetRequiredApplicationVersion(out version, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(20)]
        public Result GetContentIdByTypeAndIdOffset(out ContentId contentId, ContentMetaKey key, ContentType type, byte idOffset)
        {
            contentId = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetContentIdByTypeAndIdOffset(out contentId, key, type, idOffset);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(21)]
        public Result GetCount(out uint count)
        {
            count = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                uint total = 0;

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetCount(out uint current);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    total += current;
                }

                count = total;
            }

            return Result.Success;
        }

        [CmifCommand(22)]
        public Result GetOwnerApplicationId(out Sdk.Ncm.ApplicationId applicationId, ContentMetaKey key)
        {
            applicationId = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetOwnerApplicationId(out applicationId, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(23)]
        public Result GetContentAccessibilities(out byte accessibilities, ContentMetaKey key)
        {
            accessibilities = 0;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetContentAccessibilities(out accessibilities, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(24)]
        public Result GetContentInfoByType(out ContentInfo contentInfo, ContentMetaKey key, ContentType type)
        {
            contentInfo = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetContentInfoByType(out contentInfo, key, type);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(25)]
        public Result GetContentInfoByTypeAndIdOffset(out ContentInfo contentInfo, ContentMetaKey key, ContentType type, byte idOffset)
        {
            contentInfo = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetContentInfoByTypeAndIdOffset(out contentInfo, key, type, idOffset);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        [CmifCommand(26)]
        public Result GetPlatform(out ContentMetaPlatform platform, ContentMetaKey key)
        {
            platform = default;

            lock (_list)
            {
                Result result = EnsureEnabled();
                if (result.IsFailure)
                {
                    return result;
                }

                if (_list.Count == 0)
                {
                    return NcmResult.ContentMetaNotFound;
                }

                foreach (var data in _list)
                {
                    result = data.MetaDatabase.GetPlatform(out platform, key);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }

                return result;
            }
        }
    }
}