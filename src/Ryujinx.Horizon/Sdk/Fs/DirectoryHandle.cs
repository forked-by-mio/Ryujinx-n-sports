namespace Ryujinx.Horizon.Sdk.Fs
{
    public readonly struct DirectoryHandle
    {
        public object Value { get; }

        public DirectoryHandle(object value)
        {
            Value = value;
        }
    }
}