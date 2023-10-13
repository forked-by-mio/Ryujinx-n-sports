using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Lr;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Sm;

namespace Ryujinx.Horizon.Ncm
{
    class LrIpcServer
    {
        private const int LocationResolverManagerSessionsCount = 16;
        private const int LocationResolverExtraSessionsCount = 16;
        private const int MaxSessionsCount = LocationResolverManagerSessionsCount + LocationResolverExtraSessionsCount;

        private const int PointerBufferSize = 0x400;
        private const int MaxDomains = 0;
        private const int MaxDomainObjects = 0;
        private const int MaxPortsCount = 1;

        private static readonly ManagerOptions _managerOptions = new(PointerBufferSize, MaxDomains, MaxDomainObjects, false);

        private readonly HeapAllocator _allocator;
        private readonly SmApi _sm;
        private readonly ILocationResolverManager _manager;
        private ServerManager _serverManager;
        private ThreadType _thread;

        public LrIpcServer(HeapAllocator allocator, SmApi sm, ILocationResolverManager manager)
        {
            _allocator = allocator;
            _sm = sm;
            _manager = manager;
        }

        public Result Initialize()
        {
            _serverManager = new ServerManager(_allocator, _sm, MaxPortsCount, _managerOptions, MaxSessionsCount);

            return _serverManager.RegisterObjectForServer(_manager, ServiceName.Encode("lr"), LocationResolverManagerSessionsCount);
        }

        public Result StartThreads()
        {
            Result result = Os.CreateThread(out _thread, ServiceRequests, 21);
            if (result.IsFailure)
            {
                return result;
            }

            Os.SetThreadName(_thread, "LocationResolverServerIpcSession");
            Os.StartThread(_thread);

            return Result.Success;
        }

        private void ServiceRequests()
        {
            _serverManager.ServiceRequests();
            _serverManager.Dispose();
        }

        public void Wait()
        {
            Os.WaitThread(_thread);
        }
    }
}
