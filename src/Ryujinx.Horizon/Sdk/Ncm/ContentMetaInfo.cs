using Ryujinx.Common.Memory;
using Ryujinx.Horizon.Sdk.Ncm;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x10)]
    readonly struct ContentMetaInfo
    {
        public readonly ulong Id;
        public readonly uint Version;
        public readonly ContentMetaType Type;
        public readonly byte Attributes;
        public readonly Array2<byte> Padding;

        public ContentMetaInfo(ulong id, uint version, ContentMetaType type, byte attributes)
        {
            Id = id;
            Version = version;
            Type = type;
            Attributes = attributes;
            Padding = default;
        }
    }
}