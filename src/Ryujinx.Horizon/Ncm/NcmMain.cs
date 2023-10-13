using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Lr;
using Ryujinx.Horizon.Sdk.Ncm.Detail;
using Ryujinx.Horizon.Sdk.Sm;

namespace Ryujinx.Horizon.Ncm
{
    class NcmMain : IService
    {
        public static void Main(ServiceTable serviceTable)
        {
            MountNameGenerator mng = new();
            IFsClient fsClient = HorizonStatic.Options.FsClient;

            HeapAllocator allocator = new();

            SmApi sm = new();
            sm.Initialize().AbortOnFailure();

            using ContentManagerImpl contentManagerServiceObject = new(mng, fsClient);
            NcmIpcServer contentManagerIpcServer = new(allocator, sm, contentManagerServiceObject);
            LocationResolverManagerImpl locationResolverManagerServiceObject = new(contentManagerServiceObject);
            LrIpcServer locationResolverIpcServer = new(allocator, sm, locationResolverManagerServiceObject);

            contentManagerServiceObject.Initialize(new(false, false, false));

            contentManagerIpcServer.Initialize().AbortOnFailure();
            contentManagerIpcServer.StartThreads().AbortOnFailure();

            locationResolverIpcServer.Initialize().AbortOnFailure();
            locationResolverIpcServer.StartThreads().AbortOnFailure();

            serviceTable.SignalServiceReady();

            contentManagerIpcServer.Wait();
            locationResolverIpcServer.Wait();
        }
    }
}
