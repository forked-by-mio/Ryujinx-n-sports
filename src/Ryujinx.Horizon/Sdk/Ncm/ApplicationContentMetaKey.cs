using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x18)]
    readonly struct ApplicationContentMetaKey
    {
        public readonly ContentMetaKey Key;
        public readonly ulong ApplicationId;

        public ApplicationContentMetaKey(ContentMetaKey key, ulong applicationId)
        {
            Key = key;
            ApplicationId = applicationId;
        }
    }
}