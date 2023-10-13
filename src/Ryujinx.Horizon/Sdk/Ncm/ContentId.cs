using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    readonly struct ContentId : IEquatable<ContentId>
    {
        public readonly UInt128 Id;

        public static int Length => Unsafe.SizeOf<UInt128>();

        public ContentId(UInt128 id)
        {
            Id = id;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentId contentId && contentId.Equals(this);
        }

        public bool Equals(ContentId other)
        {
            return other.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(ContentId lhs, ContentId rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ContentId lhs, ContentId rhs)
        {
            return !lhs.Equals(rhs);
        }

        private static string GetStringFromBytes(ReadOnlySpan<byte> bytes)
        {
            string str = string.Empty;

            for (int i = 0; i < bytes.Length; i++)
            {
                str += bytes[i].ToString("x2");
            }

            return str;
        }

        public string GetString()
        {
            UInt128 id = Id;

            return GetStringFromBytes(MemoryMarshal.Cast<UInt128, byte>(MemoryMarshal.CreateSpan(ref id, 1)));
        }

        public static bool TryParse(string s, out ContentId contentId)
        {
            if ((s.Length % 2) != 0 || 16 * 2 < s.Length)
            {
                contentId = default;

                return false;
            }

            Span<byte> data = stackalloc byte[16];

            for (int index = 0; index < s.Length; index += 2)
            {
                if (!byte.TryParse(s.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                {
                    contentId = default;

                    return false;
                }

                data[index / 2] = value;
            }

            contentId = new(MemoryMarshal.Cast<byte, UInt128>(data)[0]);

            return true;
        }

        public override string ToString()
        {
            return $"0x{GetString()}";
        }
    }
}