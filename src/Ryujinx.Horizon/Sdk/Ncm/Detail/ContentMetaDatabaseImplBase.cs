using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Kvdb;
using System;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    abstract partial class ContentMetaDatabaseImplBase : IContentMetaDatabase
    {
        protected MemoryKeyValueStore<ContentMetaKey> Kvs { get; }
        protected string MountName { get; }
        protected bool Disabled { get; set; }

        protected ContentMetaDatabaseImplBase(MemoryKeyValueStore<ContentMetaKey> kvs) : this(kvs, string.Empty)
        {
        }

        protected ContentMetaDatabaseImplBase(MemoryKeyValueStore<ContentMetaKey> kvs, string mountName)
        {
            Kvs = kvs;
            MountName = mountName;
            Disabled = false;
        }

        protected Result EnsureEnabled()
        {
            if (Disabled)
            {
                return NcmResult.InvalidContentMetaDatabase;
            }

            return Result.Success;
        }

        protected Result GetContentMetaSize(out int size, in ContentMetaKey key)
        {
            Result result = Kvs.GetValueSize(out size, key);

            if (result == KvdbResult.KeyNotFound)
            {
                return NcmResult.ContentMetaNotFound;
            }

            return result;
        }

        protected Result GetContentMetaValue(out byte[] value, in ContentMetaKey key)
        {
            Result result = Kvs.GetValue(out value, key);

            if (result == KvdbResult.KeyNotFound)
            {
                return NcmResult.ContentMetaNotFound;
            }

            return result;
        }

        public abstract Result Set(ContentMetaKey key, ReadOnlySpan<byte> value);
        public abstract Result Get(out long valueSize, ContentMetaKey key, Span<byte> valueBuffer);
        public abstract Result Remove(ContentMetaKey key);
        public abstract Result GetContentIdByType(out ContentId contentId, ContentMetaKey key, ContentType type);
        public abstract Result ListContentInfo(out int count, Span<ContentInfo> outInfo, ContentMetaKey key, int startIndex);
        public abstract Result List(out int totalEntryCount, out int matchedEntryCount, Span<ContentMetaKey> keys, ContentMetaType type, ulong applicationTitleId, ulong minTitleId, ulong maxTitleId, ContentInstallType installType);
        public abstract Result GetLatestContentMetaKey(out ContentMetaKey key, ulong titleId);
        public abstract Result ListApplication(out int totalEntryCount, out int matchedEntryCount, Span<ApplicationContentMetaKey> keys, ContentMetaType type);
        public abstract Result Has(out bool hasKey, ContentMetaKey key);
        public abstract Result HasAll(out bool hasAllKeys, ReadOnlySpan<ContentMetaKey> keys);
        public abstract Result GetSize(out long size, ContentMetaKey key);
        public abstract Result GetRequiredSystemVersion(out int version, ContentMetaKey key);
        public abstract Result GetPatchContentMetaId(out ulong patchId, ContentMetaKey key);
        public abstract Result DisableForcibly();
        public abstract Result LookupOrphanContent(Span<bool> outOrphaned, ReadOnlySpan<ContentId> contentIds);
        public abstract Result Commit();
        public abstract Result HasContent(out bool hasContent, ContentMetaKey key, ContentId contentId);
        public abstract Result ListContentMetaInfo(out int entryCount, Span<ContentMetaInfo> outInfo, ContentMetaKey key, int startIndex);
        public abstract Result GetAttributes(out ContentMetaAttribute attributes, ContentMetaKey key);
        public abstract Result GetRequiredApplicationVersion(out int version, ContentMetaKey key);
        public abstract Result GetContentIdByTypeAndIdOffset(out ContentId contentId, ContentMetaKey key, ContentType type, byte idOffset);
        public abstract Result GetCount(out uint count);
        public abstract Result GetOwnerApplicationId(out Sdk.Ncm.ApplicationId applicationId, ContentMetaKey key);
        public abstract Result GetContentAccessibilities(out byte accessibilities, ContentMetaKey key);
        public abstract Result GetContentInfoByType(out ContentInfo contentInfo, ContentMetaKey key, ContentType type);
        public abstract Result GetContentInfoByTypeAndIdOffset(out ContentInfo contentInfo, ContentMetaKey key, ContentType type, byte idOffset);
        public abstract Result GetPlatform(out ContentMetaPlatform platform, ContentMetaKey key);
    }
}