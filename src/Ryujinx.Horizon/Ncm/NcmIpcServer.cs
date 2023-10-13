using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Sm;

namespace Ryujinx.Horizon.Ncm
{
    class NcmIpcServer
    {
        private const int ContentManagerManagerSessionsCount = 16;
        private const int ContentManagerExtraSessionsCount = 16;
        private const int MaxSessionsCount = ContentManagerManagerSessionsCount + ContentManagerExtraSessionsCount;

        private const int PointerBufferSize = 0x400;
        private const int MaxDomains = 0;
        private const int MaxDomainObjects = 0;
        private const int MaxPortsCount = 1;

        private static readonly ManagerOptions _managerOptions = new(PointerBufferSize, MaxDomains, MaxDomainObjects, false);

        private readonly HeapAllocator _allocator;
        private readonly SmApi _sm;
        private readonly IContentManager _manager;
        private ServerManager _serverManager;
        private ThreadType _thread;

        public NcmIpcServer(HeapAllocator allocator, SmApi sm, IContentManager manager)
        {
            _allocator = allocator;
            _sm = sm;
            _manager = manager;
        }

        public Result Initialize()
        {
            _serverManager = new ServerManager(_allocator, _sm, MaxPortsCount, _managerOptions, MaxSessionsCount);

            return _serverManager.RegisterObjectForServer(_manager, ServiceName.Encode("ncm"), ContentManagerManagerSessionsCount);
        }

        public Result StartThreads()
        {
            Result result = Os.CreateThread(out _thread, ServiceRequests, 21);
            if (result.IsFailure)
            {
                return result;
            }

            Os.SetThreadName(_thread, "ContentManagerServerIpcSession");
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
