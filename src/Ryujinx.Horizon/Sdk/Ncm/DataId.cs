using System;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    readonly struct DataId : IEquatable<DataId>
    {
        public readonly ulong Id;

        public static int Length => sizeof(ulong);

        public DataId(ulong id)
        {
            Id = id;
        }

        public override bool Equals(object obj)
        {
            return obj is DataId dataId && dataId.Equals(this);
        }

        public bool Equals(DataId other)
        {
            return other.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(DataId lhs, DataId rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(DataId lhs, DataId rhs)
        {
            return !lhs.Equals(rhs);
        }
    }
}