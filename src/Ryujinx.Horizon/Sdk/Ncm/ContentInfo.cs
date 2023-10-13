using Ryujinx.Horizon.Sdk.Fs;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    interface IContentInfo
    {
        ContentType Type { get; }
        byte IdOffset { get; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x18)]
    readonly struct ContentInfo : IEquatable<ContentInfo>, IContentInfo
    {
        public readonly ContentId ContentId;
        public readonly uint SizeLow;
        public readonly byte SizeHigh;
        public readonly ContentAttributes ContentAttributes;
        public readonly ContentType ContentType;
        public readonly byte IdOffset;

        ContentType IContentInfo.Type => ContentType;
        byte IContentInfo.IdOffset => IdOffset;

        public ContentInfo(ContentId contentId, ulong size, ContentAttributes contentAttributes, ContentType contentType, byte idOffset)
        {
            ContentId = contentId;
            SizeLow = (uint)size;
            SizeHigh = (byte)(size >> 32);
            ContentAttributes = contentAttributes;
            ContentType = contentType;
            IdOffset = idOffset;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ContentId, SizeLow, SizeHigh, ContentAttributes, ContentType, IdOffset);
        }

        public override bool Equals(object obj)
        {
            return obj is ContentInfo other && Equals(other);
        }

        public bool Equals(ContentInfo other)
        {
            return ContentId == other.ContentId &&
                SizeLow == other.SizeLow &&
                SizeHigh == other.SizeHigh &&
                ContentAttributes == other.ContentAttributes &&
                ContentType == other.ContentType &&
                IdOffset == other.IdOffset;
        }
    }
}