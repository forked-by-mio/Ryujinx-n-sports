using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.Sf;
using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    interface ILocationResolverManager : IServiceObject
    {
        Result OpenLocationResolver(out ILocationResolver resolver, StorageId storageId);
        Result OpenRegisteredLocationResolver(out IRegisteredLocationResolver resolver);
        Result RefreshLocationResolver(StorageId storageId);
        Result OpenAddOnContentLocationResolver(out IAddOnContentLocationResolver resolver);
        Result SetEnabled(ReadOnlySpan<StorageId> storageIds);
    }
}