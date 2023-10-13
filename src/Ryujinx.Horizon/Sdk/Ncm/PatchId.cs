namespace Ryujinx.Horizon.Sdk.Ncm
{
    readonly struct PatchId
    {
        public readonly ulong Id;

        public static int Length => sizeof(ulong);

        public PatchId(ulong id)
        {
            Id = id;
        }
    }
}