using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x10)]
    readonly struct ContentMetaKey : IEquatable<ContentMetaKey>, IComparable<ContentMetaKey>
    {
        public readonly ulong TitleId;
        public readonly uint Version;
        public readonly ContentMetaType Type;
        public readonly ContentInstallType InstallType;

        public ContentMetaKey(ulong titleId, uint version, ContentMetaType type, ContentInstallType installType)
        {
            TitleId = titleId;
            Version = version;
            Type = type;
            InstallType = installType;
        }

        public static ContentMetaKey CreateUnknwonType(ulong titleId, uint version)
        {
            return new(titleId, version, ContentMetaType.Unknown, ContentInstallType.Full);
        }

        public bool Equals(ContentMetaKey other)
        {
            return TitleId == other.TitleId && Version == other.Version && Type == other.Type && InstallType == other.InstallType;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentMetaKey other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(TitleId, Version, Type, InstallType);
        }

        public readonly int CompareTo(ContentMetaKey other)
        {
            int titleIdComparison = TitleId.CompareTo(other.TitleId);
            if (titleIdComparison != 0)
            {
                return titleIdComparison;
            }

            int versionComparison = Version.CompareTo(other.Version);
            if (versionComparison != 0)
            {
                return versionComparison;
            }

            int typeComparison = Type.CompareTo(other.Type);
            if (typeComparison != 0)
            {
                return typeComparison;
            }

            return InstallType.CompareTo(other.InstallType);
        }

        public readonly int CompareTo(object obj)
        {
            if (obj is null)
            {
                return 1;
            }

            return obj is ContentMetaKey other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(ContentMetaKey)}");
        }
    }
}