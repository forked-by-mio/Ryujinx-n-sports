using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    [Flags]
    enum RedirectionFlags
    {
        None = 0,
        Application = 1 << 0,
    }
}