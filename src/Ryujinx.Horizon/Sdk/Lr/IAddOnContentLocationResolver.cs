using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.Sf;
using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    interface IAddOnContentLocationResolver : IServiceObject
    {
        Result ResolveAddOnContentPath(out Path path, DataId dataId);
        Result RegisterAddOnContentStorage(DataId dataId, Ncm.ApplicationId applicationId, StorageId storageId);
        Result UnregisterAllAddOnContentPath();
        Result RefreshApplicationAddOnContent(ReadOnlySpan<Ncm.ApplicationId> ids);
        Result UnregisterApplicationAddOnContent(Ncm.ApplicationId applicationId);
        Result GetRegisteredAddOnContentPaths(out Path path, out Path path2, DataId dataId);
        Result RegisterAddOnContentPath(DataId dataId, Ncm.ApplicationId applicationId, in Path path);
        Result RegisterAddOnContentPaths(DataId dataId, Ncm.ApplicationId applicationId, in Path path, in Path path2);
    }
}