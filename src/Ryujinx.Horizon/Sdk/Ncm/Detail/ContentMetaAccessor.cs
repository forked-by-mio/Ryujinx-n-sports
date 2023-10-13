using Ryujinx.Common.Memory;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    interface IMetaHeader
    {
        int ExtendedHeaderSize { get; }
        int ContentCount { get; }
        int ContentMetaCount { get; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x10)]
    readonly struct ApplicationMetaExtendedHeader
    {
        public readonly PatchId PatchId;
        public readonly uint RequiredSystemVersion;
        public readonly uint RequiredApplicationVersion;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x18)]
    readonly struct PatchMetaExtendedHeader
    {
        public readonly ulong ApplicationId;
        public readonly uint RequiredSystemVersion;
        public readonly uint ExtendedDataSize;
        public readonly Array8<byte> Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x18)]
    readonly struct AddOnContentMetaExtendedHeader
    {
        public readonly ulong ApplicationId;
        public readonly uint RequiredApplicationVersion;
        public readonly byte ContentAccessibilities;
        public readonly Array3<byte> Padding;
        public readonly DataPatchId DataPatchId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x10)]
    readonly struct DeltaMetaExtendedHeader
    {
        public readonly ulong ApplicationId;
        public readonly uint ExtendedDataSize;
        public readonly uint Padding;
    }

    readonly ref struct ContentMetaAccessor<THeader, TInfo> where THeader : unmanaged, IMetaHeader where TInfo : unmanaged, IContentInfo
    {
        private readonly ReadOnlySpan<byte> _data;

        public ContentMetaAccessor(ReadOnlySpan<byte> data)
        {
            _data = data;
        }

        public int GetExtendedHeaderOffset()
        {
            return Unsafe.SizeOf<THeader>();
        }

        public int GetContentInfoStartOffset()
        {
            return GetExtendedHeaderOffset() + GetExtendedHeaderSize();
        }

        public int GetContentInfoOffset(int index)
        {
            return GetContentInfoStartOffset() + index * Unsafe.SizeOf<TInfo>();
        }

        public int GetContentMetaInfoStartOffset()
        {
            return GetContentInfoOffset(GetContentCount());
        }

        public int GetContentMetaInfoOffset(int index)
        {
            return GetContentMetaInfoStartOffset() + index * Unsafe.SizeOf<ContentMetaInfo>();
        }

        public THeader GetHeader()
        {
            return MemoryMarshal.Cast<byte, THeader>(_data)[0];
        }

        public int GetContentCount()
        {
            return GetHeader().ContentCount;
        }

        public int GetContentMetaCount()
        {
            return GetHeader().ContentMetaCount;
        }

        public TInfo GetContentInfo(int index)
        {
            if ((uint)index >= (uint)GetContentCount())
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return MemoryMarshal.Cast<byte, TInfo>(_data[GetContentInfoOffset(index)..])[0];
        }

        public TInfo? GetContentInfo(ContentType type)
        {
            TInfo? found = null;

            for (int i = 0; i < GetContentCount(); i++)
            {
                TInfo info = GetContentInfo(i);

                if (info.Type == type && (found == null || info.IdOffset < found.Value.IdOffset))
                {
                    found = info;
                }
            }

            return found;
        }

        public TInfo? GetContentInfo(ContentType type, byte idOffset)
        {
            for (int i = 0; i < GetContentCount(); i++)
            {
                TInfo info = GetContentInfo(i);

                if (info.Type == type && info.IdOffset == idOffset)
                {
                    return info;
                }
            }

            return null;
        }

        public ContentMetaInfo GetContentMetaInfo(int index)
        {
            if ((uint)index >= (uint)GetContentMetaCount())
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return MemoryMarshal.Cast<byte, ContentMetaInfo>(_data[GetContentMetaInfoOffset(index)..])[0];
        }

        public int GetExtendedHeaderSize()
        {
            return GetHeader().ExtendedHeaderSize;
        }

        public T GetExtendedHeader<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(_data[GetExtendedHeaderOffset()..])[0];
        }

        public ulong? GetApplicationId(in ContentMetaKey key)
        {
            return key.Type switch
            {
                ContentMetaType.Application => key.TitleId,
                ContentMetaType.Patch => GetExtendedHeader<PatchMetaExtendedHeader>().ApplicationId,
                ContentMetaType.AddOnContent => GetExtendedHeader<AddOnContentMetaExtendedHeader>().ApplicationId,
                ContentMetaType.Delta => GetExtendedHeader<DeltaMetaExtendedHeader>().ApplicationId,
                _ => null,
            };
        }
    }
}