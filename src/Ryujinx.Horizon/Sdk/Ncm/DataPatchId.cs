namespace Ryujinx.Horizon.Sdk.Ncm
{
    readonly struct DataPatchId
    {
        public readonly ulong Id;

        public static int Length => sizeof(ulong);

        public DataPatchId(ulong id)
        {
            Id = id;
        }
    }
}