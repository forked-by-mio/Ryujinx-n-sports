using Ryujinx.Common.Memory;
using Ryujinx.Horizon.Sdk.Fs;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Lr
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    readonly struct RedirectionAttributes
    {
        public readonly ContentAttributes ContentAttributes;
        private readonly Array15<byte> _padding;

        public RedirectionAttributes(ContentAttributes attributes)
        {
            ContentAttributes = attributes;
            _padding = default;
        }
    }
}