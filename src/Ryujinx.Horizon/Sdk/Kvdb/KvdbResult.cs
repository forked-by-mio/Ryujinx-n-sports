using Ryujinx.Horizon.Common;

namespace Ryujinx.Horizon.Sdk.Kvdb
{
    static class KvdbResult
    {
        public const int ModuleId = 20;

        public static Result OutOfKeyResource => new(ModuleId, 1);
        public static Result KeyNotFound => new(ModuleId, 2);
        public static Result AllocationFailed => new(ModuleId, 4);
        public static Result InvalidKeyValue => new(ModuleId, 5);
        public static Result BufferInsufficient => new(ModuleId, 6);
        public static Result InvalidFileSystemState => new(ModuleId, 8);
        public static Result NotCreated => new(ModuleId, 9);
    }
}