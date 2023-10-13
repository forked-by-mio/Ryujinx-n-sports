
using System;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    readonly ref struct ContentMetaReader
    {
        private readonly ContentMetaAccessor<ContentMetaHeader, ContentInfo> _accessor;

        public ContentMetaReader(ReadOnlySpan<byte> data)
        {
            _accessor = new(data);
        }

        public ContentMetaHeader GetHeader()
        {
            return _accessor.GetHeader();
        }

        public int GetContentCount()
        {
            return _accessor.GetContentCount();
        }

        public int GetContentMetaCount()
        {
            return _accessor.GetContentMetaCount();
        }

        public ContentInfo GetContentInfo(int index)
        {
            return _accessor.GetContentInfo(index);
        }

        public ContentInfo? GetContentInfo(ContentType type)
        {
            return _accessor.GetContentInfo(type);
        }

        public ContentInfo? GetContentInfo(ContentType type, byte idOffset)
        {
            return _accessor.GetContentInfo(type, idOffset);
        }

        public ContentMetaInfo GetContentMetaInfo(int index)
        {
            return _accessor.GetContentMetaInfo(index);
        }

        public int GetExtendedHeaderSize()
        {
            return _accessor.GetExtendedHeaderSize();
        }

        public T GetExtendedHeader<T>() where T : unmanaged
        {
            return _accessor.GetExtendedHeader<T>();
        }

        public ulong? GetApplicationId(in ContentMetaKey key)
        {
            return _accessor.GetApplicationId(key);
        }
    }
}