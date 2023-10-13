using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
    readonly struct ContentMetaHeader : IMetaHeader
    {
        public readonly ushort ExtendedHeaderSize;
        public readonly ushort ContentCount;
        public readonly ushort ContentMetaCount;
        public readonly ContentMetaAttribute Attributes;
        public readonly ContentMetaPlatform Platform;

        int IMetaHeader.ExtendedHeaderSize => ExtendedHeaderSize;
        int IMetaHeader.ContentCount => ContentCount;
        int IMetaHeader.ContentMetaCount => ContentMetaCount;
    }
}