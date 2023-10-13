using System;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    readonly struct PlaceHolderId
    {
        public readonly UInt128 Id;

        public static int Length => sizeof(ulong);

        public PlaceHolderId(UInt128 id)
        {
            Id = id;
        }
    }
}