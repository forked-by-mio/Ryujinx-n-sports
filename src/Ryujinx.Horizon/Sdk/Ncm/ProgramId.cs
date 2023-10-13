using System;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    public readonly struct ProgramId : IEquatable<ProgramId>
    {
        public readonly ulong Id;

        public static int Length => sizeof(ulong);

        public ProgramId(ulong id)
        {
            Id = id;
        }

        public override bool Equals(object obj)
        {
            return obj is ProgramId programId && programId.Equals(this);
        }

        public bool Equals(ProgramId other)
        {
            return other.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(ProgramId lhs, ProgramId rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ProgramId lhs, ProgramId rhs)
        {
            return !lhs.Equals(rhs);
        }
    }
}