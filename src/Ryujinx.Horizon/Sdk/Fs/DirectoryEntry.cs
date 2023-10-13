using Ryujinx.Common.Memory;

namespace Ryujinx.Horizon.Sdk.Fs
{
    public readonly struct DirectoryEntry
    {
        public readonly Array769<byte> Name;
        public readonly NxFileAttributes Attributes;
        public readonly Array2<byte> Reserved302;
        public readonly DirectoryEntryType Type;
        public readonly Array3<byte> Reserved305;
        public readonly long Size;

        public DirectoryEntry(in Array769<byte> name, NxFileAttributes attributes, DirectoryEntryType type, long size)
        {
            Name = name;
            Attributes = attributes;
            Reserved302 = default;
            Type = type;
            Reserved305 = default;
            Size = size;
        }
    }
}