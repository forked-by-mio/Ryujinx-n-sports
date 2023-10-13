
using Ryujinx.Horizon.Sdk.Ncm;

namespace Ryujinx.Horizon.Sdk.Lr
{
    readonly struct RedirectionPath
    {
        public readonly Path Path;
        public readonly RedirectionAttributes Attributes;

        public RedirectionPath(in Path path, RedirectionAttributes attributes)
        {
            Path = path;
            Attributes = attributes;
        }
    }
}