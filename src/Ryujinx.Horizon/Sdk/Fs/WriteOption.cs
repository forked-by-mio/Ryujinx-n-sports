
namespace Ryujinx.Horizon.Sdk.Fs
{
    public readonly struct WriteOption
    {
        public readonly WriteOptionFlag Flags;

        public static WriteOption None => new(WriteOptionFlag.None);
        public static WriteOption Flush => new(WriteOptionFlag.Flush);

        public WriteOption(WriteOptionFlag flags)
        {
            Flags = flags;
        }

        public bool HasFlushFlag()
        {
            return Flags.HasFlag(WriteOptionFlag.Flush);
        }
    }
}