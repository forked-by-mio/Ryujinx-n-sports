
namespace Ryujinx.Horizon.Sdk.Fs
{
    public readonly struct GameCardHandle
    {
        public object Value { get; }

        public GameCardHandle(object value)
        {
            Value = value;
        }
    }
}