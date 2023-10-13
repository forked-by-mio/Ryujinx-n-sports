using Ryujinx.Horizon.Common;
using System.Threading;

namespace Ryujinx.Horizon.Sdk.OsTypes
{
    static partial class Os
    {
        private const int UserThreadPriorityOffset = 28;

        public static Result CreateThread(out ThreadType thread, ThreadStart function, int priority, int idealCore = -2)
        {
            var options = HorizonStatic.Options;
            var syscall = HorizonStatic.Syscall;
            var addressSpace = HorizonStatic.AddressSpace;

            void ThreadStart(object obj)
            {
                IThreadContext context = (IThreadContext)obj;
                HorizonStatic.Register(options, syscall, addressSpace, context, (int)context.GetX(1));
                function();
            }

            Result result = HorizonStatic.Syscall.CreateThread(out int handle, 0UL, 0UL, 0UL, priority + UserThreadPriorityOffset, idealCore, ThreadStart);

            thread = new()
            {
                NativeHandle = handle,
            };

            return result;
        }

        public static void SetThreadName(in ThreadType thread, string name)
        {
            // TODO: Actually set the name.
        }

        public static Result StartThread(in ThreadType thread)
        {
            return HorizonStatic.Syscall.StartThread(thread.NativeHandle);
        }

        public static Result WaitThread(in ThreadType thread)
        {
            return HorizonStatic.Syscall.WaitSynchronization(out _, stackalloc[] { thread.NativeHandle }, -1L);
        }
    }
}
