using Ryujinx.Common.Memory;
using System;
using System.Text;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    struct Path
    {
        private Array768<byte> _data;

        public Path(string path)
        {
            _data = new();
            Encoding.UTF8.GetBytes(path, _data.AsSpan());
        }

        public Span<byte> AsSpan()
        {
            return _data.AsSpan();
        }

        public override string ToString()
        {
            ReadOnlySpan<byte> data = _data.AsSpan();

            int length = data.IndexOf((byte)0);
            if (length >= 0)
            {
                data = data[..length];
            }

            return Encoding.UTF8.GetString(data);
        }
    }
}