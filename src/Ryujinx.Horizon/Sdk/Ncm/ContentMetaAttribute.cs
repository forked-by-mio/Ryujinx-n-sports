namespace Ryujinx.Horizon.Sdk.Ncm
{
    enum ContentMetaAttribute : byte
    {
        None = 0,
        IncludesExFatDriver = 1 << 0,
        Rebootless = 1 << 1,
        Compacted = 1 << 2,
    }
}